using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 통계 시스템 EditMode 테스트
/// - EditMode 에서는 처리 코루틴이 시작되지 않으므로 Enqueue 후 Flush 로 집계를 확인한다
/// </summary>
public class StatisticsManagerTests
{
    private readonly List<Object> created = new List<Object>();

    private StatisticsManager manager;
    private int leftMonsterLayer;
    private int rightMonsterLayer;

    [SetUp]
    public void SetUp()
    {
        manager = MakeGo("Statistics").AddComponent<StatisticsManager>();
        leftMonsterLayer = LayerMask.NameToLayer("LMonster");
        rightMonsterLayer = LayerMask.NameToLayer("RMonster");
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

    [Test]
    public void Enqueue_IsDeferredUntilFlush()
    {
        manager.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 5f));

        Assert.AreEqual(1, manager.PendingCount);
        Assert.AreEqual(0f, manager.Get(PlayerSide.Left).DamageTaken);
        Assert.IsFalse(manager.IsProcessing);

        manager.Flush();

        Assert.AreEqual(0, manager.PendingCount);
        Assert.AreEqual(5f, manager.Get(PlayerSide.Left).DamageTaken);
    }

    [Test]
    public void Kill_IsAttributedByMonsterLayer()
    {
        Assume.That(0 <= leftMonsterLayer && 0 <= rightMonsterLayer, "LMonster / RMonster 레이어가 없음");

        GameObject leftMonster = MakeGo("LeftMonster");
        leftMonster.layer = leftMonsterLayer;
        GameObject rightMonster = MakeGo("RightMonster");
        rightMonster.layer = rightMonsterLayer;
        GameObject other = MakeGo("Other");
        other.layer = 0;

        Assert.IsTrue(manager.EnqueueKill(leftMonster));
        Assert.IsTrue(manager.EnqueueKill(leftMonster));
        Assert.IsTrue(manager.EnqueueKill(rightMonster));
        Assert.IsFalse(manager.EnqueueKill(other));
        Assert.IsFalse(manager.EnqueueKill(null));

        manager.Flush();

        Assert.AreEqual(2, manager.Get(PlayerSide.Left).Kills);
        Assert.AreEqual(1, manager.Get(PlayerSide.Right).Kills);
    }

    [Test]
    public void WeaponDamage_IsSummedPerWeaponAndSide()
    {
        Weapon sword = MakeGo("Sword(Clone)").AddComponent<Weapon>();
        Weapon bow = MakeGo("Bow").AddComponent<Weapon>();

        manager.EnqueueWeaponDamage(PlayerSide.Left, sword, 10f);
        manager.EnqueueWeaponDamage(PlayerSide.Left, sword, 15f);
        manager.EnqueueWeaponDamage(PlayerSide.Left, bow, 7f);
        manager.EnqueueWeaponDamage(PlayerSide.Right, sword, 3f);
        manager.EnqueueWeaponDamage(PlayerSide.Right, null, 100f);

        manager.Flush();

        IReadOnlyDictionary<string, float> left = manager.Get(PlayerSide.Left).WeaponDamage;
        IReadOnlyDictionary<string, float> right = manager.Get(PlayerSide.Right).WeaponDamage;

        Assert.AreEqual("Sword", manager.GetWeaponKey(sword));
        Assert.AreEqual(2, left.Count);
        Assert.AreEqual(25f, left["Sword"]);
        Assert.AreEqual(7f, left["Bow"]);
        Assert.AreEqual(1, right.Count);
        Assert.AreEqual(3f, right["Sword"]);
        Assert.AreEqual(32f, manager.Get(PlayerSide.Left).TotalWeaponDamage);

        List<KeyValuePair<string, float>> sorted = new List<KeyValuePair<string, float>>();
        manager.Get(PlayerSide.Left).GetSortedWeaponDamage(sorted);
        Assert.AreEqual("Sword", sorted[0].Key);
        Assert.AreEqual("Bow", sorted[1].Key);
    }

    [Test]
    public void DamageTakenAndExp_AreSeparatedBySide()
    {
        manager.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 4f));
        manager.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Right, 9f));
        manager.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 1f));
        manager.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Right, 12f));
        manager.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Right, 3f));

        manager.Flush();

        PlayerCombatStats left = manager.Get(PlayerSide.Left);
        PlayerCombatStats right = manager.Get(PlayerSide.Right);
        Assert.AreEqual(5f, left.DamageTaken);
        Assert.AreEqual(0f, left.Exp);
        Assert.AreEqual(9f, right.DamageTaken);
        Assert.AreEqual(15f, right.Exp);
    }

    [Test]
    public void ResetAll_ClearsStatsAndQueue()
    {
        manager.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Left, 10f));
        manager.Flush();
        manager.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Left, 10f));

        manager.ResetAll();
        manager.Flush();

        Assert.AreEqual(0, manager.PendingCount);
        Assert.AreEqual(0f, manager.Get(PlayerSide.Left).Exp);
    }

    [Test]
    public void Lock_IgnoresEnqueueUntilResetAll()
    {
        manager.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 5f));
        manager.Flush();
        manager.Lock();

        Assert.IsTrue(manager.IsLocked);

        manager.Enqueue(new StatRecord(STAT_TYPE.DamageTaken, PlayerSide.Left, 100f));
        manager.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Right, 100f));
        Assert.AreEqual(0, manager.PendingCount);

        manager.Flush();
        Assert.AreEqual(5f, manager.Get(PlayerSide.Left).DamageTaken);
        Assert.AreEqual(0f, manager.Get(PlayerSide.Right).Exp);

        manager.ResetAll();
        Assert.IsFalse(manager.IsLocked);

        manager.Enqueue(new StatRecord(STAT_TYPE.Exp, PlayerSide.Right, 3f));
        manager.Flush();
        Assert.AreEqual(3f, manager.Get(PlayerSide.Right).Exp);
    }

    private GameObject MakeGo(string _name)
    {
        GameObject go = new GameObject(_name);
        created.Add(go);
        return go;
    }
}
