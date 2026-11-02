using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

/// <summary>
/// 차원 선택 EditMode 테스트
/// DimensionSelection, RewardManager.SetPoolFor / IsPoolOverriddenFor, 시작 무기 보상(WeaponRewardEffect + DimensionData.FindStartWeaponReward)
/// </summary>
public class DimensionSelectEditTests
{
    private readonly List<Object> created = new List<Object>();
    private RewardManager manager;

    [SetUp]
    public void SetUp()
    {
        DimensionSelection.Clear();
        manager = Track(new GameObject("Manager")).AddComponent<RewardManager>();
    }

    [TearDown]
    public void TearDown()
    {
        DimensionSelection.Clear();
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
    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private static void SetField(object _target, string _field, object _value)
    {
        FieldInfo f = _target.GetType().GetField(_field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(f, $"field {_field} not found on {_target.GetType().Name}");
        f.SetValue(_target, _value);
    }

    private DimensionData NewDim(string _name)
    {
        DimensionData d = Track(ScriptableObject.CreateInstance<DimensionData>());
        d.name = _name;
        return d;
    }

    private RewardData NewReward(string _name)
    {
        RewardData r = Track(ScriptableObject.CreateInstance<RewardData>());
        r.name = _name;
        return r;
    }

    private Weapon NewWeapon(string _name)
    {
        GameObject go = Track(new GameObject(_name));
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

    private RewardData NewWeaponReward(string _name, Weapon _weapon, WeaponUpgradeTable _table)
    {
        WeaponRewardEffect e = Track(ScriptableObject.CreateInstance<WeaponRewardEffect>());
        SetField(e, "weaponPrefab", _weapon);
        SetField(e, "upgradeTable", _table);
        RewardData r = NewReward(_name);
        r.SetName(_name);
        r.SetEffect(e);
        return r;
    }

    private WeaponUpgradeTable NewTable(int _steps)
    {
        WeaponUpgradeTable t = Track(ScriptableObject.CreateInstance<WeaponUpgradeTable>());
        WeaponUpgradeStep[] steps = new WeaponUpgradeStep[_steps];
        for (int i = 0; i < _steps; ++i)
        {
            steps[i] = new WeaponUpgradeStep($"S{i + 1}", $"D{i + 1}",
                new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f));
        }
        t.SetSteps(steps, 1f);
        return t;
    }

    private PlayerStats NewPlayer(PlayerSide _side)
    {
        PlayerStats p = Track(new GameObject("P_" + _side)).AddComponent<PlayerStats>();
        SetField(p, "side", _side);
        return p;
    }

    private List<RewardData> Pick(PlayerSide _side, RewardPoolType _type)
    {
        List<RewardData> result = new List<RewardData>();
        manager.GetRandomUnique(100, new[] { _type }, _side, result);
        return result;
    }
    #endregion

    #region DimensionSelection
    [Test]
    public void Selection_DefaultIsEmpty()
    {
        Assert.IsNull(DimensionSelection.Left);
        Assert.IsNull(DimensionSelection.Right);
        Assert.IsFalse(DimensionSelection.HasSelection);
        Assert.IsNull(DimensionSelection.Get(PlayerSide.Left));
        Assert.IsNull(DimensionSelection.Get(PlayerSide.Right));
    }

    [Test]
    public void Selection_SetThenGet_ReturnsPerSide()
    {
        DimensionData a = NewDim("A");
        DimensionData b = NewDim("B");

        DimensionSelection.Set(a, b);

        Assert.AreSame(a, DimensionSelection.Left);
        Assert.AreSame(b, DimensionSelection.Right);
        Assert.AreSame(a, DimensionSelection.Get(PlayerSide.Left));
        Assert.AreSame(b, DimensionSelection.Get(PlayerSide.Right));
        Assert.IsTrue(DimensionSelection.HasSelection);
    }

    [Test]
    public void Selection_PartialSet_HasSelectionFalse()
    {
        DimensionSelection.Set(NewDim("A"), null);
        Assert.IsFalse(DimensionSelection.HasSelection);
        DimensionSelection.Set(null, NewDim("B"));
        Assert.IsFalse(DimensionSelection.HasSelection);
    }

    [Test]
    public void Selection_Clear_ResetsBoth()
    {
        DimensionSelection.Set(NewDim("A"), NewDim("B"));
        DimensionSelection.Clear();
        Assert.IsNull(DimensionSelection.Left);
        Assert.IsNull(DimensionSelection.Right);
        Assert.IsFalse(DimensionSelection.HasSelection);
    }

    [Test]
    public void Selection_Set_Overwrites()
    {
        DimensionData a = NewDim("A");
        DimensionData b = NewDim("B");
        DimensionData c = NewDim("C");
        DimensionSelection.Set(a, b);
        DimensionSelection.Set(c, a);
        Assert.AreSame(c, DimensionSelection.Left);
        Assert.AreSame(a, DimensionSelection.Right);
    }
    #endregion

    #region SetPoolFor
    [Test]
    public void SetPoolFor_OverridesOnlyThatSide_OtherSideUsesShared()
    {
        RewardData shared = NewReward("Shared");
        RewardData dimW = NewReward("DimW");
        manager.Add(RewardPoolType.Weapon, shared, 1f);

        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { dimW }, 1f);

        Assert.IsTrue(manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.Weapon));
        Assert.IsFalse(manager.IsPoolOverriddenFor(PlayerSide.Right, RewardPoolType.Weapon));
        CollectionAssert.AreEquivalent(new[] { dimW }, Pick(PlayerSide.Left, RewardPoolType.Weapon));
        CollectionAssert.AreEquivalent(new[] { shared }, Pick(PlayerSide.Right, RewardPoolType.Weapon));
    }

    [Test]
    public void SetPoolFor_DoesNotAffectOtherTypes()
    {
        RewardData buff = NewReward("Buff");
        manager.Add(RewardPoolType.PassiveBuff, buff, 1f);

        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { NewReward("W") }, 1f);

        Assert.IsFalse(manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.PassiveBuff));
        CollectionAssert.Contains(Pick(PlayerSide.Left, RewardPoolType.PassiveBuff), buff);
    }

    [Test]
    public void SetPoolFor_CalledAgain_ReplacesPreviousItems()
    {
        RewardData a = NewReward("A");
        RewardData b = NewReward("B");
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { a }, 1f);
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { b }, 1f);

        CollectionAssert.AreEquivalent(new[] { b }, Pick(PlayerSide.Left, RewardPoolType.Weapon));
    }

    [Test]
    public void SetPoolFor_NullItems_KeepsSharedPool()
    {
        RewardData shared = NewReward("Shared");
        manager.Add(RewardPoolType.Weapon, shared, 1f);
        LogAssert.ignoreFailingMessages = true;
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, null, 1f);
        LogAssert.ignoreFailingMessages = false;

        Assert.IsFalse(manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.Weapon));
        CollectionAssert.AreEquivalent(new[] { shared }, Pick(PlayerSide.Left, RewardPoolType.Weapon));
    }

    [Test]
    public void SetPoolFor_SkipsNullEntries()
    {
        RewardData a = NewReward("A");
        manager.SetPoolFor(PlayerSide.Right, RewardPoolType.Weapon, new RewardData[] { null, a, null }, 1f);
        CollectionAssert.AreEquivalent(new[] { a }, Pick(PlayerSide.Right, RewardPoolType.Weapon));
    }

    [Test]
    public void SetPoolFor_ExcludeForStillHidesItem()
    {
        RewardData a = NewReward("A");
        RewardData b = NewReward("B");
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { a, b }, 1f);
        manager.ExcludeFor(PlayerSide.Left, a);

        CollectionAssert.AreEquivalent(new[] { b }, Pick(PlayerSide.Left, RewardPoolType.Weapon));
    }

    [Test]
    public void IsPoolOverriddenFor_DefaultFalse_ClearForResets()
    {
        Assert.IsFalse(manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.Weapon));
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { NewReward("A") }, 1f);
        manager.ClearFor(PlayerSide.Left);
        Assert.IsFalse(manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.Weapon));
    }
    #endregion

    #region Start weapon reward
    [Test]
    public void FindStartWeaponReward_ReturnsMatchingReward()
    {
        Weapon w1 = NewWeapon("W1");
        Weapon w2 = NewWeapon("W2");
        RewardData r1 = NewWeaponReward("R1", w1, null);
        RewardData r2 = NewWeaponReward("R2", w2, null);
        DimensionData d = NewDim("D");
        SetField(d, "startWeapon", w2);
        SetField(d, "weaponRewards", new[] { r1, r2 });

        Assert.AreSame(r2, d.FindStartWeaponReward());
    }

    [Test]
    public void FindStartWeaponReward_NoMatchOrNull_ReturnsNull()
    {
        Weapon w1 = NewWeapon("W1");
        DimensionData d = NewDim("D");
        Assert.IsNull(d.FindStartWeaponReward(), "무기·보상 모두 없음");

        SetField(d, "startWeapon", w1);
        Assert.IsNull(d.FindStartWeaponReward(), "weaponRewards 없음");

        SetField(d, "weaponRewards", new RewardData[] { null, NewWeaponReward("Other", NewWeapon("W2"), null), NewReward("NoEffect") });
        Assert.IsNull(d.FindStartWeaponReward(), "다른 무기 / null / effect 없음만 있음");
    }

    [Test]
    public void ApplyStartReward_HidesRewardForThatSide_AndAddsLevel1Upgrade()
    {
        Weapon w = NewWeapon("Start");
        Weapon other = NewWeapon("Other");
        RewardData start = NewWeaponReward("Start", w, NewTable(5));
        RewardData otherReward = NewWeaponReward("Other", other, NewTable(5));
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { start, otherReward }, 1f);
        PlayerStats left = NewPlayer(PlayerSide.Left);

        start.Apply(new RewardContext { Player = left, Side = PlayerSide.Left, Manager = manager });

        Assert.IsTrue(left.HasWeapon(w));
        Assert.IsTrue(manager.IsExcludedFor(PlayerSide.Left, start));
        CollectionAssert.AreEquivalent(new[] { otherReward }, Pick(PlayerSide.Left, RewardPoolType.Weapon), "시작 무기 보상은 후보에서 제외");
        Assert.AreEqual(1, Pick(PlayerSide.Left, RewardPoolType.WeaponUpgrade).Count, "1단계 업그레이드 추가");
        Assert.AreEqual(0, Pick(PlayerSide.Right, RewardPoolType.WeaponUpgrade).Count, "다른 진영엔 없음");
    }

    [Test]
    public void ApplyStartReward_NoUpgradeTable_OnlyHides()
    {
        Weapon w = NewWeapon("Start");
        RewardData start = NewWeaponReward("Start", w, null);
        manager.SetPoolFor(PlayerSide.Left, RewardPoolType.Weapon, new[] { start }, 1f);
        PlayerStats left = NewPlayer(PlayerSide.Left);

        start.Apply(new RewardContext { Player = left, Side = PlayerSide.Left, Manager = manager });

        Assert.IsTrue(left.HasWeapon(w));
        Assert.AreEqual(0, Pick(PlayerSide.Left, RewardPoolType.Weapon).Count);
        Assert.AreEqual(0, Pick(PlayerSide.Left, RewardPoolType.WeaponUpgrade).Count);
    }
    #endregion

    #region Spawner by side
    private Spawner NewSpawner(string _name)
    {
        return Track(new GameObject(_name)).AddComponent<Spawner>();
    }

    private RewardData NewSpawnerReward(float _value)
    {
        SpawnerRewardEffect e = Track(ScriptableObject.CreateInstance<SpawnerRewardEffect>());
        RewardData r = NewReward("MonsterCount");
        r.SetEffect(e);
        r.SetValue(_value);
        return r;
    }

    [Test]
    public void ApplySpawnerReward_GoesToSpawnerRegisteredForReceivingSide()
    {
        Spawner leftTarget = NewSpawner("LeftTargetSpawner");
        Spawner rightTarget = NewSpawner("RightTargetSpawner");
        manager.RegisterSpawner(PlayerSide.Left, leftTarget);
        manager.RegisterSpawner(PlayerSide.Right, rightTarget);

        manager.ApplyReward(NewSpawnerReward(0.5f), PlayerSide.Left);

        Assert.AreSame(leftTarget, manager.GetSpawnerFor(PlayerSide.Left));
        Assert.AreSame(rightTarget, manager.GetSpawnerFor(PlayerSide.Right));
        Assert.AreEqual(1.5f, leftTarget.GetRate(SpawnModifierType.Count), 0.0001f, "받은 진영을 노리는 스포너에 적용");
        Assert.AreEqual(1f, rightTarget.GetRate(SpawnModifierType.Count), 0.0001f, "다른 진영 스포너는 그대로");
    }

    [Test]
    public void GetSpawnerFor_NotRegistered_FallsBackToSerializedSpawner()
    {
        Spawner fallback = NewSpawner("Fallback");
        Spawner leftTarget = NewSpawner("LeftTargetSpawner");
        SetField(manager, "spawner", fallback);
        manager.RegisterSpawner(PlayerSide.Left, leftTarget);

        Assert.AreSame(fallback, manager.GetSpawnerFor(PlayerSide.Right), "등록 없는 진영은 직렬화 필드");

        manager.RegisterSpawner(PlayerSide.Left, null);
        Assert.AreSame(fallback, manager.GetSpawnerFor(PlayerSide.Left), "null 등록은 해제");
    }
    #endregion
}
