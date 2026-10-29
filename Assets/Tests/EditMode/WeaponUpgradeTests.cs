using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 무기 업그레이드 시스템 EditMode 테스트
/// RewardManager 플레이어별 풀 / WeaponRewardEffect / WeaponUpgradeRewardEffect / Weapon.GetParam, HasFlag
/// </summary>
public class WeaponUpgradeTests
{
    private readonly List<Object> created = new List<Object>();

    private RewardManager manager;
    private Weapon weaponPrefab;
    private WeaponUpgradeTable table;
    private RewardData weaponReward;
    private PlayerStats left;
    private PlayerStats right;

    [SetUp]
    public void SetUp()
    {
        manager = MakeGo("Manager").AddComponent<RewardManager>();

        weaponPrefab = MakeWeaponPrefab("TestWeapon");
        table = MakeTable(10);

        WeaponRewardEffect effect = Track(ScriptableObject.CreateInstance<WeaponRewardEffect>());
        SetField(effect, "weaponPrefab", weaponPrefab);
        SetField(effect, "upgradeTable", table);

        weaponReward = Track(ScriptableObject.CreateInstance<RewardData>());
        weaponReward.name = "TestWeaponReward";
        weaponReward.SetName("TestWeapon");
        weaponReward.SetEffect(effect);

        manager.Add(RewardPoolType.Weapon, weaponReward, 1f);

        left = MakePlayer("Left", PlayerSide.Left);
        right = MakePlayer("Right", PlayerSide.Right);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; --i)
        {
            if (null != created[i])
            {
                Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
    }

    #region Helpers
    private T Track<T>(T _obj) where T : Object
    {
        created.Add(_obj);
        return _obj;
    }

    private GameObject MakeGo(string _name)
    {
        return Track(new GameObject(_name));
    }

    private Weapon MakeWeaponPrefab(string _name)
    {
        // 비활성 오브젝트로 만들어 복제본도 비활성 (Awake / Update 가 돌지 않게)
        GameObject go = MakeGo(_name);
        go.SetActive(false);
        Weapon w = go.AddComponent<Weapon>();
        w.damage = 10f;
        w.attackSpeed = 1f;
        w.range = 5f;
        w.fireCount = 1;
        w.hitCount = 1;
        w.areaRadius = 2f;
        return w;
    }

    private PlayerStats MakePlayer(string _name, PlayerSide _side)
    {
        GameObject go = MakeGo(_name);
        PlayerStats p = go.AddComponent<PlayerStats>();
        SetField(p, "side", _side);
        return p;
    }

    private WeaponUpgradeTable MakeTable(int _steps)
    {
        WeaponUpgradeTable t = Track(ScriptableObject.CreateInstance<WeaponUpgradeTable>());
        WeaponUpgradeStep[] steps = new WeaponUpgradeStep[_steps];
        for (int i = 0; i < _steps; ++i)
        {
            steps[i] = new WeaponUpgradeStep(
                $"Step{i + 1}", $"Desc{i + 1}",
                new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f));
        }
        t.SetSteps(steps, 1f);
        return t;
    }

    private static void SetField(object _target, string _field, object _value)
    {
        FieldInfo f = _target.GetType().GetField(_field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(f, $"field {_field} not found on {_target.GetType().Name}");
        f.SetValue(_target, _value);
    }

    private RewardContext Ctx(PlayerStats _player)
    {
        return new RewardContext { Player = _player, Side = _player.Side, Manager = manager };
    }

    private static readonly RewardPoolType[] WeaponTypes = { RewardPoolType.Weapon };
    private static readonly RewardPoolType[] UpgradeTypes = { RewardPoolType.WeaponUpgrade };

    private List<RewardData> Pick(PlayerSide _side, RewardPoolType[] _types)
    {
        List<RewardData> result = new List<RewardData>();
        manager.GetRandomUnique(100, _types, _side, result);
        return result;
    }

    private RewardData SingleUpgrade(PlayerSide _side)
    {
        List<RewardData> list = Pick(_side, UpgradeTypes);
        Assert.AreEqual(1, list.Count, "WeaponUpgrade 풀에는 항상 하나만 있어야 한다");
        return list[0];
    }

    private RewardData MakeItem(string _name)
    {
        RewardData d = Track(ScriptableObject.CreateInstance<RewardData>());
        d.name = _name;
        return d;
    }
    #endregion

    #region 1. RewardManager per-side pool
    [Test]
    public void ExcludeFor_HidesOnlyForThatSide()
    {
        RewardData a = MakeItem("A");
        RewardData b = MakeItem("B");
        manager.Add(RewardPoolType.PassiveBuff, a, 1f);
        manager.Add(RewardPoolType.PassiveBuff, b, 1f);
        RewardPoolType[] types = { RewardPoolType.PassiveBuff };

        manager.ExcludeFor(PlayerSide.Left, a);

        List<RewardData> l = Pick(PlayerSide.Left, types);
        List<RewardData> r = Pick(PlayerSide.Right, types);
        CollectionAssert.DoesNotContain(l, a);
        CollectionAssert.Contains(l, b);
        CollectionAssert.Contains(r, a);
        CollectionAssert.Contains(r, b);
        Assert.IsTrue(manager.IsExcludedFor(PlayerSide.Left, a));
        Assert.IsFalse(manager.IsExcludedFor(PlayerSide.Right, a));
    }

    [Test]
    public void ExcludeFor_DoesNotRemoveFromSharedPool()
    {
        RewardData a = MakeItem("A");
        manager.Add(RewardPoolType.PassiveBuff, a, 1f);
        manager.ExcludeFor(PlayerSide.Left, a);
        Assert.AreEqual(1, manager.Count(RewardPoolType.PassiveBuff));
    }

    [Test]
    public void ExcludeFor_Null_DoesNothing()
    {
        Assert.DoesNotThrow(() => manager.ExcludeFor(PlayerSide.Left, null));
        Assert.IsFalse(manager.IsExcludedFor(PlayerSide.Left, null));
    }

    [Test]
    public void AddFor_AppearsOnlyForThatSide()
    {
        RewardData u = MakeItem("U");
        manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u, 1f);

        CollectionAssert.Contains(Pick(PlayerSide.Left, UpgradeTypes), u);
        CollectionAssert.DoesNotContain(Pick(PlayerSide.Right, UpgradeTypes), u);
        // 공유 풀에는 들어가지 않는다
        Assert.AreEqual(0, manager.Count(RewardPoolType.WeaponUpgrade));
    }

    [Test]
    public void AddFor_OnlyAppearsInItsOwnPoolType()
    {
        RewardData u = MakeItem("U");
        manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u, 1f);
        CollectionAssert.DoesNotContain(Pick(PlayerSide.Left, WeaponTypes), u);
    }

    [Test]
    public void AddFor_ZeroWeight_NotPicked()
    {
        RewardData u = MakeItem("U");
        manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u, 0f);
        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count);
    }

    [Test]
    public void AddFor_Null_DoesNothing()
    {
        Assert.DoesNotThrow(() => manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, null, 1f));
        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count);
    }

    [Test]
    public void RemoveFor_RemovesAddedItem()
    {
        RewardData u = MakeItem("U");
        manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u, 1f);

        Assert.IsTrue(manager.RemoveFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u));
        CollectionAssert.DoesNotContain(Pick(PlayerSide.Left, UpgradeTypes), u);
    }

    [Test]
    public void RemoveFor_ReturnsFalse_WhenNotPresentOrOtherSide()
    {
        RewardData u = MakeItem("U");
        Assert.IsFalse(manager.RemoveFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u), "아무것도 안 넣은 상태");

        manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, u, 1f);
        Assert.IsFalse(manager.RemoveFor(PlayerSide.Right, RewardPoolType.WeaponUpgrade, u), "반대 side");
        Assert.IsFalse(manager.RemoveFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, null), "null");
        CollectionAssert.Contains(Pick(PlayerSide.Left, UpgradeTypes), u);
    }

    [Test]
    public void GetRandomUnique_WithSide_CountLimit()
    {
        for (int i = 0; i < 5; ++i)
        {
            manager.AddFor(PlayerSide.Left, RewardPoolType.WeaponUpgrade, MakeItem("U" + i), 1f);
        }
        List<RewardData> result = new List<RewardData>();
        Assert.AreEqual(3, manager.GetRandomUnique(3, UpgradeTypes, PlayerSide.Left, result));
        Assert.AreEqual(0, manager.GetRandomUnique(0, UpgradeTypes, PlayerSide.Left, result));
        Assert.AreEqual(0, result.Count);
    }
    #endregion

    #region 2. WeaponRewardEffect
    [Test]
    public void WeaponReward_HidesWeaponForAcquiringSideOnly()
    {
        CollectionAssert.Contains(Pick(PlayerSide.Left, WeaponTypes), weaponReward);

        weaponReward.Apply(Ctx(left));

        CollectionAssert.DoesNotContain(Pick(PlayerSide.Left, WeaponTypes), weaponReward);
        CollectionAssert.Contains(Pick(PlayerSide.Right, WeaponTypes), weaponReward);
        Assert.IsTrue(left.HasWeapon(weaponPrefab));
        Assert.IsFalse(right.HasWeapon(weaponPrefab));
    }

    [Test]
    public void WeaponReward_AddsLevel1ToThatSideUpgradePool()
    {
        weaponReward.Apply(Ctx(left));

        RewardData up = SingleUpgrade(PlayerSide.Left);
        Assert.AreEqual("TestWeapon Lv.1", up.GetName());
        Assert.IsTrue(up.GetEffect() is WeaponUpgradeRewardEffect);
        Assert.AreEqual(1, ((WeaponUpgradeRewardEffect)up.GetEffect()).Level);
        Assert.AreEqual(0, Pick(PlayerSide.Right, UpgradeTypes).Count);
        // 단계 설명이 explain 에 들어간다
        Assert.AreEqual("Step1", up.GetFormattedExplain1());
        Assert.AreEqual("Desc1", up.GetFormattedExplain2());
    }

    [Test]
    public void WeaponReward_BothSidesGetOwnUpgrades()
    {
        weaponReward.Apply(Ctx(left));
        weaponReward.Apply(Ctx(right));

        RewardData l = SingleUpgrade(PlayerSide.Left);
        RewardData r = SingleUpgrade(PlayerSide.Right);
        Assert.AreNotSame(l, r);
        CollectionAssert.DoesNotContain(Pick(PlayerSide.Right, WeaponTypes), weaponReward);
    }

    [Test]
    public void WeaponReward_SecondApply_DoesNotDuplicateUpgrade()
    {
        weaponReward.Apply(Ctx(left));
        weaponReward.Apply(Ctx(left));
        Assert.AreEqual(1, Pick(PlayerSide.Left, UpgradeTypes).Count);
    }

    [Test]
    public void WeaponReward_WithoutTable_NoUpgradeButStillHidden()
    {
        SetField(weaponReward.GetEffect(), "upgradeTable", null);

        weaponReward.Apply(Ctx(left));

        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count);
        CollectionAssert.DoesNotContain(Pick(PlayerSide.Left, WeaponTypes), weaponReward);
    }

    [Test]
    public void WeaponReward_NoPlayer_DoesNothing()
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            RewardContext ctx = new RewardContext { Player = null, Side = PlayerSide.Left, Manager = manager };
            Assert.DoesNotThrow(() => weaponReward.Apply(ctx));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
        CollectionAssert.Contains(Pick(PlayerSide.Left, WeaponTypes), weaponReward);
        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count);
    }
    #endregion

    #region 3. WeaponUpgradeRewardEffect
    [Test]
    public void UpgradeReward_RemovesCurrentAndAddsNext()
    {
        weaponReward.Apply(Ctx(left));
        RewardData lv1 = SingleUpgrade(PlayerSide.Left);

        lv1.Apply(Ctx(left));

        RewardData lv2 = SingleUpgrade(PlayerSide.Left);
        Assert.AreNotSame(lv1, lv2);
        Assert.AreEqual("TestWeapon Lv.2", lv2.GetName());
        Assert.AreEqual(2, ((WeaponUpgradeRewardEffect)lv2.GetEffect()).Level);
        Assert.AreEqual(1, left.GetWeapon(weaponPrefab).UpgradeLevel);
    }

    [Test]
    public void UpgradeReward_AfterLevel10_NothingAdded()
    {
        weaponReward.Apply(Ctx(left));

        for (int level = 1; level <= 10; ++level)
        {
            RewardData up = SingleUpgrade(PlayerSide.Left);
            Assert.AreEqual(level, ((WeaponUpgradeRewardEffect)up.GetEffect()).Level);
            up.Apply(Ctx(left));
        }

        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count, "10단계 이후에는 아무것도 들어가지 않는다");
        Assert.AreEqual(10, left.GetWeapon(weaponPrefab).UpgradeLevel);
    }

    [Test]
    public void UpgradeReward_ShortTable_StopsAtLastStep()
    {
        table.SetSteps(new[] { table.GetStep(1), table.GetStep(2) }, 1f);
        weaponReward.Apply(Ctx(left));

        SingleUpgrade(PlayerSide.Left).Apply(Ctx(left));
        SingleUpgrade(PlayerSide.Left).Apply(Ctx(left));

        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count);
    }

    [Test]
    public void UpgradeReward_AppliesOnlyToOwnSidesWeapon()
    {
        weaponReward.Apply(Ctx(left));
        weaponReward.Apply(Ctx(right));

        SingleUpgrade(PlayerSide.Left).Apply(Ctx(left));

        Assert.AreEqual(12f, left.GetWeapon(weaponPrefab).damage, 0.0001f);
        Assert.AreEqual(10f, right.GetWeapon(weaponPrefab).damage, 0.0001f);
        // 오른쪽 풀은 그대로 Lv.1
        Assert.AreEqual(1, ((WeaponUpgradeRewardEffect)SingleUpgrade(PlayerSide.Right).GetEffect()).Level);
    }

    [Test]
    public void UpgradeReward_ValuesFollowBasePlusBaseTimesMulPlusAdd()
    {
        // Lv1: Damage Mul 0.2 / Lv2: Damage Mul 0.2 + Add 3 / Lv3: Damage Mul -0.1
        WeaponUpgradeStep[] steps =
        {
            new WeaponUpgradeStep("1", "", new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f)),
            new WeaponUpgradeStep("2", "",
                new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f),
                new WeaponModifier(WeaponParam.Damage, StatModifierMode.Add, 3f)),
            new WeaponUpgradeStep("3", "", new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, -0.1f)),
        };
        table.SetSteps(steps, 1f);
        weaponReward.Apply(Ctx(left));
        Weapon w = left.GetWeapon(weaponPrefab);

        SingleUpgrade(PlayerSide.Left).Apply(Ctx(left));
        Assert.AreEqual(12f, w.damage, 0.0001f);                // 10 + 10*0.2
        SingleUpgrade(PlayerSide.Left).Apply(Ctx(left));
        Assert.AreEqual(17f, w.damage, 0.0001f);                // 10 + 10*0.4 + 3
        SingleUpgrade(PlayerSide.Left).Apply(Ctx(left));
        Assert.AreEqual(16f, w.damage, 0.0001f);                // 10 + 10*0.3 + 3
        // 프리팹 원본은 변하지 않는다
        Assert.AreEqual(10f, weaponPrefab.damage, 0.0001f);
    }

    [Test]
    public void UpgradeReward_NoWeapon_DoesNotChangePool()
    {
        weaponReward.Apply(Ctx(left));
        RewardData lv1 = SingleUpgrade(PlayerSide.Left);

        // 무기가 없는 오른쪽에 적용 -> 경고만 남기고 아무 일도 없어야 한다
        LogAssert.ignoreFailingMessages = true;
        try
        {
            Assert.DoesNotThrow(() => lv1.Apply(Ctx(right)));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
        Assert.AreSame(lv1, SingleUpgrade(PlayerSide.Left));
        Assert.AreEqual(0, left.GetWeapon(weaponPrefab).UpgradeLevel);
    }

    [Test]
    public void UpgradeReward_WeaponMissing_CardRemovedFromPool_NoNextAdded()
    {
        weaponReward.Apply(Ctx(left));
        RewardData lv1 = SingleUpgrade(PlayerSide.Left);

        // 무기가 사라진 상태 (파괴됨)
        Object.DestroyImmediate(left.GetWeapon(weaponPrefab).gameObject);
        Assert.IsNull(left.GetWeapon(weaponPrefab));

        LogAssert.ignoreFailingMessages = true;
        try
        {
            lv1.Apply(Ctx(left));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }

        Assert.AreEqual(0, Pick(PlayerSide.Left, UpgradeTypes).Count, "쓸 수 없는 카드는 풀에서 빠지고 다음 단계도 들어가지 않는다");
    }

    [Test]
    public void CreateRewardData_OutOfRangeLevel_ReturnsNull()
    {
        Assert.IsNull(WeaponUpgradeRewardEffect.CreateRewardData(table, weaponPrefab, "W", null, 0));
        Assert.IsNull(WeaponUpgradeRewardEffect.CreateRewardData(table, weaponPrefab, "W", null, 11));
        Assert.IsNull(WeaponUpgradeRewardEffect.CreateRewardData(null, weaponPrefab, "W", null, 1));
        Assert.IsNotNull(WeaponUpgradeRewardEffect.CreateRewardData(table, weaponPrefab, "W", null, 10));
        Assert.IsNotNull(WeaponUpgradeRewardEffect.CreateRewardData(table, weaponPrefab, "W", null, 1));
    }

    [Test]
    public void CreateRewardData_BracesInTitleAreEscaped()
    {
        table.SetSteps(new[] { new WeaponUpgradeStep("A {0} B", "C {x}") }, 1f);
        RewardData d = WeaponUpgradeRewardEffect.CreateRewardData(table, weaponPrefab, "W", null, 1);
        Assert.AreEqual("A {0} B", d.GetFormattedExplain1());
        Assert.AreEqual("C {x}", d.GetFormattedExplain2());
    }
    #endregion

    #region 4. Weapon.GetParam / HasFlag
    private Weapon NewWeapon()
    {
        GameObject go = MakeGo("W");
        go.SetActive(false);
        Weapon w = go.AddComponent<Weapon>();
        w.damage = 10f;
        w.attackSpeed = 2f;
        w.range = 6f;
        w.fireCount = 1;
        w.hitCount = 1;
        w.knockbackForce = 4f;
        return w;
    }

    [Test]
    public void GetParam_NoModifier_ReturnsBase()
    {
        Weapon w = NewWeapon();
        Assert.AreEqual(10f, w.GetParam(WeaponParam.Damage), 0.0001f);
        Assert.AreEqual(1f, w.GetParam(WeaponParam.ProjectileScale, 1f), 0.0001f);
        Assert.AreEqual(7.5f, w.GetParam(WeaponParam.OrbitRadius, 7.5f), 0.0001f);
        Assert.AreEqual(0f, w.GetParam(WeaponParam.FlagResplit), 0.0001f);
    }

    [Test]
    public void GetParam_BasePlusBaseTimesMulPlusAdd()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.ProjectileScale, StatModifierMode.Multiply, 0.2f);
        w.AddModifier(WeaponParam.ProjectileScale, StatModifierMode.Multiply, 0.2f);
        w.AddModifier(WeaponParam.ProjectileScale, StatModifierMode.Add, 0.5f);

        // 2 + 2*0.4 + 0.5
        Assert.AreEqual(3.3f, w.GetParam(WeaponParam.ProjectileScale, 2f), 0.0001f);
        Assert.AreEqual(0.4f, w.GetParamMultiply(WeaponParam.ProjectileScale), 0.0001f);
        Assert.AreEqual(0.5f, w.GetParamAdd(WeaponParam.ProjectileScale), 0.0001f);
    }

    [Test]
    public void GetParam_NegativeMultiply_Reduces()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.ArmDelay, StatModifierMode.Multiply, -0.5f);
        Assert.AreEqual(1f, w.GetParam(WeaponParam.ArmDelay, 2f), 0.0001f);
    }

    [Test]
    public void GetParam_NonCommonWithoutBase_IsAddSum()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.FanAngle, StatModifierMode.Add, 5f);
        w.AddModifier(WeaponParam.FanAngle, StatModifierMode.Add, 5f);
        Assert.AreEqual(10f, w.GetParam(WeaponParam.FanAngle), 0.0001f);
    }

    [Test]
    public void GetParamInt_Rounds()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.FragmentCount, StatModifierMode.Add, 2f);
        Assert.AreEqual(5, w.GetParamInt(WeaponParam.FragmentCount, 3));

        w.AddModifier(WeaponParam.FragmentDistance, StatModifierMode.Multiply, 0.3f);
        Assert.AreEqual(4, w.GetParamInt(WeaponParam.FragmentDistance, 3)); // 3.9 -> 4
    }

    [Test]
    public void Common_Damage_UpdatesFieldFromBase()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f);
        Assert.AreEqual(12f, w.damage, 0.0001f);
        Assert.AreEqual(12f, w.GetParam(WeaponParam.Damage), 0.0001f);

        // 두 번째 적용도 현재값이 아닌 기본값 기준 (10 + 10*0.4)
        w.AddModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f);
        Assert.AreEqual(14f, w.damage, 0.0001f);
    }

    [Test]
    public void Common_IntegerFields_RoundAndClamp()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.FireCount, StatModifierMode.Add, 1f);
        w.AddModifier(WeaponParam.HitCount, StatModifierMode.Add, 10f);
        Assert.AreEqual(2, w.fireCount);
        Assert.AreEqual(11, w.hitCount);

        // 음수로 내려가도 0 미만이 되지 않는다
        w.AddModifier(WeaponParam.FireCount, StatModifierMode.Add, -10f);
        Assert.AreEqual(0, w.fireCount);
    }

    [Test]
    public void Common_Damage_ClampedAtZero()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.Damage, StatModifierMode.Multiply, -2f);
        Assert.AreEqual(0f, w.damage, 0.0001f);
    }

    [Test]
    public void Common_Knockback_AddAndMultiply()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.Knockback, StatModifierMode.Multiply, 0.25f);
        w.AddModifier(WeaponParam.Knockback, StatModifierMode.Add, 1f);
        Assert.AreEqual(6f, w.knockbackForce, 0.0001f); // 4 + 1 + 1
    }

    [Test]
    public void SpeedRate_ZeroBase_UsesOneAsBase()
    {
        Weapon w = NewWeapon();
        w.speedRate = 0f;
        w.AddModifier(WeaponParam.SpeedRate, StatModifierMode.Multiply, 0.2f);
        Assert.AreEqual(1.2f, w.speedRate, 0.0001f);
    }

    [Test]
    public void HasFlag_DefaultFalse_AddOneTrue()
    {
        Weapon w = NewWeapon();
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagReturnDamage));

        w.AddModifier(WeaponParam.FlagReturnDamage, StatModifierMode.Add, 1f);
        Assert.IsTrue(w.HasFlag(WeaponParam.FlagReturnDamage));
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagResplit), "다른 플래그는 영향 없음");
    }

    [Test]
    public void HasFlag_ZeroOrNegative_False()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.FlagSlowZone, StatModifierMode.Add, 0f);
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagSlowZone), "0 은 꺼짐 (경계값)");

        w.AddModifier(WeaponParam.FlagSlowZone, StatModifierMode.Add, -1f);
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagSlowZone));

        w.AddModifier(WeaponParam.FlagSlowZone, StatModifierMode.Add, 1f);
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagSlowZone), "합계 0 이면 꺼짐");

        w.AddModifier(WeaponParam.FlagSlowZone, StatModifierMode.Add, 0.01f);
        Assert.IsTrue(w.HasFlag(WeaponParam.FlagSlowZone), "0 초과면 켜짐");
    }

    [Test]
    public void ResetUpgrades_RestoresBase()
    {
        Weapon w = NewWeapon();
        w.AddModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.5f);
        w.AddModifier(WeaponParam.FlagChainAmplify, StatModifierMode.Add, 1f);
        w.ResetUpgrades();

        Assert.AreEqual(10f, w.damage, 0.0001f);
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagChainAmplify));
        Assert.AreEqual(0, w.UpgradeLevel);
    }

    [Test]
    public void ApplyUpgrade_IncrementsLevelAndVersion_NullStepIgnored()
    {
        Weapon w = NewWeapon();
        int v = w.UpgradeVersion;
        w.ApplyUpgrade(null);
        Assert.AreEqual(0, w.UpgradeLevel);
        Assert.AreEqual(v, w.UpgradeVersion);

        w.ApplyUpgrade(table.GetStep(1));
        Assert.AreEqual(1, w.UpgradeLevel);
        Assert.Greater(w.UpgradeVersion, v);
    }

    [Test]
    public void ApplyUpgrade_StepWithoutModifiers_StillCountsLevel()
    {
        Weapon w = NewWeapon();
        w.ApplyUpgrade(new WeaponUpgradeStep("t", "d"));
        Assert.AreEqual(1, w.UpgradeLevel);
        Assert.AreEqual(10f, w.damage, 0.0001f);
    }
    #endregion
}
