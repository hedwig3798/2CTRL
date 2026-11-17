using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 정령 편대 / 가시 톱날 순수 로직 테스트
/// </summary>
public class DimensionWeapons_SpiritThornTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < created.Count; ++i)
        {
            if (null != created[i])
            {
                Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
    }

    private HealthSystem MakeHealth(string _name)
    {
        GameObject go = new GameObject(_name);
        created.Add(go);
        return go.AddComponent<HealthSystem>();
    }

    #region 정령 편대
    [Test]
    public void Spirit_SpawnCount_IsCapped()
    {
        Assert.AreEqual(1, SpiritSquadron.GetSpawnCount(0, 3, 1));
        Assert.AreEqual(2, SpiritSquadron.GetSpawnCount(1, 3, 2));
        Assert.AreEqual(1, SpiritSquadron.GetSpawnCount(2, 3, 5));
        Assert.AreEqual(0, SpiritSquadron.GetSpawnCount(3, 3, 1));
        Assert.AreEqual(0, SpiritSquadron.GetSpawnCount(4, 3, 1));
        Assert.AreEqual(0, SpiritSquadron.GetSpawnCount(0, 0, 1));
    }

    [Test]
    public void Drone_Count_IsSpiritMaxTimesPerSpirit()
    {
        Assert.AreEqual(3, SpiritSquadron.GetDroneCount(3, 1));
        Assert.AreEqual(6, SpiritSquadron.GetDroneCount(3, 2));
        Assert.AreEqual(0, SpiritSquadron.GetDroneCount(-1, 2));
    }

    [Test]
    public void Drone_Anchor_OrbitsSpiritsOrCenter()
    {
        Assert.AreEqual(-1, SpiritSquadron.GetDroneAnchorIndex(0, 0));
        Assert.AreEqual(0, SpiritSquadron.GetDroneAnchorIndex(0, 3));
        Assert.AreEqual(2, SpiritSquadron.GetDroneAnchorIndex(2, 3));
        Assert.AreEqual(1, SpiritSquadron.GetDroneAnchorIndex(4, 3));
    }

    [Test]
    public void Drone_Candidate_ExcludesSpiritTargets()
    {
        HealthSystem a = MakeHealth("A");
        HealthSystem b = MakeHealth("B");
        List<HealthSystem> spiritTargets = new List<HealthSystem> { a };

        Assert.IsFalse(SpiritSquadron.IsDroneCandidate(a, spiritTargets));
        Assert.IsTrue(SpiritSquadron.IsDroneCandidate(b, spiritTargets));
        Assert.IsTrue(SpiritSquadron.IsDroneCandidate(a, null));
        Assert.IsFalse(SpiritSquadron.IsDroneCandidate(null, spiritTargets));
    }

    [Test]
    public void Drone_Nearest_SkipsSpiritTarget()
    {
        HealthSystem near = MakeHealth("Near");
        HealthSystem far = MakeHealth("Far");
        StageQuadTree tree = new StageQuadTree();
        tree.Clear(new Rect(-50f, -50f, 100f, 100f));
        tree.Insert(new StageTarget(new Vector2(1f, 0f), 0f, PlayerSide.Left, near));
        tree.Insert(new StageTarget(new Vector2(5f, 0f), 0f, PlayerSide.Right, far));

        List<HealthSystem> spiritTargets = new List<HealthSystem> { near };
        Assert.IsTrue(tree.QueryNearest(Vector2.zero, 100f, t => SpiritSquadron.IsDroneCandidate(t.health, spiritTargets), out StageTarget pick));
        Assert.AreSame(far, pick.health);

        spiritTargets.Add(far);
        Assert.IsFalse(tree.QueryNearest(Vector2.zero, 100f, t => SpiritSquadron.IsDroneCandidate(t.health, spiritTargets), out _));
    }

    [Test]
    public void Spirit_Contact_UsesSurfaceDistance()
    {
        Assert.IsTrue(SpiritSquadron.IsInContact(Vector2.zero, new Vector2(1f, 0f), 0.7f, 0.35f));
        Assert.IsFalse(SpiritSquadron.IsInContact(Vector2.zero, new Vector2(2f, 0f), 0.7f, 0.35f));
    }
    #endregion

    #region 가시 톱날
    [Test]
    public void Saw_Count_IsTripleOfOriginal()
    {
        Assert.AreEqual(6, ThornSaw.GetSawCount(2, 3));
        Assert.AreEqual(9, ThornSaw.GetSawCount(3, 3));
        Assert.AreEqual(0, ThornSaw.GetSawCount(0, 3));
        Assert.AreEqual(0, ThornSaw.GetSawCount(-2, 3));
    }

    [Test]
    public void Saw_Path_CrossesWholeRect()
    {
        Rect rect = new Rect(-20f, -5f, 40f, 10f);

        ThornSaw.GetSawPath(rect, true, 0.5f, 1f, 0.5f, out Vector2 start, out Vector2 dir, out float travel);
        Assert.AreEqual(-21f, start.x, 1e-4f);
        Assert.AreEqual(0f, start.y, 1e-4f);
        Assert.AreEqual(Vector2.right, dir);
        Assert.AreEqual(42f, travel, 1e-4f);
        Assert.AreEqual(21f, (start + dir * travel).x, 1e-4f);

        ThornSaw.GetSawPath(rect, false, 0f, 1f, 0.5f, out start, out dir, out travel);
        Assert.AreEqual(21f, start.x, 1e-4f);
        Assert.AreEqual(-4.5f, start.y, 1e-4f);
        Assert.AreEqual(Vector2.left, dir);

        ThornSaw.GetSawPath(rect, true, 1f, 1f, 0.5f, out start, out _, out _);
        Assert.AreEqual(4.5f, start.y, 1e-4f);

        // 패딩이 높이보다 크면 중앙
        ThornSaw.GetSawPath(rect, true, 1f, 1f, 20f, out start, out _, out _);
        Assert.AreEqual(0f, start.y, 1e-4f);
    }

    [Test]
    public void Vine_Drops_EverySpacing()
    {
        float accum = 0f;
        int drops = 0;
        for (int frame = 0; frame < 10; ++frame)
        {
            accum += 0.4f;
            while (true == ThornSaw.TryConsumeVineDrop(ref accum, 1f, out float back))
            {
                Assert.GreaterOrEqual(back, 0f);
                Assert.Less(back, 1f);
                ++drops;
            }
        }
        Assert.AreEqual(4, drops);   // 4.0 이동 → 4개
        Assert.AreEqual(0f, accum, 1e-4f);
    }

    [Test]
    public void Vine_Drops_LargeStep_MultipleWithOffsets()
    {
        float accum = 2.5f;
        Assert.IsTrue(ThornSaw.TryConsumeVineDrop(ref accum, 1f, out float back));
        Assert.AreEqual(1.5f, back, 1e-4f);
        Assert.IsTrue(ThornSaw.TryConsumeVineDrop(ref accum, 1f, out back));
        Assert.AreEqual(0.5f, back, 1e-4f);
        Assert.IsFalse(ThornSaw.TryConsumeVineDrop(ref accum, 1f, out _));
    }

    [Test]
    public void Vine_Drops_ZeroSpacing_Never()
    {
        float accum = 100f;
        Assert.IsFalse(ThornSaw.TryConsumeVineDrop(ref accum, 0f, out _));
        Assert.AreEqual(100f, accum);
    }

    [Test]
    public void Saw_HitsSameEnemyOnce()
    {
        HealthSystem a = MakeHealth("A");
        HealthSystem b = MakeHealth("B");
        List<HealthSystem> hit = new List<HealthSystem>();

        Assert.IsFalse(ThornSaw.CheckAndMarkHit(hit, a));
        Assert.IsTrue(ThornSaw.CheckAndMarkHit(hit, a));
        Assert.IsFalse(ThornSaw.CheckAndMarkHit(hit, b));
        Assert.IsTrue(ThornSaw.CheckAndMarkHit(hit, null));
        Assert.AreEqual(2, hit.Count);
    }
    #endregion
}
