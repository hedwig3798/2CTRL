using UnityEngine;

/// <summary>
/// 차원 무기 수치 계산 (양쪽 플레이어 스텟 합산, 순수 함수)
/// - 배율 = max(minMultiplier, 1 + ΣMul_L + ΣMul_R)
/// - 최종값 = (무기 값 + ΣAdd_L + ΣAdd_R) × 배율
/// - 영구 강화가 있으면 (무기 값 + ΣAdd_L + ΣAdd_R + 영구Add) × 배율 × (1 + 영구Mul) (영구 계층은 양쪽 공통이라 한 번만)
/// </summary>
public static class DimensionStatMath
{
    /// <summary>
    /// 틱 간격 하한 (초)
    /// </summary>
    public const float MinTickInterval = 0.05f;

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
    /// 영구 강화 포함: (_value + _addLeft + _addRight + _permAdd) × CombinedMultiplier × (1 + _permMul)
    /// - _addLeft / _addRight / _mulLeft / _mulRight 는 레벨업 보상분만 (영구 계층 제외)
    /// - 영구 계층은 양쪽 공통이므로 한 번만 더하고 한 번만 곱한다
    /// </summary>
    public static float Apply(float _value, float _addLeft, float _addRight, float _mulLeft, float _mulRight, float _minMultiplier, float _permAdd, float _permMul)
    {
        return Apply(_value + _permAdd, _addLeft, _addRight, _mulLeft, _mulRight, _minMultiplier) * (1f + _permMul);
    }

    /// <summary>
    /// 발사 수 반올림 (.5 는 항상 올림, 최소 1). Weapon.FinalFireCount 와 같은 규칙
    /// </summary>
    public static int RoundCount(float _value)
    {
        return Mathf.Max(1, Mathf.FloorToInt(_value + 0.5f));
    }

    /// <summary>
    /// 공격 속도 스텟을 반영한 틱 간격 (PoisonZoneProjectile 과 같은 방식: 기본 간격 × 기본 공격 속도 / 최종 공격 속도, 최소 MinTickInterval)
    /// </summary>
    public static float ScaledTickInterval(float _interval, float _baseAttackSpeed, float _finalAttackSpeed)
    {
        float interval = Mathf.Max(MinTickInterval, _interval);
        if (_baseAttackSpeed <= 0f || _finalAttackSpeed <= 0f)
        {
            return interval;
        }
        return Mathf.Max(MinTickInterval, interval * (_baseAttackSpeed / _finalAttackSpeed));
    }
}
