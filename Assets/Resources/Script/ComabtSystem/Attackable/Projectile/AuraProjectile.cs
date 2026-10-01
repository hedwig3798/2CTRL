using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오라 발사체가 기억하는 타겟 정보
/// </summary>
public struct AuraTarget
{
    public HealthSystem healthSystem;
    /// <summary>
    /// 겹쳐 있는 콜라이더 수 (콜라이더가 여러 개인 대상의 중복 Enter / Exit 처리)
    /// </summary>
    public int contactCount;
    public float nextHitTime;
}

/// <summary>
/// 발사 후 소멸하지 않고 닿은 적을 타격하는 발사체
/// Enter 한 대상을 즉시 타격하고 기억했다가, 무기의 coolTime 마다 다시 타격한다
/// Exit 한 대상은 기억에서 제거한다
/// 이동은 BlackBoard 로 초기화되는 이동 컴포넌트(Follow, OrbitMovement 등)가 담당한다
/// </summary>
public class AuraProjectile
    : Projectile
{
    private Dictionary<DamagePipeline, AuraTarget> targets = new Dictionary<DamagePipeline, AuraTarget>();
    private List<DamagePipeline> buffer = new List<DamagePipeline>();

    protected override bool OnLaunch()
    {
        targets.Clear();
        return true;
    }

    private void Update()
    {
        if (0 == targets.Count)
        {
            return;
        }

        buffer.Clear();
        buffer.AddRange(targets.Keys);

        foreach (DamagePipeline dp in buffer)
        {
            AuraTarget target = targets[dp];
            if (false == IsValid(dp, target))
            {
                targets.Remove(dp);
                continue;
            }

            if (Time.time < target.nextHitTime)
            {
                continue;
            }

            Hit(dp);
            target.nextHitTime = Time.time + weapon.coolTime;
            targets[dp] = target;
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

        if (targets.TryGetValue(dp, out AuraTarget target))
        {
            target.contactCount++;
            targets[dp] = target;
            return;
        }

        other.gameObject.TryGetComponent(out HealthSystem hs);
        target = new AuraTarget();
        target.healthSystem = hs;
        target.contactCount = 1;
        if (false == IsValid(dp, target))
        {
            return;
        }

        // 처음 들어온 대상은 즉시 타격
        Hit(dp);
        target.nextHitTime = Time.time + weapon.coolTime;
        targets.Add(dp, target);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (false == other.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            return;
        }

        if (false == targets.TryGetValue(dp, out AuraTarget target))
        {
            return;
        }

        target.contactCount--;
        if (target.contactCount <= 0)
        {
            targets.Remove(dp);
            return;
        }
        targets[dp] = target;
    }

    private void Hit(DamagePipeline _dp)
    {
        DamageMassage duplicateMassage = damageMassage;
        _dp.ProcessDamage(ref duplicateMassage);
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

    private static bool IsValid(DamagePipeline _dp, AuraTarget _target)
    {
        if (null == _dp || false == _dp.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (null != _target.healthSystem && true == _target.healthSystem.isDead)
        {
            return false;
        }

        return true;
    }

    protected override void OnDisable()
    {
        targets.Clear();
        buffer.Clear();

        base.OnDisable();
    }
}
