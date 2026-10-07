using UnityEngine;

/// <summary>
/// 패시브 버프 보상
/// 선택한 플레이어 캐릭터의 base 스탯을 강화한다
/// </summary>
[CreateAssetMenu(fileName = "PassiveBuffReward", menuName = "Scriptable Objects/Reward/PassiveBuffReward")]
public class PassiveBuffRewardData
    : PassiveRewardData
{
    public override REWARD_TYPE RewardType => REWARD_TYPE.PassiveBuff;
}
