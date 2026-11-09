using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 밸런스 코드 EditMode 테스트 (Spawner 웨이브 배율 / PlayerStats 배율 하한 / Weapon Final / 무기 상한 / LevelTable / DropManager exp)
/// </summary>
public class BalanceTests
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

    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private GameObject MakeGo(string _name, bool _active = true)
    {
        GameObject go = Track(new GameObject(_name));
        go.SetActive(_active);
        return go;
    }

    private static void SetField(object _t, string _f, object _v)
    {
        FieldInfo fi = _t.GetType().GetField(_f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(fi, "field not found " + _f);
        fi.SetValue(_t, _v);
    }

    private Weapon MakeWeaponPrefab(string _name, float _damage = 10f, float _speed = 2f, float _range = 5f)
    {
        GameObject go = MakeGo(_name, false);
        Weapon w = go.AddComponent<Weapon>();
        w.damage = _damage;
        w.attackSpeed = _speed;
        w.range = _range;
        w.fireCount = 1;
        w.hitCount = 1;
        return w;
    }

    private PlayerStats MakePlayer(PlayerSide _side)
    {
        PlayerStats p = MakeGo("P" + _side).AddComponent<PlayerStats>();
        SetField(p, "side", _side);
        return p;
    }

    #region Spawner
    private Spawner MakeSpawner(int _waveIndex, float _hp, float _dmg, float _spd)
    {
        Spawner s = MakeGo("Spawner", false).AddComponent<Spawner>();
        WaveData w = Track(ScriptableObject.CreateInstance<WaveData>());
        w.hpRate = _hp;
        w.damageRate = _dmg;
        w.speedRate = _spd;
        SetField(s, "HPRate", 1f);
        SetField(s, "damagerate", 1f);
        SetField(s, "speedRate", 1f);
        SetField(s, "waveArray", new[] { w });
        SetField(s, "currentWaveIndex", _waveIndex);
        return s;
    }

    [Test]
    public void Spawner_NoWave_RateIsOne()
    {
        Spawner s = MakeSpawner(-1, 3f, 3f, 3f);
        Assert.AreEqual(1f, s.GetRate(SpawnModifierType.HP), 1e-4f);
        Assert.AreEqual(1f, s.GetWaveRate(SpawnModifierType.Damage), 1e-4f);
    }

    [Test]
    public void Spawner_WaveRate_MultipliesModifiers()
    {
        Spawner s = MakeSpawner(0, 2f, 3f, 1.5f);
        LogAssert.ignoreFailingMessages = true;
        s.AddRate(SpawnModifierType.HP, StatModifierMode.Add, 0.5f);
        s.AddRate(SpawnModifierType.HP, StatModifierMode.Multiply, 0.5f);
        LogAssert.ignoreFailingMessages = false;
        // 2 * (1 + 0.5) * (1 + 0.5) = 4.5
        Assert.AreEqual(4.5f, s.GetRate(SpawnModifierType.HP), 1e-4f);
        Assert.AreEqual(3f, s.GetRate(SpawnModifierType.Damage), 1e-4f);
        Assert.AreEqual(1.5f, s.GetRate(SpawnModifierType.Speed), 1e-4f);
    }

    [Test]
    public void Spawner_Count_NotAffectedByWaveRate()
    {
        Spawner s = MakeSpawner(0, 5f, 5f, 5f);
        Assert.AreEqual(1f, s.GetRate(SpawnModifierType.Count), 1e-4f);
        LogAssert.ignoreFailingMessages = true;
        s.AddRate(SpawnModifierType.Count, StatModifierMode.Multiply, 1f);
        LogAssert.ignoreFailingMessages = false;
        Assert.AreEqual(2f, s.GetRate(SpawnModifierType.Count), 1e-4f);
    }

    [Test]
    public void Spawner_Count_FloorsAtMin()
    {
        Spawner s = MakeSpawner(0, 1f, 1f, 1f);
        LogAssert.ignoreFailingMessages = true;
        s.AddRate(SpawnModifierType.Count, StatModifierMode.Multiply, -5f);
        LogAssert.ignoreFailingMessages = false;
        Assert.AreEqual(0.1f, s.GetRate(SpawnModifierType.Count), 1e-4f);
    }

    [Test]
    public void Spawner_WaveIndexOutOfRange_RateIsOne()
    {
        Spawner s = MakeSpawner(7, 3f, 3f, 3f);
        Assert.IsNull(s.CurrentWave);
        Assert.AreEqual(1f, s.GetRate(SpawnModifierType.HP), 1e-4f);
    }
    #endregion

    #region PlayerStats / Weapon
    [Test]
    public void Multiplier_FloorsAtHalf()
    {
        PlayerStats p = MakePlayer(PlayerSide.Left);
        Assert.AreEqual(1f, p.GetMultiplier(ValueType.Damage), 1e-4f);
        p.AddModifier(ValueType.Damage, StatModifierMode.Multiply, -0.3f);
        Assert.AreEqual(0.7f, p.GetMultiplier(ValueType.Damage), 1e-4f);
        p.AddModifier(ValueType.Damage, StatModifierMode.Multiply, -0.9f);
        Assert.AreEqual(0.5f, p.GetMultiplier(ValueType.Damage), 1e-4f);
    }

    [Test]
    public void Weapon_Final_AddThenMultiply()
    {
        PlayerStats p = MakePlayer(PlayerSide.Left);
        Weapon prefab = MakeWeaponPrefab("W", 10f, 2f, 5f);
        Assert.IsTrue(p.AddWeapon(prefab));
        Weapon w = p.GetWeapon(prefab);

        p.AddModifier(ValueType.Damage, StatModifierMode.Add, 5f);
        p.AddModifier(ValueType.Damage, StatModifierMode.Multiply, 1f);
        p.AddModifier(ValueType.AttackSpeed, StatModifierMode.Add, 1f);
        p.AddModifier(ValueType.AttackSpeed, StatModifierMode.Multiply, 0.5f);
        p.AddModifier(ValueType.Range, StatModifierMode.Multiply, 0.2f);

        Assert.AreEqual((10f + 5f) * 2f, w.FinalDamage, 1e-4f);
        Assert.AreEqual((2f + 1f) * 1.5f, w.FinalAttackSpeed, 1e-4f);
        Assert.AreEqual(5f * 1.2f, w.FinalRange, 1e-4f);
    }

    [Test]
    public void Weapon_Final_AddOnly_Unchanged()
    {
        PlayerStats p = MakePlayer(PlayerSide.Left);
        Weapon prefab = MakeWeaponPrefab("W", 10f, 2f, 5f);
        p.AddWeapon(prefab);
        Weapon w = p.GetWeapon(prefab);
        p.AddModifier(ValueType.Damage, StatModifierMode.Add, 3f);
        Assert.AreEqual(13f, w.FinalDamage, 1e-4f);
        Assert.AreEqual(2f, w.FinalAttackSpeed, 1e-4f);
        Assert.AreEqual(5f, w.FinalRange, 1e-4f);
    }

    [Test]
    public void Weapon_Final_DebuffClampedAtHalf()
    {
        PlayerStats p = MakePlayer(PlayerSide.Left);
        Weapon prefab = MakeWeaponPrefab("W", 10f);
        p.AddWeapon(prefab);
        p.AddModifier(ValueType.Damage, StatModifierMode.Multiply, -0.99f);
        Assert.AreEqual(5f, p.GetWeapon(prefab).FinalDamage, 1e-4f);
    }

    [Test]
    public void Weapon_NoOwner_FinalIsRaw()
    {
        Weapon w = MakeWeaponPrefab("W", 10f, 2f, 5f);
        Assert.AreEqual(10f, w.FinalDamage, 1e-4f);
        Assert.AreEqual(5f, w.FinalRange, 1e-4f);
    }

    [Test]
    public void AddWeapon_RefusesSeventh()
    {
        PlayerStats p = MakePlayer(PlayerSide.Left);
        Assert.AreEqual(6, p.MaxWeaponCount);
        Weapon[] prefabs = new Weapon[7];
        for (int i = 0; i < 7; ++i)
        {
            prefabs[i] = MakeWeaponPrefab("W" + i);
        }
        for (int i = 0; i < 6; ++i)
        {
            Assert.IsFalse(p.IsWeaponFull);
            Assert.IsTrue(p.AddWeapon(prefabs[i]));
        }
        Assert.IsTrue(p.IsWeaponFull);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
        Assert.IsFalse(p.AddWeapon(prefabs[6]));
        Assert.AreEqual(6, p.WeaponCount);
        Assert.IsFalse(p.HasWeapon(prefabs[6]));
    }

    [Test]
    public void WeaponPool_BlockedWhenFull_OtherSideUnaffected()
    {
        RewardManager manager = MakeGo("RM").AddComponent<RewardManager>();
        PlayerStats left = MakePlayer(PlayerSide.Left);
        MakePlayer(PlayerSide.Right);

        RewardData[] rewards = new RewardData[7];
        for (int i = 0; i < 7; ++i)
        {
            WeaponRewardEffect e = Track(ScriptableObject.CreateInstance<WeaponRewardEffect>());
            SetField(e, "weaponPrefab", MakeWeaponPrefab("W" + i));
            rewards[i] = Track(ScriptableObject.CreateInstance<RewardData>());
            rewards[i].SetName("W" + i);
            rewards[i].SetEffect(e);
            manager.Add(RewardPoolType.Weapon, rewards[i], 1f);
        }

        RewardContext ctx = new RewardContext { Player = left, Side = PlayerSide.Left, Manager = manager };
        for (int i = 0; i < 6; ++i)
        {
            rewards[i].Apply(ctx);
        }

        Assert.IsTrue(left.IsWeaponFull);
        Assert.IsTrue(manager.IsPoolBlockedFor(PlayerSide.Left, RewardPoolType.Weapon));
        Assert.IsFalse(manager.IsPoolBlockedFor(PlayerSide.Right, RewardPoolType.Weapon));

        List<RewardData> result = new List<RewardData>();
        int n = manager.GetRandomUnique(10, new[] { RewardPoolType.Weapon }, PlayerSide.Left, result);
        Assert.AreEqual(0, n);
        Assert.AreEqual(0, result.Count);

        n = manager.GetRandomUnique(10, new[] { RewardPoolType.Weapon }, PlayerSide.Right, result);
        Assert.AreEqual(7, n);

        manager.SetPoolBlockedFor(PlayerSide.Left, RewardPoolType.Weapon, false);
        Assert.IsFalse(manager.IsPoolBlockedFor(PlayerSide.Left, RewardPoolType.Weapon));
    }

    [Test]
    public void DebuffPairing_OnlyPassiveBuffInPassiveBuffPool()
    {
        RewardManager manager = MakeGo("RM").AddComponent<RewardManager>();
        RewardData buff = Track(ScriptableObject.CreateInstance<RewardData>());
        RewardData weapon = Track(ScriptableObject.CreateInstance<RewardData>());
        RewardData nerf = Track(ScriptableObject.CreateInstance<RewardData>());
        manager.Add(RewardPoolType.PassiveBuff, buff, 1f);
        manager.Add(RewardPoolType.Weapon, weapon, 1f);
        manager.Add(RewardPoolType.PassiveNerf, nerf, 1f);

        Assert.IsTrue(manager.IsInPoolFor(PlayerSide.Left, RewardPoolType.PassiveBuff, buff));
        Assert.IsFalse(manager.IsInPoolFor(PlayerSide.Left, RewardPoolType.PassiveBuff, weapon));
        Assert.IsFalse(manager.IsInPoolFor(PlayerSide.Left, RewardPoolType.PassiveBuff, null));
    }
    #endregion

    #region LevelTable
    private LevelTable MakeTable(float _base, float _coef, float _pow, int _offset)
    {
        LevelTable t = Track(ScriptableObject.CreateInstance<LevelTable>());
        t.baseExp = _base;
        t.coef = _coef;
        t.power = _pow;
        t.formulaLevelOffset = _offset;
        return t;
    }

    [Test]
    public void LevelTable_Formula()
    {
        LevelTable t = MakeTable(10f, 20f, 1.2f, 1);
        Assert.IsTrue(t.UseFormula);
        Assert.IsTrue(t.HasData);
        Assert.AreEqual(30f, t.GetNeedExp(0), 0.5f);
        Assert.AreEqual(327f, t.GetNeedExp(9), 1f);
        Assert.AreEqual(t.GetNeedExp(0), t.GetNeedExp(-5), 1e-4f);
    }

    [Test]
    public void LevelTable_OldTableFallback()
    {
        LevelTable t = Track(ScriptableObject.CreateInstance<LevelTable>());
        t.expTable = new[] { 5f, 7f, 9f };
        Assert.IsFalse(t.UseFormula);
        Assert.IsTrue(t.HasData);
        Assert.AreEqual(5f, t.GetNeedExp(0));
        Assert.AreEqual(9f, t.GetNeedExp(2));
        Assert.AreEqual(9f, t.GetNeedExp(50));
        Assert.AreEqual(5f, t.GetNeedExp(-1));
    }

    [Test]
    public void LevelTable_Empty_NoData()
    {
        LevelTable t = Track(ScriptableObject.CreateInstance<LevelTable>());
        Assert.IsFalse(t.HasData);
        Assert.AreEqual(0f, t.GetNeedExp(3));
    }
    #endregion

    #region DropManager
    private float DropAndCollect(float _expValue, bool _withData)
    {
        GameObject itemGo = MakeGo("ExpItemPrefab");
        BlackBoardHandler bh = itemGo.AddComponent<BlackBoardHandler>();
        Spawnable sp = itemGo.AddComponent<Spawnable>();
        sp.blackBoardHandler = bh;
        ExpItem item = itemGo.AddComponent<ExpItem>();
        SetField(item, "baseExp", 10f);
        itemGo.SetActive(false);

        GameObject dmGo = MakeGo("DropManager", false);
        DropManager dm = dmGo.AddComponent<DropManager>();
        dm.dropItem = sp;
        // EditMode 에서는 Awake 가 자동 호출되지 않으므로 직접 호출한다
        typeof(DropManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dm, null);

        GameObject monster = MakeGo("Monster", false);
        monster.transform.position = new Vector3(777f, 777f, 0f);
        if (true == _withData)
        {
            MonsterBrain brain = monster.AddComponent<MonsterBrain>();
            MonsterData data = Track(ScriptableObject.CreateInstance<MonsterData>());
            data.expValue = _expValue;
            SetField(brain, "data", data);
        }

        dm.DropItem(monster);

        foreach (ExpItem e in Object.FindObjectsByType<ExpItem>(FindObjectsInactive.Include))
        {
            if (e != item && e.transform.position.x > 700f)
            {
                created.Add(e.gameObject);
                return e.Collect();
            }
        }
        Assert.Fail("dropped item not found");
        return -1f;
    }

    [Test]
    public void DropManager_MonsterExpValue_ReachesItem()
    {
        Assert.AreEqual(30f, DropAndCollect(3f, true), 1e-4f);
    }

    [Test]
    public void DropManager_NoMonsterData_UsesDefault()
    {
        Assert.AreEqual(10f, DropAndCollect(0f, false), 1e-4f);
    }
    #endregion
}
