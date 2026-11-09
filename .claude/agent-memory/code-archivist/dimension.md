# 차원 선택 (DimensionSystem / UI/DimensionSelect)
설계: docs/dimension-select-design.md. 플레이어별(Left/Right) 차원을 고르고 GameScene에서 적용.

## DimensionSystem (`Assets/Resources/Script/DimensionSystem/`)
- `DimensionData`(SO, CreateAssetMenu 2CTRL/Dimension/DimensionData): displayName, previewSprite, themeColor / startWeapon(Weapon), weaponRewards(RewardData[]) / waves(WaveData[]), monsterSkins(MonsterSkin[]) / chunkLoaderPrefab(WFCChunkLoader) / playerAnimator(AnimatorOverrideController), playerSprite. 읽기 프로퍼티 DisplayName/PreviewSprite/ThemeColor/StartWeapon/WeaponRewards/Waves/MonsterSkins/ChunkLoaderPrefab/PlayerAnimator/PlayerSprite. `FindStartWeaponReward()`: weaponRewards 중 startWeapon 과 같은 무기 보상.
- `MonsterSkin`(struct, Serializable): monster(MonsterData 키), animator, sprite (비우면 프리팹 기본).
- `DimensionSelection`(static): `Set(left,right)`, `Clear()`, `Get(PlayerSide)` -> 씬 간 선택 전달.
- `DimensionApplier`(MonoBehaviour, DefaultExecutionOrder -100): side, defaultDimension(선택 없을 때), player/animator/spriteRenderer, camera, mapLayerName, opponentSpawner, rewardManager, weaponRewardWeight. `Dimension` 프로퍼티. Start 순서: DimensionSelection.Get(side) -> ApplyPlayerSkin -> CreateMap(청크 로더 Instantiate, 카메라 연결, 레이어 지정) -> ApplyOpponentSpawner(SetWaves + SetMonsterSkins + rewardManager.RegisterSpawner(side.Opposite(), opponentSpawner)) -> ApplyRewards(RewardManager.SetPoolFor(side, Weapon, weaponRewards, weight) 후 시작 무기 보상 ApplyReward).

## UI (`Assets/Resources/Script/UI/DimensionSelect/`)
- `DimensionSelectController`: dimensions(2개 이상), columns(기본 3, 1~차원 수로 보정; 왼쪽 위부터 채우는 격자), gameSceneName, 로드 지연(실시간 초), 진영별 키 up/down/left/right/confirm(+confirmAlt) (Left: WASD+Space, Right: 방향키+Enter, Keyboard 직접 폴링), leftView/rightView, gridCells(DimensionGridCell[], dimensions 와 같은 순서, 길이 달라도 안전). public GetCursor/GetCurrent/IsConfirmed/CanConfirm/`MoveCursor(side,dx,dy)`(dy 아래가 +, 반환 bool)/ToggleConfirm(side). 가로는 현재 줄 실제 길이 안에서, 세로는 줄 수 안에서 순환, 세로 이동 후 빈칸이면 그 줄 마지막 칸. 커서는 상대 확정 칸 포함 어디든 이동·겹침 가능(확정한 본인만 이동 불가). 상대가 확정한 차원은 확정만 불가(겹치면 먼저 누른 쪽, 같은 프레임이면 왼쪽). OnCursorChanged(side,index). RefreshViews = 좌우 View + RefreshGridCells(Show 호출). 둘 다 확정 -> DimensionSelection.Set -> 코루틴 LoadGameScene.
- `DimensionSelectView`: Show(data, confirmed, locked) 카드 표시(themeColor, 확정/잠금 오브젝트).
- `DimensionGridCell`(DimensionGridCell.cs): view(DimensionSelectView), leftFrame/rightFrame(Graphic), idleFrameAlpha(0.55). `Show(data, leftHere, leftConfirmed, rightHere, rightConfirmed)`: 커서가 있는 진영 프레임만 활성, 확정 시 알파 1 / 아니면 idleFrameAlpha. 그 칸 위에서 확정한 진영이 있으면 view.Show(data,false,locked=true). null 참조 무시.
- 씬 통합(DimensionSelectSceneBuilder): DimensionGrid(GridLayoutGroup) > Cell_N{LeftCursorFrame, RightCursorFrame, CellCard, LockedMark}, columns·gridCells 연결, 카드 400x600. 프레임 이미지는 흰 Simple 사각형(색은 진영 색으로 칠함). PlayMode 테스트 DimensionSelectPlayTests 에 격자 테스트 추가.

## 흐름
TitleMenu(기본 씬 DimensionSelectScene) -> Controller 선택/확정 -> DimensionSelection.Set -> GameScene 로드 -> DimensionApplier(진영별) 적용.
RewardManager: `RegisterSpawner(targetSide, spawner)` / `GetSpawnerFor(side)`(받는 진영을 노리는 스포너, 미등록 시 인스펙터 필드/씬 탐색). SpawnerRewardEffect는 RewardContext.Side 진영의 GetSpawnerFor 스포너에 적용.
GameScene: WFCChunkLoader 씬 배치 없음, DimensionApplier_Left/Right 가 생성. 씬 순서 Title(0)/DimensionSelectScene(1)/GameScene(2).
의존: Spawner(SetWaves/SetMonsterSkins), RewardManager(SetPoolFor), WFC 청크 로더, PlayerSide.
