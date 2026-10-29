using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rig = MonsterTestFactory.Rig;
using StateLog = MonsterTestFactory.StateLog;

/// <summary>
/// MonsterBrain 상태머신 / 기본 추적 / 돌진 / 풀 재사용 / 접촉 데미지 검증
/// </summary>
public class MonsterBrainPlayTests
{
    [TearDown]
    public void TearDown()
    {
        MonsterTestFactory.Cleanup();
    }

    private static ProbeKeyword NewProbe(string _tag = "P", int _startCount = 1, float _telegraph = 0.05f, int _ticks = 3, bool _claim = false)
    {
        ProbeKeyword p = MonsterTestFactory.So<ProbeKeyword>();
        p.tag = _tag;
        p.startCount = _startCount;
        p.telegraphTime = _telegraph;
        p.actionTicks = _ticks;
        p.claimMove = _claim;
        return p;
    }

    #region Default chase
    [UnityTest]
    public IEnumerator NoKeywords_ChasesTargetAtBaseSpeed()
    {
        MonsterData data = MonsterTestFactory.NewData(5f);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(100f, 0f));
        MonsterTestFactory.Init(r, target);

        float t0 = Time.time;
        yield return MonsterTestFactory.Wait(0.5f);
        float elapsed = Time.time - t0;

        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
        Assert.Greater(r.Pos.x, 0f, "타깃 쪽으로 이동해야 한다");
        Assert.AreEqual(0f, r.Pos.y, 0.001f);
        Assert.AreEqual(5f * elapsed, r.Pos.x, 0.6f, "속도 = baseMoveSpeed x rate(1)");
    }

    [UnityTest]
    public IEnumerator NoKeywords_SpeedRateMultipliesSpeed_ZeroRateTreatedAsOne()
    {
        MonsterData data = MonsterTestFactory.NewData(4f);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(100f, 0f));

        Rig fast = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(fast, target, _speedRate: 2f);
        Rig zero = MonsterTestFactory.Create(data, new Vector2(0f, 10f));
        MonsterTestFactory.Init(zero, target, _speedRate: 0f);
        Assert.AreEqual(2f, fast.brain.Context.moveSpeedRate);
        Assert.AreEqual(1f, zero.brain.Context.moveSpeedRate, "0 이하는 1");

        float t0 = Time.time;
        yield return MonsterTestFactory.Wait(0.4f);
        float elapsed = Time.time - t0;

        Assert.AreEqual(8f * elapsed, fast.Pos.x, 0.8f);
        Assert.AreEqual(4f * elapsed, zero.Pos.x, 0.6f);
    }

    [UnityTest]
    public IEnumerator NoKeywords_DoesNotOvershootTarget()
    {
        MonsterData data = MonsterTestFactory.NewData(50f);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(0.2f, 0f));
        MonsterTestFactory.Init(r, target);

        yield return MonsterTestFactory.Wait(0.3f);

        Assert.AreEqual(0.2f, r.Pos.x, 0.0001f);
    }

    [UnityTest]
    public IEnumerator NoKeywords_NoTarget_StaysInPlace()
    {
        MonsterData data = MonsterTestFactory.NewData(5f);
        Rig r = MonsterTestFactory.Create(data, new Vector2(3f, 3f));
        MonsterTestFactory.Init(r, null);

        yield return MonsterTestFactory.Wait(0.2f);

        Assert.AreEqual(3f, r.Pos.x, 0.0001f);
        Assert.AreEqual(3f, r.Pos.y, 0.0001f);
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
    }

    [UnityTest]
    public IEnumerator NullKeywordEntries_AreSkipped_DefaultChaseStillWorks()
    {
        MonsterData data = MonsterTestFactory.NewData(5f, new MonsterKeyword[] { null, null });
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(100f, 0f));
        MonsterTestFactory.Init(r, target);

        Assert.AreEqual(0, r.brain.Runtimes.Length);
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.Greater(r.Pos.x, 0f);
    }

    [UnityTest]
    public IEnumerator Chase_FlipsSpriteTowardMoveDirection()
    {
        MonsterData data = MonsterTestFactory.NewData(5f);
        Rig right = MonsterTestFactory.Create(data, Vector2.zero);
        Rig left = MonsterTestFactory.Create(data, new Vector2(0f, 20f));
        MonsterTestFactory.Init(right, MonsterTestFactory.CreateMarker(new Vector2(100f, 0f)));
        MonsterTestFactory.Init(left, MonsterTestFactory.CreateMarker(new Vector2(-100f, 20f)));

        yield return MonsterTestFactory.Wait(0.1f);

        // isDefaultLeft = true : 원본이 왼쪽을 보므로 오른쪽으로 갈 때 flipX
        Assert.IsTrue(right.sprite.flipX);
        Assert.IsFalse(left.sprite.flipX);
    }
    #endregion

    #region State transitions
    [UnityTest]
    public IEnumerator Probe_MoveTelegraphActionMove_HooksInOrder()
    {
        ProbeKeyword probe = NewProbe(_startCount: 1, _telegraph: 0.05f, _ticks: 3);
        MonsterData data = MonsterTestFactory.NewData(0f, probe);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(50f, 0f));

        StateLog log = new StateLog(r.brain);
        int telegraphEvents = 0;
        KeywordRuntime telegraphed = null;
        r.brain.OnActionTelegraph += (rt) => { ++telegraphEvents; telegraphed = rt; };

        MonsterTestFactory.Init(r, target);
        yield return MonsterTestFactory.WaitUntil(() => log.Count(MonsterStateId.Move) >= 2, 2f);
        yield return MonsterTestFactory.Wait(0.2f);

        CollectionAssert.AreEqual(
            new[] { MonsterStateId.Move, MonsterStateId.Telegraph, MonsterStateId.Action, MonsterStateId.Move }
            , log.States);

        ProbeRuntime rt0 = (ProbeRuntime)r.brain.Runtimes[0];
        CollectionAssert.AreEqual(
            new[] { "TelegraphStart", "ActionStart", "ActionTick", "ActionTick", "ActionTick", "ActionEnd" }
            , rt0.Log);
        Assert.Greater(rt0.TelegraphTickCalls, 0);
        Assert.AreEqual(1, telegraphEvents);
        Assert.AreSame(rt0, telegraphed);
        Assert.IsNull(r.brain.ActiveAction);
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
    }

    [UnityTest]
    public IEnumerator Probe_ZeroTelegraph_SkipsTelegraphState()
    {
        ProbeKeyword probe = NewProbe(_startCount: 1, _telegraph: 0f, _ticks: 1);
        MonsterData data = MonsterTestFactory.NewData(0f, probe);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(50f, 0f)));

        yield return MonsterTestFactory.Wait(0.3f);

        CollectionAssert.AreEqual(
            new[] { MonsterStateId.Move, MonsterStateId.Action, MonsterStateId.Move }
            , log.States);
        CollectionAssert.AreEqual(
            new[] { "ActionStart", "ActionTick", "ActionEnd" }
            , ((ProbeRuntime)r.brain.Runtimes[0]).Log);
    }

    [UnityTest]
    public IEnumerator Probe_NoTarget_NeverStartsAction()
    {
        ProbeKeyword probe = NewProbe();
        MonsterData data = MonsterTestFactory.NewData(0f, probe);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, null);

        yield return MonsterTestFactory.Wait(0.3f);

        CollectionAssert.AreEqual(new[] { MonsterStateId.Move }, log.States);
    }

    [UnityTest]
    public IEnumerator Probe_ActionKeywords_CheckedInListOrder()
    {
        List<string> shared = new List<string>();
        ProbeKeyword a = NewProbe("A", 1, 0f, 1);
        ProbeKeyword b = NewProbe("B", 1, 0f, 1);
        a.sharedLog = shared;
        b.sharedLog = shared;
        MonsterData data = MonsterTestFactory.NewData(0f, a, b);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(50f, 0f)));

        yield return MonsterTestFactory.Wait(0.4f);

        Assert.AreEqual("A:ActionStart", shared.Find(s => s.EndsWith("ActionStart")), "먼저 나열된 키워드가 먼저 행동한다");
        int idxA = shared.IndexOf("A:ActionEnd");
        int idxB = shared.IndexOf("B:ActionStart");
        Assert.GreaterOrEqual(idxA, 0);
        Assert.Greater(idxB, idxA, "A 행동이 끝난 뒤 B 가 시작한다");
    }

    [UnityTest]
    public IEnumerator Probe_FirstTryMoveTrueWins_LaterKeywordsNotAsked_NoDefaultChase()
    {
        ProbeKeyword a = NewProbe("A", 0, 0.05f, 1, _claim: true);
        ProbeKeyword b = NewProbe("B", 0, 0.05f, 1, _claim: true);
        MonsterData data = MonsterTestFactory.NewData(5f, a, b);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(50f, 0f)));

        yield return MonsterTestFactory.Wait(0.2f);

        Assert.Greater(((ProbeRuntime)r.brain.Runtimes[0]).TryMoveCalls, 0);
        Assert.AreEqual(0, ((ProbeRuntime)r.brain.Runtimes[1]).TryMoveCalls);
        Assert.AreEqual(0f, r.Pos.x, 0.0001f, "TryMove 가 true 면 기본 추적을 하지 않는다");
    }

    [UnityTest]
    public IEnumerator Probe_AllTryMoveFalse_FallsBackToDefaultChase()
    {
        ProbeKeyword a = NewProbe("A", 0, 0.05f, 1, _claim: false);
        ProbeKeyword b = NewProbe("B", 0, 0.05f, 1, _claim: false);
        MonsterData data = MonsterTestFactory.NewData(5f, a, b);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(50f, 0f)));

        yield return MonsterTestFactory.Wait(0.2f);

        Assert.Greater(((ProbeRuntime)r.brain.Runtimes[1]).TryMoveCalls, 0);
        Assert.Greater(r.Pos.x, 0f);
    }

    [UnityTest]
    public IEnumerator KnockedBack_StopsChase_ButTelegraphAndActionStillProceed()
    {
        // 넉백이 몬스터 루트를 직접 밀지 않도록 별도 트랜스폼을 밀게 한다
        MonsterData chase = MonsterTestFactory.NewData(5f);
        Rig r = MonsterTestFactory.Create(chase, Vector2.zero);
        Transform dummyRoot = MonsterTestFactory.CreateMarker(new Vector2(0f, 500f));
        r.knockback.moveRoot = dummyRoot;
        r.knockback.deceleration = 0.01f;
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(100f, 0f)));

        DamageMassage msg = new DamageMassage();
        msg.damage = 0f;
        msg.knockbackForce = 20f;
        msg.hitPoint = new Vector2(10f, 500f);
        r.pipeline.ProcessDamage(ref msg);
        Assert.IsTrue(r.knockback.IsKnockedBack);

        yield return MonsterTestFactory.Wait(0.3f);
        Assert.AreEqual(0f, r.Pos.x, 0.0001f, "넉백 중에는 추적하지 않는다");

        r.knockback.Initialize(null);
        Assert.IsFalse(r.knockback.IsKnockedBack);
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.Greater(r.Pos.x, 0f, "넉백이 끝나면 다시 추적한다");

        // 행동은 넉백을 무시하고 끝까지 진행
        ProbeKeyword probe = NewProbe(_startCount: 1, _telegraph: 0.05f, _ticks: 2);
        MonsterData data2 = MonsterTestFactory.NewData(0f, probe);
        Rig r2 = MonsterTestFactory.Create(data2, new Vector2(0f, 30f));
        r2.knockback.moveRoot = dummyRoot;
        r2.knockback.deceleration = 0.01f;
        StateLog log = new StateLog(r2.brain);
        MonsterTestFactory.Init(r2, MonsterTestFactory.CreateMarker(new Vector2(100f, 30f)));
        DamageMassage msg2 = msg;
        r2.pipeline.ProcessDamage(ref msg2);
        Assert.IsTrue(r2.knockback.IsKnockedBack);

        yield return MonsterTestFactory.Wait(0.4f);

        CollectionAssert.AreEqual(
            new[] { MonsterStateId.Move, MonsterStateId.Telegraph, MonsterStateId.Action, MonsterStateId.Move }
            , log.States);
    }

    [UnityTest]
    public IEnumerator Dead_StopsMovementAndPassiveTick()
    {
        ProbeKeyword probe = NewProbe(_startCount: 0);
        MonsterData data = MonsterTestFactory.NewData(5f, probe);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(100f, 0f)));
        yield return MonsterTestFactory.Wait(0.1f);

        r.health.Kill();
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        Vector2 deadPos = r.Pos;
        ProbeRuntime rt = (ProbeRuntime)r.brain.Runtimes[0];
        int ticks = rt.TickCalls;
        Assert.Greater(ticks, 0, "살아 있는 동안은 Tick 이 호출된다");

        yield return MonsterTestFactory.Wait(0.3f);

        Assert.AreEqual(deadPos.x, r.Pos.x, 0.0001f);
        Assert.AreEqual(ticks, rt.TickCalls, "죽은 뒤에는 Tick 이 호출되지 않는다");
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
    }

    [UnityTest]
    public IEnumerator KillDuringTelegraph_EndsActionWithoutStartingIt()
    {
        ProbeKeyword probe = NewProbe(_startCount: 1, _telegraph: 5f);
        MonsterData data = MonsterTestFactory.NewData(0f, probe);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(50f, 0f)));
        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Telegraph == r.brain.CurrentState, 1f);
        Assert.AreEqual(MonsterStateId.Telegraph, r.brain.CurrentState);

        r.health.Kill();

        ProbeRuntime rt = (ProbeRuntime)r.brain.Runtimes[0];
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        CollectionAssert.AreEqual(new[] { "TelegraphStart", "ActionEnd" }, rt.Log);
        Assert.IsNull(r.brain.ActiveAction);
    }
    #endregion

    #region Dash
    private static DashKeyword NewDash(float _range, float _telegraph, float _speed, float _duration, float _cooldown)
    {
        DashKeyword d = MonsterTestFactory.So<DashKeyword>();
        d.triggerRange = _range;
        d.telegraphTime = _telegraph;
        d.dashSpeed = _speed;
        d.dashDuration = _duration;
        d.cooldown = _cooldown;
        return d;
    }

    [UnityTest]
    public IEnumerator Dash_StopsToTelegraph_LocksDirection_ThenDashesStraight()
    {
        DashKeyword dash = NewDash(10f, 0.2f, 10f, 0.2f, 100f);
        MonsterData data = MonsterTestFactory.NewData(3f, dash);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(5f, 0f));
        StateLog log = new StateLog(r.brain);
        Vector2 posAtDashEnd = Vector2.zero;
        r.brain.OnStateChanged += (id) =>
        {
            if (MonsterStateId.Move == id && r.brain.Runtimes.Length > 0 && log.States.Count >= 4)
            {
                posAtDashEnd = r.Pos;
            }
        };
        MonsterTestFactory.Init(r, target);

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Telegraph == r.brain.CurrentState, 1f);
        Assert.AreEqual(MonsterStateId.Telegraph, r.brain.CurrentState);
        Vector2 posAtTelegraph = r.Pos;

        // 예고 중에 타깃이 옆으로 이동해도 방향은 고정
        target.position = new Vector2(0f, 8f);
        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Action == r.brain.CurrentState, 1f);
        Assert.AreEqual(posAtTelegraph.x, r.Pos.x, 0.0001f, "예고 중에는 멈춘다");
        Assert.AreEqual(posAtTelegraph.y, r.Pos.y, 0.0001f);

        yield return MonsterTestFactory.WaitUntil(() => log.States.Count >= 4, 1f);

        Assert.Greater(posAtDashEnd.x - posAtTelegraph.x, 0.8f, "고정된 +x 방향으로 돌진 (10 x 0.2 = 2)");
        Assert.AreEqual(posAtTelegraph.y, posAtDashEnd.y, 0.0001f, "타깃이 옮겨도 y 로 꺾이지 않는다");
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
    }

    [UnityTest]
    public IEnumerator Dash_Cooldown_BlocksSecondDashUntilExpired()
    {
        const float cooldown = 0.6f;
        DashKeyword dash = NewDash(100f, 0.05f, 1f, 0.05f, cooldown);
        MonsterData data = MonsterTestFactory.NewData(0f, dash);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(20f, 0f)));

        yield return MonsterTestFactory.WaitUntil(() => log.States.Count >= 4, 2f);
        Assert.AreEqual(1, log.Count(MonsterStateId.Telegraph));
        float firstEnd = log.Times[3];

        yield return MonsterTestFactory.Wait(cooldown * 0.5f);
        Assert.AreEqual(1, log.Count(MonsterStateId.Telegraph), "쿨다운 중에는 다시 돌진하지 않는다");

        yield return MonsterTestFactory.WaitUntil(() => log.Count(MonsterStateId.Telegraph) >= 2, 2f);
        Assert.AreEqual(2, log.Count(MonsterStateId.Telegraph));
        int second = log.States.LastIndexOf(MonsterStateId.Telegraph);
        Assert.GreaterOrEqual(log.Times[second] - firstEnd, cooldown - 0.0001f);
    }

    [UnityTest]
    public IEnumerator Dash_OutOfRange_NoDash_ChasesNormally()
    {
        DashKeyword dash = NewDash(2f, 0.05f, 10f, 0.1f, 1f);
        MonsterData data = MonsterTestFactory.NewData(3f, dash);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(100f, 0f)));

        yield return MonsterTestFactory.Wait(0.3f);

        CollectionAssert.AreEqual(new[] { MonsterStateId.Move }, log.States);
        Assert.Greater(r.Pos.x, 0f);
    }
    #endregion

    #region Pool reuse
    [UnityTest]
    public IEnumerator Reinitialize_ResetsRuntimeStateAndContext()
    {
        DashKeyword dash = NewDash(100f, 0.02f, 1f, 0.02f, 1000f);
        ReviveKeyword revive = MonsterTestFactory.So<ReviveKeyword>();
        revive.reviveCount = 1;
        revive.reviveDelay = 0.05f;
        revive.hpRatio = 0.5f;
        MonsterData data = MonsterTestFactory.NewData(0f, dash, revive);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(20f, 0f));
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, target, 1f, 1f);

        // 돌진 한 번 + 부활 한 번 소진
        yield return MonsterTestFactory.WaitUntil(() => log.Count(MonsterStateId.Move) >= 2, 2f);
        Assert.IsFalse(r.brain.Runtimes[0].CanStartAction(r.brain.Context), "쿨다운 중");
        r.health.Kill();
        Assert.AreEqual(MonsterStateId.Downed, r.brain.CurrentState);
        yield return MonsterTestFactory.WaitUntil(() => false == r.health.isDead, 1f);
        ReviveRuntime rr = (ReviveRuntime)r.brain.Runtimes[1];
        Assert.AreEqual(0, rr.Remain);

        // 풀 반환 후 재사용
        r.root.SetActive(false);
        r.root.SetActive(true);
        Transform target2 = MonsterTestFactory.CreateMarker(new Vector2(0f, 30f));
        MonsterTestFactory.Init(r, target2, 2f, 3f);

        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
        Assert.AreEqual(1, rr.Remain, "부활 횟수 복구");
        Assert.IsTrue(r.brain.Runtimes[0].CanStartAction(r.brain.Context), "돌진 쿨다운 리셋");
        Assert.AreSame(target2, r.brain.Context.target);
        Assert.AreEqual(2f, r.brain.Context.moveSpeedRate);
        Assert.AreEqual(3f, r.brain.Context.damageRate);
        Assert.IsNull(r.brain.ActiveAction);
        Assert.IsNull(r.brain.DownedBy);
        Assert.IsFalse(r.health.isDead);
        Assert.AreEqual(100f, r.health.CurrHP);
    }

    [UnityTest]
    public IEnumerator Reinitialize_AfterFinalDeath_ReturnsToMove()
    {
        MonsterData data = MonsterTestFactory.NewData(2f);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(100f, 0f));
        MonsterTestFactory.Init(r, target);
        yield return null;

        r.health.Kill();
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);

        r.root.SetActive(false);
        r.root.SetActive(true);
        MonsterTestFactory.Init(r, target);
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
        Assert.IsFalse(r.health.isDead);

        float x0 = r.Pos.x;
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.Greater(r.Pos.x, x0, "재사용 후 다시 움직인다");
    }

    [UnityTest]
    public IEnumerator Reinitialize_NullBoard_ClearsTargetAndRates()
    {
        MonsterData data = MonsterTestFactory.NewData(2f);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(Vector2.one), 3f, 4f);
        Assert.IsTrue(r.brain.Context.HasTarget);

        r.brain.Initialize(null);

        Assert.IsFalse(r.brain.Context.HasTarget);
        Assert.AreEqual(1f, r.brain.Context.moveSpeedRate);
        Assert.AreEqual(1f, r.brain.Context.damageRate);
        Assert.IsNull(r.brain.Context.spawner);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Disable_CallsOnDespawn_AndEndsActiveAction()
    {
        ProbeKeyword probe = NewProbe(_startCount: 1, _telegraph: 5f);
        MonsterData data = MonsterTestFactory.NewData(0f, probe);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(50f, 0f)));
        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Telegraph == r.brain.CurrentState, 1f);

        ProbeRuntime rt = (ProbeRuntime)r.brain.Runtimes[0];
        r.root.SetActive(false);

        Assert.AreEqual(1, rt.DespawnCount);
        Assert.AreEqual("ActionEnd", rt.Log[rt.Log.Count - 1], "예고 중 비활성화되면 행동 종료 처리");
        Assert.AreEqual(MonsterStateId.None, r.brain.CurrentState);
    }
    #endregion

    #region Contact damage check
    [UnityTest]
    public IEnumerator MissingContactDamage_LogsErrorOnceOnAwake()
    {
        MonsterData data = MonsterTestFactory.NewData(1f);

        // Awake 에서 한 번만 남기고, 재초기화(풀 재사용)마다 반복하지 않는다
        LogAssert.Expect(LogType.Error, new Regex("ContactDamage"));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero, _contact: false);

        Transform marker = MonsterTestFactory.CreateMarker(new Vector2(10f, 0f));
        MonsterTestFactory.Init(r, marker);
        MonsterTestFactory.Init(r, marker);

        yield return MonsterTestFactory.Wait(0.1f);
        LogAssert.NoUnexpectedReceived();
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState, "에러를 남겨도 동작은 계속한다");
    }

    [UnityTest]
    public IEnumerator WithContactDamage_NoErrorLogged()
    {
        MonsterData data = MonsterTestFactory.NewData(1f);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero, _contact: true);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(10f, 0f)));

        yield return MonsterTestFactory.Wait(0.1f);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator MissingMonsterData_LogsErrorOnAwake_DoesNotThrow()
    {
        LogAssert.Expect(LogType.Error, new Regex("no MonsterData"));
        Rig r = MonsterTestFactory.Create(null, Vector2.zero);

        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(10f, 0f)));
        yield return MonsterTestFactory.Wait(0.1f);
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
    }
    #endregion
}
