using UnityEngine;

/// <summary>
/// 자폭: 가까우면 부풀어 오른 뒤 폭발하고 Kill() 한다
/// explodeOnDeath 면 처치될 때도 폭발한다 (한 생애에 한 번). 부활 시 크기를 원복한다
/// </summary>
[CreateAssetMenu(fileName = "SelfDestructKeyword", menuName = "2CTRL/Monster/Keyword/SelfDestruct")]
public class SelfDestructKeyword
    : MonsterKeyword
{
    [Tooltip("이 거리 안이면 자폭을 시작한다")]
    [Min(0f)]
    public float triggerRange = 1.5f;

    [Tooltip("부풀어 오르는 시간")]
    [Min(0f)]
    public float telegraphTime = 0.8f;

    [Tooltip("폭발 반경")]
    [Min(0f)]
    public float radius = 2f;

    [Tooltip("폭발 데미지. damageRate 를 곱한다")]
    [Min(0f)]
    public float damage = 20f;

    [Min(0f)]
    public float knockbackForce = 6f;

    [Tooltip("처치될 때도 폭발한다")]
    public bool explodeOnDeath = false;

    [Tooltip("예고 끝의 크기 배율")]
    [Min(0.01f)]
    public float inflateScale = 1.5f;

    [Tooltip("폭발 연출 (선택). 반경에 맞춰 크기를 바꾼다")]
    public PooledEffect explosionEffect;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Attack;

    public override KeywordRuntime CreateRuntime()
    {
        return new SelfDestructRuntime(this);
    }
}

public class SelfDestructRuntime
    : KeywordRuntime<SelfDestructKeyword>
{
    private Transform scaleTarget;
    private Vector3 baseScale = Vector3.one;
    private bool hasBaseScale;
    private bool exploded;
    private float timer;

    public SelfDestructRuntime(SelfDestructKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        if (false == hasBaseScale)
        {
            scaleTarget = null != _ctx.spriteRenderer ? _ctx.spriteRenderer.transform : _ctx.root;
            baseScale = scaleTarget.localScale;
            hasBaseScale = true;
        }

        RestoreScale();
        exploded = false;
        timer = 0f;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        if (true == exploded)
        {
            return false;
        }
        return _ctx.DistanceToTarget() <= data.triggerRange;
    }

    public override float TelegraphTime => data.telegraphTime;

    public override void OnTelegraphStart(MonsterContext _ctx)
    {
        timer = 0f;
    }

    public override void OnTelegraphTick(MonsterContext _ctx, float _dt)
    {
        timer += _dt;
        if (false == hasBaseScale)
        {
            return;
        }

        float t = data.telegraphTime > 0f ? Mathf.Clamp01(timer / data.telegraphTime) : 1f;
        scaleTarget.localScale = Vector3.Lerp(baseScale, baseScale * data.inflateScale, t);
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        Explode(_ctx);
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        // 사망은 DeathInterceptor 를 거친다 (부활 키워드가 있으면 다운)
        if (null != _ctx.health)
        {
            _ctx.health.Kill();
        }
        return true;
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        // 예고 중 끊겼으면 크기를 되돌린다. 폭발했으면 부활할 때 되돌린다
        if (false == exploded)
        {
            RestoreScale();
        }
    }

    public override void OnRevive(MonsterContext _ctx)
    {
        RestoreScale();
        exploded = false;
    }

    public override void OnDeath(MonsterContext _ctx)
    {
        if (true == data.explodeOnDeath && false == exploded)
        {
            Explode(_ctx);
        }
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        RestoreScale();
    }

    private void Explode(MonsterContext _ctx)
    {
        exploded = true;

        Vector2 center = _ctx.Position;
        MonsterAreaDamage.Apply(
            center
            , data.radius
            , _ctx.TargetLayer
            , data.damage * _ctx.damageRate
            , data.knockbackForce
            , _ctx.pipeline);

        EffectPool.Spawn(data.explosionEffect, center, data.radius);
    }

    private void RestoreScale()
    {
        if (true == hasBaseScale && null != scaleTarget)
        {
            scaleTarget.localScale = baseScale;
        }
    }
}
