using UnityEngine;

/// <summary>
/// 분열탄 본탄
/// 가장 가까운 적을 향해 날아가 처음 닿은 적에게 데미지를 주고,
/// 그 자리에서 조각 fragmentCount 개를 사방(균등한 각도 + 랜덤 회전)으로 흩뿌린 뒤 사라진다
/// 조각은 Weapon.FireSub 로 발사하며, 본탄이 맞힌 적은 다시 맞히지 않는다
/// 이동은 BlackBoard 로 초기화되는 이동 컴포넌트(Straight)가 담당한다
/// </summary>
public class SplitShotProjectile
    : Projectile
{
    [Header("fragment")]
    [Tooltip("분열 시 흩뿌릴 조각 발사체 프리팹. 조각은 다시 분열하지 않는 발사체(OneHitProjectile 등)를 쓴다")]
    public Projectile fragmentPrefab;
    [Tooltip("분열 시 흩뿌릴 조각 수")]
    [Min(0)]
    public int fragmentCount = 6;
    [Tooltip("조각 하나의 데미지 배율. 조각 데미지 = 무기 최종 데미지 * fragmentDamageRate")]
    [Min(0f)]
    public float fragmentDamageRate = 0.4f;
    [Tooltip("조각이 향할 지점까지의 거리. 방향을 정하는 데만 쓰이며, 실제 비행 거리는 조각의 속도와 Lifetime 이 정한다")]
    [Min(0.1f)]
    public float fragmentDistance = 3f;

    [Header("burst effect")]
    [Tooltip("분열할 때 타격 지점에 재생할 연출. 비워두면 연출 없음")]
    public PooledEffect burstEffect;
    [Tooltip("분열 연출의 반지름. 0 이면 연출 프리팹 크기 그대로")]
    [Min(0f)]
    public float burstRadius = 0f;

    // 본탄이 이미 타격(분열)했는지. 한 번 분열하면 사라질 때까지 더 이상 타격하지 않는다
    private bool hasHit;

    // LaunchAt 으로 발사된 경우(다른 무기의 보조 발사체 등) 분열하지 않는다
    private bool canSplit = true;

    protected override bool OnLaunch()
    {
        hasHit = false;
        canSplit = true;
        return AimAtNearest();
    }

    protected override bool OnLaunchAt()
    {
        hasHit = false;
        canSplit = false;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (true == hasHit)
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

        hasHit = true;
        ApplyDamage(dp);

        if (true == canSplit)
        {
            Split(transform.position, dp);
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// _hitPoint 에서 조각 fragmentCount 개를 균등한 각도로 흩뿌린다 (전체 각도에 랜덤 회전을 더한다)
    /// 조각은 _hitTarget 을 타격하지 않는다
    /// </summary>
    private void Split(Vector3 _hitPoint, DamagePipeline _hitTarget)
    {
        if (null != burstEffect)
        {
            EffectPool.Spawn(burstEffect, _hitPoint, burstRadius);
        }

        if (null == weapon || null == fragmentPrefab || fragmentCount <= 0)
        {
            return;
        }

        _hitPoint.z = 0f;
        float step = 360f / fragmentCount;
        float offset = Random.Range(0f, step);

        for (int i = 0; i < fragmentCount; ++i)
        {
            float angle = (offset + step * i) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            Projectile p = weapon.FireSub(
                fragmentPrefab
                , _hitPoint
                , _hitPoint + dir * fragmentDistance
                , fragmentDamageRate
                , i
                , fragmentCount
            );

            if (p is OneHitProjectile oneHit)
            {
                oneHit.IgnoreTarget(_hitTarget);
            }
        }
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
        hasHit = false;

        base.OnEnable();
    }
}
