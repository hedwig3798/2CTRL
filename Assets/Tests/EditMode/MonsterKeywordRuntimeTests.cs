using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 키워드 런타임 / MonsterContext / StateMachine / MonsterData 순수 로직 검증 (씬, 물리 없이)
/// </summary>
public class MonsterKeywordRuntimeTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    private T So<T>() where T : ScriptableObject
    {
        T so = ScriptableObject.CreateInstance<T>();
        created.Add(so);
        return so;
    }

    private Transform NewTransform(Vector2 _pos, float _z = 0f)
    {
        GameObject go = new GameObject("t");
        go.transform.position = new Vector3(_pos.x, _pos.y, _z);
        created.Add(go);
        return go.transform;
    }

    private MonsterContext NewCtx(Vector2 _pos, Vector2? _targetPos, float _speed = 1f, float _rate = 1f)
    {
        MonsterData data = So<MonsterData>();
        data.baseMoveSpeed = _speed;

        MonsterContext ctx = new MonsterContext();
        ctx.root = NewTransform(_pos);
        ctx.data = data;
        ctx.moveSpeedRate = _rate;
        ctx.target = _targetPos.HasValue ? NewTransform(_targetPos.Value) : null;
        return ctx;
    }

    #region CreateRuntime
    [Test]
    public void CreateRuntime_AllKeywords_ReturnDistinctInstancesWithExpectedCategory()
    {
        var cases = new List<(MonsterKeyword kw, MonsterKeywordCategory cat)>
        {
            (So<DashKeyword>(), MonsterKeywordCategory.Move),
            (So<OrbitKeyword>(), MonsterKeywordCategory.Move),
            (So<PredictKeyword>(), MonsterKeywordCategory.Move),
            (So<TeleportKeyword>(), MonsterKeywordCategory.Move),
            (So<SelfDestructKeyword>(), MonsterKeywordCategory.Attack),
            (So<HazardTrailKeyword>(), MonsterKeywordCategory.Attack),
            (So<SummonerKeyword>(), MonsterKeywordCategory.Attack),
            (So<SplitKeyword>(), MonsterKeywordCategory.Death),
            (So<ReviveKeyword>(), MonsterKeywordCategory.Death),
            (So<KnockbackResistKeyword>(), MonsterKeywordCategory.Death),
        };

        foreach (var c in cases)
        {
            KeywordRuntime a = c.kw.CreateRuntime();
            KeywordRuntime b = c.kw.CreateRuntime();
            Assert.IsNotNull(a, c.kw.GetType().Name);
            Assert.AreNotSame(a, b, c.kw.GetType().Name + " : 개체마다 새 런타임");
            Assert.AreEqual(c.cat, c.kw.Category, c.kw.GetType().Name);
        }
    }

    [Test]
    public void Runtimes_FromSameKeyword_DoNotShareState()
    {
        ReviveKeyword kw = So<ReviveKeyword>();
        kw.reviveCount = 1;
        ReviveRuntime a = (ReviveRuntime)kw.CreateRuntime();
        ReviveRuntime b = (ReviveRuntime)kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        a.OnInitialize(ctx);
        b.OnInitialize(ctx);

        Assert.IsTrue(a.InterceptDeath(ctx));

        Assert.AreEqual(0, a.Remain);
        Assert.AreEqual(1, b.Remain);
    }

    [Test]
    public void BaseKeywordRuntime_DefaultHooks_DoNothing()
    {
        KeywordRuntime rt = So<SplitKeyword>().CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, Vector2.one);
        Assert.IsFalse(rt.TryMove(ctx, 0.1f));
        Assert.IsFalse(rt.CanStartAction(ctx));
        Assert.IsFalse(rt.InterceptDeath(ctx));
        Assert.AreEqual(0f, rt.TelegraphTime);
        Assert.IsTrue(rt.TickAction(ctx, 0.1f), "기본은 즉시 종료");
        Assert.AreEqual(1f, rt.ReviveHPRatio);
    }
    #endregion

    #region Revive
    [Test]
    public void Revive_InterceptsReviveCountTimes_ThenStops()
    {
        ReviveKeyword kw = So<ReviveKeyword>();
        kw.reviveCount = 2;
        kw.reviveDelay = 1.25f;
        kw.hpRatio = 0.3f;
        ReviveRuntime rt = (ReviveRuntime)kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        rt.OnInitialize(ctx);

        Assert.IsTrue(rt.InterceptDeath(ctx));
        Assert.IsTrue(rt.InterceptDeath(ctx));
        Assert.IsFalse(rt.InterceptDeath(ctx), "횟수 소진 후에는 가로채지 않는다");
        Assert.IsFalse(rt.InterceptDeath(ctx));
        Assert.AreEqual(0, rt.Remain);
        Assert.AreEqual(1.25f, rt.ReviveDelay);
        Assert.AreEqual(0.3f, rt.ReviveHPRatio);

        rt.OnInitialize(ctx);
        Assert.AreEqual(2, rt.Remain, "재사용 시 횟수 복구");
    }

    [Test]
    public void Revive_ZeroCount_NeverIntercepts()
    {
        ReviveKeyword kw = So<ReviveKeyword>();
        kw.reviveCount = 0;
        ReviveRuntime rt = (ReviveRuntime)kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        rt.OnInitialize(ctx);

        Assert.IsFalse(rt.InterceptDeath(ctx));
    }
    #endregion

    #region KnockbackResist
    [Test]
    public void KnockbackResist_AppliesOnInitialize_RestoresOnDespawn()
    {
        KnockbackResistKeyword kw = So<KnockbackResistKeyword>();
        kw.resistance = 0.8f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        GameObject go = new GameObject("kb");
        created.Add(go);
        ctx.knockback = go.AddComponent<Knockback>();
        ctx.knockback.resistance = 0.25f;

        rt.OnInitialize(ctx);
        Assert.AreEqual(0.8f, ctx.knockback.resistance, 0.0001f);

        rt.OnDespawn(ctx);
        Assert.AreEqual(0.25f, ctx.knockback.resistance, 0.0001f);
    }

    [Test]
    public void KnockbackResist_DoubleInitializeWithoutDespawn_KeepsOriginalValue()
    {
        KnockbackResistKeyword kw = So<KnockbackResistKeyword>();
        kw.resistance = 1f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        GameObject go = new GameObject("kb");
        created.Add(go);
        ctx.knockback = go.AddComponent<Knockback>();
        ctx.knockback.resistance = 0.1f;

        rt.OnInitialize(ctx);
        rt.OnInitialize(ctx);
        rt.OnDespawn(ctx);

        Assert.AreEqual(0.1f, ctx.knockback.resistance, 0.0001f, "원래 값(0.1) 보존");

        rt.OnDespawn(ctx);
        Assert.AreEqual(0.1f, ctx.knockback.resistance, 0.0001f, "두 번째 Despawn 은 무시");
    }

    [Test]
    public void KnockbackResist_NoKnockbackComponent_DoesNotThrow()
    {
        KeywordRuntime rt = So<KnockbackResistKeyword>().CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        Assert.DoesNotThrow(() => { rt.OnInitialize(ctx); rt.OnDespawn(ctx); });
    }
    #endregion

    #region Orbit
    private OrbitKeyword NewOrbit(float _start = 5f, float _min = 2f, float _shrink = 1f, float _angular = 90f)
    {
        OrbitKeyword o = So<OrbitKeyword>();
        o.startRadius = _start;
        o.minRadius = _min;
        o.shrinkSpeed = _shrink;
        o.angularSpeed = _angular;
        return o;
    }

    [Test]
    public void Orbit_OutsideStartRadius_Approaches()
    {
        KeywordRuntime rt = NewOrbit().CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(10f, 0f), Vector2.zero, 2f);
        rt.OnInitialize(ctx);

        Assert.IsTrue(rt.TryMove(ctx, 0.5f));

        Assert.AreEqual(9f, ctx.Position.x, 0.0001f, "속도 2 x 0.5초 직선 접근");
        Assert.AreEqual(0f, ctx.Position.y, 0.0001f);
    }

    [Test]
    public void Orbit_Inside_ShrinksRadiusDownToMinRadiusAndNoFurther()
    {
        KeywordRuntime rt = NewOrbit(5f, 2f, 1f, 90f).CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(4f, 0f), Vector2.zero, 20f);
        rt.OnInitialize(ctx);

        float prev = 4f;
        for (int i = 0; i < 100; ++i)
        {
            Assert.IsTrue(rt.TryMove(ctx, 0.02f));
            float r = ctx.Position.magnitude;
            Assert.LessOrEqual(r, prev + 0.01f, "반경은 줄어들기만 한다");
            Assert.GreaterOrEqual(r, 2f - 0.001f, "minRadius 아래로 내려가지 않는다");
            prev = r;
        }
        Assert.Less(prev, 4f - 1f, "2초 동안 1/s 로 줄어듦");

        for (int i = 0; i < 500; ++i)
        {
            rt.TryMove(ctx, 0.02f);
        }
        Assert.AreEqual(2f, ctx.Position.magnitude, 0.01f, "최종 반경 = minRadius");
    }

    [Test]
    public void Orbit_ExactlyAtStartRadius_StartsOrbitingAtOnce()
    {
        KeywordRuntime rt = NewOrbit(5f, 2f, 1f, 90f).CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(5f, 0f), Vector2.zero, 100f);
        rt.OnInitialize(ctx);

        rt.TryMove(ctx, 0.1f);

        Assert.AreEqual(4.9f, ctx.Position.magnitude, 0.01f, "접근이 아니라 선회: 반경 5 - 1 x 0.1");
        Assert.AreNotEqual(0f, ctx.Position.y, "각도가 변한다");
    }

    [Test]
    public void Orbit_ZeroShrinkSpeed_KeepsRadiusConstant()
    {
        KeywordRuntime rt = NewOrbit(5f, 2f, 0f, 90f).CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(4f, 0f), Vector2.zero, 100f);
        rt.OnInitialize(ctx);

        for (int i = 0; i < 100; ++i)
        {
            rt.TryMove(ctx, 0.02f);
        }

        Assert.AreEqual(4f, ctx.Position.magnitude, 0.01f);
    }

    [Test]
    public void Orbit_Reinitialize_ResetsToApproachPhase()
    {
        KeywordRuntime rt = NewOrbit(5f, 2f, 1f, 90f).CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(4f, 0f), Vector2.zero, 100f);
        rt.OnInitialize(ctx);
        rt.TryMove(ctx, 0.1f);

        ctx.root.position = new Vector3(10f, 0f, 0f);
        rt.OnInitialize(ctx);
        rt.TryMove(ctx, 0.01f);

        Assert.AreEqual(9f, ctx.Position.x, 0.01f, "다시 접근 단계 (속도 100 x 0.01)");
    }
    #endregion

    #region Teleport
    private TeleportKeyword NewTeleport(Vector2 _ring, float _min = 7f)
    {
        TeleportKeyword t = So<TeleportKeyword>();
        t.minDistance = _min;
        t.appearRadius = _ring;
        t.cooldown = 1e9f;
        t.fadeOnTelegraph = false;
        return t;
    }

    [TestCase(2f, 3f)]
    [TestCase(3f, 2f)]
    [TestCase(0f, 1f)]
    [TestCase(-1f, 2f)]
    [TestCase(2f, 2f)]
    public void Teleport_AppearPosition_StaysInsideRing(float _x, float _y)
    {
        TeleportKeyword kw = NewTeleport(new Vector2(_x, _y));
        TeleportRuntime rt = (TeleportRuntime)kw.CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(-30f, 0f), new Vector2(10f, 5f));
        ctx.root.position = new Vector3(-30f, 0f, 3f);
        rt.OnInitialize(ctx);

        float lo = Mathf.Max(0f, Mathf.Min(_x, _y));
        float hi = Mathf.Max(_x, _y);
        float minSeen = float.MaxValue;
        float maxSeen = 0f;
        for (int i = 0; i < 300; ++i)
        {
            rt.OnActionStart(ctx);
            float d = Vector2.Distance(ctx.Position, new Vector2(10f, 5f));
            Assert.GreaterOrEqual(d, lo - 0.001f);
            Assert.LessOrEqual(d, hi + 0.001f);
            minSeen = Mathf.Min(minSeen, d);
            maxSeen = Mathf.Max(maxSeen, d);
        }
        Assert.AreEqual(3f, ctx.root.position.z, "z 유지");
        if (hi - lo > 0.5f)
        {
            Assert.Greater(maxSeen - minSeen, 0.3f, "링 안에서 골고루 흩어진다");
        }
    }

    [Test]
    public void Teleport_CanStart_FarEnoughOnly_AndCooldownBlocksAfterAction()
    {
        TeleportKeyword kw = NewTeleport(new Vector2(2f, 3f), 7f);
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext far = NewCtx(Vector2.zero, new Vector2(7f, 0f));
        MonsterContext near = NewCtx(Vector2.zero, new Vector2(6.99f, 0f));
        rt.OnInitialize(far);

        Assert.IsTrue(rt.CanStartAction(far), "거리 == minDistance 는 포함");
        Assert.IsFalse(rt.CanStartAction(near));

        rt.OnActionEnd(far);
        Assert.IsFalse(rt.CanStartAction(far), "쿨다운");
        rt.OnInitialize(far);
        Assert.IsTrue(rt.CanStartAction(far), "초기화하면 쿨다운 리셋");
    }

    [Test]
    public void Teleport_PostDelay_TickActionFinishesAfterDelay()
    {
        TeleportKeyword kw = NewTeleport(new Vector2(2f, 3f));
        kw.postDelay = 0.5f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, new Vector2(20f, 0f));
        rt.OnInitialize(ctx);
        rt.OnActionStart(ctx);

        Assert.IsFalse(rt.TickAction(ctx, 0.2f));
        Assert.IsFalse(rt.TickAction(ctx, 0.2f));
        Assert.IsTrue(rt.TickAction(ctx, 0.2f));
    }
    #endregion

    #region Dash / SelfDestruct / Summoner / Split guards
    [Test]
    public void Dash_CanStart_RangeBoundaryAndCooldown()
    {
        DashKeyword kw = So<DashKeyword>();
        kw.triggerRange = 4f;
        kw.cooldown = 1e9f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext atRange = NewCtx(Vector2.zero, new Vector2(4f, 0f));
        MonsterContext beyond = NewCtx(Vector2.zero, new Vector2(4.01f, 0f));
        rt.OnInitialize(atRange);

        Assert.IsTrue(rt.CanStartAction(atRange), "거리 == triggerRange 는 포함");
        Assert.IsFalse(rt.CanStartAction(beyond));

        rt.OnActionEnd(atRange);
        Assert.IsFalse(rt.CanStartAction(atRange), "쿨다운 중");
        rt.OnInitialize(atRange);
        Assert.IsTrue(rt.CanStartAction(atRange), "Initialize 로 리셋");
    }

    [Test]
    public void Dash_DirectionLockedAtTelegraphStart_SpeedScaledByRate()
    {
        DashKeyword kw = So<DashKeyword>();
        kw.dashSpeed = 10f;
        kw.dashDuration = 0.5f;
        kw.telegraphTime = 0.3f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, new Vector2(0f, 5f), 1f, 2f);
        rt.OnInitialize(ctx);

        rt.OnTelegraphStart(ctx);
        ctx.target.position = new Vector3(-9f, 0f, 0f);
        rt.OnActionStart(ctx);
        bool done = rt.TickAction(ctx, 0.25f);

        Assert.IsFalse(done);
        Assert.AreEqual(0f, ctx.Position.x, 0.0001f, "방향은 예고 시작 시점의 +y");
        Assert.AreEqual(5f, ctx.Position.y, 0.0001f, "10 x rate 2 x 0.25");
        Assert.IsTrue(rt.TickAction(ctx, 0.25f), "dashDuration 도달 시 종료");
    }

    [Test]
    public void Dash_TargetOnTopOfMonster_FallsBackToRightDirection()
    {
        DashKeyword kw = So<DashKeyword>();
        kw.dashSpeed = 4f;
        kw.dashDuration = 1f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, Vector2.zero);
        rt.OnInitialize(ctx);

        rt.OnTelegraphStart(ctx);
        rt.OnActionStart(ctx);
        rt.TickAction(ctx, 0.5f);

        Assert.AreEqual(2f, ctx.Position.x, 0.0001f);
        Assert.IsFalse(float.IsNaN(ctx.Position.y));
    }

    [Test]
    public void SelfDestruct_CanStart_RangeBoundary_AndExplodedBlocksUntilRevive()
    {
        SelfDestructKeyword kw = So<SelfDestructKeyword>();
        kw.triggerRange = 1.5f;
        kw.explodeOnDeath = false;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, new Vector2(1.5f, 0f));
        rt.OnInitialize(ctx);
        MonsterContext beyond = NewCtx(Vector2.zero, new Vector2(1.51f, 0f));

        Assert.IsTrue(rt.CanStartAction(ctx));
        Assert.IsFalse(rt.CanStartAction(beyond));

        rt.OnActionStart(ctx);
        Assert.IsFalse(rt.CanStartAction(ctx), "폭발한 생애에서는 다시 시작하지 않는다");

        rt.OnRevive(ctx);
        Assert.IsTrue(rt.CanStartAction(ctx), "부활하면 다시 가능");
    }

    [Test]
    public void SelfDestruct_InflateAndRestoreScale()
    {
        SelfDestructKeyword kw = So<SelfDestructKeyword>();
        kw.telegraphTime = 1f;
        kw.inflateScale = 3f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, Vector2.one);
        ctx.root.localScale = new Vector3(2f, 2f, 1f);
        rt.OnInitialize(ctx);

        rt.OnTelegraphStart(ctx);
        rt.OnTelegraphTick(ctx, 0.5f);
        Assert.AreEqual(2f * 2f, ctx.root.localScale.x, 0.0001f, "t=0.5 : lerp(1,3)=2 배");

        rt.OnTelegraphTick(ctx, 5f);
        Assert.AreEqual(6f, ctx.root.localScale.x, 0.0001f, "t 는 1 로 clamp");

        rt.OnActionEnd(ctx);
        Assert.AreEqual(2f, ctx.root.localScale.x, 0.0001f, "예고 중 끊기면 원복");

        rt.OnTelegraphTick(ctx, 5f);
        rt.OnActionStart(ctx);
        rt.OnActionEnd(ctx);
        Assert.AreEqual(6f, ctx.root.localScale.x, 0.0001f, "폭발한 뒤에는 부활할 때 원복");
        rt.OnRevive(ctx);
        Assert.AreEqual(2f, ctx.root.localScale.x, 0.0001f);

        rt.OnTelegraphTick(ctx, 5f);
        rt.OnDespawn(ctx);
        Assert.AreEqual(2f, ctx.root.localScale.x, 0.0001f, "디스폰 시 원복");
    }

    [Test]
    public void SelfDestruct_ExplodeOnDeath_MarksExploded_SoNoSecondStart()
    {
        SelfDestructKeyword kw = So<SelfDestructKeyword>();
        kw.explodeOnDeath = true;
        kw.triggerRange = 100f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, Vector2.one);
        rt.OnInitialize(ctx);
        Assert.IsTrue(rt.CanStartAction(ctx));

        rt.OnDeath(ctx);

        Assert.IsFalse(rt.CanStartAction(ctx));
    }

    [Test]
    public void SelfDestruct_ExplodeOnDeathFalse_OnDeathDoesNothing()
    {
        SelfDestructKeyword kw = So<SelfDestructKeyword>();
        kw.explodeOnDeath = false;
        kw.triggerRange = 100f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, Vector2.one);
        rt.OnInitialize(ctx);

        rt.OnDeath(ctx);

        Assert.IsTrue(rt.CanStartAction(ctx));
    }

    [Test]
    public void Split_And_Summoner_WithoutSpawnerOrPrefab_DoNotThrow()
    {
        MonsterContext ctx = NewCtx(Vector2.zero, Vector2.one);
        KeywordRuntime split = So<SplitKeyword>().CreateRuntime();
        SummonerKeyword sk = So<SummonerKeyword>();
        sk.cooldown = 0f;
        KeywordRuntime summon = sk.CreateRuntime();
        summon.OnInitialize(ctx);

        Assert.DoesNotThrow(() => split.OnDeath(ctx));
        Assert.DoesNotThrow(() => summon.OnActionStart(ctx));
        Assert.IsFalse(summon.CanStartAction(ctx), "프리팹/스포너가 없으면 시작하지 않는다");
    }

    [Test]
    public void Summoner_KeepDistanceZero_DoesNotMove_AndWithinTolerance_Holds()
    {
        SummonerKeyword sk = So<SummonerKeyword>();
        sk.keepDistance = 0f;
        KeywordRuntime rt = sk.CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(3f, 0f), Vector2.zero, 10f);
        rt.OnInitialize(ctx);
        Assert.IsFalse(rt.TryMove(ctx, 1f));
        Assert.AreEqual(3f, ctx.Position.x);

        sk.keepDistance = 3.2f;
        sk.keepDistanceTolerance = 0.5f;
        Assert.IsTrue(rt.TryMove(ctx, 1f), "허용 오차 안이면 멈추되 이동 처리는 한 것으로");
        Assert.AreEqual(3f, ctx.Position.x, 0.0001f);
    }

    [Test]
    public void Predict_StationaryTarget_MovesStraightToTarget()
    {
        PredictKeyword kw = So<PredictKeyword>();
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(Vector2.zero, new Vector2(10f, 0f), 2f);
        rt.OnInitialize(ctx);

        for (int i = 0; i < 5; ++i)
        {
            rt.Tick(ctx, 0.1f);
            Assert.IsTrue(rt.TryMove(ctx, 0.1f));
        }

        Assert.AreEqual(1f, ctx.Position.x, 0.001f);
        Assert.AreEqual(0f, ctx.Position.y, 0.001f);
    }

    [Test]
    public void Predict_MovingTarget_LeadIsClampedToMaxLeadDistance()
    {
        PredictKeyword kw = So<PredictKeyword>();
        kw.leadTime = 10f;
        kw.maxLeadDistance = 1f;
        kw.velocitySmoothing = 100f;
        KeywordRuntime rt = kw.CreateRuntime();
        MonsterContext ctx = NewCtx(new Vector2(0f, 0f), new Vector2(10f, 0f), 1000f);
        rt.OnInitialize(ctx);

        // 타깃이 +y 로 빠르게 이동 중
        for (int i = 0; i < 20; ++i)
        {
            ctx.target.position += new Vector3(0f, 5f, 0f) * 0.1f;
            rt.Tick(ctx, 0.1f);
        }
        Vector3 targetPos = ctx.target.position;
        rt.TryMove(ctx, 1f);

        Assert.AreEqual(targetPos.x, ctx.Position.x, 0.001f);
        Assert.AreEqual(targetPos.y + 1f, ctx.Position.y, 0.05f, "예측 지점 = 타깃 + 최대 1 만큼 앞");
    }
    #endregion

    #region MonsterContext
    [Test]
    public void Context_MoveSpeed_BaseTimesRate()
    {
        Assert.AreEqual(6f, NewCtx(Vector2.zero, null, 3f, 2f).MoveSpeed, 0.0001f);
        MonsterContext noData = new MonsterContext();
        noData.root = NewTransform(Vector2.zero);
        Assert.AreEqual(0f, noData.MoveSpeed);
    }

    [Test]
    public void Context_Target_Helpers()
    {
        MonsterContext none = NewCtx(new Vector2(1f, 1f), null);
        Assert.IsFalse(none.HasTarget);
        Assert.AreEqual(float.MaxValue, none.DistanceToTarget());
        Assert.AreEqual(none.Position, none.TargetPosition, "타깃이 없으면 제자리");

        MonsterContext with = NewCtx(Vector2.zero, new Vector2(3f, 4f));
        Assert.IsTrue(with.HasTarget);
        Assert.AreEqual(5f, with.DistanceToTarget(), 0.0001f);
    }

    [Test]
    public void Context_MoveTowards_NeverOvershoots()
    {
        MonsterContext ctx = NewCtx(Vector2.zero, new Vector2(1f, 0f));
        ctx.MoveTowards(new Vector2(1f, 0f), 100f, 1f);
        Assert.AreEqual(1f, ctx.Position.x, 0.0001f);

        ctx.MoveTowards(new Vector2(1f, 0f), 100f, 1f);
        Assert.AreEqual(1f, ctx.Position.x, 0.0001f);

        ctx.MoveTowards(new Vector2(11f, 0f), 2f, 0.5f);
        Assert.AreEqual(2f, ctx.Position.x, 0.0001f);
    }

    [Test]
    public void Context_SetPosition_KeepsZ()
    {
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        ctx.root.position = new Vector3(0f, 0f, 7f);
        ctx.SetPosition(new Vector2(3f, 4f));
        Assert.AreEqual(new Vector3(3f, 4f, 7f), ctx.root.position);
    }

    [Test]
    public void Context_Face_FlipX_ByDefaultDirection_AndZeroKeepsPrevious()
    {
        MonsterContext ctx = NewCtx(Vector2.zero, null);
        GameObject go = new GameObject("sr");
        created.Add(go);
        ctx.spriteRenderer = go.AddComponent<SpriteRenderer>();

        ctx.isDefaultLeft = true;
        ctx.Face(1f);
        Assert.IsTrue(ctx.spriteRenderer.flipX);
        ctx.Face(-1f);
        Assert.IsFalse(ctx.spriteRenderer.flipX);
        ctx.Face(0f);
        Assert.IsFalse(ctx.spriteRenderer.flipX, "0 이면 그대로");

        ctx.isDefaultLeft = false;
        ctx.Face(1f);
        Assert.IsFalse(ctx.spriteRenderer.flipX);
        ctx.Face(-1f);
        Assert.IsTrue(ctx.spriteRenderer.flipX);

        ctx.spriteRenderer = null;
        Assert.DoesNotThrow(() => ctx.Face(1f));
    }

    [Test]
    public void Context_IsDead_FalseWithoutHealth()
    {
        Assert.IsFalse(NewCtx(Vector2.zero, null).IsDead);
    }
    #endregion

    #region StateMachine
    private class FakeState : IMonsterState
    {
        private readonly MonsterStateId id;
        private readonly List<string> log;
        public System.Action onEnter;
        public System.Action onExit;

        public FakeState(MonsterStateId _id, List<string> _log)
        {
            id = _id;
            log = _log;
        }

        public MonsterStateId Id => id;
        public void Enter() { log.Add(id + ".Enter"); onEnter?.Invoke(); }
        public void Tick(float _dt) { log.Add(id + ".Tick"); }
        public void Exit() { log.Add(id + ".Exit"); onExit?.Invoke(); }
    }

    [Test]
    public void StateMachine_ChangeState_ExitThenEnterThenEvent_AndTick()
    {
        List<string> log = new List<string>();
        StateMachine m = new StateMachine();
        FakeState a = new FakeState(MonsterStateId.Move, log);
        FakeState b = new FakeState(MonsterStateId.Action, log);
        List<MonsterStateId> events = new List<MonsterStateId>();
        m.OnStateChanged += (id) => { events.Add(id); log.Add("event:" + id); };

        Assert.AreEqual(MonsterStateId.None, m.CurrentId);
        m.Tick(0.1f);
        m.ChangeState(a);
        m.Tick(0.1f);
        m.ChangeState(b);

        CollectionAssert.AreEqual(
            new[] { "Move.Enter", "event:Move", "Move.Tick", "Move.Exit", "Action.Enter", "event:Action" }
            , log);
        Assert.AreEqual(MonsterStateId.Action, m.CurrentId);
        CollectionAssert.AreEqual(new[] { MonsterStateId.Move, MonsterStateId.Action }, events);
    }

    [Test]
    public void StateMachine_ChangeInsideEnter_FinalStateWins_AndOnlyFinalEventForNested()
    {
        List<string> log = new List<string>();
        StateMachine m = new StateMachine();
        FakeState a = new FakeState(MonsterStateId.Move, log);
        FakeState b = new FakeState(MonsterStateId.Downed, log);
        FakeState c = new FakeState(MonsterStateId.Dead, log);
        b.onEnter = () => m.ChangeState(c);
        List<MonsterStateId> events = new List<MonsterStateId>();
        m.OnStateChanged += (id) => events.Add(id);

        m.ChangeState(a);
        m.ChangeState(b);

        Assert.AreEqual(MonsterStateId.Dead, m.CurrentId);
        CollectionAssert.AreEqual(new[] { MonsterStateId.Move, MonsterStateId.Dead }, events, "가려진 중간 상태 이벤트는 없다");
    }

    [Test]
    public void StateMachine_ChangeInsideExit_TakesNewTarget()
    {
        List<string> log = new List<string>();
        StateMachine m = new StateMachine();
        FakeState a = new FakeState(MonsterStateId.Move, log);
        FakeState b = new FakeState(MonsterStateId.Action, log);
        FakeState c = new FakeState(MonsterStateId.Dead, log);
        m.ChangeState(a);
        a.onExit = () => m.ChangeState(c);

        m.ChangeState(b);

        Assert.AreEqual(MonsterStateId.Dead, m.CurrentId);
        CollectionAssert.DoesNotContain(log, "Action.Enter");
    }

    [Test]
    public void StateMachine_Clear_DropsStateWithoutExit()
    {
        List<string> log = new List<string>();
        StateMachine m = new StateMachine();
        m.ChangeState(new FakeState(MonsterStateId.Move, log));
        log.Clear();

        m.Clear();

        Assert.AreEqual(MonsterStateId.None, m.CurrentId);
        Assert.IsNull(m.Current);
        CollectionAssert.IsEmpty(log);
    }
    #endregion

    #region MonsterData
    private static void InvokeValidate(MonsterData _d)
    {
        MethodInfo mi = typeof(MonsterData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        mi.Invoke(_d, null);
    }

    [Test]
    public void MonsterData_TwoMoveKeywords_WarnsOnValidate()
    {
        MonsterData d = So<MonsterData>();
        d.keywords = new MonsterKeyword[] { So<DashKeyword>(), So<OrbitKeyword>() };

        LogAssert.Expect(LogType.Warning, new Regex("Move 키워드가 2"));
        InvokeValidate(d);
    }

    [Test]
    public void MonsterData_OneMoveKeywordOrNullEntries_NoWarning()
    {
        MonsterData one = So<MonsterData>();
        one.keywords = new MonsterKeyword[] { So<DashKeyword>(), So<SelfDestructKeyword>(), So<ReviveKeyword>(), null };
        MonsterData none = So<MonsterData>();
        none.keywords = null;

        InvokeValidate(one);
        InvokeValidate(none);

        LogAssert.NoUnexpectedReceived();
    }
    #endregion
}
