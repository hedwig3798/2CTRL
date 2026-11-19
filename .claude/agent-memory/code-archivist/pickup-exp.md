# 획득 범위 / EXP 배율 / 타일 틈 (2026-11-18)

## EXP 배율
- ExpSystem: `expGainMultiplier`(SerializeField, 기본 1.5) + `ExpGainMultiplier` 프로퍼티. `GetExp` 는 받은 값 그대로 더함.
- ItemCollector.Absorb: 아이템 EXP x 배율 -> `ExpSystem.GetExp` 와 `EventManager.OnExpGained` 에 같은 값 전달, 반환.
- 테스트 기대값(FinalBossPlayTests EXP 이벤트)에 배율 반영.

## 아이템 획득 범위 스탯
- `ValueType.PickupRange` (enum 끝, index 8). StringTable: `stat.pickup_range`, `reward.passive.pickup_range.name`.
- PlayerStats.EnsureBaseValues: 자식 `ItemTrigger.BaseRadius` 를 base 로 읽음.
- ItemTrigger: OnEnable 에서 PlayerStats.OnStatChanged 구독, CircleCollider2D 반경 = `PlayerStats.Get(PickupRange)` (Multiply 배율, 하한 minMultiplier 0.5). Player 프리팹 기본 반경 1.43.

## 보상
- PassiveRewardBuilder: `PickupRangeUpgrade`(PassiveBuff +10%, StatPercentEffect, `reward.passive.buff_ga`) / `PickupRangeNuff`(PassiveNerf -6%, `reward.passive.nerf_ga`), 가중치 1.
- 에셋: Assets/prefab/Upgrade/PassiveBuff/PickupRangeUpgrade.asset, Assets/prefab/Upgrade/PassiveNuff/PickupRangeNuff.asset.
- 아이콘 Common_Icon_PickupRangeUp/Down.png: tools/art/pickup_icons.py(저장소 아트 합성)로 생성, ArtResourceBuilder.ICONS 매핑.

## 타일 경계 틈
- WFCChunkLoader.seamOverlap(기본 0.002): 타일 localScale x/y = tileScale x (1+seamOverlap), 이웃과 살짝 겹침.
- 원인: 타일별 사각형 래스터화 틈(경계가 픽셀 중앙에 걸릴 때 배경색 1px). 시트 여백 문제 아님.

## 테스트
- PlayMode/PickupRangeAndExpPlayTests.cs, EditMode/TileSeamTests.cs 신규, PassiveRewardTests 에 PickupRange 케이스.
- 물리 트리거 테스트는 실제 레이어(LPlayer/LItem) 필수 (Default x Default 충돌 꺼짐).

## 특수 아이템 연계 (2026-11-19)
- 자석은 같은 진영 보석을 SlingshotMovement.Pull로 끌어와 기존 Absorb(EXP 배율 1.5 등)로 흡수. 상세는 spawn-item.md "특수 드랍 아이템".
