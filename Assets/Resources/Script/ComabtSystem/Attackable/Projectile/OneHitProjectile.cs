using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가장 가까운 적을 향해 발사되어 닿은 적을 한 번씩 타격하는 발사체
/// 같은 적은 두 번 이상 타격하지 않으며, hitCount 만큼 서로 다른 적을 타격하면 사라진다
/// 이동은 BlackBoard 로 초기화되는 이동 컴포넌트(Straight, ChainMovement 등)가 담당한다
/// </summary>
public class OneHitProjectile
    : Projectile
    , Initializable
{
    /// <summary>
    /// 타격할 적 수. BlackBoard 로 받는다
    /// </summary>
    private int hitCount = 1;

    private HashSet<DamagePipeline> hitTargets = new HashSet<DamagePipeline>();
    private int currHitCount;

    /// <summary>
    /// 타격 수만 갱신한다 (타격 기록은 OnLaunch / OnDisable 에서만 초기화)
    /// </summary>
    public void Initialize(BlackBoard _data)
    {
        hitCount = Mathf.Max(1, _data.GetInt(DATA_TYPE.hitCount));
    }

    /// <summary>
    /// 공격한 적에 대한 정보를 초기화한다
    /// </summary>
    public void ResetHitTargets()
    {
        hitTargets.Clear();
        currHitCount = 0;
    }

    protected override bool OnLaunch()
    {
        ResetHitTargets();

        BlackBoard b = GetBlackBoard();
        if (null == b)
        {
            return false;
        }

        Transform target = FindNearest(weapon.transform.position, weapon.range);
        if (null == target)
        {
            return false;
        }

        // 무기가 기록한 기본 이동 대상(주인) 대신 가장 가까운 적을 향한다
        b.SetTransform(DATA_TYPE.moveTarget, target);
        return true;
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
        DamageMassage duplicateMassage = damageMassage;
        dp.ProcessDamage(ref duplicateMassage);

        ++currHitCount;
        if (currHitCount >= hitCount)
        {
            gameObject.SetActive(false);
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

    protected override void OnDisable()
    {
        ResetHitTargets();

        base.OnDisable();
    }
}
