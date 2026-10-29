using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트용 키워드. 호출된 훅을 기록한다
/// </summary>
public class ProbeKeyword
    : MonsterKeyword
{
    public MonsterKeywordCategory category = MonsterKeywordCategory.Move;
    public string tag = "P";

    /// <summary>CanStartAction 이 true 를 돌려줄 횟수 (행동 시작마다 하나씩 줄어든다)</summary>
    public int startCount = 1;
    public float telegraphTime = 0.05f;
    public int actionTicks = 3;
    public bool claimMove = false;

    /// <summary>여러 프로브가 공유하는 호출 순서 기록 (선택)</summary>
    public List<string> sharedLog;

    public override MonsterKeywordCategory Category => category;

    public override KeywordRuntime CreateRuntime()
    {
        return new ProbeRuntime(this);
    }
}

public class ProbeRuntime
    : KeywordRuntime<ProbeKeyword>
{
    public readonly List<string> Log = new List<string>();
    public int InitializeCount;
    public int TryMoveCalls;
    public int TickCalls;
    public int TelegraphTickCalls;
    public int ActionTickCalls;
    public int DespawnCount;
    private int startsLeft;

    public ProbeRuntime(ProbeKeyword _data)
        : base(_data)
    {
    }

    private void Add(string _what)
    {
        Log.Add(_what);
        if (null != data.sharedLog)
        {
            data.sharedLog.Add(data.tag + ":" + _what);
        }
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        ++InitializeCount;
        startsLeft = data.startCount;
        Log.Clear();
        TryMoveCalls = 0;
        TickCalls = 0;
        TelegraphTickCalls = 0;
        ActionTickCalls = 0;
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        ++TryMoveCalls;
        return data.claimMove;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        return startsLeft > 0;
    }

    public override float TelegraphTime => data.telegraphTime;

    public override void OnTelegraphStart(MonsterContext _ctx) { Add("TelegraphStart"); }

    public override void OnTelegraphTick(MonsterContext _ctx, float _dt) { ++TelegraphTickCalls; }

    public override void OnActionStart(MonsterContext _ctx)
    {
        --startsLeft;
        ActionTickCalls = 0;
        Add("ActionStart");
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        ++ActionTickCalls;
        Add("ActionTick");
        return ActionTickCalls >= data.actionTicks;
    }

    public override void OnActionEnd(MonsterContext _ctx) { Add("ActionEnd"); }

    public override void Tick(MonsterContext _ctx, float _dt) { ++TickCalls; }

    public override void OnDespawn(MonsterContext _ctx) { ++DespawnCount; }
}
