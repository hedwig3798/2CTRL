using UnityEngine;

/// <summary>
/// 독 장판 발사체
/// 발사 시 무기 범위 안의 가장 가까운 적을 목표로 삼고 (적이 없으면 발사하지 않는다),
/// 착지 이동(PoisonZoneLandMovement)이 끝나면 그 자리에 머물며 틱 간격마다 장판 안의 모든 적에게 데미지를 준다
/// - 판정 반지름은 현재 크기를 따른다 (unitRadius * 크기). ScaleOverLifetime 으로 등장 / 사라질 때 판정도 같이 줄어든다
/// - 틱 간격은 tickInterval 을 무기 기본 공격 속도 대비 최종 공격 속도 배율만큼 줄인 값이다 (공격 속도 스텟이 오르면 더 자주 타격)
/// - 무기의 공격 간격(1 / FinalAttackSpeed)은 장판 생성 간격으로만 쓰인다
///   (AuraProjectile 처럼 FinalCoolTime 을 틱으로 쓰면 틱을 빠르게 할수록 장판이 너무 많이 깔리므로 둘을 분리한다)
/// - 소멸은 Lifetime 이 담당한다
/// </summary>
public class PoisonZoneProjectile
    : Projectile
{
    [Header("poison zone")]
    [Tooltip("장판 안의 적을 타격하는 기본 간격 (초). 실제 간격 = tickInterval * (무기 attackSpeed / 무기 FinalAttackSpeed)")]
    [Min(0.05f)]
    public float tickInterval = 0.5f;
    [Tooltip("크기 1 일 때의 판정 반지름 (DummyCircle 기준 0.5)")]
    [Min(0.01f)]
    public float unitRadius = 0.5f;
    [Tooltip("판정 반지름이 이 값 미만이면 타격하지 않는다 (등장 직후 / 사라지기 직전)")]
    [Min(0f)]
    public float minHitRadius = 0.2f;
    [Tooltip("비워두면 같은 오브젝트의 PoisonZoneLandMovement. 없으면 발사 즉시 착지한 것으로 본다")]
    public PoisonZoneLandMovement landMovement;

    private bool landed;
    private float nextTickTime;

    /// <summary>
    /// 현재 크기에 맞춘 판정 반지름
    /// </summary>
    public float CurrentRadius => unitRadius * Mathf.Abs(transform.lossyScale.x);

    /// <summary>
    /// 공격 속도 스텟이 반영된 실제 틱 간격 (초)
    /// </summary>
    public float FinalTickInterval
    {
        get
        {
            if (null == weapon)
            {
                return tickInterval;
            }

            float speed = weapon.FinalAttackSpeed;
            if (weapon.attackSpeed <= 0f || speed <= 0f)
            {
                return tickInterval;
            }

            return Mathf.Max(0.05f, tickInterval * (weapon.attackSpeed / speed));
        }
    }

    private void Awake()
    {
        if (null == landMovement)
        {
            landMovement = GetComponent<PoisonZoneLandMovement>();
        }
    }

    protected override bool OnLaunch()
    {
        landed = false;
        return AimAtNearest();
    }

    protected override bool OnLaunchAt()
    {
        landed = false;
        return true;
    }

    private void Update()
    {
        if (null == weapon)
        {
            return;
        }

        if (false == landed)
        {
            if (null != landMovement && false == landMovement.IsLanded)
            {
                return;
            }

            // 착지한 순간 바로 첫 타격
            landed = true;
            nextTickTime = Time.time;
        }

        if (Time.time < nextTickTime)
        {
            return;
        }

        float radius = CurrentRadius;
        if (radius < minHitRadius)
        {
            return;
        }

        ApplyAreaDamage(transform.position, radius);
        nextTickTime = Time.time + FinalTickInterval;
    }

    protected override void OnDisable()
    {
        landed = false;

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, CurrentRadius);
    }
}
