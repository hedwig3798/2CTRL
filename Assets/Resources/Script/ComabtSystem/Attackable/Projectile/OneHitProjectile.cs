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

    // 같은 오브젝트의 타격 데미지 배율 / 타격 알림 컴포넌트
    private IHitDamageModifier[] damageModifiers;
    private IProjectileHitListener[] hitListeners;

    /// <summary>
    /// 이 발사체를 발사한 무기 (발사 전이면 null)
    /// </summary>
    public Weapon OwnerWeapon => weapon;

    private void Awake()
    {
        damageModifiers = GetComponents<IHitDamageModifier>();
        hitListeners = GetComponents<IProjectileHitListener>();
    }

    /// <summary>
    /// 타격 수만 갱신한다 (타격 기록은 OnLaunch / OnDisable 에서만 초기화)
    /// </summary>
    public void Initialize(BlackBoard _data)
    {
        hitCount = Mathf.Max(1, _data.GetInt(DATA_TYPE.hitCount));
    }

    /// <summary>
    /// 타격 수를 덮어쓴다 (보조 발사체를 관통형으로 만들 때 등, 발사 직후에 쓴다)
    /// </summary>
    public void SetHitCount(int _count)
    {
        hitCount = Mathf.Max(1, _count);
    }

    /// <summary>
    /// _dp 를 이미 타격한 대상으로 기록해 타격하지 않게 한다 (타격 수에는 세지 않는다)
    /// 분열탄 조각이 원래 탄이 맞힌 적을 다시 맞히지 않도록 발사 직후에 쓴다
    /// </summary>
    public void IgnoreTarget(DamagePipeline _dp)
    {
        if (null != _dp)
        {
            hitTargets.Add(_dp);
        }
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
        return AimAtNearest();
    }

    protected override bool OnLaunchAt()
    {
        ResetHitTargets();
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
        HitTarget(dp, currHitCount);

        ++currHitCount;
        if (currHitCount >= hitCount)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 데미지 배율 컴포넌트의 배율을 곱해 타격하고, 타격 알림 컴포넌트에 알린다
    /// </summary>
    private void HitTarget(DamagePipeline _dp, int _hitIndex)
    {
        float rate = 1f;
        if (null != damageModifiers)
        {
            foreach (IHitDamageModifier m in damageModifiers)
            {
                rate *= m.GetHitDamageRate(this, _hitIndex);
            }
        }

        if (1f == rate)
        {
            ApplyDamage(_dp);
        }
        else
        {
            float baseDamage = damageMassage.damage;
            damageMassage.damage = baseDamage * Mathf.Max(0f, rate);
            ApplyDamage(_dp);
            damageMassage.damage = baseDamage;
        }

        if (null != hitListeners)
        {
            foreach (IProjectileHitListener l in hitListeners)
            {
                l.OnProjectileHit(this, _dp, _hitIndex);
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

    protected override void OnDisable()
    {
        ResetHitTargets();

        base.OnDisable();
    }
}
