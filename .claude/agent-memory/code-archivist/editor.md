# editor
`Assets/Editor/` (asmdef Game.Editor): `EditModeUIHider`(static), `TileDataEditor`. 빌더는 `Assets/Editor/Builders/`.
Runtime asmdef: `Assets/Resources/Script/Game.Runtime.asmdef`.

## 차원 관련 빌더 (미커밋)
- `DimensionDataBuilder.cs` (Tools/Builders/DimensionData): Wave30Builder 선실행 후 `Resources/Data/Dimension/{Forest,Factory,Storm}.asset` 생성, `prefab/Player/Dimension/Player_*.overrideController` 생성.
- `DimensionGameSceneBuilder.cs`: GameScene에서 WFCChunkLoader 제거(적용기가 생성), DimensionApplier_Left/Right 배치.
- `DimensionSelectSceneBuilder.cs`: DimensionSelectScene 생성. DimensionGrid(GridLayoutGroup) 아래 Cell_N(LeftCursorFrame/RightCursorFrame/CellCard/LockedMark, DimensionGridCell 연결), controller columns·gridCells 연결, 큰 카드 400x600. ArtResourceBuilder 의 프레임은 흰 Simple 사각형 유지(진영 색 보존).
- `Wave30Builder`: 차원별 웨이브 생성, 씬 스포너 덮어쓰기 제거. `MonsterKeywordBuilder`: 씬 덮어쓰기 제거.
## 아트 리소스 (2026-11-02)
- `Builders/Visual/ArtResourceBuilder.cs` (멱등): Textuer/Art 임포트(PPU32, Point, 그리드 슬라이스), 애니 클립, PlayerBase/MonsterBase 컨트롤러·오버라이드, 차원별 WFC 타일 프리팹(prefab/TileMap/Art/D1~3)+ChunkLoader_D1~3, 무기/아이템/아이콘 스프라이트, UI·폰트(Galmuri11 Pixel TMP), Title/DimensionSelect/GameScene 적용. DimensionData 의 playerAnimator·monsterSkins·chunkLoaderPrefab·previewSprite 채움.
- `Game.Editor.asmdef` 에 `Unity.2D.Sprite.Editor` 참조 추가.
- 기타: `tools/art/*.py`(이미지 생성), `docs/art-resource-report.md`, `CREDITS.md`.
- (2026-11-03) `Visual/ArtResourceBuilder.BuildExtraWeapons`(추가 무기 7종 아트·아이콘), `Visual/WeaponTestUiVisual.cs`(테스트 씬 무기 목록 2열). 실행 순서: ExtraWeaponBuilder → DimensionDataBuilder → ArtResourceBuilder(마지막).
