using UnityEngine;

/// <summary>
/// 레벨업 보상 종류
/// </summary>
public enum REWARD_TYPE
{
    /// <summary>
    /// 아직 습득하지 않은 신규 무기
    /// </summary>
    NewWeapon,
    /// <summary>
    /// 습득한 무기의 base 스탯 강화
    /// </summary>
    WeaponUpgrade,
    /// <summary>
    /// 캐릭터 base 스탯 강화
    /// </summary>
    PassiveBuff,
    /// <summary>
    /// 캐릭터 base 스탯 약화 (강화 선택지에 조합됨)
    /// </summary>
    PassiveNerf,
}

/// <summary>
/// 레벨업 보상 공통 데이터
/// RewardPool 에 들어가 가중치 랜덤으로 선택된다
/// </summary>
public abstract class RewardData
    : ScriptableObject
{
    [Header("info")]
    public string rewardID;
    public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;

    [Header("pool")]
    [Tooltip("랜덤 선택 가중치 (0 이하는 뽑히지 않음)")]
    public float weight = 1f;
    [Tooltip("선택 가능 횟수. 도달하면 풀에서 제거 (0 이하는 무제한)")]
    public int maxPickCount = 1;

    public abstract REWARD_TYPE RewardType { get; }
}
