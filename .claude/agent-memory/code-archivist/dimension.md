# 차원 선택 (DimensionSystem / UI/DimensionSelect)
설계: docs/dimension-select-design.md. 플레이어별(Left/Right) 차원을 고르고 GameScene에서 적용.

## DimensionSystem (`Assets/Resources/Script/DimensionSystem/`)
- `DimensionData`(SO, CreateAssetMenu 2CTRL/Dimension/DimensionData): displayName, previewSprite, themeColor / startWeapon(Weapon), weaponRewards(RewardData[]) / waves(WaveData[]), monsterSkins(MonsterSkin[]) / chunkLoaderPrefab(WFCChunkLoader) / playerAnimator(AnimatorOverrideController), playerSprite. 읽기 프로퍼티 DisplayName/PreviewSprite/ThemeColor/StartWeapon/WeaponRewards/Waves/MonsterSkins/ChunkLoaderPrefab/PlayerAnimator/PlayerSprite. `FindStartWeaponReward()`: weaponRewards 중 startWeapon 과 같은 무기 보상.
- `MonsterSkin`(struct, Serializable): monster(MonsterData 키), animator, sprite (비우면 프리팹 기본).
- `DimensionSelection`(static): `Set(left,right)`, `Clear()`, `Get(PlayerSide)` -> 씬 간 선택 전달.
- `DimensionApplier`(MonoBehaviour, DefaultExecutionOrder -100): side, defaultDimension(선택 없을 때), player/animator/spriteRenderer, camera, mapLayerName, opponentSpawner, rewardManager, weaponRewardWeight. `Dimension` 프로퍼티. Start 순서: DimensionSelection.Get(side) -> ApplyPlayerSkin -> CreateMap(청크 로더 Instantiate, 카메라 연결, 레이어 지정) -> ApplyOpponentSpawner(SetWaves + SetMonsterSkins + rewardManager.RegisterSpawner(side.Opposite(), opponentSpawner)) -> ApplyRewards(RewardManager.SetPoolFor(side, Weapon, weaponRewards, weight) 후 시작 무기 보상 ApplyReward).

## UI (`Assets/Resources/Script/UI/DimensionSelect/`)
- `DimensionSelectController`: dimensions(2개 이상), gameSceneName, 로드 지연(실시간 초), 진영별 키(prev/next/confirm/confirmAlt, Keyboard 직접 폴링). public GetCursor/GetCurrent/IsConfirmed/CanConfirm/MoveCursor(side,dir)/ToggleConfirm(side). 상대가 확정한 차원은 선택 불가. 둘 다 확정 -> DimensionSelection.Set -> 코루틴 LoadGameScene.
- `DimensionSelectView`: Show(data, confirmed, locked) 카드 표시(themeColor, 확정/잠금 오브젝트).

## 흐름
TitleMenu(기본 씬 DimensionSelectScene) -> Controller 선택/확정 -> DimensionSelection.Set -> GameScene 로드 -> DimensionApplier(진영별) 적용.
RewardManager: `RegisterSpawner(targetSide, spawner)` / `GetSpawnerFor(side)`(받는 진영을 노리는 스포너, 미등록 시 인스펙터 필드/씬 탐색). SpawnerRewardEffect는 RewardContext.Side 진영의 GetSpawnerFor 스포너에 적용.
GameScene: WFCChunkLoader 씬 배치 없음, DimensionApplier_Left/Right 가 생성. 씬 순서 Title(0)/DimensionSelectScene(1)/GameScene(2).
의존: Spawner(SetWaves/SetMonsterSkins), RewardManager(SetPoolFor), WFC 청크 로더, PlayerSide.
