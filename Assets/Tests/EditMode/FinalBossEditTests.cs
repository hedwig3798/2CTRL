using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 최종 보스 순수 로직: BossSpace 매핑, BossAttackResolver, FinalBossPatterns, PlayerScreenClamp.Clamp
/// </summary>
public class FinalBossEditTests
{
    private const float EPS = 1e-3f;

    #region BossSpace
    [Test]
    public void OrthoWorldRect_CenterSizeAspect()
    {
        Rect r = BossSpace.OrthoWorldRect(new Vector2(2f, 3f), 5f, 2f);
        Assert.AreEqual(2f, r.center.x, EPS);
        Assert.AreEqual(3f, r.center.y, EPS);
        Assert.AreEqual(10f, r.height, EPS);
        Assert.AreEqual(20f, r.width, EPS);
    }

    [Test]
    public void OrthoWorldRect_NegativeInputs_ClampedToZero()
    {
        Rect r = BossSpace.OrthoWorldRect(Vector2.zero, -3f, -1f);
        Assert.AreEqual(0f, r.width, EPS);
        Assert.AreEqual(0f, r.height, EPS);
    }

    [Test]
    public void MapPoint_Corners_AndRoundTrip_WithPartialRect()
    {
        // 레터박스처럼 목적 rect 가 화면 일부
        Rect world = BossSpace.OrthoWorldRect(new Vector2(10f, -4f), 6f, 16f / 9f);
        Rect pixel = new Rect(0f, 120f, 1920f, 840f);

        Vector2 mn = BossSpace.MapPoint(world.min, world, pixel);
        Vector2 mx = BossSpace.MapPoint(world.max, world, pixel);
        Assert.AreEqual(pixel.min.x, mn.x, EPS);
        Assert.AreEqual(pixel.min.y, mn.y, EPS);
        Assert.AreEqual(pixel.max.x, mx.x, EPS);
        Assert.AreEqual(pixel.max.y, mx.y, EPS);

        Vector2[] pts = { new Vector2(3f, -7f), new Vector2(12.5f, 0f), new Vector2(-50f, 40f) };
        foreach (Vector2 p in pts)
        {
            Vector2 back = BossSpace.MapPoint(BossSpace.MapPoint(p, world, pixel), pixel, world);
            Assert.AreEqual(p.x, back.x, EPS);
            Assert.AreEqual(p.y, back.y, EPS);
        }
    }

    [Test]
    public void MapPoint_ZeroSizeSource_ReturnsTargetCenter()
    {
        Vector2 p = BossSpace.MapPoint(new Vector2(1f, 1f), new Rect(0f, 0f, 0f, 0f), new Rect(10f, 20f, 4f, 6f));
        Assert.AreEqual(12f, p.x, EPS);
        Assert.AreEqual(23f, p.y, EPS);
    }

    [Test]
    public void MapRect_RoundTrip_AndFlipStaysNormalized()
    {
        Rect a = new Rect(-5f, -3f, 10f, 6f);
        Rect b = new Rect(100f, 50f, 400f, 300f);
        Rect r = new Rect(-1f, 0f, 2f, 1f);
        Rect back = BossSpace.MapRect(BossSpace.MapRect(r, a, b), b, a);
        Assert.AreEqual(r.xMin, back.xMin, EPS);
        Assert.AreEqual(r.yMin, back.yMin, EPS);
        Assert.AreEqual(r.width, back.width, EPS);
        Assert.AreEqual(r.height, back.height, EPS);

        Rect flipped = BossSpace.MapRect(r, a, new Rect(0f, 10f, 10f, -10f));
        Assert.GreaterOrEqual(flipped.width, 0f);
        Assert.GreaterOrEqual(flipped.height, 0f);
    }

    [Test]
    public void Union_And_Inset()
    {
        Rect u = BossSpace.Union(new Rect(0f, 0f, 2f, 2f), new Rect(5f, -1f, 1f, 1f));
        Assert.AreEqual(0f, u.xMin, EPS);
        Assert.AreEqual(-1f, u.yMin, EPS);
        Assert.AreEqual(6f, u.xMax, EPS);
        Assert.AreEqual(2f, u.yMax, EPS);

        Rect i = BossSpace.Inset(new Rect(0f, 0f, 10f, 4f), 1f);
        Assert.AreEqual(8f, i.width, EPS);
        Assert.AreEqual(2f, i.height, EPS);
        Assert.AreEqual(5f, i.center.x, EPS);
        Assert.AreEqual(2f, i.center.y, EPS);

        Rect over = BossSpace.Inset(new Rect(0f, 0f, 10f, 4f), 100f);
        Assert.AreEqual(0f, over.width, EPS);
        Assert.AreEqual(0f, over.height, EPS);
    }

    [Test]
    public void CircleOverlapsRect_InsideEdgeCornerOutside()
    {
        Rect r = new Rect(0f, 0f, 4f, 4f);
        Assert.IsTrue(BossSpace.CircleOverlapsRect(new Vector2(2f, 2f), 0.1f, r), "inside");
        Assert.IsTrue(BossSpace.CircleOverlapsRect(new Vector2(5f, 2f), 1f, r), "touch edge");
        Assert.IsFalse(BossSpace.CircleOverlapsRect(new Vector2(5.01f, 2f), 1f, r), "just outside edge");
        float d = 1f / Mathf.Sqrt(2f);
        Assert.IsTrue(BossSpace.CircleOverlapsRect(new Vector2(4f + d, 4f + d), 1.0001f, r), "touch corner");
        Assert.IsFalse(BossSpace.CircleOverlapsRect(new Vector2(4f + d, 4f + d), 0.99f, r), "outside corner");
        Assert.IsTrue(BossSpace.CircleOverlapsRect(new Vector2(-1f, -1f), 0f, new Rect(-1f, -1f, 1f, 1f)), "zero radius on corner");
    }
    #endregion

    #region BossAttackResolver
    [Test]
    public void Circle_Boundary()
    {
        Assert.IsTrue(BossAttackResolver.CircleContainsPoint(Vector2.zero, 1f, new Vector2(1f, 0f)), "boundary inclusive");
        Assert.IsFalse(BossAttackResolver.CircleContainsPoint(Vector2.zero, 1f, new Vector2(1.01f, 0f)));
        Assert.IsTrue(BossAttackResolver.CircleContainsPoint(Vector2.zero, 1f, new Vector2(1.3f, 0f), 0.3f), "point radius");
        Assert.IsFalse(BossAttackResolver.CircleContainsPoint(Vector2.zero, -1f, new Vector2(0.1f, 0f)), "negative radius = 0");
        Assert.IsTrue(BossAttackResolver.CircleContainsPoint(Vector2.zero, 0f, Vector2.zero));
    }

    [Test]
    public void CircleOverlapsCircle_SumOfRadii()
    {
        Assert.IsTrue(BossAttackResolver.CircleOverlapsCircle(Vector2.zero, 1f, new Vector2(3f, 0f), 2f));
        Assert.IsFalse(BossAttackResolver.CircleOverlapsCircle(Vector2.zero, 1f, new Vector2(3.01f, 0f), 2f));
    }

    [Test]
    public void DistanceToSegment_EndpointsMiddleDegenerate()
    {
        Vector2 a = new Vector2(0f, 0f);
        Vector2 b = new Vector2(10f, 0f);
        Assert.AreEqual(3f, BossAttackResolver.DistanceToSegment(new Vector2(5f, 3f), a, b), EPS);
        Assert.AreEqual(5f, BossAttackResolver.DistanceToSegment(new Vector2(-3f, 4f), a, b), EPS);
        Assert.AreEqual(5f, BossAttackResolver.DistanceToSegment(new Vector2(13f, 4f), a, b), EPS);
        Assert.AreEqual(5f, BossAttackResolver.DistanceToSegment(new Vector2(3f, 4f), a, a), EPS, "zero length");
    }

    [Test]
    public void Capsule_Boundary()
    {
        Vector2 a = Vector2.zero;
        Vector2 b = new Vector2(10f, 0f);
        Assert.IsTrue(BossAttackResolver.CapsuleContainsPoint(a, b, 2f, new Vector2(5f, 1f)), "half width boundary");
        Assert.IsFalse(BossAttackResolver.CapsuleContainsPoint(a, b, 2f, new Vector2(5f, 1.01f)));
        Assert.IsTrue(BossAttackResolver.CapsuleContainsPoint(a, b, 2f, new Vector2(5f, 1.3f), 0.3f));
        Assert.IsTrue(BossAttackResolver.CapsuleContainsPoint(a, b, 2f, new Vector2(11f, 0f)), "end cap");
        Assert.IsFalse(BossAttackResolver.CapsuleContainsPoint(a, b, 2f, new Vector2(11.01f, 0f)));
        Assert.IsFalse(BossAttackResolver.CapsuleContainsPoint(a, b, -2f, new Vector2(5f, 0.1f)), "negative width");
    }
    #endregion

    #region FinalBossPatterns
    [Test]
    public void PhaseFor_Boundaries()
    {
        Assert.AreEqual(0, FinalBossPatterns.PhaseFor(1f, 0.66f, 0.33f));
        Assert.AreEqual(0, FinalBossPatterns.PhaseFor(0.67f, 0.66f, 0.33f));
        Assert.AreEqual(1, FinalBossPatterns.PhaseFor(0.66f, 0.66f, 0.33f), "66% exact = phase 2");
        Assert.AreEqual(1, FinalBossPatterns.PhaseFor(0.34f, 0.66f, 0.33f));
        Assert.AreEqual(2, FinalBossPatterns.PhaseFor(0.33f, 0.66f, 0.33f), "33% exact = phase 3");
        Assert.AreEqual(2, FinalBossPatterns.PhaseFor(0f, 0.66f, 0.33f));
    }

    [Test]
    public void TelegraphTime_MinimumApplied()
    {
        Assert.AreEqual(1f, FinalBossPatterns.TelegraphTime(1f, 0.85f, 1f), EPS);
        Assert.AreEqual(1f, FinalBossPatterns.TelegraphTime(0.1f, 1f, 1f), EPS);
        Assert.AreEqual(1.5f, FinalBossPatterns.TelegraphTime(3f, 0.5f, 1f), EPS);
        Assert.GreaterOrEqual(FinalBossPatterns.TelegraphTime(1.2f, 0.85f, 1f), 1f);
    }

    [Test]
    public void PickPattern_NeverRepeatsLast_Simulation()
    {
        System.Random rng = new System.Random(1234);
        for (int phase = 0; phase <= 2; ++phase)
        {
            FinalBossPattern last = FinalBossPattern.None;
            for (int i = 0; i < 3000; ++i)
            {
                FinalBossPattern next = FinalBossPatterns.PickPattern(last, phase, 0.5f, (float)rng.NextDouble(), (float)rng.NextDouble());
                Assert.AreNotEqual(FinalBossPattern.None, next);
                Assert.AreNotEqual(last, next, "phase " + phase + " i " + i);
                last = next;
            }
        }
    }

    [Test]
    public void PickPattern_Extremes_NoOutOfRange()
    {
        foreach (float rp in new[] { 0f, 0.9999f, 1f })
        {
            FinalBossPattern p = FinalBossPatterns.PickPattern(FinalBossPattern.Meteor, 0, 0f, 0f, rp);
            Assert.AreNotEqual(FinalBossPattern.Meteor, p);
            Assert.AreNotEqual(FinalBossPattern.None, p);
        }
    }

    [Test]
    public void PickPattern_Combo_OnlyInPhase3_AndByChance()
    {
        Assert.IsTrue(IsCombo(FinalBossPatterns.PickPattern(FinalBossPattern.None, 2, 0.5f, 0.49f, 0f)));
        Assert.IsFalse(IsCombo(FinalBossPatterns.PickPattern(FinalBossPattern.None, 2, 0.5f, 0.5f, 0f)), "r == chance is base");
        Assert.IsFalse(IsCombo(FinalBossPatterns.PickPattern(FinalBossPattern.None, 1, 1f, 0f, 0f)), "no combo in phase 2");
        Assert.IsFalse(IsCombo(FinalBossPatterns.PickPattern(FinalBossPattern.None, 0, 1f, 0f, 0f)));
    }

    private static bool IsCombo(FinalBossPattern _p)
    {
        return FinalBossPattern.LaserMeteor == _p || FinalBossPattern.DashMeteor == _p;
    }

    private static readonly Rect LeftArea = new Rect(-20f, -5f, 16f, 10f);
    private static readonly Rect RightArea = new Rect(4f, -5f, 16f, 10f);
    private static readonly Rect StageArea = new Rect(-20f, -5f, 40f, 10f);

    [Test]
    public void GenerateLasers_EachSideHasSafeGap()
    {
        for (int seed = 0; seed < 40; ++seed)
        {
            List<LaserLine> lines = new List<LaserLine>();
            int n = FinalBossPatterns.GenerateLasers(lines, 5, StageArea, LeftArea, RightArea, 0.8f, 2f, 30, 0.4f, new System.Random(seed));
            Assert.AreEqual(n, lines.Count, "count matches seed " + seed);
            Assert.LessOrEqual(n, 5);
            Assert.Greater(n, 0, "seed " + seed);
            Assert.IsTrue(FinalBossPatterns.HasSafeSpot(lines, LeftArea, 0.4f, 1f, 0.4f), "left seed " + seed);
            Assert.IsTrue(FinalBossPatterns.HasSafeSpot(lines, RightArea, 0.4f, 1f, 0.4f), "right seed " + seed);
        }
    }

    [Test]
    public void GenerateLasers_ImpossibleGap_ReducesDownToZero()
    {
        List<LaserLine> lines = new List<LaserLine>();
        int n = FinalBossPatterns.GenerateLasers(lines, 4, StageArea, LeftArea, RightArea, 0.8f, 50f, 3, 0.4f, new System.Random(1));
        Assert.AreEqual(0, n);
        Assert.AreEqual(0, lines.Count);
    }

    [Test]
    public void GenerateLasers_ZeroCount_ReturnsZero()
    {
        List<LaserLine> lines = new List<LaserLine>();
        Assert.AreEqual(0, FinalBossPatterns.GenerateLasers(lines, 0, StageArea, LeftArea, RightArea, 0.8f, 2f, 5, 0.4f, new System.Random(1)));
    }

    [Test]
    public void GenerateLasers_ManyLines_CountReducedButStillSafe()
    {
        List<LaserLine> lines = new List<LaserLine>();
        int n = FinalBossPatterns.GenerateLasers(lines, 200, StageArea, LeftArea, RightArea, 3f, 2f, 5, 0.4f, new System.Random(7));
        Assert.Less(n, 200);
        if (n > 0)
        {
            Assert.IsTrue(FinalBossPatterns.HasSafeSpot(lines, LeftArea, 1.5f, 1f, 0.4f));
            Assert.IsTrue(FinalBossPatterns.HasSafeSpot(lines, RightArea, 1.5f, 1f, 0.4f));
        }
    }

    [Test]
    public void HasSafeSpot_NoLines_True_AreaTooSmall_False()
    {
        Assert.IsTrue(FinalBossPatterns.HasSafeSpot(new List<LaserLine>(), LeftArea, 0.4f, 1f, 0.4f));
        Assert.IsFalse(FinalBossPatterns.HasSafeSpot(new List<LaserLine>(), new Rect(0f, 0f, 1f, 1f), 0.4f, 1f, 0.4f));
    }

    [Test]
    public void HasSafeSpot_FullCoverLines_False()
    {
        List<LaserLine> lines = new List<LaserLine>();
        for (float y = -5f; y <= 5f; y += 1f)
        {
            lines.Add(new LaserLine(new Vector2(-30f, y), new Vector2(30f, y)));
        }
        Assert.IsFalse(FinalBossPatterns.HasSafeSpot(lines, LeftArea, 0.4f, 1f, 0.4f));
    }

    [Test]
    public void HasSafeSpotFast_NeverStricterNorLooserThanPrecise_Random()
    {
        System.Random rng = new System.Random(99);
        int falseCount = 0;
        for (int i = 0; i < 400; ++i)
        {
            List<LaserLine> lines = new List<LaserLine>();
            int n = rng.Next(0, 25);
            for (int k = 0; k < n; ++k)
            {
                lines.Add(FinalBossPatterns.RandomLine(StageArea, rng));
            }
            float half = 0.2f + (float)rng.NextDouble();
            bool precise = FinalBossPatterns.HasSafeSpot(lines, LeftArea, half, 1f, 0.4f);
            bool fast = FinalBossPatterns.HasSafeSpotFast(lines, LeftArea, half, 1f, 0.4f, 1.2f);
            if (false == precise)
            {
                ++falseCount;
            }
            // Fast=false 이면 정밀도 false (필요조건), 최종 판정은 정밀 검사와 같아야 한다
            if (false == fast)
            {
                Assert.IsFalse(precise, "case " + i);
            }
            Assert.AreEqual(precise, fast, "case " + i);
        }
        Assert.Greater(falseCount, 0, "test must cover blocked cases");
        Assert.Less(falseCount, 400, "test must cover open cases");
    }
    #endregion

    #region PlayerScreenClamp
    [Test]
    public void Clamp_InsideOnEdgeOutside()
    {
        Rect r = new Rect(-2f, -1f, 4f, 2f);
        Assert.AreEqual(new Vector2(0.5f, 0.5f), PlayerScreenClamp.Clamp(new Vector2(0.5f, 0.5f), r));
        Assert.AreEqual(new Vector2(2f, 1f), PlayerScreenClamp.Clamp(new Vector2(2f, 1f), r));
        Assert.AreEqual(new Vector2(2f, -1f), PlayerScreenClamp.Clamp(new Vector2(9f, -9f), r));
        Assert.AreEqual(new Vector2(-2f, 1f), PlayerScreenClamp.Clamp(new Vector2(-9f, 9f), r));
    }
    #endregion
}
