using UnityEngine;

/// <summary>
/// 넉백 저항: Knockback.resistance 를 설정하고 디스폰 시 원래 값으로 되돌린다
/// </summary>
[CreateAssetMenu(fileName = "KnockbackResistKeyword", menuName = "2CTRL/Monster/Keyword/KnockbackResist")]
public class KnockbackResistKeyword
    : MonsterKeyword
{
    [Tooltip("1 이면 넉백을 받지 않는다")]
    [Range(0f, 1f)]
    public float resistance = 1f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Death;

    public override KeywordRuntime CreateRuntime()
    {
        return new KnockbackResistRuntime(this);
    }
}

public class KnockbackResistRuntime
    : KeywordRuntime<KnockbackResistKeyword>
{
    private float originalResistance;
    private bool applied;

    public KnockbackResistRuntime(KnockbackResistKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        if (null == _ctx.knockback)
        {
            return;
        }

        // 디스폰 없이 다시 초기화돼도 원래 값을 덮어쓰지 않는다
        if (false == applied)
        {
            originalResistance = _ctx.knockback.resistance;
            applied = true;
        }

        _ctx.knockback.resistance = data.resistance;
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        if (false == applied || null == _ctx.knockback)
        {
            return;
        }

        _ctx.knockback.resistance = originalResistance;
        applied = false;
    }
}
