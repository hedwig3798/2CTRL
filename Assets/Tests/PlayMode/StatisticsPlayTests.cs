using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// StatisticsManager / GameOverManager / GameOverUI PlayMode 검증
/// (발사체 대신 HealthSystem.ProcessDamage 에 weapon 이 담긴 메시지를 넣어 발사체 피격 경로를 재현한다)
/// </summary>
public class StatisticsPlayTests
{
    private readonly List<Object> created = new List<Object>();
    private StatisticsManager stats;

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        stats = Make("Stats").AddComponent<StatisticsManager>();
    }

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
        Time.timeScale = 1f;
    }

    private GameObject Make(string _name, bool _active = true)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(_active);
        created.Add(go);
        return go;
    }

    private static void SetField(object _o, string _n, object _v)
    {
        FieldInfo f = _o.GetType().GetField(_n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, _n);
        f.SetValue(_o, _v);
    }

    private PlayerStats MakePlayer(PlayerSide _side, out Weapon _weapon, out HealthSystem _health)
    {
        GameObject go = Make("Player" + _side, false);
        PlayerStats ps = go.AddComponent<PlayerStats>();
        SetField(ps, "side", _side);
        go.AddComponent<CircleCollider2D>();
        _health = go.AddComponent<HealthSystem>();
        _health.maxHP = 100f;
        _health.currHP = 100f;

        GameObject wgo = new GameObject("Bow(Clone)");
        wgo.transform.SetParent(go.transform, false);
        _weapon = wgo.AddComponent<Weapon>();
        go.SetActive(true);
        return ps;
    }

    private HealthSystem MakeMonster(int _layer, float _hp = 50f)
    {
        GameObject go = Make("Monster");
        go.layer = _layer;
        go.AddComponent<CircleCollider2D>();
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = _hp;
        hs.currHP = _hp;
        return hs;
    }

    private static void Hit(HealthSystem _hs, float _dmg, Weapon _w = null)
    {
        DamageMassage m = new DamageMassage();
        m.damage = _dmg;
        m.weapon = _w;
        _hs.ProcessDamage(ref m);
    }

    [UnityTest]
    public IEnumerator Enqueue_StartsRoutine_RecordsLater_SleepsAndRestarts()
    {
        yield return null;
        Assert.IsFalse(stats.IsProcessing);

        stats.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 3f));
        Assert.IsTrue(stats.IsProcessing);
        Assert.AreEqual(1, stats.PendingCount);
        Assert.AreEqual(0f, stats.Get(PlayerSide.Left).DamageTaken, "same frame must not record");

        yield return null;
        yield return null;
        Assert.AreEqual(3f, stats.Get(PlayerSide.Left).DamageTaken);
        Assert.AreEqual(0, stats.PendingCount);
        Assert.IsFalse(stats.IsProcessing, "must sleep when queue empty");

        stats.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 4f));
        Assert.IsTrue(stats.IsProcessing, "must restart on Enqueue");
        yield return null;
        yield return null;
        Assert.AreEqual(7f, stats.Get(PlayerSide.Left).DamageTaken);
        Assert.IsFalse(stats.IsProcessing);
    }

    [UnityTest]
    public IEnumerator Process_LimitedPerFrame_ThenDrains()
    {
        SetField(stats, "maxRecordsPerFrame", 2);
        for (int i = 0; i < 5; ++i)
        {
            stats.Enqueue(new StatRecord(STAT_TYPE.Kill, PlayerSide.Right, 1f));
        }
        yield return null;
        Assert.LessOrEqual(stats.Get(PlayerSide.Right).Kills, 2);
        Assert.IsTrue(stats.IsProcessing);
        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(5, stats.Get(PlayerSide.Right).Kills);
        Assert.IsFalse(stats.IsProcessing);
    }

    [UnityTest]
    public IEnumerator Process_WorksWhenTimeScaleZero()
    {
        Time.timeScale = 0f;
        stats.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Left, 9f));
        yield return null;
        yield return null;
        Assert.AreEqual(9f, stats.Get(PlayerSide.Left).Exp);
        Assert.IsFalse(stats.IsProcessing);
    }

    [UnityTest]
    public IEnumerator WeaponDamage_AccumulatesPerSideAndWeapon()
    {
        MakePlayer(PlayerSide.Left, out Weapon lw, out HealthSystem lh);
        MakePlayer(PlayerSide.Right, out Weapon rw, out HealthSystem rh);
        yield return null;

        HealthSystem lMon = MakeMonster(LayerMask.NameToLayer("LMonster"), 1000f);
        HealthSystem rMon = MakeMonster(LayerMask.NameToLayer("RMonster"), 1000f);

        Hit(lMon, 10f, lw);
        Hit(lMon, 5f, lw);
        Hit(rMon, 7f, rw);
        Hit(rMon, 0f, rw);   // zero damage: no record
        Hit(rMon, 3f, null); // no weapon: ignored
        yield return null;
        yield return null;

        PlayerCombatStats l = stats.Get(PlayerSide.Left);
        PlayerCombatStats r = stats.Get(PlayerSide.Right);
        Assert.AreEqual(15f, l.WeaponDamage["Bow"]);
        Assert.AreEqual(7f, r.WeaponDamage["Bow"]);
        Assert.AreEqual(15f, l.TotalWeaponDamage);
        Assert.AreEqual(7f, r.TotalWeaponDamage);
    }

    [UnityTest]
    public IEnumerator Kill_CountsOnMonsterLayerSide_AndPlayerDamageTaken()
    {
        MakePlayer(PlayerSide.Left, out Weapon lw, out HealthSystem lh);
        yield return null;

        HealthSystem lMon = MakeMonster(LayerMask.NameToLayer("LMonster"));
        HealthSystem rMon = MakeMonster(LayerMask.NameToLayer("RMonster"));

        // MonsterBrain publishes OnEnemyDeath; publish the same event directly
        EventManager.Instance.OnEnemyDeath?.Invoke(lMon.gameObject);
        EventManager.Instance.OnEnemyDeath?.Invoke(rMon.gameObject);
        EventManager.Instance.OnEnemyDeath?.Invoke(rMon.gameObject);

        Hit(lh, 12f);
        Hit(lh, 8f);
        yield return null;
        yield return null;

        Assert.AreEqual(1, stats.Get(PlayerSide.Left).Kills);
        Assert.AreEqual(2, stats.Get(PlayerSide.Right).Kills);
        Assert.AreEqual(20f, stats.Get(PlayerSide.Left).DamageTaken);
        Assert.AreEqual(0f, stats.Get(PlayerSide.Right).DamageTaken);
    }

    private GameOverManager MakeGameOver(out GameObject _panel)
    {
        _panel = Make("Panel");
        GameObject uigo = Make("GameOverUI", false);
        GameOverUI ui = uigo.AddComponent<GameOverUI>();
        SetField(ui, "panel", _panel);
        uigo.SetActive(true);

        GameObject mgo = Make("GameOverMgr", false);
        GameOverManager gm = mgo.AddComponent<GameOverManager>();
        SetField(gm, "statisticsManager", stats);
        SetField(gm, "gameOverUI", ui);
        mgo.SetActive(true);
        return gm;
    }

    [UnityTest]
    public IEnumerator PlayerDeath_PausesFlushesShowsUI_SecondDeathIgnored()
    {
        MakePlayer(PlayerSide.Left, out Weapon lw, out HealthSystem lh);
        MakePlayer(PlayerSide.Right, out Weapon rw, out HealthSystem rh);
        GameOverManager gm = MakeGameOver(out GameObject panel);
        yield return null;
        Assert.IsFalse(panel.activeSelf);

        int calls = 0;
        gm.OnGameOver += () => ++calls;

        Hit(lh, 30f);
        Assert.AreEqual(1, stats.PendingCount);
        lh.Kill();

        Assert.IsTrue(gm.IsGameOver);
        Assert.AreEqual(PlayerSide.Left, gm.DeadSide);
        Assert.IsTrue(GamePause.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.AreEqual(0, stats.PendingCount);
        Assert.AreEqual(30f, stats.Get(PlayerSide.Left).DamageTaken);
        Assert.IsTrue(panel.activeSelf);
        Assert.AreEqual(1, calls);

        rh.Kill();
        Assert.AreEqual(PlayerSide.Left, gm.DeadSide);
        Assert.AreEqual(1, calls);
        gm.TriggerGameOver(PlayerSide.Right);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(PlayerSide.Left, gm.DeadSide);

        Object.DestroyImmediate(gm.gameObject);
        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisableEnable_SingleRoutine_ProcessesRemaining()
    {
        yield return null;
        stats.Enqueue(new StatRecord(STAT_TYPE.Kill, PlayerSide.Left, 1f));
        stats.Enqueue(new StatRecord(STAT_TYPE.Kill, PlayerSide.Left, 1f));
        Assert.IsTrue(stats.IsProcessing);
        stats.enabled = false;
        Assert.IsFalse(stats.IsProcessing);
        stats.enabled = true;
        stats.enabled = false;
        stats.enabled = true;
        Assert.IsTrue(stats.IsProcessing);
        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(2, stats.Get(PlayerSide.Left).Kills, "no double processing");
        Assert.AreEqual(0, stats.PendingCount);
        Assert.IsFalse(stats.IsProcessing);
    }

    [UnityTest]
    public IEnumerator Enqueue_AfterGameOver_Ignored()
    {
        MakePlayer(PlayerSide.Left, out Weapon lw, out HealthSystem lh);
        GameOverManager gm = MakeGameOver(out GameObject panel);
        yield return null;
        lh.Kill();
        Assert.IsTrue(stats.IsLocked);
        stats.Enqueue(new StatRecord(STAT_TYPE.Kill, PlayerSide.Right, 1f));
        yield return null;
        yield return null;
        Assert.AreEqual(0, stats.PendingCount);
        Assert.AreEqual(0, stats.Get(PlayerSide.Right).Kills);
        Object.DestroyImmediate(gm.gameObject);
    }
}
