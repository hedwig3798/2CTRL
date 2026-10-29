using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 휘두르기(Sweep) 무기의 칼날 발사체
/// 발사 시 무기 범위 안의 가장 가까운 적을 조준하고 (없으면 발사하지 않음), 휘두르는 동안 닿은 적을 한 번씩 타격한다
/// - 타격 수 제한이 없어 한 번 휘두를 때 닿은 모든 적을 타격하며, 같은 적은 한 번만 타격한다
///   (오라처럼 재타격하지 않으므로 공격 속도가 올라 공격 간격이 휘두르는 시간보다 짧아져도 한 번의 휘두르기가 같은 적을 두 번 때리지 않는다)
/// - 넉백은 칼날 위치가 아니라 휘두르는 중심(무기 = 플레이어) 에서 바깥쪽으로 밀어낸다
/// - 칼날 길이(areaRadius) 를 무기의 최종 범위에 맞춰 BlackBoard 에 기록한다 (SweepMovement 가 사용)
/// 이동 / 회전은 SweepMovement 가 담당한다
/// - 업그레이드 : 칼날 길이 = weapon.GetParam(Reach, 기본 칼날 길이). 양방향은 fireCount +1 이면 SweepMovement 가 formation 으로 반대 방향에 배치한다
/// - ★ FlagSlashWave : 첫 번째 칼날(formation 0)을 휘두를 때 조준 방향으로 관통 투사체(slashPrefab)를 데미지 slashDamageRate 배로 쏜다
/// </summary>
public class SweepProjectile
    : Projectile
{
    [Header("sweep")]
    [Tooltip("칼날 끝이 닿는 거리 = 무기 최종 범위(FinalRange) + reachPadding. 범위 끝에 있는 적의 몸통까지 닿도록 조금 더 길게 둔다. 무기 areaRadius 가 0 보다 크면 그 값을 그대로 쓴다")]
    [Min(0f)]
    [SerializeField]
    private float reachPadding = 0.3f;

    [Header("slash wave (★)")]
    [Tooltip("검기 발사체 프리팹 (OneHitProjectile + Straight 등). 비어 있으면 검기를 쏘지 않는다")]
    [SerializeField]
    private Projectile slashPrefab;
    [Tooltip("검기 데미지 = 무기 최종 데미지 * 이 값")]
    [Min(0f)]
    [SerializeField]
    private float slashDamageRate = 0.6f;
    [Tooltip("검기가 관통해 타격할 적 수 (OneHitProjectile 일 때)")]
    [Min(1)]
    [SerializeField]
    private int slashHitCount = 99;
    [Tooltip("검기 수명 (초). 0 이면 무기 lifetime / 프리팹 Lifetime 기본값 그대로 (Sweep 무기 lifetime 은 휘두르는 시간이므로 따로 지정한다)")]
    [Min(0f)]
    [SerializeField]
    private float slashLifetime = 0.8f;

    private HashSet<DamagePipeline> hitTargets = new HashSet<DamagePipeline>();

    protected override bool OnLaunch()
    {
        hitTargets.Clear();
        if (false == AimAtNearest())
        {
            return false;
        }

        WriteReach();
        TryFireSlash();
        return true;
    }

    protected override bool OnLaunchAt()
    {
        hitTargets.Clear();
        WriteReach();
        return true;
    }

    /// <summary>
    /// 칼날 끝이 닿는 거리를 BlackBoard 의 areaRadius 로 기록한다
    /// </summary>
    private void WriteReach()
    {
        BlackBoard b = GetBlackBoard();
        if (null == b)
        {
            return;
        }

        float reach = weapon.areaRadius > 0f ? weapon.areaRadius : weapon.FinalRange + reachPadding;
        reach = weapon.GetParam(WeaponParam.Reach, reach);
        b.SetFloat(DATA_TYPE.areaRadius, Mathf.Max(0.1f, reach));
    }

    /// <summary>
    /// ★ 검기 : 첫 번째 칼날만 무기 위치에서 조준 대상(moveTarget) 방향으로 관통 투사체를 쏜다
    /// </summary>
    private void TryFireSlash()
    {
        if (0 != formationIndex || null == slashPrefab || false == weapon.HasFlag(WeaponParam.FlagSlashWave))
        {
            return;
        }

        BlackBoard b = GetBlackBoard();
        Transform target = null != b ? b.GetTransform(DATA_TYPE.moveTarget) : null;
        if (null == target)
        {
            return;
        }

        Vector3 start = weapon.transform.position;
        Vector3 point = target.position;
        if ((point - start).sqrMagnitude < 0.0001f)
        {
            point = start + Vector3.right;
        }

        Projectile p = weapon.FireSub(slashPrefab, start, point, slashDamageRate);
        if (null == p)
        {
            return;
        }

        if (p is OneHitProjectile oneHit)
        {
            oneHit.SetHitCount(slashHitCount);
        }

        if (slashLifetime > 0f && p.TryGetComponent(out Lifetime lifetime))
        {
            lifetime.lifetime = slashLifetime;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (false == IsTargetLayer(other.gameObject.layer))
        {
            return;
        }

        if (false == other.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            return;
        }

        if (true == hitTargets.Contains(dp))
        {
            return;
        }

        if (other.gameObject.TryGetComponent(out HealthSystem hs) && true == hs.isDead)
        {
            return;
        }

        hitTargets.Add(dp);
        Hit(dp);
    }

    /// <summary>
    /// 휘두르는 중심(무기 위치) 을 타격 지점으로 데미지를 보낸다. 넉백이 중심에서 바깥쪽으로 향한다
    /// </summary>
    private void Hit(DamagePipeline _dp)
    {
        SendDamage(_dp, null != weapon ? weapon.transform.position : transform.position);
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

    protected override void OnDisable()
    {
        hitTargets.Clear();

        base.OnDisable();
    }
}
