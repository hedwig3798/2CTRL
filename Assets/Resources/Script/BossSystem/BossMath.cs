using UnityEngine;

/// <summary>
/// 중간 보스 판정용 순수 함수 모음 (테스트에서 바로 부를 수 있도록 상태를 갖지 않는다)
/// </summary>
public static class BossMath
{
    /// <summary>
    /// _center 에서 _forward 방향 ±_spreadDeg/2 부채꼴, 거리 _minRadius ~ _maxRadius 링 위의 점
    /// _angle01 / _distance01 은 0 ~ 1 (보통 Random.value). _forward 가 0 이면 오른쪽
    /// </summary>
    public static Vector2 ForwardRingPoint(
        Vector2 _center
        , Vector2 _forward
        , float _minRadius
        , float _maxRadius
        , float _spreadDeg
        , float _angle01
        , float _distance01)
    {
        Vector2 forward = _forward.sqrMagnitude > 0.0001f ? _forward.normalized : Vector2.right;
        float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        float angle = (baseAngle + (Mathf.Clamp01(_angle01) - 0.5f) * Mathf.Max(0f, _spreadDeg)) * Mathf.Deg2Rad;

        float minR = Mathf.Max(0f, Mathf.Min(_minRadius, _maxRadius));
        float maxR = Mathf.Max(_minRadius, _maxRadius);
        float dist = Mathf.Lerp(minR, maxR, Mathf.Clamp01(_distance01));
        return _center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
    }

    /// <summary>
    /// 정사영 카메라 뷰 사각형 (중심, orthographicSize, aspect) 을 _inset 만큼 안쪽으로 줄인 것 (음수 크기는 0)
    /// </summary>
    public static Rect ViewRect(Vector2 _cameraPos, float _orthoSize, float _aspect, float _inset)
    {
        float halfH = Mathf.Max(0f, _orthoSize - _inset);
        float halfW = Mathf.Max(0f, _orthoSize * _aspect - _inset);
        return new Rect(_cameraPos.x - halfW, _cameraPos.y - halfH, halfW * 2f, halfH * 2f);
    }

    /// <summary>
    /// 사각형 경계 반사. 경계에 닿았거나 밖에 있으면서 바깥쪽으로 가는 축만 뒤집는다 (밖에 있으면 안으로 들어오게)
    /// 한 축이라도 뒤집었으면 true
    /// </summary>
    public static bool Bounce(Vector2 _pos, Vector2 _dir, Rect _rect, out Vector2 _newDir)
    {
        _newDir = _dir;
        bool bounced = false;

        if ((_pos.x >= _rect.xMax && _dir.x > 0f) || (_pos.x <= _rect.xMin && _dir.x < 0f))
        {
            _newDir.x = -_dir.x;
            bounced = true;
        }

        if ((_pos.y >= _rect.yMax && _dir.y > 0f) || (_pos.y <= _rect.yMin && _dir.y < 0f))
        {
            _newDir.y = -_dir.y;
            bounced = true;
        }

        return bounced;
    }

    /// <summary>
    /// _from 에서 _to 쪽 사분면의 대각 방향 (정규화). 같은 축 값이면 + 쪽
    /// </summary>
    public static Vector2 DiagonalToward(Vector2 _from, Vector2 _to)
    {
        float sx = _to.x >= _from.x ? 1f : -1f;
        float sy = _to.y >= _from.y ? 1f : -1f;
        return new Vector2(sx, sy).normalized;
    }

    /// <summary>
    /// 고리 판정: 중심에서 _distance 떨어진 점이 위험 영역인지
    /// - _bandHalfWidth 가 0 이하면 고리 1개: 안쪽 = _distance &lt; _radius
    /// - 0 보다 크면 띠(반지름 ± _bandHalfWidth): 안쪽 = 띠 안 (|_distance - _radius| &lt; _bandHalfWidth)
    /// _insideDanger 가 true 면 안쪽이 위험, false 면 바깥쪽이 위험
    /// </summary>
    public static bool IsRingDanger(float _distance, float _radius, float _bandHalfWidth, bool _insideDanger)
    {
        bool inside = _bandHalfWidth > 0f
            ? Mathf.Abs(_distance - _radius) < _bandHalfWidth
            : _distance < _radius;
        return inside == _insideDanger;
    }

    /// <summary>
    /// 이번 고리 판정에서 안쪽을 위험으로 할지 고른다
    /// 직전과 같은 쪽이 _maxStreak 번 연속이면 반대쪽, 아니면 _random01 &lt; 0.5 면 안쪽
    /// _streak 는 직전 쪽(_lastInside)이 연속된 횟수 (처음이면 0)
    /// </summary>
    public static bool PickRingSide(bool _lastInside, int _streak, int _maxStreak, float _random01)
    {
        if (_maxStreak > 0 && _streak >= _maxStreak)
        {
            return false == _lastInside;
        }
        return _random01 < 0.5f;
    }

    /// <summary>
    /// HP 비율이 _ratio 이하인지 (최대 HP 가 0 이하면 false)
    /// </summary>
    public static bool IsHPAtOrBelow(float _currHP, float _maxHP, float _ratio)
    {
        if (_maxHP <= 0f)
        {
            return false;
        }
        return _currHP / _maxHP <= _ratio;
    }

    /// <summary>
    /// 당기기 거리: 보스와 _gap 이상 떨어진 곳에서 멈추도록 _distance 를 줄인다 (0 미만은 0)
    /// </summary>
    public static float ClampPullDistance(float _pullDistance, float _currentDistance, float _gap)
    {
        return Mathf.Clamp(_pullDistance, 0f, Mathf.Max(0f, _currentDistance - _gap));
    }
}
