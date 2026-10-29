using UnityEngine;

/// <summary>
/// 돌진: 사거리 안이면 멈춰서 예고하고, 예고 시작 시 고정한 방향으로 직선 돌진한다
/// </summary>
[CreateAssetMenu(fileName = "DashKeyword", menuName = "2CTRL/Monster/Keyword/Dash")]
public class DashKeyword
    : MonsterKeyword
{
    [Tooltip("이 거리 안이면 돌진을 시작한다")]
    [Min(0f)]
    public float triggerRange = 4f;

    [Tooltip("멈춰서 예고하는 시간")]
    [Min(0f)]
    public float telegraphTime = 0.6f;

    [Tooltip("돌진 속도. moveSpeedRate 를 곱한다")]
    [Min(0f)]
    public float dashSpeed = 10f;

    [Min(0f)]
    public float dashDuration = 0.4f;

    [Min(0f)]
    public float cooldown = 3f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new DashRuntime(this);
    }
}

public class DashRuntime
    : KeywordRuntime<DashKeyword>
{
    private Vector2 dir;
    private float timer;
    private float readyTime;

    public DashRuntime(DashKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        dir = Vector2.zero;
        timer = 0f;
        readyTime = 0f;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        if (Time.time < readyTime)
        {
            return false;
        }
        return _ctx.DistanceToTarget() <= data.triggerRange;
    }

    public override float TelegraphTime => data.telegraphTime;

    public override void OnTelegraphStart(MonsterContext _ctx)
    {
        // 예고 시작 시 방향을 고정한다
        dir = _ctx.TargetPosition - _ctx.Position;
        dir = dir.sqrMagnitude > Mathf.Epsilon ? dir.normalized : Vector2.right;
        _ctx.Face(dir.x);
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        timer = 0f;
        // 예고 없이 바로 돌진하는 경우
        if (data.telegraphTime <= 0f)
        {
            OnTelegraphStart(_ctx);
        }
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        _ctx.Move(dir * data.dashSpeed * _ctx.moveSpeedRate * _dt);
        timer += _dt;
        return timer >= data.dashDuration;
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        readyTime = Time.time + data.cooldown;
    }
}
