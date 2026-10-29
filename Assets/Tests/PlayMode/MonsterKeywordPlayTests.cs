using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rig = MonsterTestFactory.Rig;
using StateLog = MonsterTestFactory.StateLog;

/// <summary>
/// 키워드 통합 검증: 순간이동, 자폭(+부활), 분열, 소환, 장판, 넉백 저항
/// </summary>
public class MonsterKeywordPlayTests
{
    [TearDown]
    public void TearDown()
    {
        MonsterTestFactory.Cleanup();
    }

    private static float Hp(GameObject _go)
    {
        return _go.GetComponent<HealthSystem>().CurrHP;
    }

    #region Teleport
    private static TeleportKeyword NewTeleport()
    {
        TeleportKeyword t = MonsterTestFactory.So<TeleportKeyword>();
        t.minDistance = 7f;
        t.telegraphTime = 0.2f;
        t.appearRadius = new Vector2(2f, 3f);
        t.postDelay = 0.3f;
        t.cooldown = 100f;
        t.fadeOnTelegraph = true;
        return t;
    }

    [UnityTest]
    public IEnumerator Teleport_FarTarget_FadesThenAppearsInRingAndHolds()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewTeleport());
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform target = MonsterTestFactory.CreateMarker(new Vector2(20f, 0f));
        StateLog log = new StateLog(r.brain);

        Vector2 posAtTelegraph = Vector2.one * 999f;
        Vector2 posAtAction = Vector2.zero;
        float alphaAtAction = -1f;
        r.brain.OnStateChanged += (id) =>
        {
            if (MonsterStateId.Telegraph == id)
            {
                posAtTelegraph = r.Pos;
            }
            else if (MonsterStateId.Action == id)
            {
                posAtAction = r.Pos;
                alphaAtAction = r.sprite.color.a;
            }
        };
        MonsterTestFactory.Init(r, target);

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Telegraph == r.brain.CurrentState, 1f);
        yield return MonsterTestFactory.Wait(0.1f);
        float alphaMid = r.sprite.color.a;
        Assert.Less(alphaMid, 1f, "예고 중에는 사라지는 중");
        Assert.GreaterOrEqual(alphaMid, 0f);
        Assert.AreEqual(0f, r.Pos.x, 0.0001f, "예고 중에는 제자리");

        yield return MonsterTestFactory.WaitUntil(() => log.States.Count >= 3, 1f);
        Assert.AreEqual(Vector2.zero, posAtTelegraph);
        float d = Vector2.Distance(posAtAction, target.position);
        Assert.That(d, Is.InRange(2f - 0.001f, 3f + 0.001f), "타깃 주변 링 안");
        Assert.AreEqual(1f, alphaAtAction, 0.0001f, "나타나면서 알파 복구");

        // postDelay 동안 정지
        yield return MonsterTestFactory.Wait(0.1f);
        Assert.AreEqual(MonsterStateId.Action, r.brain.CurrentState);
        Assert.AreEqual(posAtAction.x, r.Pos.x, 0.0001f);
        Assert.AreEqual(posAtAction.y, r.Pos.y, 0.0001f);

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Move == r.brain.CurrentState, 1f);
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
    }

    [UnityTest]
    public IEnumerator Teleport_NearTarget_DoesNotTeleport()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewTeleport());
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(3f, 0f)));

        yield return MonsterTestFactory.Wait(0.4f);

        CollectionAssert.AreEqual(new[] { MonsterStateId.Move }, log.States);
        Assert.AreEqual(0f, r.Pos.x, 0.0001f);
    }
    #endregion

    #region SelfDestruct
    private static SelfDestructKeyword NewBomb(float _damage = 20f, float _radius = 3f, bool _explodeOnDeath = false)
    {
        SelfDestructKeyword s = MonsterTestFactory.So<SelfDestructKeyword>();
        s.triggerRange = 2f;
        s.telegraphTime = 0.15f;
        s.radius = _radius;
        s.damage = _damage;
        s.knockbackForce = 0f;
        s.explodeOnDeath = _explodeOnDeath;
        s.inflateScale = 2f;
        return s;
    }

    [UnityTest]
    public IEnumerator SelfDestruct_InflatesThenDamagesOnceInRadiusAndKills()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(20f, 3f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject near = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 100f);
        GameObject far = MonsterTestFactory.CreateTarget(new Vector2(10f, 0f), 100f);
        int deaths = 0;
        r.health.OnDeath += (go) => ++deaths;
        MonsterTestFactory.Init(r, near.transform, _damageRate: 1.5f);

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Telegraph == r.brain.CurrentState, 1f);
        yield return MonsterTestFactory.Wait(0.08f);
        Assert.Greater(r.body.transform.localScale.x, 1.05f, "예고 중 부풀어 오른다");
        Assert.AreEqual(100f, Hp(near), "예고 중에는 데미지 없음");

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Dead == r.brain.CurrentState, 1f);
        yield return MonsterTestFactory.Wait(0.3f);

        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        Assert.IsTrue(r.health.isDead);
        Assert.AreEqual(1, deaths);
        Assert.AreEqual(100f - 30f, Hp(near), 0.001f, "damage 20 x damageRate 1.5, 한 번만");
        Assert.AreEqual(100f, Hp(far), "반경 밖은 맞지 않는다");
    }

    [UnityTest]
    public IEnumerator SelfDestruct_TargetOutsideTriggerRange_DoesNotStart()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb());
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(2.5f, 0f), 100f);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, t.transform);

        yield return MonsterTestFactory.Wait(0.4f);

        CollectionAssert.AreEqual(new[] { MonsterStateId.Move }, log.States);
        Assert.AreEqual(100f, Hp(t));
    }

    [UnityTest]
    public IEnumerator SelfDestruct_LeftMonster_HitsLPlayerOnly_NotRPlayer()
    {
        const int LMonster = 27;
        const int RPlayer = 28;
        const int LPlayer = 29;
        bool prev = Physics2D.GetIgnoreLayerCollision(LMonster, RPlayer);
        bool prev2 = Physics2D.GetIgnoreLayerCollision(LMonster, LPlayer);
        Physics2D.IgnoreLayerCollision(LMonster, RPlayer, true);
        Physics2D.IgnoreLayerCollision(LMonster, LPlayer, false);
        try
        {
            MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(20f, 3f));
            data.targetLayer = (1 << RPlayer) | (1 << LPlayer);
            Rig r = MonsterTestFactory.Create(data, Vector2.zero);
            r.root.layer = LMonster;
            GameObject lp = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 100f);
            lp.layer = LPlayer;
            GameObject rp = MonsterTestFactory.CreateTarget(new Vector2(-1f, 0f), 100f);
            rp.layer = RPlayer;
            Physics2D.SyncTransforms();
            MonsterTestFactory.Init(r, lp.transform);

            yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Dead == r.brain.CurrentState, 1.5f);
            yield return MonsterTestFactory.Wait(0.3f);

            Assert.AreEqual(80f, Hp(lp), 0.001f, "같은 진영 플레이어는 맞는다");
            Assert.AreEqual(100f, Hp(rp), "반대 진영 플레이어는 맞지 않는다");
        }
        finally
        {
            Physics2D.IgnoreLayerCollision(LMonster, RPlayer, prev);
            Physics2D.IgnoreLayerCollision(LMonster, LPlayer, prev2);
        }
    }

    [UnityTest]
    public IEnumerator SelfDestruct_ExplodeOnDeath_KilledByDamage_HitsTargetOnce()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(20f, 3f, _explodeOnDeath: true));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        // 트리거 범위 밖(2.5)이지만 반경(3) 안
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(2.5f, 0f), 100f);
        MonsterTestFactory.Init(r, t.transform);
        yield return null;

        DamageMassage msg = new DamageMassage();
        msg.damage = 1000f;
        r.pipeline.ProcessDamage(ref msg);

        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        Assert.AreEqual(80f, Hp(t), 0.001f, "처치될 때도 폭발");
    }

    [UnityTest]
    public IEnumerator SelfDestruct_ExplodeOnDeathFalse_KilledByDamage_NoExplosion()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(20f, 3f, _explodeOnDeath: false));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(2.5f, 0f), 100f);
        MonsterTestFactory.Init(r, t.transform);
        yield return null;

        r.health.Kill();

        Assert.AreEqual(100f, Hp(t));
    }

    [UnityTest]
    public IEnumerator SelfDestruct_ExplodeOnDeath_AfterSelfDestruct_NoDoubleExplosion()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(20f, 3f, _explodeOnDeath: true));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 100f);
        MonsterTestFactory.Init(r, t.transform);

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Dead == r.brain.CurrentState, 2f);
        yield return MonsterTestFactory.Wait(0.2f);

        Assert.AreEqual(80f, Hp(t), 0.001f, "자폭 폭발 + 사망 시 폭발이 겹치지 않는다");
    }

    [UnityTest]
    public IEnumerator SelfDestruct_DisabledMidTelegraph_RestoresScale()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb());
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 100f);
        MonsterTestFactory.Init(r, t.transform);
        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Telegraph == r.brain.CurrentState, 1f);
        yield return MonsterTestFactory.Wait(0.08f);
        Assert.Greater(r.body.transform.localScale.x, 1.05f);

        r.root.SetActive(false);

        Assert.AreEqual(1f, r.body.transform.localScale.x, 0.0001f, "비활성화 시 크기 원복");
    }
    #endregion

    #region SelfDestruct + Revive
    private static ReviveKeyword NewRevive(int _count, float _delay, float _ratio)
    {
        ReviveKeyword rv = MonsterTestFactory.So<ReviveKeyword>();
        rv.reviveCount = _count;
        rv.reviveDelay = _delay;
        rv.hpRatio = _ratio;
        return rv;
    }

    [UnityTest]
    public IEnumerator SelfDestructRevive_Downed_ThenRevivesWithScaleResetAndHpRatio()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(10f, 3f), NewRevive(1, 0.15f, 0.4f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 1000f);
        int deaths = 0;
        r.health.OnDeath += (go) => ++deaths;

        float scaleAtDown = 0f;
        bool downedFlag = false;
        bool colliderOnAtDown = true;
        float scaleAtRevive = 0f;
        float hpAtRevive = 0f;
        bool deadAtRevive = true;
        bool colliderOnAtRevive = false;
        bool revived = false;
        Collider2D col = r.body.GetComponent<Collider2D>();
        r.brain.OnStateChanged += (id) =>
        {
            if (MonsterStateId.Downed == id)
            {
                scaleAtDown = r.body.transform.localScale.x;
                downedFlag = r.health.IsDowned;
                colliderOnAtDown = col.enabled;
            }
            else if (MonsterStateId.Move == id && false == revived && scaleAtDown > 0f)
            {
                revived = true;
                scaleAtRevive = r.body.transform.localScale.x;
                hpAtRevive = r.health.CurrHP;
                deadAtRevive = r.health.isDead;
                colliderOnAtRevive = col.enabled;
            }
        };
        MonsterTestFactory.Init(r, t.transform);

        yield return MonsterTestFactory.WaitUntil(() => revived, 3f);

        Assert.IsTrue(revived, "부활해야 한다");
        Assert.IsTrue(downedFlag, "다운 상태");
        Assert.IsFalse(colliderOnAtDown, "다운 중 콜라이더 꺼짐");
        Assert.AreEqual(2f, scaleAtDown, 0.05f, "다운 중에는 부푼 채로");
        Assert.AreEqual(1f, scaleAtRevive, 0.0001f, "부활 후 스케일 원복");
        Assert.AreEqual(40f, hpAtRevive, 0.001f, "maxHP 100 x hpRatio 0.4");
        Assert.IsFalse(deadAtRevive);
        Assert.IsTrue(colliderOnAtRevive);
        Assert.AreEqual(0, deaths, "부활 시에는 OnDeath 가 불리지 않는다");
    }

    [UnityTest]
    public IEnumerator SelfDestructRevive_AfterReviveCountExhausted_OnDeathFiresOnce()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewBomb(10f, 3f), NewRevive(1, 0.1f, 0.5f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject t = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 1000f);
        int deaths = 0;
        r.health.OnDeath += (go) => ++deaths;
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, t.transform);

        yield return MonsterTestFactory.WaitUntil(() => MonsterStateId.Dead == r.brain.CurrentState, 4f);
        yield return MonsterTestFactory.Wait(0.2f);

        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        Assert.AreEqual(1, deaths, "부활 1회 소진 후 두 번째 사망에서만 OnDeath");
        Assert.AreEqual(1, log.Count(MonsterStateId.Downed));
        Assert.AreEqual(0, ((ReviveRuntime)r.brain.Runtimes[1]).Remain);
        Assert.AreEqual(1000f - 20f, Hp(t), 0.001f, "자폭은 각 생애마다 한 번씩 총 두 번");
        Assert.IsFalse(r.health.IsDowned);
    }

    [UnityTest]
    public IEnumerator Revive_KillRepeatedly_RespectsReviveCount()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewRevive(2, 0.05f, 0.5f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        int deaths = 0;
        r.health.OnDeath += (go) => ++deaths;
        MonsterTestFactory.Init(r, null);

        for (int i = 0; i < 2; ++i)
        {
            r.health.Kill();
            Assert.AreEqual(MonsterStateId.Downed, r.brain.CurrentState, $"{i + 1}번째 사망은 다운");
            Assert.IsTrue(r.health.IsDowned);
            yield return MonsterTestFactory.WaitUntil(() => false == r.health.isDead, 1f);
            Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
            Assert.AreEqual(50f, r.health.CurrHP, 0.001f);
        }
        Assert.AreEqual(0, deaths);

        r.health.Kill();

        Assert.AreEqual(1, deaths);
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        Assert.IsFalse(r.health.IsDowned);
    }

    [UnityTest]
    public IEnumerator Revive_ZeroCount_DiesImmediately()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewRevive(0, 0.05f, 0.5f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        int deaths = 0;
        r.health.OnDeath += (go) => ++deaths;
        MonsterTestFactory.Init(r, null);

        r.health.Kill();

        Assert.AreEqual(1, deaths);
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Revive_LethalDamageWhileDowned_IsIgnored()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewRevive(1, 0.3f, 0.5f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        int deaths = 0;
        r.health.OnDeath += (go) => ++deaths;
        MonsterTestFactory.Init(r, null);

        DamageMassage msg = new DamageMassage();
        msg.damage = 999f;
        r.pipeline.ProcessDamage(ref msg);
        Assert.AreEqual(MonsterStateId.Downed, r.brain.CurrentState);

        r.pipeline.ProcessDamage(ref msg);
        yield return MonsterTestFactory.WaitUntil(() => false == r.health.isDead, 2f);

        Assert.AreEqual(0, deaths);
        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
    }
    #endregion

    #region Split
    private static Rig NewChildTemplate(out MonsterData _childData, float _hp = 50f)
    {
        _childData = MonsterTestFactory.NewData(0f);
        return MonsterTestFactory.Create(_childData, new Vector2(500f, 500f), _hp, _contact: true, _activate: false, _spawnable: true);
    }

    [UnityTest]
    public IEnumerator Split_OnFinalDeath_SpawnsCountChildrenWithinScatterRadius()
    {
        Rig child = NewChildTemplate(out MonsterData childData);
        SplitKeyword split = MonsterTestFactory.So<SplitKeyword>();
        split.childPrefab = child.spawnable;
        split.count = 3;
        split.scatterRadius = 1f;

        MonsterData data = MonsterTestFactory.NewData(0f, split);
        Rig r = MonsterTestFactory.Create(data, new Vector2(4f, 4f));
        Transform player = MonsterTestFactory.CreateMarker(new Vector2(40f, 4f));
        Spawner sp = MonsterTestFactory.CreateSpawner(player);
        MonsterTestFactory.Init(r, player, _spawner: sp);
        yield return null;
        Assert.AreEqual(0, MonsterTestFactory.FindActive(childData).Count);

        r.health.Kill();

        List<MonsterBrain> kids = MonsterTestFactory.FindActive(childData);
        Assert.AreEqual(3, kids.Count);
        foreach (MonsterBrain k in kids)
        {
            Assert.LessOrEqual(Vector2.Distance(k.transform.position, new Vector2(4f, 4f)), 1f + 0.001f);
            Assert.AreEqual(MonsterStateId.Move, k.CurrentState, "스폰 시 Initialize 됨");
            Assert.AreSame(player, k.Context.target);
            Assert.AreSame(sp, k.Context.spawner);
            Assert.AreEqual(50f, k.Context.health.CurrHP);
        }
    }

    [UnityTest]
    public IEnumerator Split_WithRevive_SpawnsOnlyOnFinalDeath()
    {
        Rig child = NewChildTemplate(out MonsterData childData);
        SplitKeyword split = MonsterTestFactory.So<SplitKeyword>();
        split.childPrefab = child.spawnable;
        split.count = 2;
        split.scatterRadius = 0.5f;

        MonsterData data = MonsterTestFactory.NewData(0f, split, NewRevive(1, 0.05f, 0.5f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform player = MonsterTestFactory.CreateMarker(new Vector2(40f, 0f));
        Spawner sp = MonsterTestFactory.CreateSpawner(player);
        MonsterTestFactory.Init(r, player, _spawner: sp);

        r.health.Kill();
        Assert.AreEqual(MonsterStateId.Downed, r.brain.CurrentState);
        Assert.AreEqual(0, MonsterTestFactory.FindActive(childData).Count, "다운(가로채기) 시에는 분열하지 않는다");

        yield return MonsterTestFactory.WaitUntil(() => false == r.health.isDead, 1f);
        Assert.AreEqual(0, MonsterTestFactory.FindActive(childData).Count);

        r.health.Kill();
        Assert.AreEqual(2, MonsterTestFactory.FindActive(childData).Count);
    }

    [UnityTest]
    public IEnumerator Split_CountOne_SpawnsOne()
    {
        Rig child = NewChildTemplate(out MonsterData childData);
        SplitKeyword split = MonsterTestFactory.So<SplitKeyword>();
        split.childPrefab = child.spawnable;
        split.count = 1;
        split.scatterRadius = 0f;

        MonsterData data = MonsterTestFactory.NewData(0f, split);
        Rig r = MonsterTestFactory.Create(data, new Vector2(2f, 2f));
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        MonsterTestFactory.Init(r, null, _spawner: sp);

        r.health.Kill();

        List<MonsterBrain> kids = MonsterTestFactory.FindActive(childData);
        Assert.AreEqual(1, kids.Count);
        Assert.AreEqual(2f, kids[0].transform.position.x, 0.0001f, "scatterRadius 0 이면 사망 위치");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Split_NoSpawnerOrNoChildPrefab_DoesNothing()
    {
        Rig child = NewChildTemplate(out MonsterData childData);
        SplitKeyword withPrefab = MonsterTestFactory.So<SplitKeyword>();
        withPrefab.childPrefab = child.spawnable;
        SplitKeyword noPrefab = MonsterTestFactory.So<SplitKeyword>();
        noPrefab.childPrefab = null;

        Rig noSpawner = MonsterTestFactory.Create(MonsterTestFactory.NewData(0f, withPrefab), Vector2.zero);
        MonsterTestFactory.Init(noSpawner, null);
        Rig noChild = MonsterTestFactory.Create(MonsterTestFactory.NewData(0f, noPrefab), Vector2.zero);
        MonsterTestFactory.Init(noChild, null, _spawner: MonsterTestFactory.CreateSpawner(null));

        noSpawner.health.Kill();
        noChild.health.Kill();

        Assert.AreEqual(0, MonsterTestFactory.FindActive(childData).Count);
        Assert.AreEqual(MonsterStateId.Dead, noSpawner.brain.CurrentState);
        Assert.AreEqual(MonsterStateId.Dead, noChild.brain.CurrentState);
        yield return null;
    }
    #endregion

    #region Summoner
    private static SummonerKeyword NewSummoner(Spawnable _minion, int _count, int _maxAlive, float _cooldown = 0.2f)
    {
        SummonerKeyword s = MonsterTestFactory.So<SummonerKeyword>();
        s.minionPrefab = _minion;
        s.count = _count;
        s.maxAlive = _maxAlive;
        s.spawnRadius = 1.5f;
        s.telegraphTime = 0.05f;
        s.cooldown = _cooldown;
        s.keepDistance = 0f;
        return s;
    }

    [UnityTest]
    public IEnumerator Summoner_SpawnsInBatches_NeverExceedsMaxAlive_RefillsAfterDeath()
    {
        Rig minionTemplate = NewChildTemplate(out MonsterData minionData);
        SummonerKeyword sum = NewSummoner(minionTemplate.spawnable, 3, 4, 0.2f);
        MonsterData data = MonsterTestFactory.NewData(0f, sum);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform player = MonsterTestFactory.CreateMarker(new Vector2(60f, 0f));
        Spawner sp = MonsterTestFactory.CreateSpawner(player);
        MonsterTestFactory.Init(r, player, _spawner: sp);

        yield return MonsterTestFactory.Wait(0.05f);
        Assert.AreEqual(0, MonsterTestFactory.CountAlive(minionData), "초기 쿨다운 동안은 소환하지 않는다");

        yield return MonsterTestFactory.WaitUntil(() => MonsterTestFactory.CountAlive(minionData) >= 4, 3f);
        Assert.AreEqual(4, MonsterTestFactory.CountAlive(minionData), "3 + 1 (maxAlive 로 잘림)");
        foreach (MonsterBrain m in MonsterTestFactory.FindActive(minionData))
        {
            Assert.LessOrEqual(Vector2.Distance(m.transform.position, Vector2.zero), 1.5f + 0.001f);
        }

        int max = 0;
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            max = Mathf.Max(max, MonsterTestFactory.CountAlive(minionData));
            yield return null;
        }
        Assert.AreEqual(4, max, "maxAlive 초과 금지");

        // 하나 죽으면 다시 채운다
        MonsterTestFactory.FindActive(minionData)[0].Context.health.Kill();
        Assert.AreEqual(3, MonsterTestFactory.CountAlive(minionData));
        yield return MonsterTestFactory.WaitUntil(() => MonsterTestFactory.CountAlive(minionData) >= 4, 3f);
        Assert.AreEqual(4, MonsterTestFactory.CountAlive(minionData));
        Assert.AreEqual(5, MonsterTestFactory.FindActive(minionData).Count, "죽은 개체 1 + 살아있는 4");
    }

    [UnityTest]
    public IEnumerator Summoner_MaxAliveSmallerThanCount_SpawnsOnlyMaxAlive()
    {
        Rig minionTemplate = NewChildTemplate(out MonsterData minionData);
        SummonerKeyword sum = NewSummoner(minionTemplate.spawnable, 5, 2, 0.1f);
        MonsterData data = MonsterTestFactory.NewData(0f, sum);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform player = MonsterTestFactory.CreateMarker(new Vector2(60f, 0f));
        MonsterTestFactory.Init(r, player, _spawner: MonsterTestFactory.CreateSpawner(player));

        yield return MonsterTestFactory.Wait(1f);

        Assert.AreEqual(2, MonsterTestFactory.CountAlive(minionData));
    }

    [UnityTest]
    public IEnumerator Summoner_NoSpawner_NeverStartsAction()
    {
        Rig minionTemplate = NewChildTemplate(out MonsterData minionData);
        SummonerKeyword sum = NewSummoner(minionTemplate.spawnable, 2, 4, 0.05f);
        MonsterData data = MonsterTestFactory.NewData(0f, sum);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        StateLog log = new StateLog(r.brain);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(new Vector2(60f, 0f)));

        yield return MonsterTestFactory.Wait(0.4f);

        CollectionAssert.AreEqual(new[] { MonsterStateId.Move }, log.States);
        Assert.AreEqual(0, MonsterTestFactory.FindActive(minionData).Count);
    }

    [UnityTest]
    public IEnumerator Summoner_KeepDistance_ApproachesWhenFarAndBacksOffWhenClose()
    {
        SummonerKeyword sum = NewSummoner(null, 1, 1);
        sum.keepDistance = 5f;
        sum.keepDistanceTolerance = 0.2f;
        MonsterData data = MonsterTestFactory.NewData(10f, sum);
        Rig far = MonsterTestFactory.Create(data, new Vector2(20f, 0f));
        Rig close = MonsterTestFactory.Create(data, new Vector2(0f, 20f));
        Transform c1 = MonsterTestFactory.CreateMarker(Vector2.zero);
        Transform c2 = MonsterTestFactory.CreateMarker(new Vector2(0f, 21f));
        MonsterTestFactory.Init(far, c1);
        MonsterTestFactory.Init(close, c2);

        yield return MonsterTestFactory.Wait(2f);

        Assert.AreEqual(5f, Vector2.Distance(far.Pos, c1.position), 0.25f);
        Assert.AreEqual(5f, Vector2.Distance(close.Pos, c2.position), 0.25f);
    }
    #endregion

    #region Hazard
    private static HazardZone NewZonePrefab()
    {
        GameObject go = new GameObject("TestZonePrefab");
        go.SetActive(false);
        MonsterTestFactory.Track(go);
        return go.AddComponent<HazardZone>();
    }

    [UnityTest]
    public IEnumerator HazardZone_DamagesEachTargetOncePerTick()
    {
        HazardZone prefab = NewZonePrefab();
        // 콜라이더가 여러 개여도 대상당 한 틱에 1회
        GameObject multi = MonsterTestFactory.CreateTarget(Vector2.zero, 100f, 2);
        GameObject single = MonsterTestFactory.CreateTarget(new Vector2(0.5f, 0f), 100f);
        GameObject outside = MonsterTestFactory.CreateTarget(new Vector2(5f, 0f), 100f);

        HazardZone zone = HazardZone.Spawn(prefab, Vector2.zero, 1f, 10f, 0.6f, 5f, MonsterTestFactory.TargetMask);
        Assert.IsNotNull(zone);

        yield return MonsterTestFactory.Wait(0.25f);
        Assert.AreEqual(95f, Hp(multi), 0.001f, "첫 틱 즉시 1회");
        Assert.AreEqual(95f, Hp(single), 0.001f);
        Assert.AreEqual(100f, Hp(outside));

        yield return MonsterTestFactory.Wait(0.6f);
        Assert.AreEqual(90f, Hp(multi), 0.001f, "다음 틱 1회 추가");
        Assert.AreEqual(90f, Hp(single), 0.001f);
        Assert.AreEqual(100f, Hp(outside));
    }

    [UnityTest]
    public IEnumerator HazardZone_ReturnsToPoolAfterDuration_AndIsReused()
    {
        HazardZone prefab = NewZonePrefab();
        HazardZone first = HazardZone.Spawn(prefab, Vector2.zero, 1f, 0.2f, 0.1f, 1f, MonsterTestFactory.TargetMask);
        Assert.IsTrue(first.gameObject.activeSelf);

        yield return MonsterTestFactory.Wait(0.6f);
        Assert.IsFalse(first.gameObject.activeSelf, "수명이 끝나면 풀로 반환");

        HazardZone second = HazardZone.Spawn(prefab, new Vector2(3f, 0f), 1f, 5f, 0.1f, 1f, MonsterTestFactory.TargetMask);
        Assert.AreSame(first, second, "풀 재사용");
        Assert.IsTrue(second.gameObject.activeSelf);
        Assert.AreEqual(3f, second.transform.position.x, 0.0001f);
    }

    [Test]
    public void HazardZone_NullPrefab_ReturnsNull()
    {
        Assert.IsNull(HazardZone.Spawn(null, Vector2.zero, 1f, 1f, 1f, 1f, MonsterTestFactory.TargetMask));
    }

    [UnityTest]
    public IEnumerator HazardTrail_LeavesZonesWhileAlive_DamageScaledByDamageRate()
    {
        HazardTrailKeyword trail = MonsterTestFactory.So<HazardTrailKeyword>();
        trail.zonePrefab = NewZonePrefab();
        trail.interval = 0.2f;
        trail.radius = 1f;
        trail.duration = 10f;
        trail.tickInterval = 5f;
        trail.damagePerTick = 4f;

        MonsterData data = MonsterTestFactory.NewData(0f, trail);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        GameObject victim = MonsterTestFactory.CreateTarget(new Vector2(0.5f, 0f), 1000f);
        MonsterTestFactory.Init(r, victim.transform, _damageRate: 2f);

        yield return MonsterTestFactory.Wait(0.75f);

        int zones = 0;
        foreach (HazardZone z in Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            ++zones;
        }
        Assert.GreaterOrEqual(zones, 2, "interval 마다 장판 생성");
        float lost = 1000f - Hp(victim);
        Assert.Greater(lost, 0f);
        Assert.AreEqual(0f, lost % 8f, 0.001f, "틱당 4 x damageRate 2 = 8 의 배수");

        // 죽으면 더 만들지 않는다
        r.health.Kill();
        int before = 0;
        foreach (HazardZone z in Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            ++before;
        }
        yield return MonsterTestFactory.Wait(0.6f);
        int after = 0;
        foreach (HazardZone z in Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            ++after;
        }
        Assert.AreEqual(before, after);
    }

    [UnityTest]
    public IEnumerator HazardTrail_NullZonePrefab_DoesNotThrow()
    {
        HazardTrailKeyword trail = MonsterTestFactory.So<HazardTrailKeyword>();
        trail.zonePrefab = null;
        trail.interval = 0.05f;
        MonsterData data = MonsterTestFactory.NewData(0f, trail);
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(r, MonsterTestFactory.CreateMarker(Vector2.one));

        yield return MonsterTestFactory.Wait(0.3f);

        Assert.AreEqual(MonsterStateId.Move, r.brain.CurrentState);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator MonsterAreaDamage_Boundaries()
    {
        GameObject a = MonsterTestFactory.CreateTarget(new Vector2(1f, 0f), 100f, 1);
        GameObject b = MonsterTestFactory.CreateTarget(new Vector2(-1f, 0f), 100f);
        DamagePipeline attacker = b.GetComponent<DamagePipeline>();

        Assert.AreEqual(0, MonsterAreaDamage.Apply(Vector2.zero, 0f, MonsterTestFactory.TargetMask, 5f, 0f, null), "반경 0");
        Assert.AreEqual(0, MonsterAreaDamage.Apply(Vector2.zero, 3f, (LayerMask)0, 5f, 0f, null), "레이어 비어 있음");
        Assert.AreEqual(100f, Hp(a));

        Assert.AreEqual(2, MonsterAreaDamage.Apply(Vector2.zero, 3f, MonsterTestFactory.TargetMask, 5f, 0f, null));
        Assert.AreEqual(95f, Hp(a), 0.001f, "다중 콜라이더여도 1회");
        Assert.AreEqual(95f, Hp(b), 0.001f);

        Assert.AreEqual(1, MonsterAreaDamage.Apply(Vector2.zero, 3f, MonsterTestFactory.TargetMask, 5f, 0f, attacker), "공격자 본인 제외");
        Assert.AreEqual(90f, Hp(a), 0.001f);
        Assert.AreEqual(95f, Hp(b), 0.001f);
        yield return null;
    }
    #endregion

    #region KnockbackResist
    private static KnockbackResistKeyword NewResist(float _value)
    {
        KnockbackResistKeyword k = MonsterTestFactory.So<KnockbackResistKeyword>();
        k.resistance = _value;
        return k;
    }

    private static void Hit(Rig _r, float _force)
    {
        DamageMassage msg = new DamageMassage();
        msg.damage = 0f;
        msg.knockbackForce = _force;
        msg.hitPoint = new Vector2(-10f, 0f);
        _r.pipeline.ProcessDamage(ref msg);
    }

    [UnityTest]
    public IEnumerator KnockbackResist_FullResistance_BlocksKnockback_RestoredOnDisable()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewResist(1f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        r.knockback.resistance = 0.2f;
        r.knockback.deceleration = 0.01f;
        MonsterTestFactory.Init(r, null);
        Assert.AreEqual(1f, r.knockback.resistance);

        Hit(r, 10f);
        Assert.IsFalse(r.knockback.IsKnockedBack, "저항 1 이면 밀리지 않는다");

        r.root.SetActive(false);
        Assert.AreEqual(0.2f, r.knockback.resistance, 0.0001f, "디스폰 시 원래 값으로");
        r.root.SetActive(true);
        MonsterTestFactory.Init(r, null);
        Assert.AreEqual(1f, r.knockback.resistance, "재사용 시 다시 적용");
        yield return null;
    }

    [UnityTest]
    public IEnumerator KnockbackResist_Partial_ScalesKnockbackVelocity()
    {
        MonsterData data = MonsterTestFactory.NewData(0f, NewResist(0.5f));
        Rig r = MonsterTestFactory.Create(data, Vector2.zero);
        Transform dummyRoot = MonsterTestFactory.CreateMarker(new Vector2(0f, 500f));
        r.knockback.moveRoot = dummyRoot;
        r.knockback.deceleration = 0.01f;
        MonsterTestFactory.Init(r, null);

        // force 0.08 x (1 - 0.5) = 0.04 < 정지 임계(0.05) → 밀리지 않음, 저항 없이는 밀림
        Hit(r, 0.08f);
        Assert.IsFalse(r.knockback.IsKnockedBack);
        Hit(r, 0.2f);
        Assert.IsTrue(r.knockback.IsKnockedBack, "0.2 x 0.5 = 0.1 > 0.05");
        yield return null;
    }
    #endregion
}
