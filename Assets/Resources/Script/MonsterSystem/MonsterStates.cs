/// <summary>
/// 기본 이동 + 행동 키워드 검사
/// 넉백 중이면 이동만 멈춘다
/// </summary>
public sealed class MonsterMoveState
    : IMonsterState
{
    private readonly MonsterBrain brain;

    public MonsterStateId Id => MonsterStateId.Move;

    public MonsterMoveState(MonsterBrain _brain)
    {
        brain = _brain;
    }

    public void Enter()
    {
        brain.EndActiveAction();
    }

    public void Tick(float _dt)
    {
        MonsterContext ctx = brain.Context;
        if (true == ctx.IsDead || false == ctx.HasTarget)
        {
            return;
        }

        KeywordRuntime[] runtimes = brain.Runtimes;

        // 행동 키워드는 리스트 순서대로 검사한다
        for (int i = 0; i < runtimes.Length; ++i)
        {
            if (true == runtimes[i].CanStartAction(ctx))
            {
                brain.StartAction(runtimes[i]);
                return;
            }
        }

        // 밀려나는 동안에는 이동하지 않는다
        if (null != ctx.knockback && true == ctx.knockback.IsKnockedBack)
        {
            return;
        }

        // 첫 번째로 이동을 처리한 키워드가 이긴다
        for (int i = 0; i < runtimes.Length; ++i)
        {
            if (true == runtimes[i].TryMove(ctx, _dt))
            {
                return;
            }
        }

        // 기본 추적
        ctx.MoveTowards(ctx.TargetPosition, ctx.MoveSpeed, _dt);
    }

    public void Exit() { }
}

/// <summary>
/// 행동 예고. 넉백을 무시하고 진행한다
/// </summary>
public sealed class MonsterTelegraphState
    : IMonsterState
{
    private readonly MonsterBrain brain;
    private float timer;

    public MonsterStateId Id => MonsterStateId.Telegraph;

    public MonsterTelegraphState(MonsterBrain _brain)
    {
        brain = _brain;
    }

    public void Enter()
    {
        timer = 0f;

        KeywordRuntime rt = brain.ActiveAction;
        if (null == rt)
        {
            return;
        }

        rt.OnTelegraphStart(brain.Context);
        brain.RaiseActionTelegraph(rt);
    }

    public void Tick(float _dt)
    {
        KeywordRuntime rt = brain.ActiveAction;
        if (null == rt)
        {
            brain.ChangeToMove();
            return;
        }

        rt.OnTelegraphTick(brain.Context, _dt);

        timer += _dt;
        if (timer >= rt.TelegraphTime && this == brain.Machine.Current)
        {
            brain.ChangeToAction();
        }
    }

    // 끊긴 경우의 정리는 다음 상태(Move/Downed/Dead)의 Enter 에서 EndActiveAction 으로 한다
    public void Exit() { }
}

/// <summary>
/// 행동 실행. 넉백을 무시하고 진행한다
/// </summary>
public sealed class MonsterActionState
    : IMonsterState
{
    private readonly MonsterBrain brain;

    public MonsterStateId Id => MonsterStateId.Action;

    public MonsterActionState(MonsterBrain _brain)
    {
        brain = _brain;
    }

    public void Enter()
    {
        KeywordRuntime rt = brain.ActiveAction;
        if (null != rt)
        {
            rt.OnActionStart(brain.Context);
        }
    }

    public void Tick(float _dt)
    {
        KeywordRuntime rt = brain.ActiveAction;
        if (null == rt)
        {
            brain.ChangeToMove();
            return;
        }

        // TickAction 안에서 사망(Kill)하면 상태가 이미 바뀌어 있다
        if (true == rt.TickAction(brain.Context, _dt) && this == brain.Machine.Current)
        {
            brain.ChangeToMove();
        }
    }

    public void Exit()
    {
        brain.EndActiveAction();
    }
}

/// <summary>
/// 사망을 가로챈 뒤 부활 대기
/// </summary>
public sealed class MonsterDownedState
    : IMonsterState
{
    private readonly MonsterBrain brain;
    private float timer;

    public MonsterStateId Id => MonsterStateId.Downed;

    public MonsterDownedState(MonsterBrain _brain)
    {
        brain = _brain;
    }

    public void Enter()
    {
        timer = 0f;
        brain.EndActiveAction();
    }

    public void Tick(float _dt)
    {
        KeywordRuntime reviver = brain.DownedBy;
        float delay = null != reviver ? reviver.ReviveDelay : 0f;

        timer += _dt;
        if (timer < delay)
        {
            return;
        }

        brain.ReviveFromDowned();
    }

    public void Exit() { }
}

/// <summary>
/// 최종 사망. 키워드의 OnDeath 를 한 번 호출하고 아무것도 하지 않는다
/// </summary>
public sealed class MonsterDeadState
    : IMonsterState
{
    private readonly MonsterBrain brain;

    public MonsterStateId Id => MonsterStateId.Dead;

    public MonsterDeadState(MonsterBrain _brain)
    {
        brain = _brain;
    }

    public void Enter()
    {
        brain.EndActiveAction();

        MonsterContext ctx = brain.Context;
        KeywordRuntime[] runtimes = brain.Runtimes;
        for (int i = 0; i < runtimes.Length; ++i)
        {
            runtimes[i].OnDeath(ctx);
        }
    }

    public void Tick(float _dt) { }

    public void Exit() { }
}
