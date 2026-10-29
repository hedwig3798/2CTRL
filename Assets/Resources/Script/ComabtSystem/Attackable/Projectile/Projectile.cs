using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Weapon 이 발사하는 발사체의 베이스
/// 타겟 탐색 / 추적 / 타격 로직은 파생 클래스가 OnLaunch 에서 결정한다
/// </summary>
public abstract class Projectile
    : MonoBehaviour
{
    [Header("black board")]
    [Tooltip("Weapon 이 발사 시 공통 정보(시작 위치, 이동 대상, 속도, 배치)를 기록한다")]
    public BlackBoardHandler blackBoardHandler;

    protected Weapon weapon;
    protected DamageMassage damageMassage;

    /// <summary>
    /// 함께 발사된 발사체 중 몇 번째인지 (0 부터)
    /// </summary>
    protected int formationIndex;
    /// <summary>
    /// 함께 발사된 발사체 수
    /// </summary>
    protected int formationCount = 1;

    private IObjectPool<Projectile> pool;
    private bool returningToPool;

    public void SetPool(IObjectPool<Projectile> _pool)
    {
        pool = _pool;
    }

    public void MarkReturningToPool()
    {
        returningToPool = true;
    }

    /// <summary>
    /// 함께 발사된 발사체 수와 그 중 자신의 순서를 설정한다
    /// Launch 전에 설정되며, 유지형 무기는 발사체 수가 바뀔 때 다시 설정한다
    /// </summary>
    public void SetFormation(int _index, int _count)
    {
        formationCount = Mathf.Max(1, _count);
        formationIndex = Mathf.Clamp(_index, 0, formationCount - 1);
    }

    /// <summary>
    /// 이동 컴포넌트 등에 전달할 BlackBoard
    /// BlackBoardHandler 가 없으면 null
    /// </summary>
    public BlackBoard GetBlackBoard()
    {
        if (null == blackBoardHandler)
        {
            blackBoardHandler = GetComponent<BlackBoardHandler>();
        }

        if (null == blackBoardHandler)
        {
            return null;
        }

        return blackBoardHandler.GetBlackBoard();
    }

    /// <summary>
    /// BlackBoard 의 값으로 Initializable 컴포넌트들을 초기화한다
    /// </summary>
    public void InitializeBlackBoard()
    {
        if (null == blackBoardHandler)
        {
            return;
        }

        blackBoardHandler.Initialize();
    }

    /// <summary>
    /// 무기 스펙을 받아 발사한다
    /// 데미지는 무기 스텟 + 플레이어 스텟 (Weapon.FinalDamage)
    /// 타겟이 없어 발사하지 못하면 false 를 반환하고, 무기가 즉시 풀로 반환한다
    /// 성공하면 BlackBoard 로 이동 컴포넌트를 초기화한다 (OnLaunch 에서 값을 덮어쓸 수 있다)
    /// </summary>
    public bool Launch(Weapon _weapon)
    {
        SetupDamage(_weapon);

        if (false == OnLaunch())
        {
            return false;
        }

        WriteMoveTargetPoint();
        InitializeBlackBoard();
        return true;
    }

    /// <summary>
    /// 타겟 탐색 없이 _start 에서 _point 를 향해 발사한다 (분열탄 조각 등 보조 발사체용, Weapon.FireSub 가 호출)
    /// OnLaunch 대신 OnLaunchAt 을 부르며, BlackBoard 에 startPoint / moveTargetPoint 를 기록하고 moveTarget 은 비운다
    /// 데미지는 Weapon.FinalDamage * _damageRate. OnLaunchAt 이 false 면 발사하지 않는다
    /// </summary>
    public bool LaunchAt(Weapon _weapon, Vector3 _start, Vector3 _point, float _damageRate = 1f)
    {
        SetupDamage(_weapon);
        damageMassage.damage *= Mathf.Max(0f, _damageRate);

        if (false == OnLaunchAt())
        {
            return false;
        }

        BlackBoard b = GetBlackBoard();
        if (null != b)
        {
            b.SetTransform(DATA_TYPE.startPosition, null);
            b.SetTransform(DATA_TYPE.moveTarget, null);
            b.SetVector(DATA_TYPE.startPoint, _start);
            b.SetVector(DATA_TYPE.moveTargetPoint, _point);
        }

        transform.position = _start;
        InitializeBlackBoard();
        return true;
    }

    /// <summary>
    /// 데미지는 무기 스텟 + 플레이어 스텟 (Weapon.FinalDamage)
    /// </summary>
    private void SetupDamage(Weapon _weapon)
    {
        weapon = _weapon;

        damageMassage.damage = weapon.FinalDamage;
        damageMassage.isReflected = false;
        damageMassage.attacker = null;
        damageMassage.knockbackForce = weapon.knockbackForce;
    }

    /// <summary>
    /// 최종 이동 대상(moveTarget)을 중심으로 반지름 spreadRange 인 원 안에서 랜덤한 지점을 뽑아 moveTargetPoint 로 기록한다
    /// OnLaunch 에서 moveTarget 이 확정된 뒤 호출된다. 이동 대상이 없으면 이전 발사의 지점을 지운다
    /// </summary>
    private void WriteMoveTargetPoint()
    {
        BlackBoard b = GetBlackBoard();
        if (null == b)
        {
            return;
        }

        Transform target = b.GetTransform(DATA_TYPE.moveTarget);
        if (null == target)
        {
            b.RemoveVector(DATA_TYPE.moveTargetPoint);
            return;
        }

        Vector3 point = target.position;
        if (weapon.spreadRange > 0f)
        {
            point += (Vector3)(Random.insideUnitCircle * weapon.spreadRange);
        }
        b.SetVector(DATA_TYPE.moveTargetPoint, point);
    }

    protected abstract bool OnLaunch();

    /// <summary>
    /// LaunchAt 으로 발사될 때 호출된다. 타격 기록 초기화 등을 하고, false 면 발사하지 않는다
    /// 기본은 아무것도 하지 않고 발사한다
    /// </summary>
    protected virtual bool OnLaunchAt()
    {
        return true;
    }

    #region Damage
    // 발사체마다 따로 둬서 데미지 처리 중 다른 발사체가 범위 공격을 해도 섞이지 않게 한다
    private List<Transform> areaBuffer = new List<Transform>();

    /// <summary>
    /// 발사 시 정해진 데미지 메시지를 복사해 대상에게 보낸다
    /// 타격 지점은 발사체의 현재 위치 (넉백 방향 기준)
    /// </summary>
    protected void ApplyDamage(DamagePipeline _dp)
    {
        SendDamage(_dp, transform.position);
    }

    /// <summary>
    /// _center 를 중심으로 반지름 _radius 안의 살아있는 적 모두에게 데미지를 준다 (폭발 등)
    /// 넉백은 _center 에서 바깥쪽으로 밀어낸다. _exclude 에 포함된 대상은 제외한다
    /// 맞은 적 수를 반환한다
    /// </summary>
    protected int ApplyAreaDamage(Vector2 _center, float _radius, HashSet<Transform> _exclude = null)
    {
        if (_radius <= 0f)
        {
            return 0;
        }

        int count = FindAll(_center, _radius, areaBuffer, _exclude);
        for (int i = 0; i < count; ++i)
        {
            if (areaBuffer[i].TryGetComponent(out DamagePipeline dp))
            {
                SendDamage(dp, _center);
            }
        }
        areaBuffer.Clear();
        return count;
    }

    /// <summary>
    /// 데미지 메시지를 복사해 타격 지점(넉백 방향 기준)만 바꿔 대상에게 보낸다
    /// </summary>
    protected void SendDamage(DamagePipeline _dp, Vector2 _hitPoint)
    {
        DamageMassage duplicateMassage = damageMassage;
        duplicateMassage.hitPoint = _hitPoint;
        _dp.ProcessDamage(ref duplicateMassage);
    }
    #endregion

    #region Target
    /// <summary>
    /// 무기 범위(FinalRange) 안의 가장 가까운 적을 이동 대상(moveTarget)으로 기록한다
    /// 적이 없거나 BlackBoard 가 없으면 false
    /// </summary>
    protected bool AimAtNearest()
    {
        BlackBoard b = GetBlackBoard();
        if (null == b)
        {
            return false;
        }

        Transform target = FindNearest(weapon.transform.position, weapon.FinalRange);
        if (null == target)
        {
            return false;
        }

        // 무기가 기록한 기본 이동 대상(주인) 대신 가장 가까운 적을 향한다
        b.SetTransform(DATA_TYPE.moveTarget, target);
        return true;
    }

    /// <summary>
    /// 범위 안에서 가장 가까운 살아있는 적을 찾는다
    /// </summary>
    protected Transform FindNearest(Vector3 _center, float _range, HashSet<Transform> _exclude = null)
    {
        return TargetFinder.FindNearest(
            _center
            , _range
            , weapon.targetLayer
            , _exclude
            , weapon.transform
            , weapon.owner
        );
    }

    /// <summary>
    /// 범위 안의 살아있는 적을 모두 찾아 _result 에 담는다
    /// </summary>
    protected int FindAll(Vector3 _center, float _range, List<Transform> _result, HashSet<Transform> _exclude = null)
    {
        return TargetFinder.FindAll(
            _center
            , _range
            , weapon.targetLayer
            , _result
            , _exclude
            , weapon.transform
            , weapon.owner
        );
    }

    /// <summary>
    /// 데미지를 받을 수 있고 죽지 않은 대상인지
    /// </summary>
    protected static bool IsAlive(Transform _target)
    {
        return TargetFinder.IsAlive(_target);
    }
    #endregion

    protected virtual void OnEnable()
    {
        returningToPool = false;
    }

    protected virtual void OnDisable()
    {
        if (returningToPool || null == pool)
        {
            return;
        }

        returningToPool = true;
        pool.Release(this);
    }
}
