using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 역병 지뢰·화염 부메랑 순수 로직: 무작위 위치가 화면 안, 끌어당김 방향·거리, 부메랑 비행·귀환 전환, 조준·부채꼴, 틱 간격
/// </summary>
public class DimensionWeapons_MineBoomerangTests
{
    private const float EPS = 0.0001f;

    #region 역병 지뢰
    [Test]
    public void PointInRect_항상_여백을_뺀_화면_안()
    {
        Rect rect = new Rect(-10f, -4f, 20f, 8f);
        System.Random rng = new System.Random(7);
        for (int i = 0; i < 500; ++i)
        {
            Vector2 p = PlagueMineMath.PointInRect(rect, 0.5f, (float)rng.NextDouble(), (float)rng.NextDouble());
            Assert.That(p.x, Is.InRange(rect.xMin + 0.5f, rect.xMax - 0.5f));
            Assert.That(p.y, Is.InRange(rect.yMin + 0.5f, rect.yMax - 0.5f));
        }
    }

    [Test]
    public void PointInRect_범위밖_입력은_잘라내고_모서리()
    {
        Rect rect = new Rect(0f, 0f, 10f, 10f);
        Vector2 lo = PlagueMineMath.PointInRect(rect, 1f, -5f, -5f);
        Vector2 hi = PlagueMineMath.PointInRect(rect, 1f, 5f, 5f);
        Assert.AreEqual(new Vector2(1f, 1f), lo);
        Assert.AreEqual(new Vector2(9f, 9f), hi);
    }

    [Test]
    public void PointInRect_여백이_너무_크면_중앙()
    {
        Rect rect = new Rect(0f, 0f, 2f, 10f);
        Vector2 p = PlagueMineMath.PointInRect(rect, 3f, 0.9f, 0.2f);
        Assert.AreEqual(1f, p.x, EPS);
        Assert.AreEqual(3.8f, p.y, EPS, "여유가 있는 축은 그대로 (3 ~ 7 의 0.2)");
    }

    [Test]
    public void PullDelta_폭발_중심_방향으로_최대거리만큼()
    {
        Vector2 center = new Vector2(2f, 1f);
        Vector2 from = new Vector2(7f, 1f);
        Vector2 delta = PlagueMineMath.PullDelta(from, center, 1.5f, 0.3f);
        Assert.AreEqual(-1.5f, delta.x, EPS);
        Assert.AreEqual(0f, delta.y, EPS);

        // 대각선도 중심 쪽
        Vector2 diag = PlagueMineMath.PullDelta(new Vector2(5f, 5f), Vector2.zero, 1f, 0f);
        Assert.Greater(Vector2.Dot(diag.normalized, (Vector2.zero - new Vector2(5f, 5f)).normalized), 0.999f);
        Assert.AreEqual(1f, diag.magnitude, EPS);
    }

    [Test]
    public void PullDelta_멈춤_간격을_넘어_당기지_않음()
    {
        Vector2 delta = PlagueMineMath.PullDelta(new Vector2(1f, 0f), Vector2.zero, 5f, 0.4f);
        Assert.AreEqual(-0.6f, delta.x, EPS);

        Vector2 inside = PlagueMineMath.PullDelta(new Vector2(0.2f, 0f), Vector2.zero, 5f, 0.4f);
        Assert.AreEqual(Vector2.zero, inside);

        Vector2 same = PlagueMineMath.PullDelta(Vector2.zero, Vector2.zero, 5f, 0f);
        Assert.AreEqual(Vector2.zero, same);
    }

    [Test]
    public void ScaledTickInterval_공격속도가_오르면_짧아지고_하한()
    {
        Assert.AreEqual(0.5f, DimensionStatMath.ScaledTickInterval(0.5f, 0.5f, 0.5f), EPS);
        Assert.AreEqual(0.25f, DimensionStatMath.ScaledTickInterval(0.5f, 0.5f, 1f), EPS);
        Assert.AreEqual(0.05f, DimensionStatMath.ScaledTickInterval(0.5f, 0.5f, 100f), EPS);
        Assert.AreEqual(0.5f, DimensionStatMath.ScaledTickInterval(0.5f, 0f, 1f), EPS);
        Assert.AreEqual(0.2f, DimensionStatMath.ScaledTickInterval(0.2f, 1f, 1f), EPS);
        Assert.AreEqual(0.1f, DimensionStatMath.ScaledTickInterval(0.2f, 1f, 2f), EPS);
    }
    #endregion

    #region 화염 부메랑
    private static Vector2 FlyOut(ref BoomerangFlight _flight, Vector2 _pos, out int _frames)
    {
        _frames = 0;
        while (false == _flight.StepOut(ref _pos, 0.01f))
        {
            ++_frames;
            Assert.Less(_frames, 100000, "부메랑이 멈추지 않음");
        }
        return _pos;
    }

    [Test]
    public void Flight_목표_지점과_overshoot_만큼_가서_멈춘다()
    {
        BoomerangFlight flight = BoomerangFlight.Launch(Vector2.zero, new Vector2(4f, 0f), 8f, 1f, 2f, 1.5f);
        Assert.IsFalse(flight.isReturning);

        Vector2 stop = FlyOut(ref flight, Vector2.zero, out _);
        Assert.AreEqual(5f, stop.x, 0.1f);
        Assert.AreEqual(0f, stop.y, EPS);
        Assert.AreEqual(0f, flight.currSpeed, EPS);
        Assert.IsFalse(flight.isReturning, "StepOut 만으로는 귀환 상태가 되지 않는다 (BeginReturn 필요)");
    }

    [Test]
    public void Flight_가까운_목표도_최소거리는_날아간다()
    {
        BoomerangFlight flight = BoomerangFlight.Launch(Vector2.zero, new Vector2(0f, 0.3f), 8f, 0f, 2f, 1.5f);
        Vector2 stop = FlyOut(ref flight, Vector2.zero, out _);
        Assert.AreEqual(2f, stop.y, 0.1f);
    }

    [Test]
    public void Flight_귀환은_플레이어_현재_위치_방향으로_한번만_정한다()
    {
        BoomerangFlight flight = BoomerangFlight.Launch(Vector2.zero, new Vector2(4f, 0f), 8f, 1f, 2f, 1.5f);
        Vector2 pos = FlyOut(ref flight, Vector2.zero, out _);

        // 플레이어가 그 사이 위로 이동했다
        Vector2 home = new Vector2(pos.x, 10f);
        flight.BeginReturn(pos, true, home);
        Assert.IsTrue(flight.isReturning);
        Assert.AreEqual(0f, flight.direction.x, EPS);
        Assert.AreEqual(1f, flight.direction.y, EPS);

        // 귀환 중에는 StepOut 이 아무것도 하지 않는다
        Vector2 before = pos;
        Assert.IsFalse(flight.StepOut(ref pos, 0.1f));
        Assert.AreEqual(before, pos);

        // 가속하고 최고 속도 = speed × returnSpeedRate
        for (int i = 0; i < 1000; ++i)
        {
            flight.StepReturn(ref pos, 0.01f);
        }
        Assert.AreEqual(12f, flight.currSpeed, EPS);
        Assert.Greater(pos.y, 10f, "목표에 닿아도 방향을 바꾸지 않고 지나간다");
        Assert.AreEqual(before.x, pos.x, EPS);
    }

    [Test]
    public void Flight_귀환_목표가_없으면_날아온_반대_방향()
    {
        BoomerangFlight flight = BoomerangFlight.Launch(Vector2.zero, new Vector2(0f, -3f), 8f, 1f, 2f, 1.5f);
        Vector2 pos = FlyOut(ref flight, Vector2.zero, out _);
        flight.BeginReturn(pos, false, Vector2.zero);
        Assert.AreEqual(1f, flight.direction.y, EPS);

        BoomerangFlight same = BoomerangFlight.Launch(Vector2.zero, new Vector2(3f, 0f), 8f, 1f, 2f, 1.5f);
        Vector2 p2 = FlyOut(ref same, Vector2.zero, out _);
        same.BeginReturn(p2, true, p2);
        Assert.AreEqual(-1f, same.direction.x, EPS, "귀환 목표가 바로 그 자리면 반대 방향");
    }

    [Test]
    public void ResolveAim_대상_마지막방향_대체방향_순서()
    {
        Vector2 from = new Vector2(1f, 1f);
        Vector2 toTarget = BlazeBoomerang.ResolveAim(from, true, new Vector2(1f, 5f), Vector2.right, Vector2.left);
        Assert.AreEqual(Vector2.up, toTarget);

        Vector2 last = BlazeBoomerang.ResolveAim(from, false, Vector2.zero, new Vector2(0f, -3f), Vector2.left);
        Assert.AreEqual(Vector2.down, last);

        Vector2 fallback = BlazeBoomerang.ResolveAim(from, false, Vector2.zero, Vector2.zero, new Vector2(-2f, 0f));
        Assert.AreEqual(Vector2.left, fallback);

        Vector2 none = BlazeBoomerang.ResolveAim(from, true, from, Vector2.zero, Vector2.zero);
        Assert.AreEqual(Vector2.right, none);
    }

    [Test]
    public void SpreadDirection_가운데_기준_대칭()
    {
        Vector2 single = BlazeBoomerang.SpreadDirection(Vector2.right, 0, 1, 30f);
        Assert.AreEqual(1f, single.x, EPS);
        Assert.AreEqual(0f, single.y, EPS);

        Vector2 a = BlazeBoomerang.SpreadDirection(Vector2.right, 0, 3, 30f);
        Vector2 b = BlazeBoomerang.SpreadDirection(Vector2.right, 1, 3, 30f);
        Vector2 c = BlazeBoomerang.SpreadDirection(Vector2.right, 2, 3, 30f);
        Assert.AreEqual(-30f, Vector2.SignedAngle(Vector2.right, a), 0.01f);
        Assert.AreEqual(0f, Vector2.SignedAngle(Vector2.right, b), 0.01f);
        Assert.AreEqual(30f, Vector2.SignedAngle(Vector2.right, c), 0.01f);
    }
    #endregion
}
