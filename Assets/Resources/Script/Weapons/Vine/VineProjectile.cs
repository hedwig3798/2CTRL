using UnityEngine;

/// <summary>
/// 덩굴 조각 발사체 (독 장판 PoisonZoneProjectile 을 상속)
/// 발사 시 주인 위치에 그대로 깔리고 (착지 이동 없음), 틱 간격마다 조각 안의 모든 적에게 데미지를 준다
/// - 마지막 조각과의 거리가 segmentSpacing 미만이면 만들지 않는다 (가만히 서 있으면 쌓이지 않는다. 마지막 조각이 사라지면 다시 깐다)
///   마지막 조각은 무기의 VineTrail 이 기억한다 (없으면 무기에 붙인다)
/// - 틱 간격 / 판정 반지름 / 수명은 독 장판과 같다 (TickInterval, ScaleOverLifetime 의 areaRadius, Lifetime)
/// - ★ FlagVineThorn : 틱에 적을 맞히면 thornRange 안의 가장 가까운 적에게 가시 1발 (Weapon.FireSub, 관통 없음, 데미지 thornDamageRate 배)
///   조각마다 thornCooldown 초에 한 번
/// - 프리팹에 PoisonZoneLandMovement 를 붙이지 않는다 (붙이면 주인 위치로 날아간다)
/// </summary>
public class VineProjectile
    : PoisonZoneProjectile
{
    [Header("vine")]
    [Tooltip("마지막 조각과의 거리가 이 값 미만이면 새 조각을 만들지 않는다")]
    [Min(0f)]
    [SerializeField]
    private float segmentSpacing = 0.7f;

    [Header("thorn (★)")]
    [Tooltip("FlagVineThorn 이 켜졌을 때 쏘는 가시 (StraightMovement + OneHitProjectile 구성)")]
    [SerializeField]
    private Projectile thornPrefab;
    [Tooltip("가시 데미지 배율 (무기 FinalDamage 기준)")]
    [Min(0f)]
    [SerializeField]
    private float thornDamageRate = 0.5f;
    [Tooltip("조각 하나가 가시를 쏘는 최소 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float thornCooldown = 1f;
    [Tooltip("가시 대상을 찾는 범위 (조각 중심 기준)")]
    [Min(0f)]
    [SerializeField]
    private float thornRange = 4f;
    [Tooltip("가시 수명 (초). 0 이면 무기 lifetime (덩굴 수명) 을 그대로 쓴다")]
    [Min(0f)]
    [SerializeField]
    private float thornLifetime = 1f;

    [Header("visual")]
    [Tooltip("조각 렌더러. 비워두면 정렬 레이어를 바꾸지 않는다")]
    [SerializeField]
    private SpriteRenderer segmentRenderer;
    [Tooltip("조각 렌더러 정렬 레이어 (Default 면 바닥에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";

    private VineTrail trail;
    private float nextThornTime;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (null != segmentRenderer && false == string.IsNullOrEmpty(sortingLayerName))
        {
            segmentRenderer.sortingLayerName = sortingLayerName;
        }
    }

    /// <summary>
    /// 주인 위치에 조각을 깐다. 마지막 조각과 너무 가까우면 발사하지 않는다
    /// </summary>
    protected override bool LaunchZone()
    {
        Transform origin = null != weapon.owner ? weapon.owner : weapon.transform;
        Vector3 position = origin.position;
        position.z = transform.position.z;

        VineTrail t = GetTrail();
        if (null != t && true == t.IsTooClose(position, segmentSpacing, this))
        {
            return false;
        }

        transform.position = position;
        nextThornTime = 0f;

        if (null != t)
        {
            t.SetLast(this);
        }
        return true;
    }

    protected override void OnTick(float _radius, int _hitCount)
    {
        if (_hitCount <= 0 || null == thornPrefab)
        {
            return;
        }

        if (false == weapon.HasFlag(WeaponParam.FlagVineThorn) || Time.time < nextThornTime)
        {
            return;
        }

        Transform target = FindNearest(transform.position, thornRange);
        if (null == target)
        {
            return;
        }

        nextThornTime = Time.time + thornCooldown;
        FireThorn(target.position);
    }

    /// <summary>
    /// 조각 위치에서 _point 를 향해 가시 1발을 쏜다 (관통 없음)
    /// </summary>
    private void FireThorn(Vector3 _point)
    {
        Vector3 start = transform.position;
        _point.z = start.z;

        Projectile p = weapon.FireSub(thornPrefab, start, _point, thornDamageRate);
        if (null == p)
        {
            return;
        }

        // 무기 hitCount / lifetime 은 덩굴 기준이라 가시 값으로 덮어쓴다
        if (p is OneHitProjectile oneHit)
        {
            oneHit.SetHitCount(1);
        }

        if (thornLifetime > 0f && p.TryGetComponent(out Lifetime lifetime))
        {
            lifetime.lifetime = thornLifetime;
        }
    }

    /// <summary>
    /// 무기의 VineTrail (없으면 붙인다)
    /// </summary>
    private VineTrail GetTrail()
    {
        if (null == trail && null != weapon)
        {
            if (false == weapon.TryGetComponent(out trail))
            {
                trail = weapon.gameObject.AddComponent<VineTrail>();
            }
        }
        return trail;
    }

    protected override void OnDisable()
    {
        nextThornTime = 0f;

        base.OnDisable();
    }
}
