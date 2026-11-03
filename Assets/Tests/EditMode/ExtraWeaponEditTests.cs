using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 신규 무기 WeaponParam / 플래그 누적 로직 (MonoBehaviour 생명주기 불필요한 부분)
/// </summary>
public class ExtraWeaponEditTests
{
    private GameObject go;
    private Weapon weapon;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("EditWeapon");
        weapon = go.AddComponent<Weapon>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
    }

    [Test]
    public void 신규_파라미터는_고유값_플래그는_플래그로_분류된다()
    {
        WeaponParam[] unique = { WeaponParam.ChaseSpeed, WeaponParam.BurstDuration, WeaponParam.PullForce, WeaponParam.StrikeCount };
        foreach (WeaponParam p in unique)
        {
            Assert.IsFalse(p.IsCommon(), p.ToString());
            Assert.IsFalse(p.IsFlag(), p.ToString());
            Assert.Less((int)p, WeaponParamUtils.FlagStart, p.ToString());
        }

        WeaponParam[] flags =
        {
            WeaponParam.FlagVineThorn, WeaponParam.FlagRootChase, WeaponParam.FlagSawBounce
            , WeaponParam.FlagBurn, WeaponParam.FlagTornadoPermanent, WeaponParam.FlagStormLink
        };
        foreach (WeaponParam p in flags)
        {
            Assert.IsTrue(p.IsFlag(), p.ToString());
            Assert.IsFalse(p.IsCommon(), p.ToString());
            Assert.Less((int)p, WeaponParamUtils.SlotCount, p.ToString());
        }
    }

    [Test]
    public void 플래그는_누적값이_0보다_클_때만_켜진다()
    {
        Assert.IsFalse(weapon.HasFlag(WeaponParam.FlagStormLink));
        weapon.AddModifier(WeaponParam.FlagStormLink, StatModifierMode.Add, 1f);
        Assert.IsTrue(weapon.HasFlag(WeaponParam.FlagStormLink));
        Assert.IsFalse(weapon.HasFlag(WeaponParam.FlagTornadoPermanent), "다른 플래그는 영향 없음");

        weapon.ResetUpgrades();
        Assert.IsFalse(weapon.HasFlag(WeaponParam.FlagStormLink));
    }

    [Test]
    public void StrikeCount는_기본값에_Add가_더해지고_ChaseSpeed는_Multiply가_적용된다()
    {
        Assert.AreEqual(1, weapon.GetParamInt(WeaponParam.StrikeCount, 1));
        weapon.AddModifier(WeaponParam.StrikeCount, StatModifierMode.Add, 2f);
        Assert.AreEqual(3, weapon.GetParamInt(WeaponParam.StrikeCount, 1));

        weapon.AddModifier(WeaponParam.ChaseSpeed, StatModifierMode.Multiply, 0.2f);
        Assert.AreEqual(7.2f, weapon.GetParam(WeaponParam.ChaseSpeed, 6f), 0.0001f);
    }

    [Test]
    public void 발사체_고유값은_공통_필드를_바꾸지_않는다()
    {
        float damage = weapon.damage;
        int fire = weapon.fireCount;
        weapon.AddModifier(WeaponParam.StrikeCount, StatModifierMode.Add, 2f);
        Assert.AreEqual(damage, weapon.damage);
        Assert.AreEqual(fire, weapon.fireCount);
    }

    [Test]
    public void WEAPON_TYPE에_Capped와_Wave가_있고_기존_값은_유지된다()
    {
        Assert.AreEqual(0, (int)WEAPON_TYPE.Repeat);
        Assert.AreEqual(1, (int)WEAPON_TYPE.Persistent);
        Assert.IsTrue(System.Enum.IsDefined(typeof(WEAPON_TYPE), "Capped"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WEAPON_TYPE), "Wave"));
    }
}
