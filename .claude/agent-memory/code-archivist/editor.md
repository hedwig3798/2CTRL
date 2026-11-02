# editor
`Assets/Editor/` (asmdef Game.Editor): `EditModeUIHider`(static), `TileDataEditor`. 빌더는 `Assets/Editor/Builders/`.
Runtime asmdef: `Assets/Resources/Script/Game.Runtime.asmdef`.

## 차원 관련 빌더 (미커밋)
- `DimensionDataBuilder.cs` (Tools/Builders/DimensionData): Wave30Builder 선실행 후 `Resources/Data/Dimension/{Forest,Factory,Storm}.asset` 생성, `prefab/Player/Dimension/Player_*.overrideController` 생성.
- `DimensionGameSceneBuilder.cs`: GameScene에서 WFCChunkLoader 제거(적용기가 생성), DimensionApplier_Left/Right 배치.
- `DimensionSelectSceneBuilder.cs`: DimensionSelectScene 생성.
- `Wave30Builder`: 차원별 웨이브 생성, 씬 스포너 덮어쓰기 제거. `MonsterKeywordBuilder`: 씬 덮어쓰기 제거.
