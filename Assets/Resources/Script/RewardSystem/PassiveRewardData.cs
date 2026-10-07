using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 base 스탯을 바꾸는 패시브 보상 공통
/// </summary>
public abstract class PassiveRewardData
    : RewardData
{
    [Header("modifier")]
    public List<CharacterStatModifier> modifiers = new List<CharacterStatModifier>();

    // 패시브는 중첩이 기본이므로 생성 시 무제한으로 설정
    protected virtual void Reset()
    {
        maxPickCount = 0;
    }
}
