using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최종 보스 패턴 종류 (LaserMeteor, DashMeteor 는 P3 조합 패턴)
/// </summary>
public enum FinalBossPattern
{
    None,
    Meteor,
    Dash,
    Laser,
    LaserMeteor,
    DashMeteor,
}

/// <summary>
/// 레이저 한 줄 (보스 월드 선분)
/// </summary>
public struct LaserLine
{
    public Vector2 a;
    public Vector2 b;

    public LaserLine(Vector2 _a, Vector2 _b)
    {
        a = _a;
        b = _b;
    }
}

/// <summary>
/// 최종 보스 페이즈·패턴 선택·레이저 배치 (순수 함수, 테스트용)
/// </summary>
public static class FinalBossPatterns
{
    private static readonly FinalBossPattern[] BasePatterns =
    {
        FinalBossPattern.Meteor,
        FinalBossPattern.Dash,
        FinalBossPattern.Laser,
    };

    private static readonly FinalBossPattern[] ComboPatterns =
    {
        FinalBossPattern.LaserMeteor,
        FinalBossPattern.DashMeteor,
    };

    /// <summary>
    /// HP 비율 → 페이즈 (0 = P1, 1 = P2, 2 = P3). _ratio 가 _p2Ratio 초과면 P1, _p3Ratio 초과면 P2, 이하면 P3
    /// </summary>
    public static int PhaseFor(float _ratio, float _p2Ratio, float _p3Ratio)
    {
        if (_ratio > _p2Ratio)
        {
            return 0;
        }
        if (_ratio > _p3Ratio)
        {
            return 1;
        }
        return 2;
    }

    /// <summary>
    /// 예고 시간 (기본값 × 페이즈 배율, 최소 _min)
    /// </summary>
    public static float TelegraphTime(float _base, float _scale, float _min)
    {
        return Mathf.Max(_min, _base * _scale);
    }

    /// <summary>
    /// 다음 패턴. 직전 패턴(_last)은 고르지 않는다
    /// P3(_phase 2 이상)에서 _rCombo 가 _comboChance 미만이면 조합 패턴 중에서 고른다. _rPick(0~1)으로 후보 중 하나
    /// </summary>
    public static FinalBossPattern PickPattern(FinalBossPattern _last, int _phase, float _comboChance, float _rCombo, float _rPick)
    {
        FinalBossPattern[] pool = (2 <= _phase && _rCombo < _comboChance) ? ComboPatterns : BasePatterns;

        int count = 0;
        foreach (FinalBossPattern p in pool)
        {
            if (p != _last)
            {
                ++count;
            }
        }
        if (0 == count)
        {
            return pool[0];
        }

        int pick = Mathf.Clamp((int)(_rPick * count), 0, count - 1);
        foreach (FinalBossPattern p in pool)
        {
            if (p == _last)
            {
                continue;
            }
            if (0 == pick)
            {
                return p;
            }
            --pick;
        }
        return pool[0];
    }

    /// <summary>
    /// _area 안에 반경 _gapRadius 의 원이 어느 레이저(반폭 _laserHalfWidth)에도 닿지 않고 들어가는 자리가 있는지 (격자 _step 으로 표본, 양 끝 포함)
    /// _slack 만큼 기준을 느슨하게 한다: _slack = 격자 대각 절반이면 "거친 격자에서 실패 → 정밀 격자에서도 실패"가 보장되는 필요조건 검사
    /// </summary>
    public static bool HasSafeSpot(IReadOnlyList<LaserLine> _lines, Rect _area, float _laserHalfWidth, float _gapRadius, float _step, float _slack = 0f)
    {
        float xMin = _area.xMin + _gapRadius;
        float xMax = _area.xMax - _gapRadius;
        float yMin = _area.yMin + _gapRadius;
        float yMax = _area.yMax - _gapRadius;
        if (xMin > xMax || yMin > yMax)
        {
            return false;
        }

        float step = Mathf.Max(0.05f, _step);
        int nx = Mathf.Max(1, Mathf.CeilToInt((xMax - xMin) / step));
        int ny = Mathf.Max(1, Mathf.CeilToInt((yMax - yMin) / step));
        float clear = _laserHalfWidth + _gapRadius - Mathf.Max(0f, _slack);
        for (int ix = 0; ix <= nx; ++ix)
        {
            float x = Mathf.Lerp(xMin, xMax, (float)ix / nx);
            for (int iy = 0; iy <= ny; ++iy)
            {
                Vector2 p = new Vector2(x, Mathf.Lerp(yMin, yMax, (float)iy / ny));
                bool free = true;
                for (int i = 0; i < _lines.Count; ++i)
                {
                    if (BossAttackResolver.DistanceToSegment(p, _lines[i].a, _lines[i].b) < clear)
                    {
                        free = false;
                        break;
                    }
                }
                if (true == free)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 거친 격자(_coarseStep)로 필요조건을 먼저 보고, 통과한 후보만 정밀 격자(_step)로 검사한다
    /// </summary>
    public static bool HasSafeSpotFast(IReadOnlyList<LaserLine> _lines, Rect _area, float _laserHalfWidth, float _gapRadius, float _step, float _coarseStep)
    {
        if (_coarseStep > _step)
        {
            float slack = _coarseStep * 0.7072f;
            if (false == HasSafeSpot(_lines, _area, _laserHalfWidth, _gapRadius, _coarseStep, slack))
            {
                return false;
            }
        }
        return HasSafeSpot(_lines, _area, _laserHalfWidth, _gapRadius, _step);
    }

    /// <summary>
    /// _stage 를 가로지르는 무작위 직선 하나 (사각형 안의 한 점을 지나는 무작위 각도, 양 끝은 사각형 대각선 길이만큼 연장)
    /// </summary>
    public static LaserLine RandomLine(Rect _stage, System.Random _rng)
    {
        Vector2 p = new Vector2(
            _stage.xMin + (float)_rng.NextDouble() * _stage.width,
            _stage.yMin + (float)_rng.NextDouble() * _stage.height);
        float angle = (float)_rng.NextDouble() * Mathf.PI;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float ext = Mathf.Max(1f, _stage.size.magnitude);
        return new LaserLine(p - dir * ext, p + dir * ext);
    }

    /// <summary>
    /// 레이저 _count 줄을 _result 에 만든다. 좌우 각 절반(_left, _right)에 폭 _safeGap 이상의 안전 틈이 있어야 한다
    /// 후보마다 거친 격자 필요조건 → 정밀 검사 순서 (_coarseStep 0 이면 _step×3). _maxAttempts 번 안에 못 만들면 줄 수를 하나씩 줄여 다시 시도한다. 만든 줄 수를 돌려준다 (0 이면 실패)
    /// </summary>
    public static int GenerateLasers(List<LaserLine> _result, int _count, Rect _stage, Rect _left, Rect _right,
        float _laserWidth, float _safeGap, int _maxAttempts, float _step, System.Random _rng, float _coarseStep = 0f)
    {
        float coarse = _coarseStep > 0f ? _coarseStep : Mathf.Max(0.05f, _step) * 3f;
        float halfWidth = Mathf.Max(0f, _laserWidth) * 0.5f;
        float gapRadius = Mathf.Max(0f, _safeGap) * 0.5f;
        int attempts = Mathf.Max(1, _maxAttempts);

        for (int n = _count; n > 0; --n)
        {
            for (int attempt = 0; attempt < attempts; ++attempt)
            {
                _result.Clear();
                for (int i = 0; i < n; ++i)
                {
                    _result.Add(RandomLine(_stage, _rng));
                }

                if (true == HasSafeSpotFast(_result, _left, halfWidth, gapRadius, _step, coarse)
                    && true == HasSafeSpotFast(_result, _right, halfWidth, gapRadius, _step, coarse))
                {
                    return n;
                }
            }
        }

        _result.Clear();
        return 0;
    }
}
