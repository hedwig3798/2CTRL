using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폭발탄 발사체
/// 가장 가까운 적을 향해 날아가다가 처음 닿은 적의 자리(착탄 지점)에서 폭발한다
/// - 폭발은 착탄 지점을 중심으로 반지름 areaRadius(BlackBoard) 안의 모든 적에게 데미지를 주고 바깥쪽으로 밀어낸다
/// - 직접 맞은 적도 폭발 데미지만 한 번 받는다 (직격 + 폭발 중복 타격 없음)
/// - 적에게 닿지 못해도 목표 지점(moveTargetPoint)을 지나면 그 자리에서 폭발한다 (explodeAtTargetPoint)
/// - Lifetime 은 안전장치이며, 수명이 먼저 끝나면 폭발하지 않고 사라진다
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

        gameObject.SetActive(false);
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
