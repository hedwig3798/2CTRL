using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 차원 하나의 설정 (선택 카드, 시작 무기, 무기 풀, 상대 진영 웨이브, 몬스터·플레이어 외형, 맵)
/// - DimensionApplier 가 게임 시작 시 진영에 적용한다
/// - waves / monsterSkins 는 이 차원을 고른 진영이 아니라 반대 진영 스포너에 들어간다 (이 차원의 몬스터가 상대를 침공)
/// </summary>
[CreateAssetMenu(fileName = "DimensionData", menuName = "2CTRL/Dimension/DimensionData")]
public class DimensionData
    : ScriptableObject
{
    [Header("Card")]
    [Tooltip("카드에 표시할 이름 StringTable 키 (예: dimension.forest.name. 키가 없으면 그대로 표시, 비우면 에셋 이름)")]
    [FormerlySerializedAs("displayName")]
    [SerializeField]
    private string displayNameKey;

    [SerializeField]
    private Sprite previewSprite;

    [SerializeField]
    private Color themeColor = Color.white;

    [Header("Weapon")]
    [Tooltip("시작 무기. weaponRewards 에 같은 무기 보상이 있으면 그 보상으로 획득 처리한다")]
    [SerializeField]
    private Weapon startWeapon;

    [Tooltip("이 차원 플레이어의 무기 보상 풀 (공유 Weapon 풀 대신 쓴다)")]
    [SerializeField]
    private RewardData[] weaponRewards;

    [Header("Monster")]
    [Tooltip("상대 진영 스포너에 넣을 웨이브")]
    [SerializeField]
    private WaveData[] waves;

    [Tooltip("이 차원 몬스터 외형 (상대 진영 스포너에 적용)")]
    [SerializeField]
    private MonsterSkin[] monsterSkins;

    [Header("Map")]
    [Tooltip("이 진영의 맵")]
    [SerializeField]
    private WFCChunkLoader chunkLoaderPrefab;

    [Header("Player")]
    [SerializeField]
    private AnimatorOverrideController playerAnimator;

    [Tooltip("Animator 가 없을 때 쓸 플레이어 스프라이트")]
    [SerializeField]
    private Sprite playerSprite;

    /// <summary>
    /// 현재 언어 표시 이름
    /// </summary>
    public string DisplayName => true == string.IsNullOrEmpty(displayNameKey) ? name : StringTable.Resolve(displayNameKey);
    public string DisplayNameKey => displayNameKey;
    public Sprite PreviewSprite => previewSprite;
    public Color ThemeColor => themeColor;
    public Weapon StartWeapon => startWeapon;
    public RewardData[] WeaponRewards => weaponRewards;
    public WaveData[] Waves => waves;
    public MonsterSkin[] MonsterSkins => monsterSkins;
    public WFCChunkLoader ChunkLoaderPrefab => chunkLoaderPrefab;
    public AnimatorOverrideController PlayerAnimator => playerAnimator;
    public Sprite PlayerSprite => playerSprite;

    /// <summary>
    /// weaponRewards 중 startWeapon 을 주는 보상 (없으면 null)
    /// </summary>
    public RewardData FindStartWeaponReward()
    {
        if (null == startWeapon || null == weaponRewards)
        {
            return null;
        }

        foreach (RewardData reward in weaponRewards)
        {
            if (null != reward && reward.GetEffect() is WeaponRewardEffect effect && startWeapon == effect.WeaponPrefab)
            {
                return reward;
            }
        }
        return null;
    }
}
