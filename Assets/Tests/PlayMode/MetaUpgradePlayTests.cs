using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 영구 강화 PlayMode: Applier 시작 시 양쪽 적용 + HP 가득, GameOverManager 종료 시 차원력 1회 지급
/// </summary>
public class MetaUpgradePlayTests
{
    private static readonly string[] IDS = { "pt_hp", "pt_dmg" };
    private readonly Dictionary<string, int> backup = new Dictionary<string, int>();
    private readonly List<Object> created = new List<Object>();
    private GameOverManager gm;

    private static IEnumerable<string> AllKeys()
    {
        yield return MetaProgress.KEY_CURRENCY;
        foreach (string id in IDS)
        {
            yield return MetaProgress.KEY_LEVEL_PREFIX + id;
        }
    }

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        backup.Clear();
        foreach (string k in AllKeys())
        {
            if (PlayerPrefs.HasKey(k))
            {
                backup[k] = PlayerPrefs.GetInt(k);
            }
            PlayerPrefs.DeleteKey(k);
        }
        MetaProgress.Reload();
    }

    [TearDown]
    public void TearDown()
    {
        if (null != gm)
        {
            GamePause.Resume(gm);
        }
        Time.timeScale = 1f;
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
        gm = null;
        foreach (string k in AllKeys())
        {
            PlayerPrefs.DeleteKey(k);
            if (backup.TryGetValue(k, out int v))
            {
                PlayerPrefs.SetInt(k, v);
            }
        }
        PlayerPrefs.Save();
        MetaProgress.Reload();
    }

    private static void SetField(object _o, string _n, object _v)
    {
        FieldInfo f = _o.GetType().GetField(_n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, _n);
        f.SetValue(_o, _v);
    }

    private GameObject Make(string _name, bool _active = true)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(_active);
        created.Add(go);
        return go;
    }

    private PlayerStats MakePlayer(PlayerSide _side, out HealthSystem _health, float _startHp = 100f)
    {
        GameObject go = Make("Player" + _side, false);
        PlayerStats ps = go.AddComponent<PlayerStats>();
        SetField(ps, "side", _side);
        go.AddComponent<CircleCollider2D>();
        _health = go.AddComponent<HealthSystem>();
        _health.maxHP = 100f;
        _health.currHP = _startHp;
        go.SetActive(true);
        return ps;
    }

    private PermanentUpgradeData Data(string _id, ValueType _stat, StatModifierMode _mode, float _per, int _max)
    {
        PermanentUpgradeData d = ScriptableObject.CreateInstance<PermanentUpgradeData>();
        d.Configure(_id, _stat, _mode, _per, _max, 20, 1.5f);
        created.Add(d);
        return d;
    }

    [UnityTest]
    public IEnumerator Applier_AppliesToBothPlayers_FillsHp()
    {
        PermanentUpgradeData hp = Data("pt_hp", ValueType.HP, StatModifierMode.Multiply, 0.05f, 10);
        PermanentUpgradeData dmg = Data("pt_dmg", ValueType.Damage, StatModifierMode.Multiply, 0.02f, 10);
        PlayerPrefs.SetInt(MetaProgress.KEY_LEVEL_PREFIX + "pt_hp", 10);
        PlayerPrefs.SetInt(MetaProgress.KEY_LEVEL_PREFIX + "pt_dmg", 5);
        MetaProgress.Reload();

        PlayerStats l = MakePlayer(PlayerSide.Left, out HealthSystem lh, 40f);
        PlayerStats r = MakePlayer(PlayerSide.Right, out HealthSystem rh, 100f);

        PermanentUpgradeRegistry reg = ScriptableObject.CreateInstance<PermanentUpgradeRegistry>();
        created.Add(reg);
        reg.SetUpgrades(new[] { hp, dmg });
        GameObject ago = Make("Applier", false);
        PermanentUpgradeApplier ap = ago.AddComponent<PermanentUpgradeApplier>();
        SetField(ap, "registry", reg);
        ago.SetActive(true);
        yield return null;

        Assert.IsTrue(ap.IsApplied);
        foreach (PlayerStats p in new[] { l, r })
        {
            Assert.AreEqual(0.5f, p.GetPermanentMultiply(ValueType.HP), 1e-5f);
            Assert.AreEqual(0.1f, p.GetPermanentMultiply(ValueType.Damage), 1e-5f);
        }
        Assert.AreEqual(150f, lh.MaxHP, 1e-3f);
        Assert.AreEqual(150f, rh.MaxHP, 1e-3f);
        Assert.AreEqual(lh.MaxHP, lh.CurrHP, 1e-3f, "HP 가득");
        Assert.AreEqual(rh.MaxHP, rh.CurrHP, 1e-3f);

        // 재적용해도 누적되지 않는다
        ap.Apply();
        Assert.AreEqual(0.5f, l.GetPermanentMultiply(ValueType.HP), 1e-5f);
        Assert.AreEqual(150f, lh.MaxHP, 1e-3f);
    }

    [UnityTest]
    public IEnumerator Applier_ZeroLevels_NoEffect_DeadPlayerNotRevived()
    {
        PermanentUpgradeData hp = Data("pt_hp", ValueType.HP, StatModifierMode.Multiply, 0.05f, 10);
        PlayerStats l = MakePlayer(PlayerSide.Left, out HealthSystem lh, 100f);
        MakePlayer(PlayerSide.Right, out HealthSystem rh);
        PermanentUpgradeRegistry reg = ScriptableObject.CreateInstance<PermanentUpgradeRegistry>();
        created.Add(reg);
        reg.SetUpgrades(new[] { hp });
        PermanentUpgradeApplier ap = Make("Applier", false).AddComponent<PermanentUpgradeApplier>();
        SetField(ap, "registry", reg);
        lh.Kill();
        ap.gameObject.SetActive(true);
        yield return null;
        Assert.AreEqual(0f, l.GetPermanentMultiply(ValueType.HP));
        Assert.AreEqual(100f, lh.MaxHP, 1e-3f);
        Assert.AreEqual(0f, lh.CurrHP, 1e-3f, "죽은 플레이어는 채우지 않음");
    }

    [UnityTest]
    public IEnumerator Applier_NoRegistry_WarnsAndDoesNotThrow()
    {
        PlayerStats l = MakePlayer(PlayerSide.Left, out _);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("registry"));
        Make("Applier").AddComponent<PermanentUpgradeApplier>();
        yield return null;
        Assert.AreEqual(0f, l.GetPermanentMultiply(ValueType.HP));
    }

    private GameOverManager MakeGameOver(StatisticsManager _stats)
    {
        GameObject panel = Make("Panel");
        GameObject uigo = Make("GameOverUI", false);
        GameOverUI ui = uigo.AddComponent<GameOverUI>();
        SetField(ui, "panel", panel);
        uigo.SetActive(true);
        GameObject mgo = Make("GameOverMgr", false);
        GameOverManager m = mgo.AddComponent<GameOverManager>();
        SetField(m, "statisticsManager", _stats);
        SetField(m, "gameOverUI", ui);
        mgo.SetActive(true);
        return m;
    }

    [UnityTest]
    public IEnumerator Victory_GrantsOnce()
    {
        StatisticsManager stats = Make("Stats").AddComponent<StatisticsManager>();
        gm = MakeGameOver(stats);
        yield return null;
        Assert.IsFalse(gm.IsMetaRewardGranted);

        int before = MetaProgress.Currency;
        gm.TriggerVictory();
        Assert.IsTrue(gm.IsMetaRewardGranted);
        int granted = gm.LastMetaReward.Total;
        Assert.IsTrue(gm.LastMetaReward.Victory);
        Assert.AreEqual(before + granted, MetaProgress.Currency);

        gm.TriggerVictory();
        gm.TriggerGameOver(PlayerSide.Left);
        Assert.AreEqual(before + granted, MetaProgress.Currency, "두 번 지급 금지");
    }

    [UnityTest]
    public IEnumerator Defeat_GrantsOnce_AfterSequence_WithDefeatRate()
    {
        StatisticsManager stats = Make("Stats").AddComponent<StatisticsManager>();
        gm = MakeGameOver(stats);
        SetField(gm, "freezeSeconds", 0f);
        SetField(gm, "dimSeconds", 0f);
        SetField(gm, "holdSeconds", 0f);
        yield return null;

        int before = MetaProgress.Currency;
        gm.TriggerGameOver(PlayerSide.Right);
        gm.TriggerGameOver(PlayerSide.Left);
        float t = 0f;
        while (false == gm.IsMetaRewardGranted && t < 3f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Assert.IsTrue(gm.IsMetaRewardGranted);
        Assert.IsFalse(gm.LastMetaReward.Victory);
        Assert.AreEqual(0.5f, gm.LastMetaReward.Rate, 1e-6f);
        Assert.AreEqual(before + gm.LastMetaReward.Total, MetaProgress.Currency);
        yield return null;
        yield return null;
        Assert.AreEqual(before + gm.LastMetaReward.Total, MetaProgress.Currency);
    }
}
