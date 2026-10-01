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
    [Header("projectile damage")]
    public float baseDamage;

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
    /// 타겟이 없어 발사하지 못하면 false 를 반환하고, 무기가 즉시 풀로 반환한다
    /// 성공하면 BlackBoard 로 이동 컴포넌트를 초기화한다 (OnLaunch 에서 값을 덮어쓸 수 있다)
    /// </summary>
    public bool Launch(Weapon _weapon)
    {
        weapon = _weapon;

        float rate = weapon.damageRate;
        if (rate <= 0f)
        {
            rate = 1f;
        }
        damageMassage.damage = rate * baseDamage;
        damageMassage.isReflected = false;
        damageMassage.attacker = null;

        if (false == OnLaunch())
        {
            return false;
        }

        InitializeBlackBoard();
        return true;
    }

    protected abstract bool OnLaunch();

    #region Target
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
