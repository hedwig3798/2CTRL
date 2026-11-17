using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 차원 무기: 양쪽 버프 합산 공식, 합성 후보 판정(최대 단계 / 한쪽만 보유 / 이미 보유), 무작위 선택, 차원 무기 슬롯
/// </summary>
public class DimensionFusionTests
{
    private const int MAX_LEVEL = 10;

    private readonly List<Object> created = new List<Object>();

    private Weapon materialA;
    private Weapon materialB;
    private DimensionWeaponData recipe;

    private class TestDimensionWeapon
        : DimensionWeapon
    {
        protected override void Fire()
        {
        }
    }

    [SetUp]
    public void SetUp()
    {
        materialA = MakeWeapon("MatA");
        materialB = MakeWeapon("MatB");

        GameObject resultObject = Track(new GameObject("Result"));
        TestDimensionWeapon result = resultObject.AddComponent<TestDimensionWeapon>();

        recipe = Track(ScriptableObject.CreateInstance<DimensionWeaponData>());
        recipe.SetRecipe(materialA, materialB, result);
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
    }

    private T Track<T>(T _object) where T : Object
    {
        created.Add(_object);
        return _object;
    }

    private Weapon MakeWeapon(string _name)
    {
        GameObject go = Track(new GameObject(_name));
        return go.AddComponent<Weapon>();
    }

    private static void Upgrade(Weapon _weapon, int _levels)
    {
        for (int i = 0; i < _levels; ++i)
        {
            _weapon.ApplyUpgrade(new WeaponUpgradeStep());
        }
    }

    private static System.Func<Weapon, bool> Holds(params Weapon[] _maxed)
    {
        HashSet<Weapon> set = new HashSet<Weapon>(_maxed);
        return w => set.Contains(w);
    }

    #region 버프 합산
    [Test]
    public void CombinedMultiplier_SumsBothSides()
    {
        Assert.AreEqual(1.5f, DimensionStatMath.CombinedMultiplier(0.2f, 0.3f, 0.5f), 1e-5f);
    }

    [Test]
    public void CombinedMultiplier_ClampsToMin()
    {
        Assert.AreEqual(0.5f, DimensionStatMath.CombinedMultiplier(-0.4f, -0.4f, 0.5f), 1e-5f);
    }

    [Test]
    public void Apply_AddsBothSidesThenMultiplies()
    {
        // (10 + 2 + 3) × (1 + 0.2 + 0.3) = 22.5
        Assert.AreEqual(22.5f, DimensionStatMath.Apply(10f, 2f, 3f, 0.2f, 0.3f, 0.5f), 1e-4f);
    }

    [Test]
    public void Apply_DebuffBothSides_UsesMinMultiplier()
    {
        // (10 - 1 + 0) × max(0.5, 1 - 0.6 - 0.6) = 4.5
        Assert.AreEqual(4.5f, DimensionStatMath.Apply(10f, -1f, 0f, -0.6f, -0.6f, 0.5f), 1e-4f);
    }

    [Test]
    public void RoundCount_HalfRoundsUp_AndMinOne()
    {
        Assert.AreEqual(3, DimensionStatMath.RoundCount(2.5f));
        Assert.AreEqual(1, DimensionStatMath.RoundCount(0.2f));
    }
    #endregion

    #region 최대 단계
    [Test]
    public void IsMaxLevel_TrueAtStepCount()
    {
        Upgrade(materialA, MAX_LEVEL);
        Assert.IsTrue(DimensionFusion.IsMaxLevel(materialA, MAX_LEVEL));
    }

    [Test]
    public void IsMaxLevel_FalseBelowStepCount()
    {
        Upgrade(materialA, MAX_LEVEL - 1);
        Assert.IsFalse(DimensionFusion.IsMaxLevel(materialA, MAX_LEVEL));
    }

    [Test]
    public void IsMaxLevel_FalseForNullOrZeroMax()
    {
        Assert.IsFalse(DimensionFusion.IsMaxLevel(null, MAX_LEVEL));
        Assert.IsFalse(DimensionFusion.IsMaxLevel(materialA, 0));
    }
    #endregion

    #region 후보 판정
    [Test]
    public void IsCandidate_LeftA_RightB()
    {
        Assert.IsTrue(DimensionFusion.IsCandidate(recipe, Holds(materialA), Holds(materialB), false));
    }

    [Test]
    public void IsCandidate_LeftB_RightA()
    {
        Assert.IsTrue(DimensionFusion.IsCandidate(recipe, Holds(materialB), Holds(materialA), false));
    }

    [Test]
    public void IsCandidate_NotMaxLevel_False()
    {
        Upgrade(materialA, MAX_LEVEL);
        Upgrade(materialB, MAX_LEVEL - 1);
        System.Func<Weapon, bool> left = w => w == materialA && DimensionFusion.IsMaxLevel(materialA, MAX_LEVEL);
        System.Func<Weapon, bool> right = w => w == materialB && DimensionFusion.IsMaxLevel(materialB, MAX_LEVEL);
        Assert.IsFalse(DimensionFusion.IsCandidate(recipe, left, right, false));
    }

    [Test]
    public void IsCandidate_OnlyOneSideHoldsBoth_False()
    {
        Assert.IsFalse(DimensionFusion.IsCandidate(recipe, Holds(materialA, materialB), Holds(), false));
        Assert.IsFalse(DimensionFusion.IsCandidate(recipe, Holds(), Holds(materialA, materialB), false));
    }

    [Test]
    public void IsCandidate_BothSidesSameMaterial_False()
    {
        Assert.IsFalse(DimensionFusion.IsCandidate(recipe, Holds(materialA), Holds(materialA), false));
    }

    [Test]
    public void IsCandidate_AlreadyOwned_False()
    {
        Assert.IsFalse(DimensionFusion.IsCandidate(recipe, Holds(materialA), Holds(materialB), true));
    }

    [Test]
    public void IsCandidate_InvalidRecipe_False()
    {
        DimensionWeaponData empty = Track(ScriptableObject.CreateInstance<DimensionWeaponData>());
        Assert.IsFalse(DimensionFusion.IsCandidate(empty, Holds(materialA), Holds(materialB), false));
        Assert.IsFalse(DimensionFusion.IsCandidate(null, Holds(materialA), Holds(materialB), false));
    }

    [Test]
    public void PickRandom_EmptyNull_ElsePicksByValue()
    {
        Assert.IsNull(DimensionFusion.PickRandom(new List<DimensionWeaponData>(), 0.5f));

        DimensionWeaponData other = Track(ScriptableObject.CreateInstance<DimensionWeaponData>());
        List<DimensionWeaponData> list = new List<DimensionWeaponData> { recipe, other };
        Assert.AreSame(recipe, DimensionFusion.PickRandom(list, 0f));
        Assert.AreSame(other, DimensionFusion.PickRandom(list, 0.999f));
        Assert.AreSame(other, DimensionFusion.PickRandom(list, 1f));
    }
    #endregion

    #region 차원 무기 슬롯
    [Test]
    public void AddDimensionWeapon_TakesOneSlot_AndRejectsDuplicate()
    {
        PlayerStats stats = Track(new GameObject("Player")).AddComponent<PlayerStats>();
        int changed = 0;
        stats.OnWeaponsChanged += _ => ++changed;

        Assert.IsTrue(stats.AddDimensionWeapon(recipe.ResultPrefab, recipe));
        Assert.AreEqual(1, stats.WeaponCount);
        Assert.IsTrue(stats.HasDimensionWeapon(recipe));
        Assert.AreEqual(1, changed);

        Assert.IsFalse(stats.AddDimensionWeapon(recipe.ResultPrefab, recipe));
        Assert.AreEqual(1, stats.WeaponCount);
        Assert.AreEqual(1, changed);
    }

    [Test]
    public void RemoveWeapon_NotOwned_False()
    {
        PlayerStats stats = Track(new GameObject("Player")).AddComponent<PlayerStats>();
        Assert.IsFalse(stats.RemoveWeapon(materialA));
        Assert.IsFalse(stats.RemoveWeapon(null));
    }
    #endregion
}
