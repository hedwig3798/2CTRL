using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Letterbox.Calculate 16:9 영역 계산 검증
/// </summary>
public class LetterboxEditTests
{
    private const float EPS = 1e-4f;

    private static void AssertAspect(Rect _r, int _w, int _h)
    {
        // 정규화 영역을 픽셀로 바꾸면 16:9 여야 한다
        float aspect = (_r.width * _w) / (_r.height * _h);
        Assert.AreEqual(16f / 9f, aspect, 1e-3f);
    }

    private static void AssertCentered(Rect _r)
    {
        Assert.AreEqual(0.5f, _r.center.x, EPS);
        Assert.AreEqual(0.5f, _r.center.y, EPS);
    }

    [Test]
    public void SixteenNine_FillsWholeScreen()
    {
        Rect r = Letterbox.Calculate(1920, 1080);
        Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), r);
    }

    [Test]
    public void FourThree_HasLetterboxTopAndBottom()
    {
        Rect r = Letterbox.Calculate(1024, 768);
        Assert.AreEqual(1f, r.width, EPS);
        Assert.Less(r.height, 1f);
        Assert.AreEqual((4f / 3f) / (16f / 9f), r.height, EPS);
        AssertCentered(r);
        AssertAspect(r, 1024, 768);
    }

    [Test]
    public void UltraWide21_9_HasPillarboxLeftAndRight()
    {
        Rect r = Letterbox.Calculate(2560, 1080);
        Assert.AreEqual(1f, r.height, EPS);
        Assert.Less(r.width, 1f);
        AssertCentered(r);
        AssertAspect(r, 2560, 1080);
    }

    [Test]
    public void Portrait_ShrinksHeightOnly()
    {
        Rect r = Letterbox.Calculate(1080, 1920);
        Assert.AreEqual(1f, r.width, EPS);
        Assert.AreEqual(0f, r.x, EPS);
        Assert.Greater(r.height, 0f);
        Assert.Less(r.height, 1f);
        AssertCentered(r);
        AssertAspect(r, 1080, 1920);
    }

    [Test]
    public void Square_IsLetterboxed()
    {
        Rect r = Letterbox.Calculate(1000, 1000);
        Assert.AreEqual(9f / 16f, r.height, EPS);
        AssertCentered(r);
    }

    [TestCase(0, 0)]
    [TestCase(0, 1080)]
    [TestCase(1920, 0)]
    [TestCase(-1, -1)]
    public void InvalidSize_ReturnsFullRect(int _w, int _h)
    {
        Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), Letterbox.Calculate(_w, _h));
    }

    [Test]
    public void NearSixteenNine_StaysInsideUnitRect()
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1921, 1080), new Vector2Int(1919, 1080), new Vector2Int(1366, 768) })
        {
            Rect r = Letterbox.Calculate(size.x, size.y);
            Assert.GreaterOrEqual(r.xMin, -EPS);
            Assert.GreaterOrEqual(r.yMin, -EPS);
            Assert.LessOrEqual(r.xMax, 1f + EPS);
            Assert.LessOrEqual(r.yMax, 1f + EPS);
        }
    }
}
