using UnityEngine;

/// <summary>
/// 레벨업 필요 EXP 표
/// - 공식 : baseExp 또는 coef 가 0 보다 크면 공식을 쓴다
///   필요 EXP(L → L+1) = baseExp + coef × L^power, L = 현재 레벨(ExpSystem.currLevel) + formulaLevelOffset
///   (currLevel 은 0 부터 시작하므로 offset 1 이면 첫 레벨업이 L = 1)
/// - 공식을 쓰지 않으면 기존처럼 expTable[현재 레벨] 을 쓰고, 표 밖은 마지막 값을 쓴다
/// </summary>
[CreateAssetMenu(fileName = "LevelTable", menuName = "Scriptable Objects/LevelTable")]
public class LevelTable
    : ScriptableObject
{
    [Header("need exp when level x to x+1 (공식을 쓰지 않을 때)")]
    public float[] expTable;

    [Header("max level")]
    public int maxLevel;

    [Header("formula : baseExp + coef × L^power")]
    [Tooltip("공식의 고정값. baseExp / coef 가 모두 0 이면 공식을 쓰지 않고 expTable 을 쓴다")]
    [Min(0f)]
    public float baseExp;

    [Tooltip("공식의 계수")]
    [Min(0f)]
    public float coef;

    [Tooltip("공식의 지수")]
    [Min(0f)]
    public float power = 1f;

    [Tooltip("공식에 넣는 L = 현재 레벨 + 이 값 (currLevel 이 0 부터 시작하면 1)")]
    public int formulaLevelOffset = 1;

    /// <summary>
    /// 공식을 쓰는지
    /// </summary>
    public bool UseFormula => baseExp > 0f || coef > 0f;

    /// <summary>
    /// 필요 EXP 를 계산할 수 있는지 (공식 또는 표가 있음)
    /// </summary>
    public bool HasData => true == UseFormula || (null != expTable && 0 < expTable.Length);

    /// <summary>
    /// _level 에서 다음 레벨로 오르는 데 필요한 EXP (계산할 수 없으면 0)
    /// </summary>
    public float GetNeedExp(int _level)
    {
        if (_level < 0)
        {
            _level = 0;
        }

        if (true == UseFormula)
        {
            float l = Mathf.Max(0, _level + formulaLevelOffset);
            return baseExp + coef * Mathf.Pow(l, power);
        }

        if (null == expTable || 0 == expTable.Length)
        {
            return 0f;
        }

        if (_level >= expTable.Length)
        {
            return expTable[expTable.Length - 1];
        }

        return expTable[_level];
    }
}
