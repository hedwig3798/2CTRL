using UnityEngine;

/// <summary>
/// 보스 키워드 공용 헬퍼
/// </summary>
public static class BossKeywordUtil
{
    /// <summary>
    /// HP 비율이 _ratio 이하인지 (광폭화 판정)
    /// </summary>
    public static bool IsEnraged(MonsterContext _ctx, float _ratio)
    {
        if (null == _ctx || null == _ctx.health)
        {
            return false;
        }
        return BossMath.IsHPAtOrBelow(_ctx.health.CurrHP, _ctx.health.MaxHP, _ratio);
    }

    /// <summary>
    /// 대상 플레이어의 DamagePipeline (HealthSystem 과 같은 오브젝트의 것, 없으면 첫 DamagePipeline)
    /// </summary>
    public static DamagePipeline FindTargetPipeline(Transform _target)
    {
        if (null == _target)
        {
            return null;
        }

        PlayerStats stats = _target.GetComponentInParent<PlayerStats>();
        Transform root = null != stats ? stats.transform : _target;

        HealthSystem health = root.GetComponentInChildren<HealthSystem>(true);
        if (null != health && true == health.TryGetComponent(out DamagePipeline dp))
        {
            return dp;
        }
        return root.GetComponentInChildren<DamagePipeline>(true);
    }

    /// <summary>
    /// _target 의 파이프라인으로 데미지를 보낸다 (넉백 없음)
    /// </summary>
    public static void DamageTarget(DamagePipeline _target, float _damage, DamagePipeline _attacker, Vector2 _hitPoint)
    {
        if (null == _target || _damage <= 0f)
        {
            return;
        }

        DamageMassage msg = new DamageMassage();
        msg.damage = _damage;
        msg.attacker = _attacker;
        msg.hitPoint = _hitPoint;
        _target.ProcessDamage(ref msg);
    }

    /// <summary>
    /// 몬스터 레이어 (예고 표시를 같은 카메라에 보이게 할 때)
    /// </summary>
    public static int Layer(MonsterContext _ctx)
    {
        return null != _ctx && null != _ctx.root ? _ctx.root.gameObject.layer : 0;
    }
}
