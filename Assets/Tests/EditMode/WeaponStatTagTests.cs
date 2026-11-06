using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기 태그(Weapon.statTags) EditMode 테스트
/// - 태그가 있는 수치만 플레이어 버프 / 디버프를 받는다
/// - WeaponTagBuilder 가 적용한 프리팹 값
/// </summary>
public class WeaponStatTagTests
{
    private readonly List<Object> created = new List<Object>();

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

    private GameObject MakeGo(string _name)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(false);
        created.Add(go);
        return go;
    }

    private PlayerStats MakePlayer()
    {
        PlayerStats p = MakeGo("P").AddComponent<PlayerStats>();
        typeof(PlayerStats).GetField("side", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, PlayerSide.Left);
        p.AddModifier(ValueType.Damage, StatModifierMode.Add, 5f);
        p.AddModifier(ValueType.Damage, StatModifierMode.Multiply, 1f);
        p.AddModifier(ValueType.AttackSpeed, StatModifierMode.Multiply, 0.5f);
        p.AddModifier(ValueType.Range, StatModifierMode.Multiply, -0.3f);
        return p;
    }

    private Weapon MakeWeapon(PlayerStats _owner, WeaponStatTag _tags)
    {
        Weapon w = MakeGo("W").AddComponent<Weapon>();
        w.damage = 10f;
        w.attackSpeed = 2f;
        w.range = 5f;
        w.fireCount = 1;
        w.statTags = _tags;
        w.SetOwnerStats(_owner);
        return w;
    }

    [Test]
    public void DefaultTags_AreAll()
    {
        Weapon w = MakeGo("W").AddComponent<Weapon>();
        Assert.AreEqual(WeaponStatTag.All, w.statTags);
    }

    [Test]
    public void AllTags_ApplyEveryOwnerStat()
    {
        Weapon w = MakeWeapon(MakePlayer(), WeaponStatTag.All);
        Assert.AreEqual((10f + 5f) * 2f, w.FinalDamage, 1e-4f);
        Assert.AreEqual(2f * 1.5f, w.FinalAttackSpeed, 1e-4f);
        Assert.AreEqual(5f * 0.7f, w.FinalRange, 1e-4f);
    }

    [Test]
    public void NoTags_IgnoreOwnerStats()
    {
        Weapon w = MakeWeapon(MakePlayer(), WeaponStatTag.None);
        Assert.AreEqual(10f, w.FinalDamage, 1e-4f);
        Assert.AreEqual(2f, w.FinalAttackSpeed, 1e-4f);
        Assert.AreEqual(5f, w.FinalRange, 1e-4f);
    }

    [Test]
    public void MissingRangeTag_IgnoresRangeDebuffOnly()
    {
        Weapon w = MakeWeapon(MakePlayer(), WeaponStatTag.Damage | WeaponStatTag.AttackSpeed);
        Assert.AreEqual(30f, w.FinalDamage, 1e-4f);
        Assert.AreEqual(3f, w.FinalAttackSpeed, 1e-4f);
        Assert.AreEqual(5f, w.FinalRange, 1e-4f);
        Assert.IsFalse(w.IsAffectedBy(ValueType.Range));
        Assert.IsTrue(w.IsAffectedBy(ValueType.Damage));
    }

    [Test]
    public void MissingAttackSpeedTag_AlsoKeepsCoolTimeAndFireDelay()
    {
        Weapon w = MakeWeapon(MakePlayer(), WeaponStatTag.Damage);
        w.fireDelay = 0.2f;
        Assert.AreEqual(0.5f, w.FinalCoolTime, 1e-4f);
        Assert.AreEqual(0.2f, w.FinalFireDelay, 1e-4f);
    }

    [Test]
    public void NonWeaponValueTypes_AreNeverTagged()
    {
        Assert.IsFalse(WeaponStatTag.All.Has(ValueType.HP));
        Assert.IsFalse(WeaponStatTag.All.Has(ValueType.MoveSpeed));
    }

    private const WeaponStatTag DA = WeaponStatTag.Damage | WeaponStatTag.AttackSpeed;
    private const WeaponStatTag DAR = DA | WeaponStatTag.Range;

    [TestCase("Saw/Saw.prefab", DA)]
    [TestCase("Shockwave/Shockwave.prefab", DA)]
    [TestCase("Tornado/Tornado.prefab", DA | WeaponStatTag.ProjectileCount | WeaponStatTag.Duration)]
    [TestCase("Vine/Vine.prefab", DA | WeaponStatTag.Duration)]
    [TestCase("PoisonZone/PoisonZone.prefab", DAR | WeaponStatTag.Duration)]
    [TestCase("Flame/Flame.prefab", DAR | WeaponStatTag.Duration)]
    [TestCase("Root/Root.prefab", WeaponStatTag.All)]
    [TestCase("Boomerang/Boomerang.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("Drone/Drone.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("Explosive/Explosive.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("Laser/Laser.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("Lightning/Lightning.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("Spirit/Spirit.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("SplitShot/SplitShot.prefab", DAR | WeaponStatTag.ProjectileCount)]
    [TestCase("Chain/Chain.prefab", DAR)]
    [TestCase("Mine/Mine.prefab", DAR)]
    [TestCase("Sweep/Sweep.prefab", DAR)]
    public void Prefab_HasBuilderTags(string _path, WeaponStatTag _expected)
    {
        Weapon w = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/prefab/weapon/" + _path);
        Assert.NotNull(w, _path);
        Assert.AreEqual(_expected, w.statTags, _path);
    }

    #region Duration / ProjectileCount
    private PlayerStats MakeCountDurationPlayer(float _countAdd, float _durationMul)
    {
        PlayerStats p = MakeGo("P").AddComponent<PlayerStats>();
        typeof(PlayerStats).GetField("side", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, PlayerSide.Left);
        p.AddModifier(ValueType.ProjectileCount, StatModifierMode.Add, _countAdd);
        p.AddModifier(ValueType.Duration, StatModifierMode.Multiply, _durationMul);
        return p;
    }

    [Test]
    public void ProjectileCount_Tagged_AddsToFireCount()
    {
        Weapon w = MakeWeapon(MakeCountDurationPlayer(2f, 0f), WeaponStatTag.ProjectileCount);
        w.fireCount = 1;
        Assert.AreEqual(3, w.FinalFireCount);
        Assert.AreEqual(3, w.LaunchCount);
    }

    [Test]
    public void ProjectileCount_Multiply_HalfRoundsUp()
    {
        PlayerStats p = MakeCountDurationPlayer(0f, 0f);
        p.AddModifier(ValueType.ProjectileCount, StatModifierMode.Multiply, 0.25f);
        Weapon w = MakeWeapon(p, WeaponStatTag.ProjectileCount);
        w.fireCount = 2;
        Assert.AreEqual(3, w.FinalFireCount); // 2 × 1.25 = 2.5 → 3
    }

    [Test]
    public void ProjectileCount_Untagged_KeepsFireCount()
    {
        Weapon w = MakeWeapon(MakeCountDurationPlayer(2f, 0f), WeaponStatTag.None);
        w.fireCount = 2;
        Assert.AreEqual(2, w.FinalFireCount);
    }

    [Test]
    public void ProjectileCount_Debuff_NeverBelowOne_ZeroStaysZero()
    {
        Weapon w = MakeWeapon(MakeCountDurationPlayer(-5f, 0f), WeaponStatTag.All);
        w.fireCount = 1;
        Assert.AreEqual(1, w.FinalFireCount);
        w.fireCount = 0;
        Assert.AreEqual(0, w.FinalFireCount);
    }

    [Test]
    public void Duration_Tagged_ScalesLifetimeAndApplyDuration()
    {
        Weapon w = MakeWeapon(MakeCountDurationPlayer(0f, 0.5f), WeaponStatTag.Duration);
        w.lifetime = 3f;
        Assert.AreEqual(4.5f, w.FinalLifetime, 1e-4f);
        Assert.AreEqual(1.2f, w.ApplyDuration(0.8f), 1e-4f);
    }

    [Test]
    public void Duration_Untagged_OrZeroLifetime_Unchanged()
    {
        Weapon w = MakeWeapon(MakeCountDurationPlayer(0f, 0.5f), WeaponStatTag.None);
        w.lifetime = 3f;
        Assert.AreEqual(3f, w.FinalLifetime, 1e-4f);
        Assert.AreEqual(0.8f, w.ApplyDuration(0.8f), 1e-4f);

        w.statTags = WeaponStatTag.All;
        w.lifetime = 0f;
        Assert.AreEqual(0f, w.FinalLifetime, 1e-4f);
    }

    [Test]
    public void Lightning_HandlesFireCount_LaunchesOne()
    {
        Weapon prefab = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/prefab/weapon/Lightning/Lightning.prefab");
        Assert.NotNull(prefab);
        Assert.IsTrue(prefab.projectile.HandlesFireCount);

        Weapon w = Object.Instantiate(prefab);
        created.Add(w.gameObject);
        w.SetOwnerStats(MakeCountDurationPlayer(2f, 0f));
        Assert.AreEqual(3, w.FinalFireCount);
        Assert.AreEqual(1, w.LaunchCount);
    }

    [Test]
    public void Flame_IsCapped()
    {
        Weapon w = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/prefab/weapon/Flame/Flame.prefab");
        Assert.AreEqual(WEAPON_TYPE.Capped, w.weaponType);
        Assert.AreEqual(1, w.fireCount);
    }

    [Test]
    public void Flame_CycleMatchesOldRepeatDps()
    {
        // 예전: Repeat 공속 0.8 → 주기 1.25s. 지금: 분사 0.8s + 쿨타임(1 / attackSpeed)
        Weapon w = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/prefab/weapon/Flame/Flame.prefab");
        float cycle = 0.8f + 1f / w.attackSpeed;
        Assert.AreEqual(1.25f, cycle, 0.01f);
    }
    #endregion
}
