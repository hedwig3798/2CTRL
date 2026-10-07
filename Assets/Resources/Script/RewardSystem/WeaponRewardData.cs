using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 신규 무기 보상
/// 선택되면 신규 무기 풀에서 빠지고 upgrades 가 강화 풀에 추가된다
/// </summary>
[CreateAssetMenu(fileName = "WeaponReward", menuName = "Scriptable Objects/Reward/WeaponReward")]
public class WeaponRewardData
    : RewardData
{
    [Header("weapon")]
    public Weapon weaponPrefab;

    [Header("upgrade")]
    [Tooltip("이 무기를 획득하면 강화 풀에 추가될 선택지")]
    public List<WeaponUpgradeRewardData> upgrades = new List<WeaponUpgradeRewardData>();

    public override REWARD_TYPE RewardType => REWARD_TYPE.NewWeapon;
}
