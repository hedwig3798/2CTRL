using UnityEngine;

/// <summary>
/// 차원 무기 수치 계산 (양쪽 플레이어 스텟 합산, 순수 함수)
/// - 배율 = max(minMultiplier, 1 + ΣMul_L + ΣMul_R)
/// - 최종값 = (무기 값 + ΣAdd_L + ΣAdd_R) × 배율
/// </summary>
public static class DimensionStatMath
{
    /// <summary>
    /// 양쪽 비율 합으로 만든 배율 (하한 _minMultiplier)
    /// </summary>
    public static float CombinedMultiplier(float _mulLeft, float _mulRight, float _minMultiplier)
    {
        return Mathf.Max(_minMultiplier, 1f + _mulLeft + _mulRight);
    }

    /// <summary>
    /// (_value + _addLeft + _addRight) × CombinedMultiplier
    /// </summary>
    public static float Apply(float _value, float _addLeft, float _addRight, float _mulLeft, float _mulRight, float _minMultiplier)
    {
        return (_value + _addLeft + _addRight) * CombinedMultiplier(_mulLeft, _mulRight, _minMultiplier);
    }

    /// <summary>
    /// 발사 수 반올림 (.5 는 항상 올림, 최소 1). Weapon.FinalFireCount 와 같은 규칙
    /// </summary>
    public static int RoundCount(float _value)
    {
        return Mathf.Max(1, Mathf.FloorToInt(_value + 0.5f));
    }
}
