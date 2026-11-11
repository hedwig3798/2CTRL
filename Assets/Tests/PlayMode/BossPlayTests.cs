using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rig = MonsterTestFactory.Rig;

/// <summary>
/// 중간 보스: Spawner 고정 수치, MidBossScheduler (경고·출현·개체수 보정), PlayerStats 임시 둔화 만료
/// 프리팹·SO 없이 런타임 조립
/// </summary>
public class BossPlayTests
{
    private readonly List<GameObject> extra = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject g in extra)
        {
            if (null != g)
            {
                Object.DestroyImmediate(g);
            }
        }
        extra.Clear();
        MonsterTestFactory.Cleanup();
    }

    private static Rig NewMonster(float _hp, bool _boss)
    {
        MonsterData data = MonsterTestFactory.NewData(0f);
        Rig r = MonsterTestFactory.Create(data, new Vector2(500f, 500f), _hp, true, false, true);
        if (true == _boss)
        {
            r.root.AddComponent<MidBoss>();
        }
        return r;
    }

    private Transform NewPlayerMarker(PlayerSide _side)
    {
        Transform t = MonsterTestFactory.CreateMarker(Vector2.zero);
        PlayerStats ps = t.gameObject.AddComponent<PlayerStats>();
        typeof(PlayerStats).GetField("side", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(ps, _side);
        return t;
    }

    #region Spawner 고정 수치
    [Test]
    public void SpawnAt_MidBoss_IgnoresHPSpeedDamageRates()
    {
        Rig bossTpl = NewMonster(50f, true);
        Rig normalTpl = NewMonster(50f, false);
        Transform player = NewPlayerMarker(PlayerSide.Left);
        Spawner sp = MonsterTestFactory.CreateSpawner(player);
        sp.HPRate = 3f;
        sp.speedRate = 2f;
        sp.damagerate = 4f;

        Spawnable boss = sp.SpawnAt(bossTpl.spawnable, Vector2.zero);
        Spawnable normal = sp.SpawnAt(normalTpl.spawnable, Vector2.zero);

        MonsterBrain bb = boss.GetComponent<MonsterBrain>();
        Assert.AreEqual(50f, bb.Context.health.MaxHP, 0.001f, "보스 HP 고정");
        Assert.AreEqual(1f, bb.Context.moveSpeedRate, 1e-5f);
        Assert.AreEqual(1f, bb.Context.damageRate, 1e-5f);

        MonsterBrain nb = normal.GetComponent<MonsterBrain>();
        Assert.AreEqual(150f, nb.Context.health.MaxHP, 0.001f, "일반 몬스터는 배율 적용");
        Assert.AreEqual(2f, nb.Context.moveSpeedRate, 1e-5f);
    }

    [Test]
    public void SpawnAt_MidBoss_AfterRelease_StillFixed()
    {
        Rig bossTpl = NewMonster(50f, true);
        Spawner sp = MonsterTestFactory.CreateSpawner(NewPlayerMarker(PlayerSide.Left));
        sp.HPRate = 3f;
        Spawnable first = sp.SpawnAt(bossTpl.spawnable, Vector2.zero);
        Assert.AreEqual(50f, first.GetComponent<MonsterBrain>().Context.health.MaxHP, 0.001f);
        sp.HPRate = 5f;
        Spawnable second = sp.SpawnAt(bossTpl.spawnable, Vector2.zero);
        Assert.AreEqual(50f, second.GetComponent<MonsterBrain>().Context.health.MaxHP, 0.001f);
    }

    [Test]
    public void MidBoss_Initialize_SetsSideExecuteImmuneAndKnockbackResistance()
    {
        Rig bossTpl = NewMonster(50f, true);
        Spawner sp = MonsterTestFactory.CreateSpawner(NewPlayerMarker(PlayerSide.Right));
        Spawnable s = sp.SpawnAt(bossTpl.spawnable, Vector2.zero);

        MidBoss boss = s.GetComponent<MidBoss>();
        Assert.IsTrue(boss.HasSide);
        Assert.AreEqual(PlayerSide.Right, boss.Side);
        Assert.IsTrue(boss.Health.IsExecuteImmune);
        Assert.AreEqual(1f, boss.Health.IncomingDamageMultiplier);

        DamageMassage msg = new DamageMassage();
        msg.damage = 5f;
        boss.Health.Execute(ref msg);
        Assert.IsFalse(boss.Health.isDead);
        Assert.AreEqual(45f, boss.Health.CurrHP, 1e-3f, "즉사 면역: 일반 데미지");
    }

    [Test]
    public void MidBoss_Airborne_BlocksDamage_AndRestores()
    {
        Rig bossTpl = NewMonster(50f, true);
        Spawner sp = MonsterTestFactory.CreateSpawner(NewPlayerMarker(PlayerSide.Left));
        MidBoss boss = sp.SpawnAt(bossTpl.spawnable, Vector2.zero).GetComponent<MidBoss>();

        boss.SetAirborne(true);
        DamageMassage msg = new DamageMassage();
        msg.damage = 20f;
        boss.Health.ProcessDamage(ref msg);
        Assert.AreEqual(50f, boss.Health.CurrHP, 1e-3f);

        boss.SetAirborne(false);
        boss.Health.ProcessDamage(ref msg);
        Assert.AreEqual(30f, boss.Health.CurrHP, 1e-3f);
    }
    #endregion

    #region MidBossScheduler
    private static MidBossEntry Entry(Spawnable _prefab, float _lead, float _countMul)
    {
        MidBossEntry e = new MidBossEntry();
        e.prefab = _prefab;
        e.spawnTime = 9999f;
        e.warningLead = _lead;
        e.spawnCountMultiplier = _countMul;
        e.expTotal = 10f;
        e.gemCount = 2;
        return e;
    }

    private Spawner NewSchedulerSpawner(Transform _target, MidBossEntry[] _entries, out MidBossScheduler _sched)
    {
        GameObject go = new GameObject("TestSchedSpawner");
        go.SetActive(false);
        extra.Add(go);
        Spawner sp = go.AddComponent<Spawner>();
        sp.target = _target;
        _sched = go.AddComponent<MidBossScheduler>();
        _sched.SetEntries(_entries);
        // 웨이브 없는 Spawner.Start 의 에러 로그
        LogAssert.Expect(LogType.Error, new Regex(@"\[Spawner\]"));
        go.SetActive(true);
        return sp;
    }

    [UnityTest]
    public IEnumerator DebugSpawnMidBoss_RaisesWarningThenSpawned_AndAppliesCountMultiplier()
    {
        Rig tpl = NewMonster(50f, true);
        Transform player = NewPlayerMarker(PlayerSide.Right);
        MidBossEntry[] entries = { Entry(tpl.spawnable, 0.1f, 0.5f) };
        Spawner sp = NewSchedulerSpawner(player, entries, out MidBossScheduler sched);

        List<string> log = new List<string>();
        PlayerSide warnSide = PlayerSide.Left;
        MidBoss spawnedBoss = null;
        PlayerSide spawnSide = PlayerSide.Left;
        System.Action<PlayerSide, MidBoss> onWarn = (s, b) => { log.Add("warn"); warnSide = s; };
        System.Action<PlayerSide, MidBoss> onSpawn = (s, b) => { log.Add("spawn"); spawnSide = s; spawnedBoss = b; };
        EventManager.Instance.OnBossWarning += onWarn;
        EventManager.Instance.OnBossSpawned += onSpawn;
        try
        {
            Assert.AreEqual(1f, sp.GetRate(SpawnModifierType.Count), 1e-5f);
            Assert.IsTrue(sched.DebugSpawnMidBoss(0));
            Assert.AreEqual(new[] { "warn" }, log.ToArray(), "경고가 먼저, 즉시");
            Assert.AreEqual(PlayerSide.Right, warnSide);
            Assert.AreEqual(0, sched.AliveCount);

            yield return MonsterTestFactory.WaitUntil(() => null != spawnedBoss, 3f);
            Assert.IsNotNull(spawnedBoss, "출현");
            Assert.AreEqual(new[] { "warn", "spawn" }, log.ToArray());
            Assert.AreEqual(PlayerSide.Right, spawnSide);
            Assert.AreEqual(1, sched.AliveCount);
            Assert.AreEqual(0.5f, sp.GetRate(SpawnModifierType.Count), 1e-4f, "생존 중 개체수 x0.5");

            // 출현 위치: 플레이어 앞 스폰 링 (11 ~ 13)
            float dist = Vector2.Distance(spawnedBoss.transform.position, player.position);
            Assert.GreaterOrEqual(dist, 11f - 0.01f);
            Assert.LessOrEqual(dist, 13f + 0.01f);
            Assert.AreSame(entries[0], spawnedBoss.Entry, "Setup 으로 엔트리 전달");

            // 사망: 보석 드롭 경고(DropManager 없음) 후 복원
            LogAssert.Expect(LogType.Warning, new Regex(@"DropManager"));
            bool defeatedEvent = false;
            System.Action<PlayerSide, MidBoss> onDefeat = (s, b) => defeatedEvent = true;
            EventManager.Instance.OnBossDefeated += onDefeat;
            spawnedBoss.Health.Kill();
            EventManager.Instance.OnBossDefeated -= onDefeat;

            Assert.IsTrue(defeatedEvent, "OnBossDefeated 발행");
            Assert.AreEqual(0, sched.AliveCount);
            Assert.AreEqual(1f, sp.GetRate(SpawnModifierType.Count), 1e-4f, "사망 후 복원");

            // 이후 비활성화(풀 반환)돼도 이중 복원 없음
            spawnedBoss.gameObject.SetActive(false);
            Assert.AreEqual(1f, sp.GetRate(SpawnModifierType.Count), 1e-4f, "이중 복원 금지");
        }
        finally
        {
            EventManager.Instance.OnBossWarning -= onWarn;
            EventManager.Instance.OnBossSpawned -= onSpawn;
        }
    }

    [UnityTest]
    public IEnumerator Scheduler_BossDespawnedWithoutDeath_RestoresCountOnce()
    {
        Rig tpl = NewMonster(50f, true);
        MidBossEntry[] entries = { Entry(tpl.spawnable, 0f, 0.7f) };
        Spawner sp = NewSchedulerSpawner(NewPlayerMarker(PlayerSide.Left), entries, out MidBossScheduler sched);

        Assert.IsTrue(sched.DebugSpawnMidBoss(0));
        yield return MonsterTestFactory.WaitUntil(() => 1 == sched.AliveCount, 3f);
        Assert.AreEqual(1, sched.AliveCount);
        Assert.AreEqual(0.7f, sp.GetRate(SpawnModifierType.Count), 1e-4f);

        MidBoss boss = FindBoss();
        boss.gameObject.SetActive(false);
        Assert.AreEqual(0, sched.AliveCount);
        Assert.AreEqual(1f, sp.GetRate(SpawnModifierType.Count), 1e-4f);
    }

    [UnityTest]
    public IEnumerator Scheduler_TwoBosses_CountMultipliersStackAndRestoreIndependently()
    {
        Rig tpl = NewMonster(50f, true);
        MidBossEntry[] entries = { Entry(tpl.spawnable, 0f, 0.5f), Entry(tpl.spawnable, 0f, 0.8f) };
        Spawner sp = NewSchedulerSpawner(NewPlayerMarker(PlayerSide.Left), entries, out MidBossScheduler sched);

        sched.DebugSpawnMidBoss(0);
        sched.DebugSpawnMidBoss(1);
        yield return MonsterTestFactory.WaitUntil(() => 2 == sched.AliveCount, 3f);
        Assert.AreEqual(2, sched.AliveCount);
        // 보스 개체수 배율은 곱: 0.5 x 0.8 = 0.4
        Assert.AreEqual(0.4f, sp.GetRate(SpawnModifierType.Count), 1e-4f);

        LogAssert.Expect(LogType.Warning, new Regex(@"DropManager"));
        MidBoss[] bosses = Object.FindObjectsByType<MidBoss>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bosses[0].Health.Kill();
        Assert.AreEqual(1, sched.AliveCount);
        float afterOne = sp.GetRate(SpawnModifierType.Count);
        Assert.IsTrue(Mathf.Approximately(0.5f, afterOne) || Mathf.Approximately(0.8f, afterOne), $"한 마리 몫만 복원 (실제 {afterOne})");
    }

    [UnityTest]
    public IEnumerator DebugSpawnMidBoss_InvalidIndex_ReturnsFalse()
    {
        Rig tpl = NewMonster(50f, true);
        MidBossEntry[] entries = { Entry(tpl.spawnable, 0f, 0.5f) };
        NewSchedulerSpawner(null, entries, out MidBossScheduler sched);
        Assert.IsFalse(sched.DebugSpawnMidBoss(-1));
        Assert.IsFalse(sched.DebugSpawnMidBoss(1));
        Assert.AreEqual(0, sched.AliveCount);
        yield return null;
    }

    [Test]
    public void SetMidBosses_AddsSchedulerOnce()
    {
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.SetMidBosses(new MidBossEntry[0]);
        sp.SetMidBosses(new MidBossEntry[1]);
        Assert.AreEqual(1, sp.GetComponents<MidBossScheduler>().Length);
        Assert.AreEqual(1, sp.GetComponent<MidBossScheduler>().Entries.Count);
    }

    private static MidBoss FindBoss()
    {
        foreach (MidBoss b in Object.FindObjectsByType<MidBoss>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            return b;
        }
        return null;
    }
    #endregion

    #region Spawner 보스 개체수 배율 / MidBoss 재사용 / 데미지 배율 합성
    [Test]
    public void BossCountMultiplier_MultipliesAndRestores()
    {
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Assert.AreEqual(1f, sp.BossCountMultiplier, 1e-5f);
        sp.AddBossCountMultiplier(0.5f);
        sp.AddBossCountMultiplier(0.8f);
        Assert.AreEqual(0.4f, sp.BossCountMultiplier, 1e-5f);
        Assert.AreEqual(0.4f, sp.GetRate(SpawnModifierType.Count), 1e-4f);

        Assert.IsTrue(sp.RemoveBossCountMultiplier(0.5f));
        Assert.AreEqual(0.8f, sp.BossCountMultiplier, 1e-5f);
        Assert.IsFalse(sp.RemoveBossCountMultiplier(0.5f), "이미 뺀 값");
        Assert.IsTrue(sp.RemoveBossCountMultiplier(0.8f));
        Assert.AreEqual(1f, sp.GetRate(SpawnModifierType.Count), 1e-5f);
    }

    [Test]
    public void BossCountMultiplier_CombinesWithRewardCountMultiply()
    {
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.AddRate(SpawnModifierType.Count, StatModifierMode.Multiply, 0.5f);
        Assert.AreEqual(1.5f, sp.GetRate(SpawnModifierType.Count), 1e-4f);
        sp.AddBossCountMultiplier(0.7f);
        Assert.AreEqual(1.05f, sp.GetRate(SpawnModifierType.Count), 1e-4f);
        sp.RemoveBossCountMultiplier(0.7f);
        Assert.AreEqual(1.5f, sp.GetRate(SpawnModifierType.Count), 1e-4f);
    }

    [Test]
    public void MidBoss_Reuse_RestoresCollidersAndDamageMultiplier()
    {
        Rig tpl = NewMonster(50f, true);
        Spawner sp = MonsterTestFactory.CreateSpawner(NewPlayerMarker(PlayerSide.Left));
        Spawnable first = sp.SpawnAt(tpl.spawnable, Vector2.zero);
        MidBoss boss = first.GetComponent<MidBoss>();
        Collider2D col = first.GetComponentInChildren<Collider2D>(true);

        boss.SetAirborne(true);
        Assert.IsFalse(col.enabled);
        Assert.AreEqual(0f, boss.Health.IncomingDamageMultiplier);

        first.ReturnToPool();
        Spawnable second = sp.SpawnAt(tpl.spawnable, Vector2.zero);
        Assert.AreSame(first, second);
        Assert.IsFalse(boss.IsAirborne);
        Assert.IsTrue(col.enabled, "재사용 시 콜라이더 복원");
        Assert.AreEqual(1f, boss.Health.IncomingDamageMultiplier);
        Assert.AreEqual(1f, boss.DamageTakenMultiplier);
    }

    [Test]
    public void MidBoss_DamageTakenFactors_ComposeBySource()
    {
        Rig tpl = NewMonster(50f, true);
        Spawner sp = MonsterTestFactory.CreateSpawner(NewPlayerMarker(PlayerSide.Left));
        MidBoss boss = sp.SpawnAt(tpl.spawnable, Vector2.zero).GetComponent<MidBoss>();
        object a = new object();
        object b = new object();

        boss.SetDamageTakenFactor(a, 1.5f);
        Assert.AreEqual(1.5f, boss.Health.IncomingDamageMultiplier, 1e-5f);

        boss.SetAirborne(true);
        Assert.AreEqual(0f, boss.Health.IncomingDamageMultiplier, "공중 0 이 과열 1.5 를 덮는다");
        boss.SetAirborne(false);
        Assert.AreEqual(1.5f, boss.Health.IncomingDamageMultiplier, 1e-5f, "공중 해제해도 과열 유지");

        boss.SetDamageTakenFactor(b, 2f);
        Assert.AreEqual(3f, boss.Health.IncomingDamageMultiplier, 1e-5f);
        boss.ClearDamageTakenFactor(a);
        Assert.AreEqual(2f, boss.Health.IncomingDamageMultiplier, 1e-5f);
        boss.ClearDamageTakenFactor(b);
        boss.ClearDamageTakenFactor(b);
        Assert.AreEqual(1f, boss.Health.IncomingDamageMultiplier, 1e-5f);
        boss.SetDamageTakenFactor(null, 5f);
        Assert.AreEqual(1f, boss.Health.IncomingDamageMultiplier, 1e-5f, "null 출처 무시");
    }
    #endregion

    #region PlayerStats 임시 둔화 만료
    [UnityTest]
    public IEnumerator ApplyTimedSlow_ExpiresAndRestores()
    {
        GameObject go = new GameObject("SlowStats");
        extra.Add(go);
        PlayerStats s = go.AddComponent<PlayerStats>();

        s.ApplyTimedSlow(0.4f, 0.2f);
        Assert.AreEqual(-0.4f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);

        yield return MonsterTestFactory.Wait(0.5f);
        Assert.AreEqual(0f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f, "만료 후 원복");
        Assert.AreEqual(0f, s.TimedSlowRate);
    }

    [UnityTest]
    public IEnumerator ApplyTimedSlow_OverlapMax_ThenRestoredAfterLongest()
    {
        GameObject go = new GameObject("SlowStats2");
        extra.Add(go);
        PlayerStats s = go.AddComponent<PlayerStats>();

        s.ApplyTimedSlow(0.3f, 0.6f);
        s.ApplyTimedSlow(0.5f, 0.2f);
        Assert.AreEqual(-0.5f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);

        yield return MonsterTestFactory.Wait(0.35f);
        Assert.AreEqual(-0.5f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f, "더 늦은 종료 시각 유지");

        yield return MonsterTestFactory.Wait(0.5f);
        Assert.AreEqual(0f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
    }

    [UnityTest]
    public IEnumerator ApplyTimedSlow_ReappliedAfterExpiry_StartsFresh()
    {
        GameObject go = new GameObject("SlowStats3");
        extra.Add(go);
        PlayerStats s = go.AddComponent<PlayerStats>();

        s.ApplyTimedSlow(0.5f, 0.1f);
        yield return MonsterTestFactory.Wait(0.3f);
        s.ApplyTimedSlow(0.2f, 5f);
        Assert.AreEqual(-0.2f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
        s.ClearTimedSlow();
        Assert.AreEqual(0f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
    }
    #endregion
}
