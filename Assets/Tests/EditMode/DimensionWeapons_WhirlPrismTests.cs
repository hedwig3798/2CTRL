using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 잔영 회전검(회전 구간 판정) / 분광 레이저(분기 대상 선택) 순수 로직 테스트
/// </summary>
public class DimensionWeapons_WhirlPrismTests
{
    #region PhantomWhirl
    [Test]
    public void IsInSweep_TargetInsideSector_True()
    {
        Vector2 pos = new Vector2(Mathf.Cos(20f * Mathf.Deg2Rad), Mathf.Sin(20f * Mathf.Deg2Rad)) * 3f;
        Assert.IsTrue(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.1f, 5f, 0.1f, 0f, 30f));
    }

    [Test]
    public void IsInSweep_TargetOutsideSector_False()
    {
        Vector2 pos = new Vector2(Mathf.Cos(90f * Mathf.Deg2Rad), Mathf.Sin(90f * Mathf.Deg2Rad)) * 3f;
        Assert.IsFalse(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.1f, 5f, 0.1f, 0f, 30f));
    }

    [Test]
    public void IsInSweep_WrapsAround360()
    {
        Vector2 pos = new Vector2(Mathf.Cos(5f * Mathf.Deg2Rad), Mathf.Sin(5f * Mathf.Deg2Rad)) * 3f;
        Assert.IsTrue(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.05f, 5f, 0.05f, 350f, 20f));
        Vector2 pos2 = new Vector2(Mathf.Cos(340f * Mathf.Deg2Rad), Mathf.Sin(340f * Mathf.Deg2Rad)) * 3f;
        Assert.IsFalse(PhantomWhirl.IsInSweep(Vector2.zero, pos2, 0.05f, 5f, 0.05f, 350f, 20f));
    }

    [Test]
    public void IsInSweep_NegativeSweep_Clockwise()
    {
        Vector2 pos = new Vector2(Mathf.Cos(-10f * Mathf.Deg2Rad), Mathf.Sin(-10f * Mathf.Deg2Rad)) * 3f;
        Assert.IsTrue(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.05f, 5f, 0.05f, 0f, -20f));
        Assert.IsFalse(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.05f, 5f, 0.05f, 0f, 20f));
    }

    [Test]
    public void IsInSweep_BeyondBladeLength_False_RadiusExtendsReach()
    {
        Vector2 pos = new Vector2(6f, 0f);
        Assert.IsFalse(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.5f, 5f, 0.1f, -10f, 20f));
        Assert.IsTrue(PhantomWhirl.IsInSweep(Vector2.zero, pos, 1.5f, 5f, 0.1f, -10f, 20f));
    }

    [Test]
    public void IsInSweep_RadiusTolerance_HitsJustOutsideSector()
    {
        // 32도 위치, 반지름 0.3 / 거리 3 → 허용치 약 6.8도 (반폭 0.05 포함)
        Vector2 pos = new Vector2(Mathf.Cos(32f * Mathf.Deg2Rad), Mathf.Sin(32f * Mathf.Deg2Rad)) * 3f;
        Assert.IsTrue(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.3f, 5f, 0.05f, 0f, 30f));
        Assert.IsFalse(PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.01f, 5f, 0.01f, 0f, 30f));
    }

    [Test]
    public void IsInSweep_NearCenter_AlwaysTrue()
    {
        Assert.IsTrue(PhantomWhirl.IsInSweep(Vector2.zero, new Vector2(-0.2f, 0f), 0.3f, 5f, 0.1f, 0f, 1f));
    }

    [Test]
    public void IsInSweep_ConsecutiveSegments_CoverFullTurn()
    {
        // 프레임 구간을 이어 붙이면 모든 방향의 대상이 적어도 한 번은 판정된다
        for (int deg = 0; deg < 360; deg += 7)
        {
            Vector2 pos = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * 4f;
            bool hit = false;
            float swept = 0f;
            while (swept < 360f)
            {
                float step = PhantomWhirl.ComputePhantomStep(1f / 60f, 0.4f, 30f, swept);
                hit |= PhantomWhirl.IsInSweep(Vector2.zero, pos, 0.01f, 5f, 0.01f, 100f + swept, step);
                swept += step;
            }
            Assert.IsTrue(hit, $"deg {deg}");
        }
    }

    [Test]
    public void ComputePhantomStep_ClampsToMaxStepAndRemaining()
    {
        Assert.AreEqual(15f, PhantomWhirl.ComputePhantomStep(1f / 60f, 0.4f, 30f, 0f), 0.01f);
        Assert.AreEqual(30f, PhantomWhirl.ComputePhantomStep(0.5f, 0.4f, 30f, 0f), 0.01f);
        Assert.AreEqual(10f, PhantomWhirl.ComputePhantomStep(1f / 60f, 0.4f, 30f, 350f), 0.01f);
        Assert.AreEqual(0f, PhantomWhirl.ComputePhantomStep(0f, 0.4f, 30f, 0f), 0.0001f);
    }
    #endregion

    #region PrismLaser
    private class FakeSource : IPrismTargetSource<int>
    {
        public readonly List<Vector2> points = new List<Vector2>();
        public readonly HashSet<int> hit = new HashSet<int>();
        public readonly List<int> beamOrder = new List<int>();

        public bool TryFindNearest(Vector2 _from, float _maxRange, out int _target, out Vector2 _pos)
        {
            _target = -1;
            _pos = _from;
            float best = float.MaxValue;
            for (int i = 0; i < points.Count; ++i)
            {
                if (true == hit.Contains(i))
                {
                    continue;
                }
                float d = Vector2.Distance(points[i], _from);
                if (d > _maxRange || d >= best)
                {
                    continue;
                }
                best = d;
                _target = i;
                _pos = points[i];
            }
            return _target >= 0;
        }

        public void OnBeam(in PrismLink<int> _link)
        {
            hit.Add(_link.target);
            beamOrder.Add(_link.target);
        }
    }

    [Test]
    public void Plan_PrimaryTargetsNearestPerOrigin()
    {
        FakeSource src = new FakeSource();
        src.points.Add(new Vector2(-5f, 0f)); // 0 왼쪽 근처
        src.points.Add(new Vector2(5f, 0f));  // 1 오른쪽 근처
        src.points.Add(new Vector2(0f, 0f));  // 2 가운데
        List<PrismLink<int>> links = new List<PrismLink<int>>();

        Vector2[] origins = { new Vector2(-6f, 0f), new Vector2(6f, 0f) };
        int count = PrismBranchPlanner.Plan(src, origins, 2, 20f, 1f, 2, 0, 64, links);

        Assert.AreEqual(2, count);
        Assert.AreEqual(0, links[0].target);
        Assert.AreEqual(1, links[1].target);
        Assert.AreEqual(-1, links[0].parent);
        Assert.AreEqual(0, links[1].stage);
    }

    [Test]
    public void Plan_SameNearestForBothOrigins_SecondPicksNext()
    {
        FakeSource src = new FakeSource();
        src.points.Add(new Vector2(0f, 0f));
        src.points.Add(new Vector2(0f, 3f));
        List<PrismLink<int>> links = new List<PrismLink<int>>();

        Vector2[] origins = { new Vector2(-1f, 0f), new Vector2(1f, 0f) };
        PrismBranchPlanner.Plan(src, origins, 2, 20f, 0f, 2, 0, 64, links);

        Assert.AreEqual(2, links.Count);
        Assert.AreEqual(0, links[0].target);
        Assert.AreEqual(1, links[1].target);
    }

    [Test]
    public void Plan_BranchesToTwoNearestFromHitTarget_ExcludingHit()
    {
        FakeSource src = new FakeSource();
        src.points.Add(new Vector2(0f, 0f));  // 0 1차
        src.points.Add(new Vector2(1f, 0f));  // 1 가장 가까움
        src.points.Add(new Vector2(0f, 2f));  // 2 두 번째
        src.points.Add(new Vector2(-3f, 0f)); // 3 세 번째 (분기 안 됨)
        List<PrismLink<int>> links = new List<PrismLink<int>>();

        Vector2[] origins = { new Vector2(-0.5f, 0f) };
        PrismBranchPlanner.Plan(src, origins, 1, 20f, 10f, 2, 1, 64, links);

        Assert.AreEqual(3, links.Count);
        Assert.AreEqual(1, links[1].target);
        Assert.AreEqual(2, links[2].target);
        Assert.AreEqual(0, links[1].parent);
        Assert.AreEqual(1, links[1].stage);
        Assert.AreEqual(links[0].to, links[1].from);
    }

    [Test]
    public void Plan_MultipleStages_NoDuplicateTargets()
    {
        FakeSource src = new FakeSource();
        for (int i = 0; i < 30; ++i)
        {
            src.points.Add(new Vector2(i * 0.7f, (i % 5) * 0.9f));
        }
        List<PrismLink<int>> links = new List<PrismLink<int>>();

        Vector2[] origins = { new Vector2(-1f, 0f), new Vector2(25f, 0f) };
        int count = PrismBranchPlanner.Plan(src, origins, 2, 50f, 50f, 2, 2, 64, links);

        Assert.AreEqual(2 + PrismBranchPlanner.MaxBranchCount(2, 2, 2), count);
        Assert.AreEqual(14, count);
        HashSet<int> unique = new HashSet<int>(src.beamOrder);
        Assert.AreEqual(count, unique.Count);
        for (int i = 2; i < links.Count; ++i)
        {
            Assert.AreEqual(links[i].stage, links[links[i].parent].stage + 1);
        }
    }

    [Test]
    public void Plan_RespectsBranchRangeAndMaxBeams()
    {
        FakeSource src = new FakeSource();
        src.points.Add(new Vector2(0f, 0f));
        src.points.Add(new Vector2(10f, 0f)); // 분기 범위 밖
        List<PrismLink<int>> links = new List<PrismLink<int>>();
        Vector2[] origins = { Vector2.zero };

        Assert.AreEqual(1, PrismBranchPlanner.Plan(src, origins, 1, 5f, 3f, 2, 3, 64, links));

        FakeSource src2 = new FakeSource();
        for (int i = 0; i < 20; ++i)
        {
            src2.points.Add(new Vector2(i, 0f));
        }
        Assert.AreEqual(5, PrismBranchPlanner.Plan(src2, origins, 1, 50f, 50f, 2, 5, 5, links));
    }

    [Test]
    public void Plan_NoTargets_ReturnsZero()
    {
        FakeSource src = new FakeSource();
        List<PrismLink<int>> links = new List<PrismLink<int>>();
        Vector2[] origins = { Vector2.zero, Vector2.one };
        Assert.AreEqual(0, PrismBranchPlanner.Plan(src, origins, 2, 50f, 50f, 2, 2, 64, links));
    }

    [Test]
    public void MaxBranchCount_DefaultIsTwelve()
    {
        Assert.AreEqual(12, PrismBranchPlanner.MaxBranchCount(2, 2, 2));
        Assert.AreEqual(0, PrismBranchPlanner.MaxBranchCount(2, 2, 0));
    }
    #endregion
}
