using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 화면 쿼드트리 순수 로직: 삽입·분할, 원/사각형/선분/최근접 쿼리 정확성, 깊이 한계, 선형 폴백과 트리 결과 일치
/// </summary>
public class StageQuadTreeTests
{
    private static readonly Rect BOUNDS = new Rect(-50f, -50f, 100f, 100f);

    private static StageQuadTree MakeTree(int _linearThreshold = 0)
    {
        StageQuadTree tree = new StageQuadTree(8, 6, _linearThreshold);
        tree.Clear(BOUNDS);
        return tree;
    }

    private static StageTarget T(float _x, float _y, float _r = 0f)
    {
        return new StageTarget(new Vector2(_x, _y), _r, PlayerSide.Left, null);
    }

    private static List<Vector2> Positions(List<StageTarget> _list)
    {
        List<Vector2> result = new List<Vector2>();
        foreach (StageTarget t in _list)
        {
            result.Add(t.stagePos);
        }
        return result;
    }

    /// <summary>
    /// 결정적 의사 난수 점들
    /// </summary>
    private static List<StageTarget> RandomTargets(int _count, int _seed)
    {
        System.Random rng = new System.Random(_seed);
        List<StageTarget> list = new List<StageTarget>();
        for (int i = 0; i < _count; ++i)
        {
            float x = (float)(rng.NextDouble() * 100.0 - 50.0);
            float y = (float)(rng.NextDouble() * 100.0 - 50.0);
            float r = (float)(rng.NextDouble() * 1.5);
            list.Add(T(x, y, r));
        }
        return list;
    }

    #region 삽입
    [Test]
    public void Insert_UnderCapacity_NoSplit()
    {
        StageQuadTree tree = MakeTree();
        for (int i = 0; i < 8; ++i)
        {
            tree.Insert(T(i, i));
        }
        Assert.AreEqual(8, tree.Count);
        Assert.AreEqual(1, tree.NodeCount);
    }

    [Test]
    public void Insert_OverCapacity_Splits()
    {
        StageQuadTree tree = MakeTree();
        for (int i = 0; i < 9; ++i)
        {
            tree.Insert(T(-40f + i * 10f, -40f + i * 10f));
        }
        Assert.AreEqual(9, tree.Count);
        Assert.Greater(tree.NodeCount, 1);
    }

    [Test]
    public void Insert_SamePoint_StopsAtMaxDepth()
    {
        StageQuadTree tree = MakeTree();
        for (int i = 0; i < 200; ++i)
        {
            tree.Insert(T(1f, 1f));
        }
        Assert.AreEqual(200, tree.Count);
        Assert.LessOrEqual(tree.DeepestDepth, 6);
        Assert.AreEqual(6, tree.DeepestDepth);

        List<StageTarget> result = new List<StageTarget>();
        Assert.AreEqual(200, tree.QueryCircle(new Vector2(1f, 1f), 0.01f, result));
    }

    [Test]
    public void Clear_RemovesAll()
    {
        StageQuadTree tree = MakeTree();
        foreach (StageTarget t in RandomTargets(100, 1))
        {
            tree.Insert(t);
        }
        tree.Clear(BOUNDS);
        Assert.AreEqual(0, tree.Count);
        Assert.AreEqual(1, tree.NodeCount);

        List<StageTarget> result = new List<StageTarget>();
        Assert.AreEqual(0, tree.QueryCircle(Vector2.zero, 100f, result));
    }
    #endregion

    #region 원 쿼리
    [Test]
    public void QueryCircle_IncludesTargetRadius()
    {
        StageQuadTree tree = MakeTree();
        tree.Insert(T(5f, 0f, 1f));
        tree.Insert(T(5f, 0f, 0f));

        List<StageTarget> result = new List<StageTarget>();
        // 중심 거리 5, 쿼리 반지름 4.5 → 반지름 1 짜리만 겹침
        Assert.AreEqual(1, tree.QueryCircle(Vector2.zero, 4.5f, result));
        Assert.AreEqual(1f, result[0].radius, 1e-4f);
    }

    [Test]
    public void QueryCircle_ClearsResultFirst()
    {
        StageQuadTree tree = MakeTree();
        tree.Insert(T(0f, 0f));
        List<StageTarget> result = new List<StageTarget> { T(9f, 9f) };
        tree.QueryCircle(Vector2.zero, 1f, result);
        Assert.AreEqual(1, result.Count);
    }

    [Test]
    public void QueryCircle_MatchesBruteForce()
    {
        List<StageTarget> all = RandomTargets(500, 7);
        StageQuadTree tree = MakeTree();
        foreach (StageTarget t in all)
        {
            tree.Insert(t);
        }

        List<StageTarget> result = new List<StageTarget>();
        Vector2[] centers = { Vector2.zero, new Vector2(30f, -20f), new Vector2(-49f, 49f), new Vector2(60f, 0f) };
        float[] radii = { 3f, 10f, 0.5f, 12f };
        for (int q = 0; q < centers.Length; ++q)
        {
            int expected = 0;
            foreach (StageTarget t in all)
            {
                if (Vector2.Distance(t.stagePos, centers[q]) <= radii[q] + t.radius)
                {
                    ++expected;
                }
            }
            Assert.AreEqual(expected, tree.QueryCircle(centers[q], radii[q], result), "query " + q);
        }
    }

    [Test]
    public void QueryCircle_OutOfBoundsTarget_Found()
    {
        StageQuadTree tree = MakeTree();
        foreach (StageTarget t in RandomTargets(50, 3))
        {
            tree.Insert(t);
        }
        tree.Insert(T(80f, 80f, 0.5f));

        List<StageTarget> result = new List<StageTarget>();
        Assert.AreEqual(1, tree.QueryCircle(new Vector2(80f, 80f), 0.1f, result));
    }

    [Test]
    public void QueryCircle_LargeRadiusTargetInOtherCell_Found()
    {
        StageQuadTree tree = MakeTree();
        // 다른 칸의 점들로 분할을 일으킨다
        foreach (StageTarget t in RandomTargets(100, 11))
        {
            tree.Insert(new StageTarget(t.stagePos, 0f, PlayerSide.Left, null));
        }
        // 중심은 (-20,-20) 칸, 반지름 30 이라 우상단 칸의 (1,1) 까지 덮는다 (거리 약 29.7)
        tree.Insert(T(-20f, -20f, 30f));

        List<StageTarget> result = new List<StageTarget>();
        tree.QueryCircle(new Vector2(1f, 1f), 0.1f, result);
        Assert.IsTrue(result.Exists(t => Mathf.Approximately(30f, t.radius)));
    }
    #endregion

    #region 사각형·선분
    [Test]
    public void QueryRect_MatchesBruteForce()
    {
        List<StageTarget> all = RandomTargets(400, 21);
        StageQuadTree tree = MakeTree();
        foreach (StageTarget t in all)
        {
            tree.Insert(t);
        }

        Rect rect = new Rect(-10f, 5f, 25f, 15f);
        int expected = 0;
        foreach (StageTarget t in all)
        {
            if (true == BossSpace.CircleOverlapsRect(t.stagePos, t.radius, rect))
            {
                ++expected;
            }
        }

        List<StageTarget> result = new List<StageTarget>();
        Assert.AreEqual(expected, tree.QueryRect(rect, result));
    }

    [Test]
    public void QuerySegment_HitsWithinHalfWidthPlusRadius()
    {
        StageQuadTree tree = MakeTree();
        tree.Insert(T(0f, 1f, 0f));     // 선분에서 1
        tree.Insert(T(0f, 2f, 0.6f));   // 선분에서 2, 반지름 0.6 → 1.4
        tree.Insert(T(0f, -3f, 0f));    // 선분에서 3
        tree.Insert(T(20f, 0f, 0f));    // 선분 끝(10,0) 밖 10

        List<StageTarget> result = new List<StageTarget>();
        Assert.AreEqual(2, tree.QuerySegment(new Vector2(-10f, 0f), new Vector2(10f, 0f), 1.5f, result));
        List<Vector2> pos = Positions(result);
        Assert.Contains(new Vector2(0f, 1f), pos);
        Assert.Contains(new Vector2(0f, 2f), pos);
    }

    [Test]
    public void QuerySegment_MatchesBruteForce()
    {
        List<StageTarget> all = RandomTargets(500, 33);
        StageQuadTree tree = MakeTree();
        foreach (StageTarget t in all)
        {
            tree.Insert(t);
        }

        Vector2 a = new Vector2(-45f, -30f);
        Vector2 b = new Vector2(40f, 35f);
        float halfWidth = 2f;
        int expected = 0;
        foreach (StageTarget t in all)
        {
            float reach = halfWidth + t.radius;
            if (StageQuadTree.SegmentDistSq(t.stagePos, a, b) <= reach * reach)
            {
                ++expected;
            }
        }

        List<StageTarget> result = new List<StageTarget>();
        Assert.AreEqual(expected, tree.QuerySegment(a, b, halfWidth, result));
    }

    [Test]
    public void SegmentDistSq_DegenerateSegment_IsPointDistance()
    {
        Assert.AreEqual(25f, StageQuadTree.SegmentDistSq(new Vector2(3f, 4f), Vector2.zero, Vector2.zero), 1e-4f);
    }
    #endregion

    #region 최근접
    [Test]
    public void QueryNearest_FindsClosestEdge()
    {
        StageQuadTree tree = MakeTree();
        tree.Insert(T(4f, 0f, 0f));    // 거리 4
        tree.Insert(T(-6f, 0f, 3f));   // 중심 6, 가장자리 3
        tree.Insert(T(0f, 10f, 0f));

        Assert.IsTrue(tree.QueryNearest(Vector2.zero, 20f, (System.Predicate<StageTarget>)null, out StageTarget nearest));
        Assert.AreEqual(-6f, nearest.stagePos.x, 1e-4f);
    }

    [Test]
    public void QueryNearest_RespectsMaxRadius()
    {
        StageQuadTree tree = MakeTree();
        tree.Insert(T(10f, 0f, 0f));
        Assert.IsFalse(tree.QueryNearest(Vector2.zero, 5f, (System.Predicate<StageTarget>)null, out _));
        Assert.IsTrue(tree.QueryNearest(Vector2.zero, 10f, (System.Predicate<StageTarget>)null, out _));
    }

    [Test]
    public void QueryNearest_FilterSkips()
    {
        StageQuadTree tree = MakeTree();
        tree.Insert(T(1f, 0f));
        tree.Insert(T(2f, 0f));
        Assert.IsTrue(tree.QueryNearest(Vector2.zero, 10f, t => t.stagePos.x > 1.5f, out StageTarget nearest));
        Assert.AreEqual(2f, nearest.stagePos.x, 1e-4f);
    }

    [Test]
    public void QueryNearest_MatchesBruteForce()
    {
        List<StageTarget> all = RandomTargets(600, 99);
        StageQuadTree tree = MakeTree();
        foreach (StageTarget t in all)
        {
            tree.Insert(t);
        }

        Vector2[] centers = { Vector2.zero, new Vector2(45f, 45f), new Vector2(-30f, 12f), new Vector2(70f, -70f) };
        foreach (Vector2 c in centers)
        {
            float best = float.MaxValue;
            foreach (StageTarget t in all)
            {
                best = Mathf.Min(best, Mathf.Max(0f, Vector2.Distance(t.stagePos, c) - t.radius));
            }

            Assert.IsTrue(tree.QueryNearest(c, 1000f, (System.Predicate<StageTarget>)null, out StageTarget nearest));
            float got = Mathf.Max(0f, Vector2.Distance(nearest.stagePos, c) - nearest.radius);
            Assert.AreEqual(best, got, 1e-4f, "center " + c);
        }
    }
    #endregion

    #region 선형 폴백
    [Test]
    public void LinearFallback_SameResultsAsTree()
    {
        List<StageTarget> all = RandomTargets(20, 5);
        StageQuadTree linear = new StageQuadTree(8, 6, 32);
        StageQuadTree treeOnly = new StageQuadTree(8, 6, 0);
        linear.Clear(BOUNDS);
        treeOnly.Clear(BOUNDS);
        foreach (StageTarget t in all)
        {
            linear.Insert(t);
            treeOnly.Insert(t);
        }

        List<StageTarget> a = new List<StageTarget>();
        List<StageTarget> b = new List<StageTarget>();
        Assert.AreEqual(linear.QueryCircle(Vector2.zero, 20f, a), treeOnly.QueryCircle(Vector2.zero, 20f, b));
        Assert.AreEqual(linear.QueryRect(new Rect(-20f, -20f, 30f, 30f), a), treeOnly.QueryRect(new Rect(-20f, -20f, 30f, 30f), b));
        Assert.AreEqual(linear.QuerySegment(new Vector2(-50f, 0f), new Vector2(50f, 0f), 5f, a),
            treeOnly.QuerySegment(new Vector2(-50f, 0f), new Vector2(50f, 0f), 5f, b));
    }
    #endregion
}
