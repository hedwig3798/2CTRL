#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// WeaponTestController 패시브 스택 검증 (씬 미배치 상태라 직접 구성)
/// </summary>
public class WeaponTestPassivePlayTests
{
    private const string PlayerPath = "Assets/prefab/Player/Player.prefab";
    private const string DummyPath = "Assets/prefab/Monster/Dummy/Dummy.prefab";
    private const string EffectDir = "Assets/prefab/Upgrade/Effects/";
    private const string BuffDir = "Assets/prefab/Upgrade/PassiveBuff/";
    private const string NuffDir = "Assets/prefab/Upgrade/PassiveNuff/";

    private static readonly string[] Effects = { "BoomerangRewardEffect.asset", "SweepRewardEffect.asset" };

    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (null != go)
            {
                Object.DestroyImmediate(go);
            }
        }
        spawned.Clear();

        foreach (WeaponTestDamageMeter m in Object.FindObjectsByType<WeaponTestDamageMeter>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(m.gameObject);
        }
        foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(p.gameObject);
        }
    }

    #region Helper
    private static void SetField(object _obj, string _name, object _value)
    {
        FieldInfo f = _obj.GetType().GetField(_name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, _name);
        f.SetValue(_obj, _value);
    }

    private static T Load<T>(string _path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(_path);
        Assert.IsNotNull(asset, _path);
        return asset;
    }

    // passives: 0 DamageUpgrade, 1 ProjectileUpgrade, 2 DamageNuff, 3 null(비활성)
    private WeaponTestController NewController(int _weapon, bool _rebuild, out PlayerStats _stats, int _maxStack = 99)
    {
        GameObject player = Object.Instantiate(Load<GameObject>(PlayerPath));
        player.transform.position = Vector3.zero;
        spawned.Add(player);
        _stats = player.GetComponent<PlayerStats>();

        WeaponRewardEffect[] effects = new WeaponRewardEffect[Effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i] = Load<WeaponRewardEffect>(EffectDir + Effects[i]);
        }

        RewardData[] passives =
        {
            Load<RewardData>(BuffDir + "DamageUpgrade.asset"),
            Load<RewardData>(BuffDir + "ProjectileUpgrade.asset"),
            Load<RewardData>(NuffDir + "DamageNuff.asset"),
            null,
        };

        LogAssert.Expect(LogType.Warning, new Regex("패시브가 비어 있습니다"));
        GameObject go = new GameObject("WeaponTestController");
        go.SetActive(false);
        WeaponTestController c = go.AddComponent<WeaponTestController>();
        SetField(c, "player", _stats);
        SetField(c, "weaponEffects", effects);
        SetField(c, "dummyPrefab", Load<GameObject>(DummyPath));
        SetField(c, "initialWeaponIndex", _weapon);
        SetField(c, "initialLayoutIndex", 0);
        SetField(c, "passives", passives);
        SetField(c, "maxPassiveStack", _maxStack);
        SetField(c, "rebuildWeaponOnPassiveChange", _rebuild);
        go.SetActive(true);
        spawned.Add(go);
        return c;
    }

    private static float Value(string _path)
    {
        RewardData d = Load<RewardData>(_path);
        Assert.IsTrue(d.TryGetValue(out float v), _path);
        return v;
    }
    #endregion

    [UnityTest]
    public IEnumerator Stack_MultiplyAndAdd_EqualsStackTimesValue()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s);
        yield return null;

        Assert.AreEqual(4, c.PassiveCount);
        float dmg = Value(BuffDir + "DamageUpgrade.asset");
        float proj = Value(BuffDir + "ProjectileUpgrade.asset");

        Assert.IsTrue(c.SetPassiveStack(0, 3));
        Assert.AreEqual(3, c.GetPassiveStack(0));
        Assert.AreEqual(3 * dmg, s.GetMultiply(ValueType.Damage), 1e-4f);

        Assert.IsTrue(c.SetPassiveStack(1, 2));
        Assert.AreEqual(2 * proj, s.GetAdd(ValueType.ProjectileCount), 1e-4f);
        Assert.AreEqual(3 * dmg, s.GetMultiply(ValueType.Damage), 1e-4f, "다른 패시브가 영향 없음");
    }

    [UnityTest]
    public IEnumerator Stack_Decrease_ReturnsValue()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s);
        yield return null;
        float dmg = Value(BuffDir + "DamageUpgrade.asset");

        c.SetPassiveStack(0, 4);
        Assert.IsTrue(c.AddPassiveStack(0, -3));
        Assert.AreEqual(1, c.GetPassiveStack(0));
        Assert.AreEqual(dmg, s.GetMultiply(ValueType.Damage), 1e-4f);

        Assert.IsTrue(c.AddPassiveStack(0, -1));
        Assert.AreEqual(0f, s.GetMultiply(ValueType.Damage), 1e-4f);
    }

    [UnityTest]
    public IEnumerator Stack_BuffPlusNuff_Sums()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s);
        yield return null;
        float buff = Value(BuffDir + "DamageUpgrade.asset");
        float nuff = Value(NuffDir + "DamageNuff.asset");

        c.SetPassiveStack(0, 2);
        c.SetPassiveStack(2, 3);
        Assert.AreEqual(2 * buff + 3 * nuff, s.GetMultiply(ValueType.Damage), 1e-4f);
    }

    [UnityTest]
    public IEnumerator Stack_ClampAndInvalid()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s, 5);
        yield return null;

        Assert.IsFalse(c.AddPassiveStack(0, -1), "0 미만 불가, 변화 없음");
        Assert.AreEqual(0, c.GetPassiveStack(0));
        Assert.IsFalse(c.SetPassiveStack(0, -10));
        Assert.AreEqual(0, c.GetPassiveStack(0));

        Assert.IsTrue(c.SetPassiveStack(0, 100));
        Assert.AreEqual(5, c.GetPassiveStack(0), "max 클램프");
        Assert.IsFalse(c.AddPassiveStack(0, 1), "최대에서 변화 없음");
        Assert.IsFalse(c.SetPassiveStack(0, 5), "같은 값");

        Assert.IsFalse(c.SetPassiveStack(-1, 1));
        Assert.IsFalse(c.SetPassiveStack(4, 1));
        Assert.IsFalse(c.SetPassiveStack(3, 1), "null 패시브는 비활성");
        Assert.AreEqual(0, c.GetPassiveStack(3));
        Assert.AreEqual(0, c.GetPassiveStack(-1));
        Assert.AreEqual(0, c.GetPassiveStack(99));
    }

    [UnityTest]
    public IEnumerator ResetPassives_And_ResetAll_ClearStacksAndModifiers()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s);
        yield return null;

        c.SetPassiveStack(0, 3);
        c.SetPassiveStack(1, 2);
        c.UpgradeOnce();
        int level = c.CurrentWeapon.UpgradeLevel;

        c.ResetPassives();
        Assert.AreEqual(0, c.GetPassiveStack(0));
        Assert.AreEqual(0, c.GetPassiveStack(1));
        Assert.AreEqual(0f, s.GetMultiply(ValueType.Damage), 1e-4f);
        Assert.AreEqual(0f, s.GetAdd(ValueType.ProjectileCount), 1e-4f);
        Assert.AreEqual(level, c.CurrentWeapon.UpgradeLevel, "ResetPassives 는 강화 단계 유지");

        c.SetPassiveStack(2, 2);
        c.ResetAll();
        yield return null;
        Assert.AreEqual(0, c.GetPassiveStack(2));
        Assert.AreEqual(0f, s.GetMultiply(ValueType.Damage), 1e-4f);
    }

    [UnityTest]
    public IEnumerator Rebuild_KeepsUpgradeLevel_AndReplacesWeapon()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s);
        yield return null;

        Assert.IsTrue(c.UpgradeOnce());
        Assert.IsTrue(c.UpgradeOnce());
        Weapon before = c.CurrentWeapon;
        int level = before.UpgradeLevel;
        Assert.AreEqual(2, level);

        c.SetPassiveStack(0, 1);
        yield return null;
        Assert.IsTrue(before == null, "무기 재생성");
        Assert.IsNotNull(c.CurrentWeapon);
        Assert.AreEqual(level, c.CurrentWeapon.UpgradeLevel);
        Assert.AreEqual(1, c.GetPassiveStack(0));
        Assert.AreEqual(1, s.GetComponentsInChildren<Weapon>(true).Length);
    }

    [UnityTest]
    public IEnumerator NoRebuild_KeepsSameWeaponInstance()
    {
        WeaponTestController c = NewController(0, false, out PlayerStats s);
        yield return null;

        Weapon before = c.CurrentWeapon;
        Assert.IsTrue(c.SetPassiveStack(0, 2));
        yield return null;
        Assert.IsTrue(before == c.CurrentWeapon);
        Assert.AreEqual(2 * Value(BuffDir + "DamageUpgrade.asset"), s.GetMultiply(ValueType.Damage), 1e-4f);
    }

    [UnityTest]
    public IEnumerator Passives_Persist_OnSelectWeapon_And_Upgrade()
    {
        WeaponTestController c = NewController(0, true, out PlayerStats s);
        yield return null;
        float dmg = Value(BuffDir + "DamageUpgrade.asset");

        c.SetPassiveStack(0, 2);
        Assert.IsTrue(c.SelectWeapon(1));
        yield return null;
        Assert.AreEqual(2, c.GetPassiveStack(0));
        Assert.AreEqual(2 * dmg, s.GetMultiply(ValueType.Damage), 1e-4f);

        c.UpgradeOnce();
        Assert.AreEqual(2, c.GetPassiveStack(0));
        Assert.AreEqual(2 * dmg, s.GetMultiply(ValueType.Damage), 1e-4f);
    }
}
#endif
