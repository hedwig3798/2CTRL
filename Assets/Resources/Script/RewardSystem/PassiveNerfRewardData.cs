using UnityEngine;

/// <summary>
/// 패시브 너프
/// 강화 선택지에 조합되어 다른 플레이어 캐릭터의 base 스탯을 약화한다
/// </summary>
[CreateAssetMenu(fileName = "PassiveNerfReward", menuName = "Scriptable Objects/Reward/PassiveNerfReward")]
public class PassiveNerfRewardData
    : PassiveRewardData
{
    public override REWARD_TYPE RewardType => REWARD_TYPE.PassiveNerf;
}
