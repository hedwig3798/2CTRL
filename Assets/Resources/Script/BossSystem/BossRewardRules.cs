using System.Collections.Generic;

/// <summary>
/// 보스 보상 규칙 (순수 함수)
/// - 무기 / 무기 업그레이드 풀만 쓰고 디버프는 없다
/// - 뽑힌 보상이 없으면 그 진영 일반 레벨업 보상으로 대신한다
/// </summary>
public static class BossRewardRules
{
    /// <summary>
    /// 보스 보상 풀 (무기 보유 상한이 아닐 때)
    /// </summary>
    public static readonly RewardPoolType[] PoolTypes =
    {
        RewardPoolType.Weapon,
        RewardPoolType.WeaponUpgrade,
    };

    /// <summary>
    /// 보스 보상을 뽑을 풀을 _result 에 담는다 (먼저 비운다). 무기 보유 상한이면 Weapon 을 뺀다
    /// </summary>
    public static void GetPoolTypes(bool _isWeaponFull, List<RewardPoolType> _result)
    {
        _result.Clear();
        foreach (RewardPoolType type in PoolTypes)
        {
            if (true == _isWeaponFull && RewardPoolType.Weapon == type)
            {
                continue;
            }
            _result.Add(type);
        }
    }

    /// <summary>
    /// 보스 보상으로 채운 선택지 수가 이것이면 일반 레벨업 보상으로 대신하는지
    /// </summary>
    public static bool ShouldFallback(int _bossRewardCount)
    {
        return _bossRewardCount <= 0;
    }
}
