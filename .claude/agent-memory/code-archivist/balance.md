---
name: balance
description: 밸런스 변경(2026-11-02) 웨이브 배율, 플레이어 배율 하한, 무기 상한, 몬스터별 EXP, 레벨 공식, 디버프 규칙, 빌더. 설계 docs/balance-design.md
metadata:
  type: project
---
## 웨이브
- WaveData(SpawnSystem/WaveData.cs): hpRate/damageRate/speedRate (기본 1).
- Spawner: CurrentWaveIndex/CurrentWave/GetWaveRate(type). 최종 배율 = 웨이브배율 x (base+ΣAdd) x (1+ΣMul). Count는 웨이브 배율 없음. Wave 코루틴이 currentWaveIndex 갱신, 이후 스폰(소환/분열 포함)부터 적용.
- Wave30Builder: 14웨이브(50x3, 150x11), 공통 DAMAGE_RATE/SPEED_RATE, 차원별 hpRate·spawnInterval(설계 7.2장), 스폰거리 11~13, 옛 에셋 안 지움. ValidateCommon/ValidateTable/LoadPrefabs/WavePath.
## 플레이어 스탯
- PlayerStats: GetMultiplier = max(minMultiplier 0.5, 1+ΣMul). Get = (base+ΣAdd) x 배율. maxWeaponCount 4(시작 무기 포함), AddWeapon은 상한이면 false.
- Weapon: Final* = (무기값 + 플레이어 GetAdd) x 플레이어 GetMultiplier (ApplyOwnerStat). Damage/AttackSpeed/Range 모두 곱연산.
- 무기 태그 (2026-11-06): `StatSystem/WeaponStatTag.cs` [Flags] {None, Damage=1, AttackSpeed=2, Range=4, Duration=1<<3, ProjectileCount=1<<4, All=5개 전부}, `WeaponStatTagUtils.FromValueType`/`Has(tags, ValueType)` (HP·MoveSpeed 는 항상 false). 태그 없는 수치는 플레이어 ΣAdd/배율 무시(무기 값 그대로). Weapon.statTags 기본 All. 빌더 `WeaponTagBuilder`: 기본 Damage|AttackSpeed|Range; Range 제외 Saw/Shockwave/Tornado/Vine; Duration = Vine/PoisonZone/Root/Flame; ProjectileCount = Boomerang/Drone/Explosive/Laser/Lightning/Spirit/SplitShot/Tornado/Root. GetTags(name) 공개. ValueType 끝에 Duration/ProjectileCount(무기 전용, 플레이어 base 0; 보상 에셋 아직 없음). 테스트 `Tests/EditMode/WeaponStatTagTests.cs`.
## EXP / 레벨
- MonsterData.expValue(기본 1) -> DropManager가 죽은 MonsterBrain.Data.expValue를 아이템 expRate로(없으면 defaultExpRate 1).
- LevelTable: baseExp/coef/power/formulaLevelOffset(1). 필요 EXP = baseExp + coef x L^power, L=currLevel+offset. UseFormula/HasData/GetNeedExp(level). 둘 다 0이면 expTable 사용. ExpSystem은 GetNeedExp 위임.
## 선택지
- 디버프는 패시브 버프 패널에만, 무기 상한 시 Weapon 풀 차단 (reward-ui.md 참고).
## 빌더
- WeaponUpgradeBuilder: UPGRADE_REWARD_WEIGHT 1.5, BOOMERANG_RETURN_DAMAGE_RATE 1.5, POISON_SLOW_RATE 0.3.
- 주의: MonsterKeywordBuilder(Editor/Builders)는 몬스터 HP/데미지를 코드에 하드코딩. 밸런스 수치 바꾸면 여기도 확인.

## 데이터 빌더 (2026-11-02)
- `Assets/Editor/Builders/BalanceDataBuilder.cs` (메뉴 Tools/Builders/BalanceData, 멱등): 플레이어 HP, 몬스터 HP·접촉 데미지·EXP, 레벨 공식, 무기 데미지, 패시브 
## 데이터 빌더 (2026-11-02)
- `Assets/Editor/Builders/BalanceDataBuilder.cs` (메뉴 Tools/Builders/BalanceData, 멱등): 플레이어 HP, 몬스터 HP·접촉 데미지·EXP, 레벨 공식, 무기 데미지, 패시브 %, 보상 가중치를 기획서 값으로 SerializedObject 로 적용. `ApplyWeaponDamage()` 로 다른 빌더가 덮어쓴 무기 데미지를 복구.
- 순서: WeaponUpgradeBuilder → Wave30Builder → (필요 시) BalanceDataBuilder.
