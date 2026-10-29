using UnityEngine;

/// <summary>
/// 추적(예측): 타깃 위치 + 추정 속도 × leadTime 지점으로 이동한다 (최대 거리 제한)
/// 타깃 속도는 위치 변화로 추정한다
/// </summary>
[CreateAssetMenu(fileName = "PredictKeyword", menuName = "2CTRL/Monster/Keyword/Predict")]
public class PredictKeyword
    : MonsterKeyword
{
    [Tooltip("몇 초 뒤의 타깃 위치를 노릴지")]
    [Min(0f)]
    public float leadTime = 0.6f;

    [Tooltip("예측 지점이 타깃에서 벗어날 수 있는 최대 거리")]
    [Min(0f)]
    public float maxLeadDistance = 3f;

    [Tooltip("속도 추정 보간 강도. 클수록 최신 속도를 빨리 따라간다")]
    [Min(0.01f)]
    public float velocitySmoothing = 8f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new PredictRuntime(this);
    }
}

public class PredictRuntime
    : KeywordRuntime<PredictKeyword>
{
    private Vector2 lastTargetPos;
    private Vector2 targetVelocity;
    private bool hasLast;

    public PredictRuntime(PredictKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        hasLast = false;
        targetVelocity = Vector2.zero;
    }

    public override void OnRevive(MonsterContext _ctx)
    {
        // 다운 동안 Tick 이 멈췄으므로 그 사이 위치 변화를 속도로 잡지 않는다
        hasLast = false;
        targetVelocity = Vector2.zero;
    }

    public override void Tick(MonsterContext _ctx, float _dt)
    {
        if (false == _ctx.HasTarget || _dt <= 0f)
        {
            return;
        }

        Vector2 pos = _ctx.TargetPosition;
        if (true == hasLast)
        {
            Vector2 raw = (pos - lastTargetPos) / _dt;
            float t = 1f - Mathf.Exp(-data.velocitySmoothing * _dt);
            targetVelocity = Vector2.Lerp(targetVelocity, raw, t);
        }

        lastTargetPos = pos;
        hasLast = true;
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        Vector2 lead = Vector2.ClampMagnitude(targetVelocity * data.leadTime, data.maxLeadDistance);
        _ctx.MoveTowards(_ctx.TargetPosition + lead, _ctx.MoveSpeed, _dt);
        return true;
    }
}
