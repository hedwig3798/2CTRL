using System;
using UnityEngine;

/// <summary>
/// 무기 업그레이드 수정 하나 {바꿀 값, 방식, 값}
/// - Add      : 기본값에 value 를 그대로 더한다
/// - Multiply : 기본값 대비 비율을 더한다 (0.2 = 기본값의 +20%, 여러 번 받으면 비율끼리 더한다, 음수는 감소)
/// - ★ 플래그는 Add 1 로 켠다 (누적값이 0 보다 크면 켜진다)
/// </summary>
[Serializable]
public struct WeaponModifier
{
    public WeaponParam param;
    public StatModifierMode mode;
    public float value;

    public WeaponModifier(WeaponParam _param, StatModifierMode _mode, float _value)
    {
        param = _param;
        mode = _mode;
        value = _value;
    }
}

/// <summary>
/// 무기 업그레이드 한 단계 (표시용 이름 / 설명 + 수정 목록)
/// </summary>
[Serializable]
public class WeaponUpgradeStep
{
    [Tooltip("선택지에 표시할 이름 (예: 데미지 +20%)")]
    [SerializeField]
    private string title;
    [Tooltip("선택지에 표시할 설명")]
    [TextArea]
    [SerializeField]
    private string description;
    [Tooltip("이 단계에서 적용할 수정 목록")]
    [SerializeField]
    private WeaponModifier[] modifiers = new WeaponModifier[0];

    public string Title => null != title ? title : string.Empty;
    public string Description => null != description ? description : string.Empty;
    public WeaponModifier[] Modifiers => modifiers;

    public WeaponUpgradeStep()
    {
    }

    public WeaponUpgradeStep(string _title, string _description, params WeaponModifier[] _modifiers)
    {
        title = _title;
        description = _description;
        modifiers = null != _modifiers ? _modifiers : new WeaponModifier[0];
    }
}

/// <summary>
/// 무기 1종의 업그레이드 단계 목록 (보통 10단계)
/// - WeaponRewardEffect 에 연결하면 무기를 얻을 때 1단계 업그레이드가 그 플레이어 전용 WeaponUpgrade 풀에 들어간다
/// - 단계를 고르면 다음 단계가 들어가고, 마지막 단계 이후로는 들어가지 않는다
/// </summary>
[CreateAssetMenu(fileName = "WeaponUpgradeTable", menuName = "Scriptable Objects/Weapon Upgrade Table")]
public class WeaponUpgradeTable
    : ScriptableObject
{
    [Tooltip("업그레이드 보상의 풀 가중치")]
    [Min(0f)]
    [SerializeField]
    private float rewardWeight = 1f;
    [Tooltip("1단계부터 순서대로")]
    [SerializeField]
    private WeaponUpgradeStep[] steps = new WeaponUpgradeStep[0];

    public float RewardWeight => rewardWeight;

    /// <summary>
    /// 단계 수
    /// </summary>
    public int StepCount => null != steps ? steps.Length : 0;

    /// <summary>
    /// _level 단계 (1 부터). 범위 밖이면 null
    /// </summary>
    public WeaponUpgradeStep GetStep(int _level)
    {
        if (_level < 1 || _level > StepCount)
        {
            return null;
        }
        return steps[_level - 1];
    }

    /// <summary>
    /// 단계 목록을 통째로 바꾼다 (빌더 / 테스트용)
    /// </summary>
    public void SetSteps(WeaponUpgradeStep[] _steps, float _rewardWeight = 1f)
    {
        steps = null != _steps ? _steps : new WeaponUpgradeStep[0];
        rewardWeight = Mathf.Max(0f, _rewardWeight);
    }
}
