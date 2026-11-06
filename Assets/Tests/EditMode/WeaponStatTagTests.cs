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

    [TestCase("Saw/Saw.prefab", WeaponStatTag.Damage | WeaponStatTag.AttackSpeed)]
    [TestCase("Shockwave/Shockwave.prefab", WeaponStatTag.Damage | WeaponStatTag.AttackSpeed)]
    [TestCase("Tornado/Tornado.prefab", WeaponStatTag.Damage | WeaponStatTag.AttackSpeed)]
    [TestCase("Vine/Vine.prefab", WeaponStatTag.Damage | WeaponStatTag.AttackSpeed)]
    [TestCase("Boomerang/Boomerang.prefab", WeaponStatTag.All)]
    [TestCase("Drone/Drone.prefab", WeaponStatTag.All)]
    [TestCase("Laser/Laser.prefab", WeaponStatTag.All)]
    public void Prefab_HasBuilderTags(string _path, WeaponStatTag _expected)
    {
        Weapon w = AssetDatabase.LoadAssetAtPath<Weapon>("Assets/prefab/weapon/" + _path);
        Assert.NotNull(w, _path);
        Assert.AreEqual(_expected, w.statTags, _path);
    }
}
