using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rig = MonsterTestFactory.Rig;

/// <summary>
/// HealthSystem 의 DeathInterceptor / Kill / Revive 와 Spawner.SpawnAt 검증
/// </summary>
public class HealthAndSpawnerPlayTests
{
    [TearDown]
    public void TearDown()
    {
        MonsterTestFactory.Cleanup();
    }

    private static HealthSystem NewHealth(float _max, out Collider2D _col)
    {
        GameObject go = new GameObject("TestHealth");
        go.SetActive(false);
        _col = go.AddComponent<CircleCollider2D>();
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = _max;
        hs.currHP = _max;
        go.SetActive(true);
        MonsterTestFactory.Track(go);
        return hs;
    }

    #region HealthSystem
    [Test]
    public void Interceptor_True_DownsWithoutOnDeath_AndDisablesColliders()
    {
        HealthSystem hs = NewHealth(100f, out Collider2D col);
        int deaths = 0;
        int calls = 0;
        hs.OnDeath += (go) => ++deaths;
        hs.DeathInterceptor = () => { ++calls; return true; };

        DamageMassage msg = new DamageMassage();
        msg.damage = 500f;
        hs.ProcessDamage(ref msg);

        Assert.AreEqual(1, calls);
        Assert.AreEqual(0, deaths);
        Assert.IsTrue(hs.isDead);
        Assert.IsTrue(hs.IsDowned);
        Assert.AreEqual(0f, hs.CurrHP);
        Assert.IsFalse(col.enabled);
    }

    [Test]
    public void Interceptor_FalseButRevivedInside_DoesNotCallOnDeath()
    {
        HealthSystem hs = NewHealth(100f, out Collider2D col);
        int deaths = 0;
        hs.OnDeath += (go) => ++deaths;
        bool first = true;
        hs.DeathInterceptor = () =>
        {
            if (first)
            {
                first = false;
                hs.Revive(1f);
            }
            return false;
        };

        hs.Kill();

        Assert.AreEqual(0, deaths);
        Assert.IsFalse(hs.isDead);
        Assert.Greater(hs.CurrHP, 0f);
    }

    [Test]
    public void Interceptor_FalseOrNull_CallsOnDeathOnce_NotDowned()
    {
        HealthSystem a = NewHealth(10f, out Collider2D colA);
        HealthSystem b = NewHealth(10f, out Collider2D colB);
        int deathsA = 0;
        int deathsB = 0;
        a.OnDeath += (go) => ++deathsA;
        b.OnDeath += (go) => ++deathsB;
        a.DeathInterceptor = () => false;

        a.Kill();
        b.Kill();

        Assert.AreEqual(1, deathsA);
        Assert.AreEqual(1, deathsB);
        Assert.IsFalse(a.IsDowned);
        Assert.IsFalse(b.IsDowned);
        Assert.IsTrue(a.isDead);
        Assert.IsFalse(colA.enabled);
    }

    [Test]
    public void Kill_AlreadyDead_IsNoOp()
    {
        HealthSystem hs = NewHealth(10f, out _);
        int deaths = 0;
        hs.OnDeath += (go) => ++deaths;

        hs.Kill();
        hs.Kill();

        Assert.AreEqual(1, deaths);
    }

    [Test]
    public void Kill_IgnoresRemainingHp_AndGoesThroughInterceptor()
    {
        HealthSystem hs = NewHealth(1000f, out _);
        int calls = 0;
        hs.DeathInterceptor = () => { ++calls; return true; };

        hs.Kill();

        Assert.AreEqual(1, calls);
        Assert.AreEqual(0f, hs.CurrHP);
        Assert.IsTrue(hs.IsDowned);
    }

    [Test]
    public void Revive_RestoresHpByRatio_ReenablesCollider()
    {
        HealthSystem hs = NewHealth(100f, out Collider2D col);
        hs.DeathInterceptor = () => true;
        hs.Kill();

        hs.Revive(0.25f);

        Assert.AreEqual(25f, hs.CurrHP, 0.001f);
        Assert.IsFalse(hs.isDead);
        Assert.IsFalse(hs.IsDowned);
        Assert.IsTrue(col.enabled);
    }

    [Test]
    public void Revive_Boundaries_ZeroRatioGivesOneHp_OverOneClampsToMax()
    {
        HealthSystem zero = NewHealth(100f, out _);
        HealthSystem over = NewHealth(100f, out _);
        zero.DeathInterceptor = () => true;
        over.DeathInterceptor = () => true;
        zero.Kill();
        over.Kill();

        zero.Revive(0f);
        over.Revive(5f);

        Assert.AreEqual(1f, zero.CurrHP, 0.001f);
        Assert.AreEqual(100f, over.CurrHP, 0.001f);
    }

    [Test]
    public void Revive_WhenNotDowned_DoesNothing()
    {
        HealthSystem alive = NewHealth(100f, out _);
        alive.CurrHP = 30f;
        alive.Revive(1f);
        Assert.AreEqual(30f, alive.CurrHP);

        HealthSystem dead = NewHealth(100f, out _);
        dead.Kill();
        dead.Revive(1f);
        Assert.IsTrue(dead.isDead, "최종 사망은 되살리지 않는다");
        Assert.AreEqual(0f, dead.CurrHP);
    }

    [Test]
    public void DownedTarget_TakesNoFurtherDamage()
    {
        HealthSystem hs = NewHealth(100f, out _);
        int calls = 0;
        hs.DeathInterceptor = () => { ++calls; return true; };
        hs.Kill();

        DamageMassage msg = new DamageMassage();
        msg.damage = 50f;
        hs.ProcessDamage(ref msg);

        Assert.AreEqual(1, calls);
        Assert.AreEqual(0f, hs.CurrHP);
    }

    [Test]
    public void Initialize_AfterDowned_ClearsDownedState()
    {
        HealthSystem hs = NewHealth(100f, out Collider2D col);
        hs.DeathInterceptor = () => true;
        hs.Kill();

        hs.Initialize(new BlackBoard());

        Assert.IsFalse(hs.isDead);
        Assert.IsFalse(hs.IsDowned);
        Assert.IsTrue(col.enabled);
        Assert.AreEqual(100f, hs.CurrHP);
    }
    #endregion

    #region Spawner.SpawnAt
    private static Rig NewTemplate(out MonsterData _data)
    {
        _data = MonsterTestFactory.NewData(0f);
        return MonsterTestFactory.Create(_data, new Vector2(500f, 500f), 50f, true, false, true);
    }

    [Test]
    public void SpawnAt_NullPrefab_ReturnsNull()
    {
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Assert.IsNull(sp.SpawnAt(null, Vector2.zero));
    }

    [Test]
    public void SpawnAt_PlacesInitializesAndAppliesCurrentRates()
    {
        Rig tpl = NewTemplate(out MonsterData data);
        Transform player = MonsterTestFactory.CreateMarker(new Vector2(30f, 0f));
        Spawner sp = MonsterTestFactory.CreateSpawner(player);
        sp.speedRate = 2f;
        sp.HPRate = 1.5f;
        sp.damagerate = 3f;

        Spawnable s = sp.SpawnAt(tpl.spawnable, new Vector2(7f, -2f));

        Assert.IsNotNull(s);
        Assert.AreNotSame(tpl.spawnable, s);
        Assert.IsTrue(s.gameObject.activeSelf);
        Assert.AreEqual(7f, s.transform.position.x, 0.0001f);
        Assert.AreEqual(-2f, s.transform.position.y, 0.0001f);

        MonsterBrain brain = s.GetComponent<MonsterBrain>();
        Assert.AreEqual(MonsterStateId.Move, brain.CurrentState);
        Assert.AreSame(player, brain.Context.target);
        Assert.AreSame(sp, brain.Context.spawner, "BlackBoard.spawner 가 들어간다");
        Assert.AreEqual(2f, brain.Context.moveSpeedRate);
        Assert.AreEqual(3f, brain.Context.damageRate);
        Assert.AreEqual(75f, brain.Context.health.MaxHP, 0.001f, "HP 50 x 1.5");
        Assert.AreEqual(75f, brain.Context.health.CurrHP, 0.001f);
    }

    [Test]
    public void SpawnAt_AfterRewardChangesRates_AppliesNewRates()
    {
        Rig tpl = NewTemplate(out MonsterData data);
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.speedRate = 1f;
        sp.HPRate = 1f;
        sp.damagerate = 1f;
        sp.AddRate(SpawnModifierType.HP, StatModifierMode.Add, 1f);

        Spawnable s = sp.SpawnAt(tpl.spawnable, Vector2.zero);

        Assert.AreEqual(100f, s.GetComponent<MonsterBrain>().Context.health.MaxHP, 0.001f, "(1+1) x 50");
    }

    [Test]
    public void SpawnAt_ReleasedInstanceIsReusedAndReinitialized()
    {
        Rig tpl = NewTemplate(out MonsterData data);
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Spawnable first = sp.SpawnAt(tpl.spawnable, Vector2.zero);
        MonsterBrain brain = first.GetComponent<MonsterBrain>();
        brain.Context.health.Kill();
        Assert.AreEqual(MonsterStateId.Dead, brain.CurrentState);

        first.ReturnToPool();
        Assert.IsFalse(first.gameObject.activeSelf);
        Spawnable second = sp.SpawnAt(tpl.spawnable, new Vector2(1f, 1f));

        Assert.AreSame(first, second, "풀 재사용");
        Assert.IsTrue(second.gameObject.activeSelf);
        Assert.AreEqual(MonsterStateId.Move, brain.CurrentState);
        Assert.IsFalse(brain.Context.health.isDead);
        Assert.AreEqual(50f, brain.Context.health.CurrHP);
    }

    [Test]
    public void SpawnAt_PrefabWithoutBlackBoardHandler_LogsErrorAndReturnsNull()
    {
        GameObject go = new GameObject("NoHandler");
        go.SetActive(false);
        MonsterTestFactory.Track(go);
        Spawnable bare = go.AddComponent<Spawnable>();
        Spawner sp = MonsterTestFactory.CreateSpawner(null);

        LogAssert.Expect(LogType.Error, new Regex("BlackBoardHandler"));
        Spawnable s = sp.SpawnAt(bare, Vector2.zero);

        Assert.IsNull(s);
    }
    #endregion
}
