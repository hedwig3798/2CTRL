using System;
using UnityEngine;

/// <summary>
/// 영구 강화 한 종류 (타이틀 "차원 강화" 패널에서 차원력으로 산다)
/// - 레벨 n 의 효과 = valuePerLevel × n, PermanentUpgradeApplier 가 게임 시작 시 양쪽 PlayerStats 영구 계층에 넣는다
/// - 가격(n → n+1) = round(baseCost × costGrowth^n) (0.5 는 올림)
/// - id 는 저장 키(MetaProgress, PlayerPrefs "Meta.Level.{id}") 이므로 한 번 정하면 바꾸지 않는다
/// </summary>
[CreateAssetMenu(fileName = "PermanentUpgradeData", menuName = "2CTRL/Meta/PermanentUpgradeData")]
public class PermanentUpgradeData
    : ScriptableObject
{
    [Tooltip("저장 키 (바꾸면 기존 세이브의 레벨을 잃는다)")]
    [SerializeField]
    private string id;

    [Tooltip("올릴 스텟")]
    [SerializeField]
    private ValueType stat = ValueType.Damage;

    [Tooltip("Add = 고정값, Multiply = 비율 (0.02 = +2%)")]
    [SerializeField]
    private StatModifierMode mode = StatModifierMode.Multiply;

    [Tooltip("레벨당 효과")]
    [SerializeField]
    private float valuePerLevel = 0.02f;

    [Min(1)]
    [SerializeField]
    private int maxLevel = 10;

    [Tooltip("레벨 0 → 1 가격")]
    [Min(0)]
    [SerializeField]
    private int baseCost = 20;

    [Tooltip("레벨마다 가격에 곱하는 값")]
    [Min(1f)]
    [SerializeField]
    private float costGrowth = 1.5f;

    [Tooltip("이름 StringTable 키 (비우면 스텟 이름)")]
    [SerializeField]
    private string nameKey;

    [SerializeField]
    private Sprite icon;

    public string Id => id;
    public ValueType Stat => stat;
    public StatModifierMode Mode => mode;
    public float ValuePerLevel => valuePerLevel;
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public int BaseCost => baseCost;
    public float CostGrowth => costGrowth;
    public string NameKey => nameKey;
    public Sprite Icon => icon;

    /// <summary>
    /// 현재 언어 이름 (nameKey 가 비었거나 없으면 스텟 이름)
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (false == string.IsNullOrEmpty(nameKey) && true == StringTable.TryGet(nameKey, out string value))
            {
                return value;
            }
            return stat.ToDisplayName();
        }
    }

    /// <summary>
    /// _level → _level + 1 가격 = round(baseCost × costGrowth^_level) (0.5 는 올림, int 범위로 자름)
    /// </summary>
    public int GetCost(int _level)
    {
        double cost = baseCost * Math.Pow(costGrowth, Mathf.Max(0, _level));
        cost = Math.Round(cost, MidpointRounding.AwayFromZero);
        if (cost >= int.MaxValue)
        {
            return int.MaxValue;
        }
        return Mathf.Max(0, (int)cost);
    }

    /// <summary>
    /// 레벨 _level 의 누적 효과 (valuePerLevel × 레벨, 레벨은 0 ~ maxLevel 로 자름)
    /// </summary>
    public float GetTotalValue(int _level)
    {
        return valuePerLevel * Mathf.Clamp(_level, 0, MaxLevel);
    }

    /// <summary>
    /// 값을 한 번에 넣는다 (에디터 빌더·테스트용)
    /// </summary>
    public void Configure(string _id, ValueType _stat, StatModifierMode _mode, float _valuePerLevel, int _maxLevel, int _baseCost, float _costGrowth, string _nameKey = null, Sprite _icon = null)
    {
        id = _id;
        stat = _stat;
        mode = _mode;
        valuePerLevel = _valuePerLevel;
        maxLevel = Mathf.Max(1, _maxLevel);
        baseCost = Mathf.Max(0, _baseCost);
        costGrowth = Mathf.Max(1f, _costGrowth);
        nameKey = _nameKey;
        icon = _icon;
    }
}
