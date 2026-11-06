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
/// WeaponTestDamageMeter / WeaponTestController 검증 (씬 미배치 상태라 직접 구성)
/// </summary>
public class WeaponTestToolPlayTests
{
    private const string PlayerPath = "Assets/prefab/Player/Player.prefab";
    private const string DummyPath = "Assets/prefab/Monster/Dummy/Dummy.prefab";
    private const string EffectDir = "Assets/prefab/Upgrade/Effects/";

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

    private static HealthSystem NewHealth()
    {
        GameObject go = new GameObject("MeterHealth");
        go.AddComponent<CircleCollider2D>();
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = 1000000f;
        hs.currHP = 1000000f;
        return hs;
    }

    private static void Hit(HealthSystem _hs, float _dmg)
    {
        DamageMassage msg = new DamageMassage();
        msg.damage = _dmg;
        _hs.ProcessDamage(ref msg);
    }

    private WeaponTestController NewController(string[] _effects, int _weapon, int _layout, out GameObject _player)
    {
        _player = Object.Instantiate(Load<GameObject>(PlayerPath));
        _player.transform.position = Vector3.zero;
        spawned.Add(_player);

        WeaponRewardEffect[] effects = new WeaponRewardEffect[_effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i] = Load<WeaponRewardEffect>(EffectDir + _effects[i]);
        }

        GameObject go = new GameObject("WeaponTestController");
        go.SetActive(false);
        WeaponTestController c = go.AddComponent<WeaponTestController>();
        SetField(c, "player", _player.GetComponent<PlayerStats>());
        SetField(c, "weaponEffects", effects);
        SetField(c, "dummyPrefab", Load<GameObject>(DummyPath));
        SetField(c, "initialWeaponIndex", _weapon);
        SetField(c, "initialLayoutIndex", _layout);
        go.SetActive(true);
        spawned.Add(go);
        return c;
    }

    private static readonly string[] Effects =
    {
        "BoomerangRewardEffect.asset", "SweepRewardEffect.asset", "SplitShotRewardEffect.asset", "ChainRewardEffect.asset"
    };

    private static List<GameObject> Dummies()
    {
        List<GameObject> list = new List<GameObject>();
        foreach (WeaponTestDamageMeter m in Object.FindObjectsByType<WeaponTestDamageMeter>(FindObjectsInactive.Include))
        {
            list.Add(m.gameObject);
        }
        return list;
    }

    private static int WeaponCount(GameObject _player)
    {
        return _player.GetComponentsInChildren<Weapon>(true).Length;
    }
    #endregion

    #region DamageMeter
    [UnityTest]
    public IEnumerator Meter_OnDamaged_UpdatesStats()
    {
        HealthSystem hs = NewHealth();
        spawned.Add(hs.gameObject);
        WeaponTestDamageMeter meter = hs.gameObject.AddComponent<WeaponTestDamageMeter>();
        meter.Bind(hs, new WeaponTestDamageMeterSettings());
        yield return null;

        Assert.AreEqual(0, meter.HitCount);
        Assert.AreEqual(0f, meter.Dps);

        Hit(hs, 10f);
        Hit(hs, 25f);
        Assert.AreEqual(25f, meter.LastDamage);
        Assert.AreEqual(35f, meter.TotalDamage);
        Assert.AreEqual(2, meter.HitCount);
    }

    [UnityTest]
    public IEnumerator Meter_Dps_UsesMinWindowThenElapsed()
    {
        HealthSystem hs = NewHealth();
        spawned.Add(hs.gameObject);
        WeaponTestDamageMeter meter = hs.gameObject.AddComponent<WeaponTestDamageMeter>();
        meter.Bind(hs, new WeaponTestDamageMeterSettings());
        yield return null;

        Hit(hs, 50f);
        // 경과 < 1초 : 분모 1초
        Assert.AreEqual(50f, meter.Dps, 0.01f);

        float start = Time.time;
        yield return new WaitForSeconds(1.5f);
        float elapsed = Time.time - start;
        Assert.AreEqual(50f / elapsed, meter.Dps, 0.5f);
        Assert.Less(meter.Dps, 50f);
    }

    [UnityTest]
    public IEnumerator Meter_Reset_ClearsAll()
    {
        HealthSystem hs = NewHealth();
        spawned.Add(hs.gameObject);
        WeaponTestDamageMeter meter = hs.gameObject.AddComponent<WeaponTestDamageMeter>();
        meter.Bind(hs, new WeaponTestDamageMeterSettings());
        yield return null;

        Hit(hs, 40f);
        meter.ResetMeter();
        Assert.AreEqual(0f, meter.LastDamage);
        Assert.AreEqual(0f, meter.TotalDamage);
        Assert.AreEqual(0, meter.HitCount);
        Assert.AreEqual(0f, meter.Dps);

        Hit(hs, 5f);
        Assert.AreEqual(1, meter.HitCount);
        Assert.AreEqual(5f, meter.TotalDamage);
    }

    [UnityTest]
    public IEnumerator Meter_ZeroDamage_NotCounted()
    {
        HealthSystem hs = NewHealth();
        spawned.Add(hs.gameObject);
        WeaponTestDamageMeter meter = hs.gameObject.AddComponent<WeaponTestDamageMeter>();
        meter.Bind(hs, null);
        yield return null;

        Hit(hs, 0f);
        Assert.AreEqual(0, meter.HitCount);
    }
    #endregion

    #region Controller
    [UnityTest]
    public IEnumerator SelectLayout_CreatesExpectedCount_AndRemovesPrevious()
    {
        WeaponTestController c = NewController(Effects, -1, -1, out GameObject player);
        yield return null;

        int[] expected = { 1, 8, 10, 11 };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.IsTrue(c.SelectLayout(i));
            yield return null; // Destroy 반영
            Assert.AreEqual(expected[i], Dummies().Count, "layout " + i);
            Assert.AreEqual(expected[i], c.Meters.Count, "meters " + i);
            Assert.AreEqual(i, c.CurrentLayoutIndex);
        }
    }

    [UnityTest]
    public IEnumerator Dummy_IsInvincible_TakesDamageButNeverDies()
    {
        WeaponTestController c = NewController(Effects, -1, 0, out GameObject player);
        yield return null;

        List<GameObject> dummies = Dummies();
        Assert.AreEqual(1, dummies.Count);
        HealthSystem hs = dummies[0].GetComponentInChildren<HealthSystem>(true);
        Assert.IsNotNull(hs);
        Assert.IsTrue(hs.IsInvincible);

        WeaponTestDamageMeter meter = c.Meters[0];
        float lethal = hs.MaxHP * 10f;
        Hit(hs, lethal);
        Hit(hs, lethal);
        yield return null;

        Assert.IsFalse(hs.isDead);
        Assert.AreEqual(hs.MaxHP, hs.CurrHP);
        Assert.AreEqual(2, meter.HitCount);
        Assert.AreEqual(lethal, meter.LastDamage);
        Assert.IsNotNull(dummies[0]);
        Assert.IsTrue(dummies[0].activeInHierarchy);
    }

    [UnityTest]
    public IEnumerator SelectLayout_InvalidIndex_ReturnsFalse_KeepsState()
    {
        WeaponTestController c = NewController(Effects, -1, 0, out GameObject player);
        yield return null;

        LogAssert.Expect(LogType.Warning, new Regex("배치가 없습니다"));
        Assert.IsFalse(c.SelectLayout(99));
        LogAssert.Expect(LogType.Warning, new Regex("배치가 없습니다"));
        Assert.IsFalse(c.SelectLayout(-1));
        Assert.AreEqual(1, Dummies().Count);
        Assert.AreEqual(0, c.CurrentLayoutIndex);
    }

    [UnityTest]
    public IEnumerator SelectWeapon_ReplacesWeapon_SingleWithMatchingLayers()
    {
        WeaponTestController c = NewController(Effects, 0, 0, out GameObject player);
        yield return null;

        Weapon first = c.CurrentWeapon;
        Assert.IsNotNull(first);
        Assert.AreEqual(1, WeaponCount(player));

        Assert.IsTrue(c.SelectWeapon(1));
        yield return null;

        Assert.IsTrue(first == null, "이전 무기 파괴");
        Assert.IsNotNull(c.CurrentWeapon);
        Assert.AreEqual(1, c.CurrentWeaponIndex);
        Assert.AreEqual(1, WeaponCount(player));

        GameObject dummy = Dummies()[0];
        Assert.AreEqual(1 << dummy.layer, c.CurrentWeapon.targetLayer.value);
        string attack = player.GetComponent<PlayerStats>().Side == PlayerSide.Left ? "LAttack" : "RAttack";
        Assert.AreEqual(LayerMask.NameToLayer(attack), c.CurrentWeapon.gameObject.layer);
    }

    [UnityTest]
    public IEnumerator SelectWeapon_InvalidIndex_KeepsCurrent()
    {
        WeaponTestController c = NewController(Effects, 0, 0, out GameObject player);
        yield return null;

        Weapon w = c.CurrentWeapon;
        LogAssert.Expect(LogType.Warning, new Regex("무기 프리팹이 없습니다"));
        Assert.IsFalse(c.SelectWeapon(99));
        Assert.IsTrue(w == c.CurrentWeapon);
        Assert.AreEqual(0, c.CurrentWeaponIndex);
    }

    [UnityTest]
    public IEnumerator Upgrade_Once_Max_Reset()
    {
        WeaponTestController c = NewController(Effects, 0, 0, out GameObject player);
        yield return null;

        WeaponRewardEffect eff = Load<WeaponRewardEffect>(EffectDir + Effects[0]);
        Assert.IsNotNull(eff.UpgradeTable, "Boomerang 업그레이드 테이블");
        int steps = eff.UpgradeTable.StepCount;
        Assert.Greater(steps, 1);
        Assert.AreEqual(0, c.CurrentWeapon.UpgradeLevel);

        Assert.IsTrue(c.UpgradeOnce());
        Assert.AreEqual(1, c.CurrentWeapon.UpgradeLevel);

        int applied = c.UpgradeMax();
        Assert.AreEqual(steps - 1, applied);
        Assert.AreEqual(steps, c.CurrentWeapon.UpgradeLevel);

        // 최대 이후엔 더 오르지 않음
        Assert.IsFalse(c.UpgradeOnce());
        Assert.AreEqual(0, c.UpgradeMax());
        Assert.AreEqual(steps, c.CurrentWeapon.UpgradeLevel);

        c.ResetAll();
        yield return null;
        Assert.IsNotNull(c.CurrentWeapon);
        Assert.AreEqual(0, c.CurrentWeapon.UpgradeLevel);
        Assert.AreEqual(1, WeaponCount(player));
        foreach (WeaponTestDamageMeter m in c.Meters)
        {
            Assert.AreEqual(0, m.HitCount);
            Assert.AreEqual(0f, m.TotalDamage);
        }
    }

    [UnityTest]
    public IEnumerator Upgrade_ResetsMeters()
    {
        WeaponTestController c = NewController(Effects, 0, 0, out GameObject player);
        yield return null;

        WeaponTestDamageMeter m = c.Meters[0];
        HealthSystem hs = m.Target;
        Assert.IsNotNull(hs);
        Hit(hs, 1f);
        Assert.AreEqual(1, m.HitCount);
        c.UpgradeOnce();
        Assert.AreEqual(0, m.HitCount);
    }

    [UnityTest]
    public IEnumerator SwitchWeapon_LeavesNoOldProjectiles()
    {
        WeaponTestController c = NewController(Effects, 2, 1, out GameObject player);
        yield return new WaitForSeconds(2f);

        List<Projectile> before = new List<Projectile>(Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include));
        Assert.Greater(before.Count, 0, "발사체가 생성돼야 함");

        Assert.IsTrue(c.SelectWeapon(0));
        yield return null;
        yield return null;

        foreach (Projectile p in before)
        {
            Assert.IsTrue(p == null, "이전 발사체 잔존");
        }
    }

    [UnityTest]
    public IEnumerator Dummy_ReceivesDamage_Within5Seconds()
    {
        WeaponTestController c = NewController(Effects, 2, 0, out GameObject player);

        float end = Time.time + 5f;
        yield return null;
        while (Time.time < end && c.Meters.Count > 0 && c.Meters[0].HitCount == 0)
        {
            yield return null;
        }

        Assert.AreEqual(1, c.Meters.Count);
        Assert.Greater(c.Meters[0].HitCount, 0, "5초 내 피격 없음");
        Assert.Greater(c.Meters[0].TotalDamage, 0f);
        Assert.Greater(c.Meters[0].Dps, 0f);
    }
    #endregion
}
#endif
