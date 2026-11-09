using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HealthSystem.Execute / MonsterDeadState 즉사 처리 / MonsterBrain.CollectAlive 검증
/// </summary>
public class ExecuteAndCollectPlayTests
{
    private const int LayerA = MonsterTestFactory.MonsterLayer;
    private const int LayerB = 28;

    private readonly List<System.Action> cleanups = new List<System.Action>();

    [TearDown]
    public void TearDown()
    {
        foreach (System.Action a in cleanups)
        {
            a();
        }
        cleanups.Clear();
        WeaponTestKit.Cleanup();
    }

    private static MonsterTestFactory.Rig NewMonster(Vector2 _pos, float _hp = 100f, int _layer = LayerA, params MonsterKeyword[] _keywords)
    {
        MonsterTestFactory.Rig r = MonsterTestFactory.Create(MonsterTestFactory.NewData(0f, _keywords), _pos, _hp, true);
        r.root.layer = _layer;
        r.body.layer = _layer; // 수집은 HealthSystem 오브젝트(몸체) 레이어 기준
        MonsterTestFactory.Init(r, WeaponTestKit.Marker(new Vector2(500f, 500f)));
        return r;
    }

    private static ReviveKeyword NewRevive(int _count, float _delay)
    {
        ReviveKeyword rv = MonsterTestFactory.So<ReviveKeyword>();
        rv.reviveCount = _count;
        rv.reviveDelay = _delay;
        rv.hpRatio = 0.5f;
        return rv;
    }

    private Weapon NewOwnedWeapon()
    {
        GameObject pgo = new GameObject("TestPlayer");
        pgo.SetActive(false);
        WeaponTestKit.Track(pgo);
        PlayerStats ps = pgo.AddComponent<PlayerStats>();
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<FireModeTestProjectile>(), WeaponTestKit.Marker(Vector2.zero), 10f, 0f);
        w.SetOwnerStats(ps);
        return w;
    }

    private List<float> ListenWeaponDamage()
    {
        List<float> list = new List<float>();
        EventManager em = EventManager.Instance;
        System.Action<PlayerSide, Weapon, float> h = (s, w, d) => list.Add(d);
        em.OnWeaponDamage += h;
        cleanups.Add(() => { if (EventManager.HasInstance) { EventManager.Instance.OnWeaponDamage -= h; } });
        return list;
    }

    #region HealthSystem.Execute
    [Test]
    public void Execute_통계_OnWeaponDamage는_남은_HP_기준으로_한_번만_발행된다()
    {
        List<float> reported = ListenWeaponDamage();
        Weapon w = NewOwnedWeapon();
        MonsterTestFactory.Rig m = NewMonster(Vector2.zero, 100f);

        DamageMassage hit = new DamageMassage { damage = 30f, weapon = w };
        m.health.ProcessDamage(ref hit);
        reported.Clear();

        DamageMassage msg = new DamageMassage { damage = 5f, weapon = w };
        m.health.Execute(ref msg);

        Assert.IsTrue(m.health.isDead);
        Assert.AreEqual(1, reported.Count);
        Assert.AreEqual(70f, reported[0], 0.001f);
    }

    [Test]
    public void Execute_남은_HP가_1_미만이면_실제로_깎인_양만_보고한다()
    {
        List<float> reported = ListenWeaponDamage();
        Weapon w = NewOwnedWeapon();
        MonsterTestFactory.Rig m = NewMonster(Vector2.zero, 100f);
        m.health.CurrHP = 0.5f;

        DamageMassage msg = new DamageMassage { weapon = w };
        m.health.Execute(ref msg);

        Assert.IsTrue(m.health.isDead);
        Assert.AreEqual(1, reported.Count);
        Assert.AreEqual(0.5f, reported[0], 0.001f);
    }

    [UnityTest]
    public IEnumerator Execute_부활_키워드를_무시하고_다시_일어나지_않는다()
    {
        MonsterTestFactory.Rig m = NewMonster(Vector2.zero, 100f, LayerA, NewRevive(2, 0.1f));
        int deaths = 0;
        m.health.OnDeath += (go) => ++deaths;

        DamageMassage msg = new DamageMassage();
        m.health.Execute(ref msg);

        Assert.IsTrue(m.health.isDead);
        Assert.IsFalse(m.health.IsDowned);
        Assert.IsTrue(m.health.IsExecuted);
        Assert.AreEqual(1, deaths);
        Assert.AreEqual(MonsterStateId.Dead, m.brain.CurrentState);

        yield return MonsterTestFactory.Wait(0.4f);
        Assert.IsTrue(m.health.isDead);
        Assert.AreEqual(MonsterStateId.Dead, m.brain.CurrentState);
    }

    [UnityTest]
    public IEnumerator Execute_다운_상태에서_호출하면_최종_사망하고_재부활하지_않는다()
    {
        MonsterTestFactory.Rig m = NewMonster(Vector2.zero, 100f, LayerA, NewRevive(2, 0.2f));
        int deaths = 0;
        m.health.OnDeath += (go) => ++deaths;

        m.health.Kill();
        Assert.IsTrue(m.health.IsDowned);
        Assert.AreEqual(0, deaths);

        DamageMassage msg = new DamageMassage();
        m.health.Execute(ref msg);
        Assert.IsFalse(m.health.IsDowned);
        Assert.IsTrue(m.health.isDead);
        Assert.AreEqual(1, deaths);
        Assert.AreEqual(MonsterStateId.Dead, m.brain.CurrentState);

        m.health.Execute(ref msg);
        Assert.AreEqual(1, deaths, "이미 죽었으면 다시 호출해도 무시");

        yield return MonsterTestFactory.Wait(0.5f);
        Assert.IsTrue(m.health.isDead);
        Assert.IsFalse(m.health.IsDowned);
        Assert.AreEqual(MonsterStateId.Dead, m.brain.CurrentState);
    }

    [Test]
    public void Execute_무적이면_죽지_않고_IsExecuted도_남지_않는다()
    {
        MonsterTestFactory.Rig m = NewMonster(Vector2.zero, 100f);
        m.health.IsInvincible = true;

        DamageMassage msg = new DamageMassage();
        m.health.Execute(ref msg);

        Assert.IsFalse(m.health.isDead);
        Assert.IsFalse(m.health.IsExecuted);
        Assert.AreEqual(100f, m.health.CurrHP, 0.001f);
    }

    [Test]
    public void Execute_Initialize_뒤에는_IsExecuted가_초기화된다()
    {
        MonsterTestFactory.Rig m = NewMonster(Vector2.zero, 100f);
        DamageMassage msg = new DamageMassage();
        m.health.Execute(ref msg);
        Assert.IsTrue(m.health.IsExecuted);

        m.health.Initialize(null);
        Assert.IsFalse(m.health.IsExecuted);
        Assert.IsFalse(m.health.isDead);
        Assert.AreEqual(100f, m.health.CurrHP, 0.001f);
    }

    [Test]
    public void Execute_DeathInterceptor를_호출하지_않지만_Kill은_호출한다()
    {
        HealthSystem Make()
        {
            GameObject go = new GameObject("PlainHealth");
            go.SetActive(false);
            WeaponTestKit.Track(go);
            go.AddComponent<CircleCollider2D>();
            HealthSystem hs = go.AddComponent<HealthSystem>();
            hs.maxHP = 50f;
            hs.currHP = 50f;
            go.SetActive(true);
            return hs;
        }

        int intercepts = 0;
        HealthSystem a = Make();
        a.DeathInterceptor = () => { ++intercepts; return true; };
        DamageMassage msg = new DamageMassage();
        a.Execute(ref msg);
        Assert.AreEqual(0, intercepts);
        Assert.IsTrue(a.isDead);
        Assert.IsFalse(a.IsDowned);

        HealthSystem b = Make();
        b.DeathInterceptor = () => { ++intercepts; return true; };
        b.Kill();
        Assert.AreEqual(1, intercepts, "비교군: Kill 은 가로채기를 거친다");
        Assert.IsTrue(b.IsDowned);
    }
    #endregion

    #region MonsterDeadState
    [UnityTest]
    public IEnumerator DeadState_Execute로_죽으면_분열이_발동하지_않고_일반_사망은_발동한다()
    {
        MonsterData childData = MonsterTestFactory.NewData(0f);
        MonsterTestFactory.Rig child = MonsterTestFactory.Create(childData, new Vector2(500f, 500f), 50f, true, false, true);
        SplitKeyword split = MonsterTestFactory.So<SplitKeyword>();
        split.childPrefab = child.spawnable;
        split.count = 2;
        split.scatterRadius = 0.5f;

        Transform player = MonsterTestFactory.CreateMarker(new Vector2(40f, 0f));
        Spawner sp = MonsterTestFactory.CreateSpawner(player);
        MonsterData data = MonsterTestFactory.NewData(0f, split);

        MonsterTestFactory.Rig executed = MonsterTestFactory.Create(data, Vector2.zero);
        MonsterTestFactory.Init(executed, player, _spawner: sp);
        MonsterTestFactory.Rig normal = MonsterTestFactory.Create(data, new Vector2(5f, 5f));
        MonsterTestFactory.Init(normal, player, _spawner: sp);
        yield return null;

        DamageMassage msg = new DamageMassage();
        executed.health.Execute(ref msg);
        Assert.AreEqual(MonsterStateId.Dead, executed.brain.CurrentState);
        Assert.AreEqual(0, MonsterTestFactory.FindActive(childData).Count, "즉사는 분열하지 않음");

        normal.health.Kill();
        Assert.AreEqual(MonsterStateId.Dead, normal.brain.CurrentState);
        Assert.AreEqual(2, MonsterTestFactory.FindActive(childData).Count, "일반 사망은 분열");
    }
    #endregion

    #region CollectAlive
    private static List<HealthSystem> Collect(int _mask, bool _includeDowned = false)
    {
        List<HealthSystem> list = new List<HealthSystem> { null };
        HealthSystem.CollectAlive(_mask, list, _includeDowned);
        return list;
    }

    [Test]
    public void CollectAlive_레이어_마스크로_진영을_나누고_기존_목록은_비운다()
    {
        MonsterTestFactory.Rig a1 = NewMonster(Vector2.zero);
        MonsterTestFactory.Rig a2 = NewMonster(Vector2.right);
        MonsterTestFactory.Rig a3 = NewMonster(Vector2.up);
        MonsterTestFactory.Rig b1 = NewMonster(Vector2.left, 100f, LayerB);
        MonsterTestFactory.Rig b2 = NewMonster(Vector2.down, 100f, LayerB);

        List<HealthSystem> a = Collect(1 << LayerA);
        Assert.AreEqual(3, a.Count);
        CollectionAssert.AreEquivalent(new[] { a1.health, a2.health, a3.health }, a);

        List<HealthSystem> b = Collect(1 << LayerB);
        CollectionAssert.AreEquivalent(new[] { b1.health, b2.health }, b);

        Assert.AreEqual(5, Collect((1 << LayerA) | (1 << LayerB)).Count);
        Assert.AreEqual(0, Collect(0).Count);
    }

    [Test]
    public void CollectAlive_죽은_몬스터는_제외되고_다운은_includeDowned일_때만_포함된다()
    {
        MonsterTestFactory.Rig alive = NewMonster(Vector2.zero);
        MonsterTestFactory.Rig dead = NewMonster(Vector2.right);
        MonsterTestFactory.Rig downed = NewMonster(Vector2.up, 100f, LayerA, NewRevive(1, 5f));

        dead.health.Kill();
        downed.health.Kill();
        Assert.IsTrue(dead.health.isDead);
        Assert.IsFalse(dead.health.IsDowned);
        Assert.IsTrue(downed.health.IsDowned);

        CollectionAssert.AreEquivalent(new[] { alive.health }, Collect(1 << LayerA));
        CollectionAssert.AreEquivalent(new[] { alive.health }, Collect(1 << LayerA, false));
        CollectionAssert.AreEquivalent(new[] { alive.health, downed.health }, Collect(1 << LayerA, true), "죽은 몬스터는 includeDowned 여도 제외");
    }

    [Test]
    public void CollectAlive_MonsterBrain이_없는_적도_수집하고_플레이어는_제외한다()
    {
        GameObject bat = new GameObject("Bat");
        bat.SetActive(false);
        bat.layer = LayerA;
        WeaponTestKit.Track(bat);
        bat.AddComponent<CircleCollider2D>();
        HealthSystem batHealth = bat.AddComponent<HealthSystem>();
        batHealth.maxHP = 10f;
        batHealth.currHP = 10f;
        bat.AddComponent<DamagePipeline>();
        bat.SetActive(true);

        GameObject pgo = new GameObject("TestPlayerRoot");
        pgo.SetActive(false);
        pgo.layer = LayerA;
        WeaponTestKit.Track(pgo);
        pgo.AddComponent<PlayerStats>();
        GameObject child = new GameObject("PlayerBody");
        child.transform.SetParent(pgo.transform, false);
        child.layer = LayerA;
        child.AddComponent<CircleCollider2D>();
        HealthSystem playerHealth = child.AddComponent<HealthSystem>();
        playerHealth.maxHP = 10f;
        playerHealth.currHP = 10f;
        pgo.SetActive(true);

        List<HealthSystem> list = Collect(1 << LayerA);
        CollectionAssert.Contains(list, batHealth);
        CollectionAssert.DoesNotContain(list, playerHealth, "PlayerStats 부모를 가진 HealthSystem 은 제외");
    }

    [Test]
    public void CollectAlive_비활성화된_몬스터는_목록에서_빠진다()
    {
        MonsterTestFactory.Rig a = NewMonster(Vector2.zero);
        MonsterTestFactory.Rig b = NewMonster(Vector2.right);
        Assert.AreEqual(2, Collect(1 << LayerA).Count);

        b.root.SetActive(false);
        CollectionAssert.AreEquivalent(new[] { a.health }, Collect(1 << LayerA));

        b.root.SetActive(true);
        Assert.AreEqual(2, Collect(1 << LayerA).Count, "다시 켜면 돌아온다");
    }
    #endregion
}
