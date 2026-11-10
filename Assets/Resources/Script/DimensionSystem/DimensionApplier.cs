using UnityEngine;

/// <summary>
/// 고른 차원을 한 진영에 적용한다 (GameScene, 진영마다 하나)
/// - 차원은 DimensionSelection 에서 가져오고, 비어 있으면 defaultDimension 을 쓴다
/// - 적용 순서
///   1. 자기 진영 플레이어 : 외형(애니메이터 / 스프라이트)
///   2. 자기 진영 맵 : chunkLoader 를 생성하고 카메라를 연결
///   3. 반대 진영 스포너 : 차원의 웨이브와 몬스터 외형 (이 차원의 몬스터가 상대를 침공)
///      RewardManager 에 반대 진영 플레이어를 노리는 스포너로 등록한다 (몬스터 보정 보상용)
///   4. RewardManager : 자기 진영 Weapon 풀을 차원 무기로 바꾸고, 시작 무기를 획득 처리
/// - 모든 Awake / OnEnable(플레이어 등록, 매니져 초기화) 뒤, Spawner / LevelUpUI 의 Start 보다 먼저 적용되도록 실행 순서를 앞당긴 Start 에서 적용한다
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class DimensionApplier
    : MonoBehaviour
{
    [Tooltip("이 적용기가 맡는 진영")]
    [SerializeField]
    private PlayerSide side = PlayerSide.Left;

    [Tooltip("선택 없이 GameScene 을 실행했을 때 쓸 차원")]
    [SerializeField]
    private DimensionData defaultDimension;

    [Header("Player")]
    [SerializeField]
    private PlayerStats player;

    [Tooltip("플레이어 애니메이터 (비어 있으면 player 자식에서 찾음)")]
    [SerializeField]
    private Animator playerAnimator;

    [Tooltip("Animator 가 없을 때 스프라이트를 바꿀 렌더러 (비어 있으면 player 자식에서 찾음)")]
    [SerializeField]
    private SpriteRenderer playerRenderer;

    [Header("Map")]
    [Tooltip("자기 진영 카메라 (맵 로더에 연결)")]
    [SerializeField]
    private Camera sideCamera;

    [Tooltip("맵 로더를 둘 레이어 이름 (자기 진영 카메라만 그리는 레이어). 비우면 프리팹 레이어를 쓴다")]
    [SerializeField]
    private string mapLayerName;

    [Header("Monster")]
    [Tooltip("반대 진영 스포너 (이 차원의 웨이브와 몬스터 외형을 넣음)")]
    [SerializeField]
    private Spawner opponentSpawner;

    [Header("Reward")]
    [Tooltip("비어 있으면 RewardManager.Instance")]
    [SerializeField]
    private RewardManager rewardManager;

    [Tooltip("차원 무기 보상의 가중치")]
    [SerializeField]
    private float weaponRewardWeight = 1f;

    private DimensionData dimension;
    private WFCChunkLoader chunkLoader;

    public PlayerSide Side => side;

    /// <summary>
    /// 자기 진영 카메라 (연결되지 않았으면 null)
    /// </summary>
    public Camera SideCamera => sideCamera;

    /// <summary>
    /// 적용한 차원 (적용 전이거나 없으면 null)
    /// </summary>
    public DimensionData Dimension => dimension;

    /// <summary>
    /// 생성한 맵 로더 (없으면 null)
    /// </summary>
    public WFCChunkLoader ChunkLoader => chunkLoader;

    private void Start()
    {
        dimension = DimensionSelection.Get(side);
        if (null == dimension)
        {
            dimension = defaultDimension;
        }

        if (null == dimension)
        {
            Debug.LogError($"[DimensionApplier] {side} 진영에 적용할 차원이 없습니다. ({name})", this);
            return;
        }

        ApplyPlayerSkin();
        CreateMap();
        ApplyOpponentSpawner();
        ApplyRewards();
    }

    /// <summary>
    /// 1. 플레이어 외형
    /// </summary>
    private void ApplyPlayerSkin()
    {
        if (null == player)
        {
            player = PlayerStats.Find(side);
        }

        if (null == playerAnimator && null != player)
        {
            playerAnimator = player.GetComponentInChildren<Animator>(true);
        }

        if (null == playerRenderer && null != player)
        {
            playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (null != playerAnimator && null != dimension.PlayerAnimator)
        {
            playerAnimator.runtimeAnimatorController = dimension.PlayerAnimator;
        }

        if (null != playerRenderer && null != dimension.PlayerSprite)
        {
            playerRenderer.sprite = dimension.PlayerSprite;
        }
    }

    /// <summary>
    /// 2. 맵 로더 생성 (Instantiate 직후 카메라를 넣어 로더의 Start 에서 그 카메라를 기준으로 시작한다)
    /// </summary>
    private void CreateMap()
    {
        if (null == dimension.ChunkLoaderPrefab)
        {
            Debug.LogWarning($"[DimensionApplier] {dimension.name} 에 chunkLoaderPrefab 이 없습니다. ({name})", this);
            return;
        }

        chunkLoader = Instantiate(dimension.ChunkLoaderPrefab);
        chunkLoader.name = $"{dimension.ChunkLoaderPrefab.name}_{side}";

        if (false == string.IsNullOrEmpty(mapLayerName))
        {
            int layer = LayerMask.NameToLayer(mapLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[DimensionApplier] 레이어 {mapLayerName} 이(가) 없습니다. ({name})", this);
            }
            else
            {
                LayerUtils.SetLayer(chunkLoader.gameObject, layer);
            }
        }

        if (null != sideCamera)
        {
            chunkLoader.targetCamera = sideCamera;
        }
    }

    /// <summary>
    /// 3. 반대 진영 스포너에 이 차원의 웨이브와 몬스터 외형
    /// </summary>
    private void ApplyOpponentSpawner()
    {
        if (null == opponentSpawner)
        {
            Debug.LogWarning($"[DimensionApplier] opponentSpawner 가 연결되지 않았습니다. ({name})", this);
            return;
        }

        opponentSpawner.SetWaves(dimension.Waves);
        opponentSpawner.SetMonsterSkins(dimension.MonsterSkins);

        // 반대 진영 스포너는 반대 진영 플레이어를 노리므로, 그 진영이 받은 몬스터 보상이 이 스포너에 적용된다
        RewardManager manager = ResolveRewardManager();
        if (null != manager)
        {
            manager.RegisterSpawner(side.Opposite(), opponentSpawner);
        }
    }

    private RewardManager ResolveRewardManager()
    {
        return null != rewardManager ? rewardManager : RewardManager.Instance;
    }

    /// <summary>
    /// 4. 자기 진영 Weapon 풀을 차원 무기로 바꾸고 시작 무기를 준다
    /// - 시작 무기 보상이 무기 풀에 있으면 그 보상을 적용해 일반 획득과 같게 처리한다 (풀에서 숨김 + 1단계 업그레이드)
    /// </summary>
    private void ApplyRewards()
    {
        RewardManager manager = ResolveRewardManager();
        if (null == manager)
        {
            Debug.LogWarning($"[DimensionApplier] RewardManager 가 없어 무기 풀을 바꾸지 못했습니다. ({name})", this);
        }
        else
        {
            manager.SetPoolFor(side, RewardPoolType.Weapon, dimension.WeaponRewards, weaponRewardWeight);
        }

        if (null == dimension.StartWeapon)
        {
            return;
        }

        RewardData startReward = dimension.FindStartWeaponReward();
        if (null == manager)
        {
            Debug.LogWarning($"[DimensionApplier] RewardManager 가 없어 시작 무기를 업그레이드 없이 무기만 줍니다. ({name})", this);
        }
        else if (null == startReward)
        {
            Debug.LogWarning($"[DimensionApplier] {dimension.name} 의 weaponRewards 에 시작 무기 보상이 없어 업그레이드 없이 무기만 줍니다. ({name})", this);
        }
        else if (null == PlayerStats.Find(side))
        {
            // ApplyReward 는 PlayerStats.Find(side) 로 플레이어를 찾으므로 등록되지 않았으면 무기를 주지 못한다
            Debug.LogWarning($"[DimensionApplier] {side} 진영에 등록된 PlayerStats 가 없어 시작 무기를 업그레이드 없이 무기만 줍니다. ({name})", this);
        }
        else
        {
            manager.ApplyReward(startReward, side);
        }

        // 보상으로 무기를 받지 못했으면 인스펙터의 player 에게 무기만 준다 (업그레이드 보상 없음)
        if (null != player && false == player.HasWeapon(dimension.StartWeapon))
        {
            player.AddWeapon(dimension.StartWeapon);
        }
    }
}
