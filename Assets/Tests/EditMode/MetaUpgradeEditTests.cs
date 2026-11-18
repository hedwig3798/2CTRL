using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 영구 강화: MetaProgress(가격/구매/저장), MetaReward(계산), PlayerStats 영구 계층, DimensionStatMath 오버로드, Applier.BuildModifiers
/// - MetaProgress.KEY_PREFIX 로 시작하는 PlayerPrefs 키를 백업/삭제하고 종료 시 복원한다.
/// </summary>
public class MetaUpgradeEditTests
{
    private static readonly string[] IDS = { "t_a", "t_b", "t_c" };
    private readonly Dictionary<string, int> backup = new Dictionary<string, int>();
    private readonly List<Object> created = new List<Object>();

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
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    private PermanentUpgradeData MakeData(string _id, int _max = 3, int _base = 20, float _growth = 1.5f,
        ValueType _stat = ValueType.Damage, StatModifierMode _mode = StatModifierMode.Multiply, float _per = 0.02f)
    {
        PermanentUpgradeData d = ScriptableObject.CreateInstance<PermanentUpgradeData>();
        d.Configure(_id, _stat, _mode, _per, _max, _base, _growth);
        created.Add(d);
        return d;
    }

    private PlayerStats MakeStats()
    {
        GameObject go = new GameObject("MetaStats");
        created.Add(go);
        return go.AddComponent<PlayerStats>();
    }

    #region Cost
    [Test]
    public void GetCost_Formula_RoundsHalfUp()
    {
        PermanentUpgradeData d = MakeData("t_a", 10);
        Assert.AreEqual(20, d.GetCost(0));
        Assert.AreEqual(30, d.GetCost(1));
        Assert.AreEqual(45, d.GetCost(2));
        Assert.AreEqual(68, d.GetCost(3)); // 67.5 -> 68
        Assert.AreEqual(101, d.GetCost(4)); // 101.25
    }

    [Test]
    public void GetCost_NegativeLevel_TreatedAsZero_AndHugeLevel_Clamped()
    {
        PermanentUpgradeData d = MakeData("t_a");
        Assert.AreEqual(20, d.GetCost(-5));
        Assert.AreEqual(int.MaxValue, d.GetCost(10000));
    }

    [Test]
    public void GetTotalValue_ClampsLevel()
    {
        PermanentUpgradeData d = MakeData("t_a", 3, 20, 1.5f, ValueType.Damage, StatModifierMode.Multiply, 0.1f);
        Assert.AreEqual(0f, d.GetTotalValue(-1), 1e-6f);
        Assert.AreEqual(0.2f, d.GetTotalValue(2), 1e-6f);
        Assert.AreEqual(0.3f, d.GetTotalValue(99), 1e-6f);
    }
    #endregion

    #region Purchase
    [Test]
    public void Purchase_DeductsCurrency_AndRaisesLevel_PriceGrows()
    {
        PermanentUpgradeData d = MakeData("t_a", 3);
        MetaProgress.AddCurrency(100);
        Assert.AreEqual(0, MetaProgress.GetLevel(d));
        Assert.AreEqual(20, MetaProgress.GetCost(d));

        Assert.IsTrue(MetaProgress.TryPurchase(d));
        Assert.AreEqual(80, MetaProgress.Currency);
        Assert.AreEqual(1, MetaProgress.GetLevel(d));
        Assert.AreEqual(30, MetaProgress.GetCost(d));
    }

    [Test]
    public void Purchase_InsufficientCurrency_Fails_NoChange()
    {
        PermanentUpgradeData d = MakeData("t_a", 3);
        MetaProgress.AddCurrency(19);
        Assert.IsFalse(MetaProgress.CanPurchase(d));
        Assert.IsFalse(MetaProgress.TryPurchase(d));
        Assert.AreEqual(19, MetaProgress.Currency);
        Assert.AreEqual(0, MetaProgress.GetLevel(d));
    }

    [Test]
    public void Purchase_ExactCurrency_Succeeds_LeavesZero()
    {
        PermanentUpgradeData d = MakeData("t_a", 3);
        MetaProgress.AddCurrency(20);
        Assert.IsTrue(MetaProgress.TryPurchase(d));
        Assert.AreEqual(0, MetaProgress.Currency);
    }

    [Test]
    public void Purchase_AtMaxLevel_Fails_CostMinusOne()
    {
        PermanentUpgradeData d = MakeData("t_a", 2);
        MetaProgress.AddCurrency(100000);
        Assert.IsTrue(MetaProgress.TryPurchase(d));
        Assert.IsTrue(MetaProgress.TryPurchase(d));
        int after = MetaProgress.Currency;
        Assert.IsTrue(MetaProgress.IsMaxLevel(d));
        Assert.AreEqual(-1, MetaProgress.GetCost(d));
        Assert.IsFalse(MetaProgress.CanPurchase(d));
        Assert.IsFalse(MetaProgress.TryPurchase(d));
        Assert.AreEqual(after, MetaProgress.Currency);
        Assert.AreEqual(2, MetaProgress.GetLevel(d));
    }

    [Test]
    public void Purchase_NullOrEmptyId_Fails()
    {
        MetaProgress.AddCurrency(1000);
        Assert.IsFalse(MetaProgress.TryPurchase(null));
        PermanentUpgradeData d = MakeData("");
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("id"));
        Assert.IsFalse(MetaProgress.TryPurchase(d));
        Assert.AreEqual(0, MetaProgress.GetLevel(d));
        Assert.AreEqual(1000, MetaProgress.Currency);
    }

    [Test]
    public void AddCurrency_IgnoresNonPositive_AndClampsToIntMax()
    {
        MetaProgress.AddCurrency(0);
        MetaProgress.AddCurrency(-5);
        Assert.AreEqual(0, MetaProgress.Currency);
        MetaProgress.AddCurrency(int.MaxValue);
        MetaProgress.AddCurrency(10);
        Assert.AreEqual(int.MaxValue, MetaProgress.Currency);
    }

    [Test]
    public void OnChanged_FiresOnPurchaseAndAdd_NotOnFailure()
    {
        PermanentUpgradeData d = MakeData("t_a");
        int n = 0;
        System.Action h = () => ++n;
        MetaProgress.OnChanged += h;
        try
        {
            MetaProgress.AddCurrency(50);
            MetaProgress.TryPurchase(d);
            MetaProgress.TryPurchase(MakeData("t_b", 3, 9999));
        }
        finally
        {
            MetaProgress.OnChanged -= h;
        }
        Assert.AreEqual(2, n);
    }

    [Test]
    public void Persistence_SurvivesReload()
    {
        PermanentUpgradeData a = MakeData("t_a", 5);
        PermanentUpgradeData b = MakeData("t_b", 5);
        MetaProgress.AddCurrency(500);
        MetaProgress.TryPurchase(a);
        MetaProgress.TryPurchase(a);
        MetaProgress.TryPurchase(b);
        int currency = MetaProgress.Currency;

        MetaProgress.Reload();
        Assert.AreEqual(currency, MetaProgress.Currency);
        Assert.AreEqual(2, MetaProgress.GetLevel(a));
        Assert.AreEqual(1, MetaProgress.GetLevel(b));
        Assert.AreEqual(500 - 20 - 30 - 20, MetaProgress.Currency);
    }

    [Test]
    public void Load_CorruptValues_ClampedAndCapped()
    {
        PermanentUpgradeData a = MakeData("t_a", 3);
        PlayerPrefs.SetInt(MetaProgress.KEY_CURRENCY, -50);
        PlayerPrefs.SetInt(MetaProgress.KEY_LEVEL_PREFIX + "t_a", 99);
        MetaProgress.Reload();
#if !DEMO_INVINCIBLE
        Assert.AreEqual(0, MetaProgress.Currency);
#endif
        Assert.AreEqual(3, MetaProgress.GetLevel(a));
        PlayerPrefs.SetInt(MetaProgress.KEY_LEVEL_PREFIX + "t_a", -4);
        MetaProgress.Reload();
        Assert.AreEqual(0, MetaProgress.GetLevel(a));
    }

    [Test]
    public void ResetAll_ClearsCurrencyAndLevels()
    {
        PermanentUpgradeData a = MakeData("t_a");
        MetaProgress.AddCurrency(100);
        MetaProgress.TryPurchase(a);
        MetaProgress.ResetAll(new[] { a });
#if !DEMO_INVINCIBLE
        Assert.AreEqual(0, MetaProgress.Currency);
#endif
        Assert.AreEqual(0, MetaProgress.GetLevel(a));
        Assert.IsFalse(PlayerPrefs.HasKey(MetaProgress.KEY_LEVEL_PREFIX + "t_a"));
    }
    #endregion

    #region Reward
    [Test]
    public void Reward_Victory_DefaultCoefs()
    {
        // (10*15 + 1000*0.005 + 20*2) * 1.0 = 195
        MetaRewardResult r = MetaReward.Calculate(10, 1000, 20, true, null);
        Assert.AreEqual(195, r.Total);
        Assert.AreEqual(1f, r.Rate, 1e-6f);
        Assert.AreEqual(150f, r.WaveValue, 1e-4f);
        Assert.AreEqual(5f, r.KillValue, 1e-4f);
        Assert.AreEqual(40f, r.LevelValue, 1e-4f);
        Assert.IsTrue(r.Victory);
    }

    [Test]
    public void Reward_Defeat_HalfRate_FloorsDown()
    {
        // 195 * 0.5 = 97.5 -> 97
        MetaRewardResult r = MetaReward.Calculate(10, 1000, 20, false, null);
        Assert.AreEqual(97, r.Total);
        Assert.AreEqual(0.5f, r.Rate, 1e-6f);
    }

    [Test]
    public void Reward_FloatCoefPrecision_DoesNotFloorOneLess()
    {
        // 200 * 0.005 = 정확히 1
        MetaRewardResult r = MetaReward.Calculate(0, 200, 0, true, MetaRewardConfig.DEFAULT_WAVE_COEF,
            MetaRewardConfig.DEFAULT_KILL_COEF, MetaRewardConfig.DEFAULT_LEVEL_COEF, 1f, 0.5f);
        Assert.AreEqual(1, r.Total);
    }

    [Test]
    public void Reward_ConfigCoefs_Used()
    {
        MetaRewardConfig c = ScriptableObject.CreateInstance<MetaRewardConfig>();
        created.Add(c);
        c.Configure(1f, 0.1f, 3f, 2f, 0.25f);
        // (2 + 1 + 12) * 2 = 30
        Assert.AreEqual(30, MetaReward.Calculate(2, 10, 4, true, c).Total);
        // 15 * 0.25 = 3.75 -> 3
        Assert.AreEqual(3, MetaReward.Calculate(2, 10, 4, false, c).Total);
    }

    [Test]
    public void Reward_Zero_And_Negative_Inputs_GiveZero()
    {
        Assert.AreEqual(0, MetaReward.Calculate(0, 0, 0, true, null).Total);
        MetaRewardResult r = MetaReward.Calculate(-3, -100, -1, true, null);
        Assert.AreEqual(0, r.Total);
        Assert.AreEqual(0, r.ClearedWaves);
    }

    [Test]
    public void Reward_HugeValues_ClampToIntMax_NoThrow()
    {
        MetaRewardResult r = MetaReward.Calculate(int.MaxValue, int.MaxValue, int.MaxValue, true, 1e9f, 1e9f, 1e9f, 1e9f, 1f);
        Assert.AreEqual(int.MaxValue, r.Total);
        Assert.DoesNotThrow(() => MetaReward.Calculate(1, 1, 1, true, float.PositiveInfinity, float.NaN, 1f, float.PositiveInfinity, 1f));
    }

    [Test]
    public void ClearedWaves_FinishedUsesCount_OtherwiseCurrentIndex()
    {
        Assert.AreEqual(14, MetaReward.ClearedWaves(true, 14, 13));
        Assert.AreEqual(5, MetaReward.ClearedWaves(false, 14, 5));
        Assert.AreEqual(0, MetaReward.ClearedWaves(false, 14, -1));
        Assert.AreEqual(0, MetaReward.ClearedWaves(true, -2, 0));
    }

    [Test]
    public void Collect_NullInputs_AreZero()
    {
        Assert.AreEqual(0, MetaReward.CollectClearedWaves(null));
        Assert.AreEqual(0, MetaReward.CollectClearedWaves(new Spawner[0]));
        Assert.AreEqual(0, MetaReward.CollectKills(null));
    }
    #endregion

    #region PlayerStats permanent layer
    [Test]
    public void Stats_Formula_AddThenMultiply()
    {
        PlayerStats s = MakeStats();
        // (0 + 2 + 3) * max(0.5, 1 + 0.5) * (1 + 0.2) = 9
        s.AddModifier(ValueType.ProjectileCount, StatModifierMode.Add, 2f);
        s.AddModifier(ValueType.ProjectileCount, StatModifierMode.Multiply, 0.5f);
        s.SetPermanent(ValueType.ProjectileCount, 3f, 0.2f);
        Assert.AreEqual(9f, s.Get(ValueType.ProjectileCount), 1e-4f);
        Assert.AreEqual(5f, s.GetAdd(ValueType.ProjectileCount), 1e-5f);
        Assert.AreEqual(1.8f, s.GetMultiplier(ValueType.ProjectileCount), 1e-5f);
    }

    [Test]
    public void Stats_Floor_AppliesOnlyToLevelUpLayer_NotPermanent()
    {
        PlayerStats s = MakeStats();
        s.AddModifier(ValueType.Damage, StatModifierMode.Multiply, -0.9f);
        Assert.AreEqual(0.5f, s.GetMultiplier(ValueType.Damage), 1e-5f);
        s.SetPermanent(ValueType.Damage, 0f, 0.2f);
        Assert.AreEqual(0.5f * 1.2f, s.GetMultiplier(ValueType.Damage), 1e-5f);
        s.SetPermanent(ValueType.Damage, 0f, -0.5f);
        Assert.AreEqual(0.25f, s.GetMultiplier(ValueType.Damage), 1e-5f);
    }

    [Test]
    public void Stats_GetMultiply_IsLevelUpOnly()
    {
        PlayerStats s = MakeStats();
        s.AddModifier(ValueType.Damage, StatModifierMode.Multiply, 0.1f);
        s.SetPermanent(ValueType.Damage, 0f, 0.3f);
        Assert.AreEqual(0.1f, s.GetMultiply(ValueType.Damage), 1e-6f);
        Assert.AreEqual(0.3f, s.GetPermanentMultiply(ValueType.Damage), 1e-6f);
    }

    [Test]
    public void Stats_ResetModifiers_KeepsPermanent()
    {
        PlayerStats s = MakeStats();
        s.AddModifier(ValueType.Damage, StatModifierMode.Multiply, 0.1f);
        s.AddModifier(ValueType.ProjectileCount, StatModifierMode.Add, 2f);
        s.SetPermanent(ValueType.Damage, 0f, 0.3f);
        s.SetPermanent(ValueType.ProjectileCount, 1f, 0f);
        s.ResetModifiers();
        Assert.AreEqual(0f, s.GetMultiply(ValueType.Damage));
        Assert.AreEqual(0.3f, s.GetPermanentMultiply(ValueType.Damage), 1e-6f);
        Assert.AreEqual(1f, s.GetPermanentAdd(ValueType.ProjectileCount), 1e-6f);
        Assert.AreEqual(1f, s.GetAdd(ValueType.ProjectileCount), 1e-6f);
        Assert.AreEqual(1.3f, s.GetMultiplier(ValueType.Damage), 1e-5f);
    }

    [Test]
    public void Stats_SetPermanent_Overwrites_NotAccumulates()
    {
        PlayerStats s = MakeStats();
        int n = 0;
        s.OnStatChanged += _ => ++n;
        s.SetPermanent(ValueType.Range, 1f, 0.1f);
        s.SetPermanent(ValueType.Range, 2f, 0.2f);
        Assert.AreEqual(2f, s.GetPermanentAdd(ValueType.Range), 1e-6f);
        Assert.AreEqual(0.2f, s.GetPermanentMultiply(ValueType.Range), 1e-6f);
        Assert.AreEqual(2, n);
    }

    [Test]
    public void Stats_SetPermanentModifiers_ShortOrNullArrays_ZeroRest_NotifiesOnce()
    {
        PlayerStats s = MakeStats();
        s.SetPermanent(ValueType.PickupRange, 5f, 0.5f);
        int n = 0;
        s.OnStatChanged += _ => ++n;
        float[] add = new float[2];
        add[(int)ValueType.Damage] = 1f;
        s.SetPermanentModifiers(add, null);
        Assert.AreEqual(1, n);
        Assert.AreEqual(1f, s.GetPermanentAdd(ValueType.Damage), 1e-6f);
        Assert.AreEqual(0f, s.GetPermanentAdd(ValueType.PickupRange));
        Assert.AreEqual(0f, s.GetPermanentMultiply(ValueType.PickupRange));
    }

    [Test]
    public void Stats_Get_ClampedAtZero_WithNegativeAdd()
    {
        PlayerStats s = MakeStats();
        s.SetPermanent(ValueType.HPRegen, -3f, 0f);
        Assert.AreEqual(0f, s.Get(ValueType.HPRegen));
    }
    #endregion

    #region DimensionStatMath
    [Test]
    public void DimensionMath_PermanentOverload_AddOnce_MulOnce()
    {
        // (10 + 1 + 2 + 3) * max(0.5, 1+0.1+0.2) * (1+0.5)
        float v = DimensionStatMath.Apply(10f, 1f, 2f, 0.1f, 0.2f, 0.5f, 3f, 0.5f);
        Assert.AreEqual(16f * 1.3f * 1.5f, v, 1e-4f);
    }

    [Test]
    public void DimensionMath_ZeroPermanent_EqualsLegacy()
    {
        Assert.AreEqual(DimensionStatMath.Apply(10f, 1f, 2f, 0.1f, 0.2f, 0.5f),
            DimensionStatMath.Apply(10f, 1f, 2f, 0.1f, 0.2f, 0.5f, 0f, 0f), 1e-5f);
    }

    [Test]
    public void DimensionMath_Floor_NotAppliedToPermanent()
    {
        float v = DimensionStatMath.Apply(10f, 0f, 0f, -1f, -0.5f, 0.5f, 0f, 0.2f);
        Assert.AreEqual(10f * 0.5f * 1.2f, v, 1e-4f);
    }
    #endregion

    #region Applier.BuildModifiers
    [Test]
    public void BuildModifiers_SumsPerStatAndMode_UsingLevels()
    {
        PermanentUpgradeData dmg = MakeData("t_a", 10, 20, 1.5f, ValueType.Damage, StatModifierMode.Multiply, 0.02f);
        PermanentUpgradeData dmg2 = MakeData("t_b", 10, 20, 1.5f, ValueType.Damage, StatModifierMode.Multiply, 0.01f);
        PermanentUpgradeData proj = MakeData("t_c", 1, 300, 1.5f, ValueType.ProjectileCount, StatModifierMode.Add, 1f);
        var levels = new Dictionary<PermanentUpgradeData, int> { { dmg, 5 }, { dmg2, 99 }, { proj, 1 } };
        float[] add = new float[System.Enum.GetValues(typeof(ValueType)).Length];
        float[] mul = new float[add.Length];
        add[0] = 77f;

        PermanentUpgradeApplier.BuildModifiers(new[] { dmg, dmg2, proj, null }, d => levels[d], add, mul);
        Assert.AreEqual(0.1f + 0.1f, mul[(int)ValueType.Damage], 1e-5f);
        Assert.AreEqual(0f, add[(int)ValueType.Damage]);
        Assert.AreEqual(1f, add[(int)ValueType.ProjectileCount], 1e-6f);
    }

    [Test]
    public void BuildModifiers_NullInputs_ClearsAndDoesNotThrow()
    {
        float[] add = { 1f, 2f };
        float[] mul = { 3f };
        Assert.DoesNotThrow(() => PermanentUpgradeApplier.BuildModifiers(null, null, add, mul));
        Assert.AreEqual(0f, add[0] + add[1] + mul[0]);
        Assert.DoesNotThrow(() => PermanentUpgradeApplier.BuildModifiers(new PermanentUpgradeData[0], d => 0, null, null));
    }

    [Test]
    public void BuildModifiers_ShortArray_IgnoresOutOfRangeStat()
    {
        PermanentUpgradeData pick = MakeData("t_a", 5, 20, 1.5f, ValueType.PickupRange, StatModifierMode.Multiply, 0.05f);
        float[] add = new float[1];
        float[] mul = new float[1];
        Assert.DoesNotThrow(() => PermanentUpgradeApplier.BuildModifiers(new[] { pick }, d => 3, add, mul));
    }

    [Test]
    public void Registry_Find_ById()
    {
        PermanentUpgradeRegistry reg = ScriptableObject.CreateInstance<PermanentUpgradeRegistry>();
        created.Add(reg);
        PermanentUpgradeData a = MakeData("t_a");
        reg.SetUpgrades(new[] { a, null });
        Assert.AreSame(a, reg.Find("t_a"));
        Assert.IsNull(reg.Find("nope"));
        Assert.IsNull(reg.Find(null));
        reg.SetUpgrades(null);
        Assert.AreEqual(0, reg.Upgrades.Count);
    }
    #endregion
}
