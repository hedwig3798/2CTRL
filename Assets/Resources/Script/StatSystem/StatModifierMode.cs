/// <summary>
/// 수치 보정 방식
/// - 최종값 = (base + ΣAdd) × (1 + ΣMultiply)
/// </summary>
public enum StatModifierMode
{
    /// <summary>
    /// 고정값을 더한다 (value = 1 이면 +1)
    /// </summary>
    Add,
    /// <summary>
    /// 비율을 더한다 (value = 0.1 이면 +10%)
    /// </summary>
    Multiply,
}
