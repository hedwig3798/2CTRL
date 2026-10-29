using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폭발탄 발사체
/// 가장 가까운 적을 향해 날아가다가 처음 닿은 적의 자리(착탄 지점)에서 폭발한다
/// - 폭발은 착탄 지점을 중심으로 반지름 areaRadius(BlackBoard) 안의 모든 적에게 데미지를 주고 바깥쪽으로 밀어낸다
/// - 직접 맞은 적도 폭발 데미지만 한 번 받는다 (직격 + 폭발 중복 타격 없음)
/// - 적에게 닿지 못해도 목표 지점(moveTargetPoint)을 지나면 그 자리에서 폭발한다 (explodeAtTargetPoint)
/// - Lifetime 은 안전장치이며, 수명이 먼저 끝나면 폭발하지 않고 사라진다
/// - ★ FlagSecondaryExplosion : secondaryDelay 뒤 같은 자리에서 반지름 secondaryRadiusRate 배, 데미지 secondaryDamageRate 배로 한 번 더 폭발한다
///   (발사체는 바로 풀로 돌아가므로 2차 폭발은 무기의 코루틴이 처리한다)
/// 이동은 BlackBoard 로 초기화되는 이동 컴포넌트(Straight 등)가 담당한다
/// </summary>
public class ExplosiveProjectile
    : Projectile
    , Initializable
{
    [Header("explosion")]
    [Tooltip("폭발 연출 프리팹. EffectPool 로 꺼내며 크기는 폭발 반지름에 맞춘다")]
    public PooledEffect explosionEffect;
    [Tooltip("BlackBoard 의 areaRadius 가 0 이하일 때 쓸 폭발 반지름")]
    [Min(0f)]
    public float defaultRadius = 1.5f;

    [Header("target point")]
    [Tooltip("켜면 적에게 닿지 않아도 목표 지점에 도착하면 그 자리에서 폭발한다")]
    public bool explodeAtTargetPoint = true;

    [Header("secondary explosion (★)")]
    [Tooltip("1차 폭발 후 2차 폭발까지의 시간 (초)")]
    [Min(0f)]
    [SerializeField]
    private float secondaryDelay = 0.3f;
    [Tooltip("2차 폭발 반지름 = 1차 반지름 * 이 값")]
    [Min(0f)]
    [SerializeField]
    private float secondaryRadiusRate = 0.6f;
    [Tooltip("2차 폭발 데미지 = 1차 데미지 * 이 값")]
    [Min(0f)]
    [SerializeField]
    private float secondaryDamageRate = 0.5f;

    // 2차 폭발 범위 탐색용 (코루틴은 한 번에 하나씩만 실행되므로 공유한다)
    private static readonly List<Transform> secondaryBuffer = new List<Transform>();

    // 2차 폭발 대기 (secondaryDelay 가 같으면 재사용)
    private WaitForSeconds secondaryWait;
    private float secondaryWaitDelay = -1f;

    /// <summary>
    /// 폭발 반지름. BlackBoard 로 받는다
    /// </summary>
    private float radius;

    private bool hasTargetPoint;
    private Vector2 startPoint;
    private Vector2 targetPoint;
    private float travelSqrDistance;

    // 한 프레임에 여러 적과 겹쳐도 한 번만 폭발하도록
    private bool exploded;

    // 폭발 범위 데미지에서 제외할 대상 (직접 맞은 적)
    private HashSet<Transform> excludeTargets = new HashSet<Transform>();

    /// <summary>
    /// 폭발 반지름과 목표 지점을 갱신한다
    /// </summary>
    public void Initialize(BlackBoard _data)
    {
        float value = _data.GetFloat(DATA_TYPE.areaRadius);
        radius = value > 0f ? value : defaultRadius;

        hasTargetPoint = false;
        if (true == _data.TryGetVector(DATA_TYPE.moveTargetPoint, out Vector3 point)
            && true == _data.TryGetStartPoint(out Vector3 start))
        {
            startPoint = start;
            targetPoint = point;
            travelSqrDistance = (targetPoint - startPoint).sqrMagnitude;
            hasTargetPoint = true;
        }
    }

    protected override bool OnLaunch()
    {
        exploded = false;
        return AimAtNearest();
    }

    protected override bool OnLaunchAt()
    {
        exploded = false;
        return true;
    }

    private void Update()
    {
        if (true == exploded || false == explodeAtTargetPoint || false == hasTargetPoint)
        {
            return;
        }

        // 시작 지점에서 목표 지점까지의 거리만큼 날아갔으면 목표 지점에서 폭발한다
        if (((Vector2)transform.position - startPoint).sqrMagnitude >= travelSqrDistance)
        {
            Explode(targetPoint, null);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (true == exploded)
        {
            return;
        }

        if (false == IsTargetLayer(other.gameObject.layer))
        {
            return;
        }

        if (false == other.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            return;
        }

        if (other.gameObject.TryGetComponent(out HealthSystem hs) && true == hs.isDead)
        {
            return;
        }

        Explode(transform.position, dp);
    }

    /// <summary>
    /// _center 에서 폭발한다
    /// _direct(직접 맞은 적)는 범위 탐색에서 빼고 따로 한 번만 타격해, 반지름 밖에 중심이 있어도 반드시 맞고 두 번 맞지는 않게 한다
    /// </summary>
    private void Explode(Vector2 _center, DamagePipeline _direct)
    {
        exploded = true;
        transform.position = new Vector3(_center.x, _center.y, transform.position.z);

        excludeTargets.Clear();
        if (null != _direct)
        {
            excludeTargets.Add(_direct.transform);
            // 타격 지점 = 발사체 위치 = 폭발 중심이므로 넉백도 폭발 중심에서 바깥쪽
            ApplyDamage(_direct);
        }

        ApplyAreaDamage(_center, radius, excludeTargets);
        excludeTargets.Clear();

        EffectPool.Spawn(explosionEffect, _center, radius);

        TryStartSecondaryExplosion(_center);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// ★ 2차 폭발을 무기의 코루틴으로 예약한다 (발사체는 곧 풀로 돌아가 재사용될 수 있으므로 필요한 값을 복사해 넘긴다)
    /// </summary>
    private void TryStartSecondaryExplosion(Vector2 _center)
    {
        if (null == weapon
            || false == weapon.isActiveAndEnabled
            || false == weapon.HasFlag(WeaponParam.FlagSecondaryExplosion))
        {
            return;
        }

        DamageMassage message = damageMassage;
        message.damage *= secondaryDamageRate;
        weapon.StartCoroutine(SecondaryExplosionRoutine(
            weapon
            , explosionEffect
            , _center
            , radius * secondaryRadiusRate
            , message
            , GetSecondaryWait()
        ));
    }

    /// <summary>
    /// secondaryDelay 대기 객체 (0 이하면 null = 대기 없음). 값이 바뀔 때만 새로 만든다
    /// </summary>
    private WaitForSeconds GetSecondaryWait()
    {
        if (secondaryDelay <= 0f)
        {
            return null;
        }

        if (null == secondaryWait || secondaryWaitDelay != secondaryDelay)
        {
            secondaryWait = new WaitForSeconds(secondaryDelay);
            secondaryWaitDelay = secondaryDelay;
        }
        return secondaryWait;
    }

    private static IEnumerator SecondaryExplosionRoutine(Weapon _weapon, PooledEffect _effect, Vector2 _center, float _radius, DamageMassage _message, WaitForSeconds _wait)
    {
        if (null != _wait)
        {
            yield return _wait;
        }

        if (null == _weapon || _radius <= 0f)
        {
            yield break;
        }

        int count = TargetFinder.FindAll(
            _center
            , _radius
            , _weapon.targetLayer
            , secondaryBuffer
            , null
            , _weapon.transform
            , _weapon.owner
        );
        for (int i = 0; i < count; ++i)
        {
            if (secondaryBuffer[i].TryGetComponent(out DamagePipeline dp))
            {
                DamageMassage message = _message;
                message.hitPoint = _center;
                dp.ProcessDamage(ref message);
            }
        }
        secondaryBuffer.Clear();

        EffectPool.Spawn(_effect, _center, _radius);
    }

    private bool IsTargetLayer(int _layer)
    {
        if (_layer == gameObject.layer)
        {
            return false;
        }

        if (null == weapon)
        {
            return false;
        }

        return 0 != (weapon.targetLayer.value & (1 << _layer));
    }

    protected override void OnEnable()
    {
        exploded = false;

        base.OnEnable();
    }

    protected override void OnDisable()
    {
        hasTargetPoint = false;
        excludeTargets.Clear();

        base.OnDisable();
    }
}
