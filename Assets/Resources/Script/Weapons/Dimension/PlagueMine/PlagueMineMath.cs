using UnityEngine;

/// <summary>
/// 역병 지뢰 순수 계산 (테스트용으로 분리)
/// </summary>
public static class PlagueMineMath
{
    /// <summary>
    /// _rect 를 _margin 만큼 안으로 줄인 사각형 안의 점. _u, _v 는 0 ~ 1 (범위 밖은 잘라낸다)
    /// 여백 때문에 폭/높이가 0 미만이 되는 축은 그 축의 중앙
    /// </summary>
    public static Vector2 PointInRect(Rect _rect, float _margin, float _u, float _v)
    {
        float margin = Mathf.Max(0f, _margin);
        return new Vector2(
            Axis(_rect.xMin, _rect.xMax, margin, _u),
            Axis(_rect.yMin, _rect.yMax, margin, _v));
    }

    private static float Axis(float _min, float _max, float _margin, float _t)
    {
        float lo = _min + _margin;
        float hi = _max - _margin;
        if (hi < lo)
        {
            return (_min + _max) * 0.5f;
        }
        return Mathf.Lerp(lo, hi, Mathf.Clamp01(_t));
    }

    /// <summary>
    /// _from 을 _center 쪽으로 끌어당기는 이동량 (최대 _distance, 중심과 _gap 이상 떨어진 곳에서 멈춘다)
    /// 화면 공간과 진영 월드는 평행이동 관계라 이 이동량을 진영 월드에 그대로 더하면 된다
    /// </summary>
    public static Vector2 PullDelta(Vector2 _from, Vector2 _center, float _distance, float _gap)
    {
        Vector2 toCenter = _center - _from;
        float dist = toCenter.magnitude;
        if (dist < 0.0001f)
        {
            return Vector2.zero;
        }

        float move = BossMath.ClampPullDistance(_distance, dist, Mathf.Max(0f, _gap));
        return toCenter / dist * move;
    }
}
