using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// HUD 지원 로직 (Spawner 총 시간 / PlayerStats 무기 획득 순서 / Weapon 레벨) 순수 로직 검증
/// </summary>
public class HudEditTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    private WaveData Wave(float _time, bool _hasSpawnArray = true)
    {
        WaveData w = ScriptableObject.CreateInstance<WaveData>();
        w.time = _time;
        w.spwanArray = _hasSpawnArray ? new SpawnData[0] : null;
        created.Add(w);
        return w;
    }

    private Spawner MakeSpawner(WaveData[] _waves)
    {
        GameObject go = new GameObject("HudSpawner");
        created.Add(go);
        Spawner sp = go.AddComponent<Spawner>();
        typeof(Spawner).GetField("waveArray", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sp, _waves);
        return sp;
    }

    private Weapon WeaponPrefab(string _name)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(false);
        created.Add(go);
        return go.AddComponent<Weapon>();
    }

    private PlayerStats Player()
    {
        GameObject go = new GameObject("HudPlayer");
        created.Add(go);
        return go.AddComponent<PlayerStats>();
    }

    #region Spawner
    [Test]
    public void Spawner_Total_SumsValidWaves()
    {
        Spawner sp = MakeSpawner(new[] { Wave(10f), Wave(20.5f), Wave(30f) });
        Assert.AreEqual(60.5f, sp.TotalWaveTime, 0.001f);
    }

    [Test]
    public void Spawner_Total_ExcludesNullWaveAndNullSpawnArray()
    {
        Spawner sp = MakeSpawner(new[] { Wave(10f), null, Wave(99f, false), Wave(5f) });
        Assert.AreEqual(15f, sp.TotalWaveTime, 0.001f);
    }

    [Test]
    public void Spawner_Total_NegativeTimeCountsAsZero()
    {
        Spawner sp = MakeSpawner(new[] { Wave(10f), Wave(-4f) });
        Assert.AreEqual(10f, sp.TotalWaveTime, 0.001f);
    }

    [Test]
    public void Spawner_Total_AllInvalidOrEmpty_IsZero()
    {
        Assert.AreEqual(0f, MakeSpawner(new[] { null, Wave(3f, false) }).TotalWaveTime, 0.001f);
        Assert.AreEqual(0f, MakeSpawner(new WaveData[0]).TotalWaveTime, 0.001f);
        Assert.AreEqual(0f, MakeSpawner(null).TotalWaveTime, 0.001f);
    }

    [Test]
    public void Spawner_BeforeStart_NotStarted_RemainingEqualsTotal()
    {
        Spawner sp = MakeSpawner(new[] { Wave(10f), Wave(5f) });
        Assert.IsFalse(sp.IsStarted);
        Assert.AreEqual(-1f, sp.WaveStartTime);
        Assert.AreEqual(15f, sp.RemainingTime, 0.001f);
        Assert.IsFalse(sp.IsFinished);
    }
    #endregion

    #region PlayerStats weapons
    [Test]
    public void Player_InitiallyNoAcquiredWeapons()
    {
        PlayerStats ps = Player();
        Assert.AreEqual(0, ps.AcquiredWeaponPrefabs.Count);
        Assert.AreEqual(0, ps.AcquiredWeapons.Count);
    }

    [Test]
    public void Player_AddWeapon_RecordsAcquisitionOrderAndFiresEvent()
    {
        PlayerStats ps = Player();
        Weapon a = WeaponPrefab("A");
        Weapon b = WeaponPrefab("B");
        Weapon c = WeaponPrefab("C");
        List<Weapon> eventPrefabs = new List<Weapon>();
        List<Weapon> eventWeapons = new List<Weapon>();
        ps.OnWeaponAdded += (p, pf, w) =>
        {
            Assert.AreSame(ps, p);
            eventPrefabs.Add(pf);
            eventWeapons.Add(w);
        };

        Assert.IsTrue(ps.AddWeapon(b));
        Assert.IsTrue(ps.AddWeapon(a));
        Assert.IsTrue(ps.AddWeapon(c));

        Assert.AreEqual(new[] { b, a, c }, new List<Weapon>(ps.AcquiredWeaponPrefabs).ToArray());
        Assert.AreEqual(3, ps.AcquiredWeapons.Count);
        Assert.AreEqual(eventPrefabs, new List<Weapon>(ps.AcquiredWeaponPrefabs));
        Assert.AreEqual(eventWeapons, new List<Weapon>(ps.AcquiredWeapons));
        for (int i = 0; i < 3; i++)
        {
            Assert.AreNotSame(ps.AcquiredWeaponPrefabs[i], ps.AcquiredWeapons[i]);
            Assert.AreEqual(ps.AcquiredWeaponPrefabs[i].name, ps.AcquiredWeapons[i].name);
        }
    }

    [Test]
    public void Player_AddWeapon_DuplicateAndNull_NoEventNoRecord()
    {
        PlayerStats ps = Player();
        Weapon a = WeaponPrefab("A");
        int events = 0;
        ps.OnWeaponAdded += (p, pf, w) => ++events;

        Assert.IsTrue(ps.AddWeapon(a));
        Assert.IsFalse(ps.AddWeapon(a));
        Assert.IsFalse(ps.AddWeapon(null));

        Assert.AreEqual(1, events);
        Assert.AreEqual(1, ps.AcquiredWeaponPrefabs.Count);
        Assert.AreEqual(1, ps.AcquiredWeapons.Count);
    }

    [Test]
    public void Player_AddWeapon_AtCapacity_Rejected_NoEvent()
    {
        LogAssert.ignoreFailingMessages = true;
        PlayerStats ps = Player();
        int events = 0;
        ps.OnWeaponAdded += (p, pf, w) => ++events;
        int added = 0;
        for (int i = 0; i < 10; i++)
        {
            if (ps.AddWeapon(WeaponPrefab("W" + i)))
            {
                ++added;
            }
        }
        Assert.Less(added, 10);
        Assert.Greater(added, 0);
        Assert.AreEqual(added, events);
        Assert.AreEqual(added, ps.AcquiredWeaponPrefabs.Count);
    }
    #endregion

    #region Weapon level
    [Test]
    public void Weapon_UpgradeLevel_StartsZero_IncrementsPerApply()
    {
        Weapon w = WeaponPrefab("W");
        Assert.AreEqual(0, w.UpgradeLevel);
        w.ApplyUpgrade(new WeaponUpgradeStep("t", "d"));
        Assert.AreEqual(1, w.UpgradeLevel);
        w.ApplyUpgrade(null);
        Assert.AreEqual(1, w.UpgradeLevel);
    }
    #endregion
}
