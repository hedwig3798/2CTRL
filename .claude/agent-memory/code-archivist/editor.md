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
- (2026-11-18, 아트만 변경, 로직 무변경) 보스 전용 스프라이트: `tools/art/bosses.py` -> `Textuer/Art/Boss/*_Walk.png`(칸=이미지 높이). 매핑 ForestBoss10=GiantBamboo, 20=GiantBamboo2, FactoryBoss10=DemonCyclop, 20=GiantFlam, StormBoss10=GiantSpirit, 20=TenguBlue, FinalBoss=SquidRed(Idle). ArtResourceBuilder: ImportAll/SliceGrid 가 /Boss/ 를 높이 칸으로 슬라이스, Clip() 이 _Boss_ 이름을 Boss/ 에서 찾음, `BuildBosses()`(BuildMonsters 직후)가 중간 보스 6종 visual(스프라이트·MonsterBase 오버라이드 애니·흰색·스케일)과 FinalBoss Visual(지름 4 스케일) 적용. MidBossBuilder/FinalBossBuilder 재실행 후엔 ArtResourceBuilder 재실행. FinalBossVisual 은 보스 틴트 제거(Color.white).
- (2026-11-18) D2 바닥: tiles.py 가 Interior/TilesetInteriorFloor.png 회녹색 판재 블록(11,6)으로 생성(A=리벳, B=테두리, 안쪽 모서리=패널 가운데). 코드 그림 factory/factory_a 제거.
- (2026-11-18) 무기 아트 팩 원본화: fx.py(독장판=Smoke 합성+초록, 분열=Explosion 프레임2, 레이저=IceSpike, 드론탄=EnergyBall, 지뢰=Bomb, 폭발탄=CanonBall, 체인 라인=Thunder 팔레트), extra_weapons.py(인자 <pack_root> <out_dir>, AI gen/ 제거: 덩굴=Particle/Grass, 뿌리=Particle/Leaf, 정령=Spirit 무틴트, 톱날=Shuriken, 회오리=SpriteSheetCircular 호 2개 64x64 -> 회오리 visual 스케일 1.6->0.8), spin_storm.py(인자 <out_dir> <pack_root>, 칼날=BigKunai x2 세 개, 전격구체=Magic/Circle/SpriteSheetSpark2, 연결선=Particle/Spark). 아이콘: Saw=Guard, Lightning=Meteo/Rain, SpinBlade=Spell/MagicWeapon, StormOrb=Spell/OrbWater. 남은 코드 그림: SpinBlade Trail, Common_Monster_HazardZone(blob), 체인 라인 모양.
- 주의: docs/art-resource-report.md 는 메모리 밖이라 갱신 못 함(메인이 직접 처리 필요).
