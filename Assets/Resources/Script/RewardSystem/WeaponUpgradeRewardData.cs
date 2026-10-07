using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 강화 보상
/// 습득한 무기의 base 스탯을 강화한다
/// </summary>
[CreateAssetMenu(fileName = "WeaponUpgradeReward", menuName = "Scriptable Objects/Reward/WeaponUpgradeReward")]
public class WeaponUpgradeRewardData
    : RewardData
{
    [Header("target")]
    public WeaponRewardData targetWeapon;

    [Header("modifier")]
    public List<WeaponStatModifier> modifiers = new List<WeaponStatModifier>();

    public override REWARD_TYPE RewardType => REWARD_TYPE.WeaponUpgrade;
}
