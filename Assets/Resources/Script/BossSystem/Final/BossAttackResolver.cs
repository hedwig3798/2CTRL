using UnityEngine;

/// <summary>
/// 최종 보스 공격 판정 (보스 월드 좌표, 순수 함수)
/// - 플레이어 위치는 BossSpace.FromSideWorld 로 보스 월드에 옮긴 뒤 판정한다
/// </summary>
public static class BossAttackResolver
{
    /// <summary>
    /// 원 안에 점(반경 _pointRadius 의 원)이 닿는지
    /// </summary>
    public static bool CircleContainsPoint(Vector2 _center, float _radius, Vector2 _point, float _pointRadius = 0f)
    {
        float r = Mathf.Max(0f, _radius) + Mathf.Max(0f, _pointRadius);
        return (_point - _center).sqrMagnitude <= r * r;
    }

    /// <summary>
    /// 두 원이 겹치는지 (접촉 포함)
    /// </summary>
    public static bool CircleOverlapsCircle(Vector2 _a, float _ra, Vector2 _b, float _rb)
    {
        return CircleContainsPoint(_a, _ra, _b, _rb);
    }

    /// <summary>
    /// 점과 선분 사이 거리
    /// </summary>
    public static float DistanceToSegment(Vector2 _p, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float len2 = ab.sqrMagnitude;
        if (len2 <= 0.000001f)
        {
            return Vector2.Distance(_p, _a);
        }
        float t = Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / len2);
        return Vector2.Distance(_p, _a + ab * t);
    }

    /// <summary>
    /// 캡슐(선분 _a~_b, 전체 폭 _width) 안에 점(반경 _pointRadius)이 닿는지
    /// </summary>
    public static bool CapsuleContainsPoint(Vector2 _a, Vector2 _b, float _width, Vector2 _point, float _pointRadius = 0f)
    {
        float r = Mathf.Max(0f, _width) * 0.5f + Mathf.Max(0f, _pointRadius);
        return DistanceToSegment(_point, _a, _b) <= r;
    }
}
