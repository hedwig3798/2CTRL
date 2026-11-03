using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 번개 모양 지그재그 선을 만드는 공용 헬퍼 (LineRenderer, 월드 좌표)
/// ChainLightningLine 의 마디 나누기와 같은 방식이다 (그쪽은 OneHitProjectile 전용이라 따로 둔다)
/// </summary>
public static class LightningLineUtil
{
    /// <summary>
    /// _from 에서 _to 직전까지 지그재그 마디를 _positions 에 추가한다 (_to 는 넣지 않는다. 다음 구간의 시작점 또는 직접 추가)
    /// _segments 가 1 이하면 _from 만 추가한다 (직선)
    /// </summary>
    public static void AddLink(List<Vector3> _positions, Vector3 _from, Vector3 _to, int _segments, float _jitter)
    {
        _positions.Add(_from);

        Vector3 delta = _to - _from;
        delta.z = 0f;
        if (delta.sqrMagnitude < 0.0001f || _segments <= 1)
        {
            return;
        }

        Vector3 normal = new Vector3(-delta.y, delta.x, 0f).normalized;
        for (int s = 1; s < _segments; ++s)
        {
            float t = (float)s / _segments;
            _positions.Add(_from + delta * t + normal * Random.Range(-_jitter, _jitter));
        }
    }

    /// <summary>
    /// _positions 를 선에 그대로 옮긴다
    /// </summary>
    public static void Apply(LineRenderer _line, List<Vector3> _positions)
    {
        if (null == _line)
        {
            return;
        }

        _line.positionCount = _positions.Count;
        for (int i = 0; i < _positions.Count; ++i)
        {
            _line.SetPosition(i, _positions[i]);
        }
    }

    /// <summary>
    /// 선을 비운다
    /// </summary>
    public static void Clear(LineRenderer _line)
    {
        if (null == _line)
        {
            return;
        }

        _line.positionCount = 0;
    }
}
