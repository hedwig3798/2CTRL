using UnityEngine;

/// <summary>
/// 뿌리 속박 지역 발사체 (독 장판 PoisonZoneProjectile 을 상속)
/// 발사 시 주인 기준 무기 사거리(FinalRange) 안의 랜덤한 위치에 바로 생기고 (착지 이동 없음, 적이 없어도 생긴다),
/// 틱 간격마다 지역 안의 모든 적에게 데미지 + 둔화 (둔화는 플래그 없이 항상. 비율은 rootSlowRate)
/// - 틱 간격 / 판정 반지름 / 수명은 독 장판과 같다 (TickInterval, ScaleOverLifetime 의 areaRadius, Lifetime)
/// - ★ FlagRootChase : 지역이 가장 가까운 적을 향해 천천히 이동한다 (속도 = weapon.GetParam(ChaseSpeed, chaseSpeed))
/// - 프리팹에 PoisonZoneLandMovement 를 붙이지 않는다
/// </summary>
public class RootProjectile
    : PoisonZoneProjectile
{
    [Header("slow")]
    [Tooltip("지역 위 적의 이동 속도 감소 비율 (0.4 = -40%). 독 장판의 slowRate 대신 쓴다")]
    [Range(0f, 1f)]
    [SerializeField]
    private float rootSlowRate = 0.4f;

    [Header("chase (★)")]
    [Tooltip("FlagRootChase 가 켜졌을 때 지역의 이동 속도")]
    [Min(0f)]
    [SerializeField]
    private float chaseSpeed = 1.2f;
    [Tooltip("추적 대상을 찾는 범위 (지역 중심 기준). 0 이면 무기 FinalRange")]
    [Min(0f)]
    [SerializeField]
    private float chaseRange = 0f;
    [Tooltip("추적 대상을 다시 찾는 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float chaseSearchInterval = 0.25f;

    [Header("visual")]
    [Tooltip("지역 렌더러. 비워두면 정렬 레이어를 바꾸지 않는다")]
    [SerializeField]
    private SpriteRenderer zoneRenderer;
    [Tooltip("지역 렌더러 정렬 레이어 (Default 면 바닥에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";

    private Transform chaseTarget;
    private float nextSearchTime;

    /// <summary>
    /// 둔화는 항상 켜져 있다
    /// </summary>
    protected override bool IsSlowActive => true;

    /// <summary>
    /// 둔화 비율은 rootSlowRate
    /// </summary>
    protected override float SlowRate => rootSlowRate;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (null != zoneRenderer && false == string.IsNullOrEmpty(sortingLayerName))
        {
            zoneRenderer.sortingLayerName = sortingLayerName;
        }
    }

    /// <summary>
    /// 주인 기준 사거리 안의 랜덤한 위치에 지역을 만든다
    /// </summary>
    protected override bool LaunchZone()
    {
        Transform origin = null != weapon.owner ? weapon.owner : weapon.transform;
        Vector3 position = origin.position + (Vector3)(Random.insideUnitCircle * weapon.FinalRange);
        position.z = transform.position.z;
        transform.position = position;

        ResetChase();
        return true;
    }

    /// <summary>
    /// ★ 가장 가까운 적을 향해 이동한다
    /// </summary>
    protected override void OnLandedUpdate()
    {
        if (false == weapon.HasFlag(WeaponParam.FlagRootChase))
        {
            return;
        }

        if (Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + chaseSearchInterval;
            float range = chaseRange > 0f ? chaseRange : weapon.FinalRange;
            chaseTarget = FindNearest(transform.position, range);
        }

        if (null == chaseTarget || false == IsAlive(chaseTarget))
        {
            return;
        }

        Vector3 destination = chaseTarget.position;
        destination.z = transform.position.z;
        float speed = Mathf.Max(0f, weapon.GetParam(WeaponParam.ChaseSpeed, chaseSpeed));
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
    }

    private void ResetChase()
    {
        chaseTarget = null;
        nextSearchTime = 0f;
    }

    protected override void OnDisable()
    {
        ResetChase();

        base.OnDisable();
    }
}
