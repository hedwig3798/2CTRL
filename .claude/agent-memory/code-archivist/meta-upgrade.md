# 영구 강화(차원력) — 설계: docs/meta-upgrade-design.md (최종 갱신 2026-11-18)

## Assets/Resources/Script/MetaSystem/
- PermanentUpgradeData: 강화 1종 (Id, Stat(ValueType), Mode(StatModifierMode), ValuePerLevel, MaxLevel, BaseCost, CostGrowth, NameKey, Icon)
- PermanentUpgradeRegistry: Upgrades 목록 (SO)
- MetaRewardConfig: 보상 계수 (WaveCoef 15, KillCoef 0.005, LevelCoef 2, VictoryRate 1, DefeatRate 0.5)
- MetaProgress (static): PlayerPrefs 저장. 키 접두 "Meta."(데모 빌드 "MetaDemo."), Currency/Level.<id>, DEMO_CURRENCY 9999999, event OnChanged
- MetaReward (static + 결과 struct): ClearedWaves/Kills/Level/Victory -> WaveValue/KillValue/LevelValue/Rate/Total
- PermanentUpgradeApplier: Registry, IsApplied. Start(-200)에서 최초 적용 + HP 가득. Apply() 재호출은 HP 회복 안 함.

## 연동 변경
- PlayerStats: 영구 계층 permAdd/permMul. GetAdd/GetMultiplier에 포함, GetMultiply는 레벨업만. GetLevelUpAdd 추가(레벨업 ΣAdd 만) — DimensionWeapon·WeaponTestController 패시브 변화량에 사용.
- DimensionWeapon/DimensionStatMath: 영구 계층 오버로드
- GameOverManager.EndGame: LockStatistics 직후 GrantMetaReward (패배 연출 중 이탈해도 지급). ShowResult는 표시만(GameOverUI.SetMetaReward, metaRewardLabel). 결과 문자열 meta.result는 실제 수치(웨이브 수/처치 수/레벨) 표시.
- TitleMenu: upgradeButton/upgradePanel
- UI/Title/PermanentUpgradePanel, PermanentUpgradeRow: 강화 목록 UI
- StringTable.csv: meta.* 키

## 빌더 (Assets/Editor/Builders/)
- PermanentUpgradeBuilder.cs: 에셋 Assets/prefab/Upgrade/Permanent/ 생성, GameScene에 Applier 배치
- GameOverBuilder: MetaRewardLabel / TitleSceneBuilder: UpgradeButton, UpgradeCanvas/PermanentUpgradePanel
- Visual/UpgradeUIVisual.cs: 행 스타일·버튼 disabled 표시
- 재실행 순서: TitleSceneBuilder/GameOverBuilder -> ArtResourceBuilder -> UpgradeUIVisual (안 하면 스타일 사라짐). TitleSceneBuilder는 script eval로 실행.

## 테스트
- Assets/Tests/EditMode/MetaUpgradeEditTests.cs, Assets/Tests/PlayMode/MetaUpgradePlayTests.cs
