using UnityEngine;

/// <summary>
/// 화면 공간 대상에게 데미지를 준다 (물리 콜라이더 없이 HealthSystem 에 직접)
/// - 일반 데미지: 대상의 DamagePipeline 으로 DamageMassage 전달 (없으면 HealthSystem.ProcessDamage). 투사체 SendDamage 와 같은 경로
/// - 즉사: HealthSystem.Execute (SpinBlade ★ 와 같은 방식). 즉사 면역(최종 보스 프록시, 중간 보스)이면 fallbackDamage 일반 데미지
/// - 타격 지점은 대상의 진영 월드 위치, 넉백 없음
/// </summary>
public static class StageDamage
{
    /// <summary>
    /// _damage 만큼 일반 데미지. 대상이 이미 죽었거나 꺼져 있으면 false
    /// _source 가 DamagePipeline 이거나 그것을 가진 Component/GameObject 면 attacker 로 쓴다
    /// 차원 무기는 투사체·장판도 소유 DimensionWeapon 을 _source 로 넘긴다 (귀속 유지). 이때 attacker 탐색은 생략
    /// </summary>
    public static bool Apply(HealthSystem _target, float _damage, Object _source = null)
    {
        if (false == IsHittable(_target, false) || _damage <= 0f)
        {
            return false;
        }

        DamageMassage message = MakeMessage(_target, _damage, _source);
        if (true == _target.TryGetComponent(out DamagePipeline pipeline))
        {
            pipeline.ProcessDamage(ref message);
        }
        else
        {
            _target.ProcessDamage(ref message);
        }
        return true;
    }

    public static bool Apply(in StageTarget _target, float _damage, Object _source = null)
    {
        return Apply(_target.health, _damage, _source);
    }

    /// <summary>
    /// 즉사. 즉사 면역이면 _fallbackDamage 를 일반 데미지로 준다. 부활 대기(다운) 중인 적도 최종 사망시킨다
    /// </summary>
    public static bool Execute(HealthSystem _target, float _fallbackDamage, Object _source = null)
    {
        if (null == _target)
        {
            return false;
        }
        if (true == _target.IsExecuteImmune)
        {
            return Apply(_target, _fallbackDamage, _source);
        }
        if (false == IsHittable(_target, true))
        {
            return false;
        }

        DamageMassage message = MakeMessage(_target, _fallbackDamage, _source);
        _target.Execute(ref message);
        return true;
    }

    public static bool Execute(in StageTarget _target, float _fallbackDamage, Object _source = null)
    {
        return Execute(_target.health, _fallbackDamage, _source);
    }

    private static bool IsHittable(HealthSystem _target, bool _includeDowned)
    {
        if (null == _target || false == _target.isActiveAndEnabled)
        {
            return false;
        }
        return false == _target.isDead || (true == _includeDowned && true == _target.IsDowned);
    }

    private static DamageMassage MakeMessage(HealthSystem _target, float _damage, Object _source)
    {
        DamageMassage message = new DamageMassage();
        message.damage = _damage;
        message.attacker = ResolveAttacker(_source);
        message.hitPoint = _target.transform.position;
        message.knockbackForce = 0f;
        message.weapon = _source as Weapon;
        message.dimensionWeapon = _source as DimensionWeapon;
        return message;
    }

    private static DamagePipeline ResolveAttacker(Object _source)
    {
        if (_source is DamagePipeline pipeline)
        {
            return pipeline;
        }
        // 차원 무기는 DamagePipeline 을 갖지 않는다 (타격마다 GetComponentInParent 를 부르지 않도록 생략)
        if (_source is DimensionWeapon)
        {
            return null;
        }
        if (_source is Component component && null != component)
        {
            return component.GetComponentInParent<DamagePipeline>();
        }
        if (_source is GameObject go && null != go)
        {
            return go.GetComponentInParent<DamagePipeline>();
        }
        return null;
    }
}
