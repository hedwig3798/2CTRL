using UnityEngine;

/// <summary>
/// 장판: 살아 있는 동안 interval 마다 발밑에 HazardZone 을 남긴다 (정적 공유 풀)
/// </summary>
[CreateAssetMenu(fileName = "HazardTrailKeyword", menuName = "2CTRL/Monster/Keyword/HazardTrail")]
public class HazardTrailKeyword
    : MonsterKeyword
{
    public HazardZone zonePrefab;

    [Tooltip("장판을 남기는 간격 (초)")]
    [Min(0.05f)]
    public float interval = 1f;

    [Tooltip("장판 반경")]
    [Min(0f)]
    public float radius = 0.8f;

    [Tooltip("장판 수명 (초)")]
    [Min(0.01f)]
    public float duration = 3f;

    [Tooltip("데미지 간격 (초)")]
    [Min(0.01f)]
    public float tickInterval = 0.5f;

    [Tooltip("틱당 데미지. damageRate 를 곱한다")]
    [Min(0f)]
    public float damagePerTick = 3f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Attack;

    public override KeywordRuntime CreateRuntime()
    {
        return new HazardTrailRuntime(this);
    }
}

public class HazardTrailRuntime
    : KeywordRuntime<HazardTrailKeyword>
{
    private float timer;

    public HazardTrailRuntime(HazardTrailKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        timer = data.interval;
    }

    public override void Tick(MonsterContext _ctx, float _dt)
    {
        timer -= _dt;
        if (timer > 0f)
        {
            return;
        }

        timer += Mathf.Max(0.05f, data.interval);

        HazardZone.Spawn(
            data.zonePrefab
            , _ctx.Position
            , data.radius
            , data.duration
            , data.tickInterval
            , data.damagePerTick * _ctx.damageRate
            , _ctx.TargetLayer
            , _ctx.pipeline);
    }
}
