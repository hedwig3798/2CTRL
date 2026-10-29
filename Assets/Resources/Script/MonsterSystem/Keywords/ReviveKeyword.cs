using UnityEngine;

/// <summary>
/// 부활: 남은 횟수가 있으면 사망을 가로채 다운 → reviveDelay 대기 → hpRatio 로 부활한다
/// </summary>
[CreateAssetMenu(fileName = "ReviveKeyword", menuName = "2CTRL/Monster/Keyword/Revive")]
public class ReviveKeyword
    : MonsterKeyword
{
    [Min(0)]
    public int reviveCount = 1;

    [Tooltip("다운 후 부활까지 대기 시간 (초)")]
    [Min(0f)]
    public float reviveDelay = 1.5f;

    [Tooltip("부활 시 최대 HP 대비 회복 비율")]
    [Range(0f, 1f)]
    public float hpRatio = 0.5f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Death;

    public override KeywordRuntime CreateRuntime()
    {
        return new ReviveRuntime(this);
    }
}

public class ReviveRuntime
    : KeywordRuntime<ReviveKeyword>
{
    private int remain;

    public int Remain => remain;

    public ReviveRuntime(ReviveKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        remain = data.reviveCount;
    }

    public override bool InterceptDeath(MonsterContext _ctx)
    {
        if (remain <= 0)
        {
            return false;
        }

        --remain;
        return true;
    }

    public override float ReviveDelay => data.reviveDelay;

    public override float ReviveHPRatio => data.hpRatio;
}
