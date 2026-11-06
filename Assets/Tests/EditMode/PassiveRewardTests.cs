using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 추가 패시브 EditMode 테스트
/// - 지속 시간 하한 (최초 지속 시간 × 0.5)
/// - 몬스터 효과가 스폰율(Count) 과 체력(HP) 을 함께 바꾼다
/// - PassiveRewardBuilder 가 만든 에셋 / 풀 등록 / 가중치
/// </summary>
public class PassiveRewardTests
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
        LogAssert.ignoreFailingMessages = false;
    }

    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private static void SetField(object _t, string _f, object _v)
    {
        FieldInfo fi = _t.GetType().GetField(_f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(fi, "field not found " + _f);
        fi.SetValue(_t, _v);
    }

    private PlayerStats MakePlayer()
    {
        GameObject go = Track(new GameObject("P"));
        go.SetActive(false);
        PlayerStats p = go.AddComponent<PlayerStats>();
        SetField(p, "side", PlayerSide.Left);
        return p;
    }

    private Weapon MakeWeapon(PlayerStats _owner, float _lifetime)
    {
        GameObject go = Track(new GameObject("W"));
        go.SetActive(false);
        Weapon w = go.AddComponent<Weapon>();
        w.lifetime = _lifetime;
        w.fireCount = 1;
        w.statTags = WeaponStatTag.All;
        w.SetOwnerStats(_owner);
        return w;
    }

    #region Duration floor
    [Test]
    public void Duration_Nerf_FloorsAtHalfOfInitialLifetime()
    {
        PlayerStats p = MakePlayer();
        Weapon w = MakeWeapon(p, 3f);
        p.AddModifier(ValueType.Duration, StatModifierMode.Add, -10f);
        Assert.AreEqual(1.5f, w.FinalLifetime, 1e-4f);
    }

    [Test]
    public void Duration_Floor_UsesInitialNotUpgradedLifetime()
    {
        PlayerStats p = MakePlayer();
        Weapon w = MakeWeapon(p, 3f);
        Assert.AreEqual(3f, w.FinalLifetime, 1e-4f); // 최초 값 기록
        w.AddModifier(WeaponParam.Lifetime, StatModifierMode.Add, 3f);
        Assert.AreEqual(6f, w.lifetime, 1e-4f);
        p.AddModifier(ValueType.Duration, StatModifierMode.Add, -100f);
        Assert.AreEqual(1.5f, w.FinalLifetime, 1e-4f);
    }

    [Test]
    public void Duration_SmallNerf_AboveFloor_Applies()
    {
        PlayerStats p = MakePlayer();
        Weapon w = MakeWeapon(p, 3f);
        p.AddModifier(ValueType.Duration, StatModifierMode.Multiply, -0.06f);
        Assert.AreEqual(3f * 0.94f, w.FinalLifetime, 1e-4f);
    }

    [Test]
    public void ApplyDuration_FloorsAtHalfOfInitial()
    {
        PlayerStats p = MakePlayer();
        Weapon w = MakeWeapon(p, 0f);
        p.AddModifier(ValueType.Duration, StatModifierMode.Add, -5f);
        Assert.AreEqual(0.4f, w.ApplyDuration(0.8f, 0.8f), 1e-4f);
        Assert.AreEqual(0f, w.ApplyDuration(0.8f), 1e-4f); // 하한 없이 호출하면 0
    }

    [Test]
    public void ApplyDuration_Untagged_Unchanged()
    {
        PlayerStats p = MakePlayer();
        Weapon w = MakeWeapon(p, 3f);
        w.statTags = WeaponStatTag.Damage;
        p.AddModifier(ValueType.Duration, StatModifierMode.Add, -5f);
        Assert.AreEqual(0.8f, w.ApplyDuration(0.8f, 0.8f), 1e-4f);
        Assert.AreEqual(3f, w.FinalLifetime, 1e-4f);
    }
    #endregion

    #region HP / Regen
    [Test]
    public void Regen_BaseZero_NerfNeverBelowZero()
    {
        PlayerStats p = MakePlayer();
        Assert.AreEqual(0f, p.Get(ValueType.HPRegen), 1e-4f);
        p.AddModifier(ValueType.HPRegen, StatModifierMode.Add, -0.3f);
        Assert.AreEqual(0f, p.Get(ValueType.HPRegen), 1e-4f);
        p.AddModifier(ValueType.HPRegen, StatModifierMode.Add, 0.5f);
        Assert.AreEqual(0.2f, p.Get(ValueType.HPRegen), 1e-4f);
    }

    private HealthSystem MakeHealth(float _max, float _curr)
    {
        GameObject go = Track(new GameObject("H"));
        go.SetActive(false);
        HealthSystem h = go.AddComponent<HealthSystem>();
        h.maxHP = _max;
        h.currHP = _curr;
        return h;
    }

    [Test]
    public void ApplyRegen_HealsUpToMax()
    {
        HealthSystem h = MakeHealth(100f, 50f);
        h.ApplyRegen(0.5f, 10f);
        Assert.AreEqual(55f, h.CurrHP, 1e-4f);
        h.ApplyRegen(0.5f, 1000f);
        Assert.AreEqual(100f, h.CurrHP, 1e-4f);
    }

    [Test]
    public void ApplyRegen_NegativeRateOrDead_DoesNothing()
    {
        HealthSystem h = MakeHealth(100f, 50f);
        h.ApplyRegen(-1f, 10f);
        Assert.AreEqual(50f, h.CurrHP, 1e-4f);
        h.isDead = true;
        h.ApplyRegen(0.5f, 10f);
        Assert.AreEqual(50f, h.CurrHP, 1e-4f);
    }

    [Test]
    public void MaxHP_HeavyNerf_StaysAboveZero()
    {
        PlayerStats p = MakePlayer();
        GameObject hgo = Track(new GameObject("H"));
        hgo.transform.SetParent(p.transform);
        HealthSystem h = hgo.AddComponent<HealthSystem>();
        h.maxHP = 100f;
        typeof(PlayerStats).GetField("isBaseInitialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, false);

        p.AddModifier(ValueType.HP, StatModifierMode.Multiply, -5f);
        Assert.AreEqual(50f, p.Get(ValueType.HP), 1e-4f); // 배율 하한 0.5
        p.AddModifier(ValueType.HP, StatModifierMode.Add, -1000f);
        Assert.AreEqual(0f, p.Get(ValueType.HP), 1e-4f);  // Get 은 0 미만을 0 으로
        // HealthSystem 은 최대 HP 를 최소 1 로 맞춘다 (HandlePlayerStatChanged)
        typeof(HealthSystem).GetMethod("HandlePlayerStatChanged", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(h, new object[] { p });
        Assert.AreEqual(1f, h.MaxHP, 1e-4f);
    }
    #endregion

    #region Monster effect
    [Test]
    public void MonsterEffect_ChangesCountAndHpTogether()
    {
        GameObject go = Track(new GameObject("Spawner"));
        go.SetActive(false);
        Spawner s = go.AddComponent<Spawner>();
        SetField(s, "HPRate", 1f);
        SetField(s, "damagerate", 1f);
        SetField(s, "speedRate", 1f);
        SetField(s, "currentWaveIndex", -1);

        SpawnerRewardEffect effect = AssetDatabase.LoadAssetAtPath<SpawnerRewardEffect>("Assets/prefab/Upgrade/Effects/MonsterCountHPEffect.asset");
        RewardData data = AssetDatabase.LoadAssetAtPath<RewardData>("Assets/prefab/Upgrade/PassiveBuff/MonsterDecrease.asset");
        Assert.NotNull(effect);
        Assert.NotNull(data);

        LogAssert.ignoreFailingMessages = true;
        effect.Apply(new RewardContext { Spawner = s }, data);

        Assert.AreEqual(0.95f, s.GetRate(SpawnModifierType.Count), 1e-4f);
        Assert.AreEqual(0.95f, s.GetRate(SpawnModifierType.HP), 1e-4f);
        Assert.AreEqual(1f, s.GetRate(SpawnModifierType.Damage), 1e-4f);
        Assert.AreEqual(1f, s.GetRate(SpawnModifierType.Speed), 1e-4f);
    }
    #endregion

    #region Assets / pools
    private static Dictionary<RewardData, float> ReadPool(RewardManager _rm, RewardPoolType _type)
    {
        var result = new Dictionary<RewardData, float>();
        SerializedProperty pools = new SerializedObject(_rm).FindProperty("pools");
        for (int i = 0; i < pools.arraySize; ++i)
        {
            SerializedProperty slot = pools.GetArrayElementAtIndex(i);
            if ((int)_type != slot.FindPropertyRelative("type").enumValueIndex)
            {
                continue;
            }
            SerializedProperty entries = slot.FindPropertyRelative("pool").FindPropertyRelative("entries");
            for (int e = 0; e < entries.arraySize; ++e)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                if (entry.FindPropertyRelative("item").objectReferenceValue is RewardData d)
                {
                    result[d] = entry.FindPropertyRelative("weight").floatValue;
                }
            }
        }
        return result;
    }

    [TestCase("PassiveBuff/MonsterDecrease", RewardPoolType.PassiveBuff, 1f, -0.05f)]
    [TestCase("PassiveBuff/DurationUpgrade", RewardPoolType.PassiveBuff, 1f, 0.1f)]
    [TestCase("PassiveBuff/ProjectileUpgrade", RewardPoolType.PassiveBuff, 0.3f, 1f)]
    [TestCase("PassiveBuff/HPRegenUpgrade", RewardPoolType.PassiveBuff, 1f, 0.5f)]
    [TestCase("PassiveNuff/HPRegenNuff", RewardPoolType.PassiveNerf, 1f, -0.3f)]
    [TestCase("PassiveNuff/MonsterIncreaseNuff", RewardPoolType.PassiveNerf, 1f, 0.03f)]
    [TestCase("PassiveNuff/DurationNuff", RewardPoolType.PassiveNerf, 1f, -0.06f)]
    public void Reward_RegisteredInPrefabPool(string _path, RewardPoolType _pool, float _weight, float _value)
    {
        RewardData data = AssetDatabase.LoadAssetAtPath<RewardData>($"Assets/prefab/Upgrade/{_path}.asset");
        Assert.NotNull(data, _path);
        Assert.IsTrue(data.TryGetValue(out float value));
        Assert.AreEqual(_value, value, 1e-4f);
        Assert.NotNull(data.GetEffect(), _path);

        RewardManager rm = AssetDatabase.LoadAssetAtPath<RewardManager>("Assets/prefab/Managers/RewardManager.prefab");
        Dictionary<RewardData, float> pool = ReadPool(rm, _pool);
        Assert.IsTrue(pool.ContainsKey(data), _path);
        Assert.AreEqual(_weight, pool[data], 1e-4f);
    }

    [Test]
    public void NoProjectileCountNerf()
    {
        RewardManager rm = AssetDatabase.LoadAssetAtPath<RewardManager>("Assets/prefab/Managers/RewardManager.prefab");
        foreach (RewardData d in ReadPool(rm, RewardPoolType.PassiveNerf).Keys)
        {
            Assert.IsFalse(d.TryGetKey(out ValueType key) && ValueType.ProjectileCount == key, d.name);
        }
    }
    #endregion
}
