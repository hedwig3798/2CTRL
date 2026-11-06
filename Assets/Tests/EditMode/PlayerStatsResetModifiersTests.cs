using NUnit.Framework;
using UnityEngine;

/// <summary>
/// PlayerStats.ResetModifiers 단위 테스트
/// </summary>
public class PlayerStatsResetModifiersTests
{
    private GameObject go;

    [TearDown]
    public void TearDown()
    {
        if (null != go)
        {
            Object.DestroyImmediate(go);
        }
    }

    private PlayerStats Make()
    {
        go = new GameObject("StatsTest");
        return go.AddComponent<PlayerStats>();
    }

    [Test]
    public void ResetModifiers_ClearsAddAndMultiply_AndNotifies()
    {
        PlayerStats s = Make();
        s.AddModifier(ValueType.Damage, StatModifierMode.Multiply, 0.15f);
        s.AddModifier(ValueType.ProjectileCount, StatModifierMode.Add, 2f);
        Assert.AreEqual(0.15f, s.GetMultiply(ValueType.Damage), 1e-5f);
        Assert.AreEqual(2f, s.GetAdd(ValueType.ProjectileCount), 1e-5f);

        int notified = 0;
        s.OnStatChanged += _ => ++notified;
        s.ResetModifiers();

        Assert.AreEqual(0f, s.GetMultiply(ValueType.Damage));
        Assert.AreEqual(0f, s.GetAdd(ValueType.ProjectileCount));
        Assert.AreEqual(1, notified);
    }

    [Test]
    public void ResetModifiers_OnEmpty_IsSafe_AndKeepsBase()
    {
        PlayerStats s = Make();
        float baseHp = s.GetBase(ValueType.HP);
        s.AddModifier(ValueType.HP, StatModifierMode.Add, 10f);
        s.ResetModifiers();
        s.ResetModifiers();
        Assert.AreEqual(baseHp, s.GetBase(ValueType.HP));
        Assert.AreEqual(0f, s.GetAdd(ValueType.HP));
    }
}
