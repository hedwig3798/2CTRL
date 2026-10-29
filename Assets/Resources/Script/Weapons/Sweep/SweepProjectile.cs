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
/// </summary>
public class SweepProjectile
    : Projectile
{
    [Header("sweep")]
    [Tooltip("칼날 끝이 닿는 거리 = 무기 최종 범위(FinalRange) + reachPadding. 범위 끝에 있는 적의 몸통까지 닿도록 조금 더 길게 둔다. 무기 areaRadius 가 0 보다 크면 그 값을 그대로 쓴다")]
    [Min(0f)]
    [SerializeField]
    private float reachPadding = 0.3f;

    private HashSet<DamagePipeline> hitTargets = new HashSet<DamagePipeline>();

    protected override bool OnLaunch()
    {
        hitTargets.Clear();
        if (false == AimAtNearest())
        {
            return false;
        }

        WriteReach();
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
        b.SetFloat(DATA_TYPE.areaRadius, reach);
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
