---
name: hud
description: 인게임 HUD (남은 웨이브 시간, 보유 무기 슬롯) 와 Spawner/PlayerStats 확장 API
metadata:
  type: project
---
# 인게임 HUD (Assets/Resources/Script/UI/HUD/, 2026-11-05 미커밋)
## Spawner 추가 (SpawnSystem/Spawner.cs)
- IsStarted, WaveStartTime(시작 전 -1), TotalWaveTime(null/spwanArray null 웨이브 제외 time 합), RemainingTime(시작 전=총합, 끝나면 0, 최소 0).
- Wave 코루틴이 시작 시 총합 계산, 웨이브마다 currentWaveStartTime/currentWaveTime/timeAfterCurrentWave 재보정 (프레임 지연 보정). Time.time 기준이라 timeScale 0 동안 정지.
## PlayerStats 추가 (StatSystem/PlayerStats.cs)
- event OnWeaponAdded(PlayerStats, 프리팹, 생성 무기): AddWeapon 성공 시 호출.
- AcquiredWeaponPrefabs / AcquiredWeapons: IReadOnlyList, 획득 순서(시작 무기 포함), 같은 인덱스끼리 대응.
## 클래스
- WaveTimerUI (TMP_Text, Spawner[]): 스포너 RemainingTime 최댓값을 올림 mm:ss 로 표시, 초 바뀔 때만 SetCharArray(GC 없음). spawners 비면 FindObjectsByType. DisplayedSeconds, GetRemainingTime().
- WeaponHUD (side, PlayerStats, WeaponSlotUI[] slots, DimensionApplier[], RewardData[] fallbackRewards): PlayerStats.Find(side) 지연 바인딩(Update 재시도), OnWeaponAdded 구독, 획득 순서대로 슬롯 채움. 아이콘은 같은 진영 Dimension.WeaponRewards 중 WeaponRewardEffect.WeaponPrefab 일치 보상 이미지, 없으면 fallback, 그래도 없으면 이름 표시+경고. 레벨은 Update 에서 슬롯별 RefreshLevel.
- WeaponSlotUI (iconImage, levelText, nameText): SetWeapon(weapon, icon, fallbackName), Clear(), RefreshLevel()(UpgradeLevel 변경 시만 갱신), Weapon/HasWeapon/DisplayedLevel.
## 의존
Spawner, PlayerStats, PlayerSide, Weapon.UpgradeLevel, DimensionApplier, WeaponRewardEffect, RewardData. 관련: [spawn-item.md](spawn-item.md), [reward-ui.md](reward-ui.md), [dimension.md](dimension.md)
## 통합·시각 단계
- 빌더 `Assets/Editor/Builders/InGameHudBuilder.cs` (Tools/Builders/InGameHud, 멱등: HUDCanvas 삭제 후 재생성). GameScene 에 HUDCanvas(Overlay sortingOrder 10, 1920x1080 스케일러, LetterboxCanvas) / WaveTimer(spawners 2개 직렬 연결) / WeaponHUD_Left·Right(Slot0~3, Slot0=화면 가장자리).
- 시각 빌더 `Assets/Editor/Builders/Visual/InGameHudVisual.cs` (Tools/Builders/Visual/InGameHud): 슬롯 3단 프레임, LevelBadge, WaveTimerPlate, 머티리얼 `Assets/Resources/Textuer/Hud/HudLevelOutline.mat`, `HudTimerOutline.mat`.
- 실행 순서: InGameHudBuilder -> InGameHudVisual -> ArtResourceBuilder. InGameHudBuilder 재실행 시 InGameHudVisual 도 재실행 필수.
- 테스트: Assets/Tests/EditMode/HudEditTests.cs, Assets/Tests/PlayMode/HudPlayTests.cs (PlayMode asmdef 에 UnityEngine.UI 참조 추가).
## 피격 비네트 (2026-11-06)
- HitVignetteUI (`Assets/Resources/Script/UI/HitVignetteUI.cs`): Image 컴포넌트. 부모 PlayerStats 아래 HealthSystem.OnDamaged 구독. 시작 알파 0, 피격마다 1로 리셋, fadeDuration(0.4s) 동안 0으로 감소, 0이면 Image 비활성.
- 빌더 `Assets/Editor/Builders/Visual/HitVignetteBuilder.cs` Build(): `Assets/Resources/Textuer/Hud/HitVignette.png`(붉은 가장자리 그라데이션, Bilinear) 생성 + Player.prefab 의 ScreenSpaceUI 아래 HitVignette 오브젝트(스트레치 Image + HitVignetteUI) 배치. 멱등.
- 실행: `unity command script eval "return Builders.Visual.HitVignetteBuilder.Build();"`
- 텍스처를 Textuer/Art 가 아닌 Textuer/Hud 에 둔 이유: ArtResourceBuilder 가 Art 폴더를 Point 필터로 덮어써 그라데이션이 깨짐.
