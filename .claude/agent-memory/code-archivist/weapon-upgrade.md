---
name: weapon-upgrade
description: 무기 업그레이드 시스템, 플레이어별 보상 풀, Chain/각 무기 ★ 효과, 부메랑 귀환, 몬스터 둔화 (설계 docs/weapon-upgrade-design.md)
metadata:
  type: project
---
기준: 2026-10-29 미커밋 diff. 경로 접두 `Assets/Resources/Script/`. 코드와 다르면 코드가 우선.

## WeaponUpgradeSystem/
- `WeaponParam.cs`: enum WeaponParam(공통: Damage/AttackSpeed/Range/FireCount/HitCount/AreaRadius/Lifetime/Knockback/SpeedRate/FireDelay/ChainCount/ChainRange/ProjectileScale, 100~ 투사체 계열: Child*/OrbitRadius/LaserWidth/FanAngle/ArmDelay/TickInterval/Fragment*/Reach, 200~ 플래그 Flag*). WeaponParamUtils: IsCommon, IsFlag, SlotCount.
- 플래그: ReturnDamage, SecondaryExplosion, ChainExplosion, SlowZone, ContractWave, Resplit, SlashWave, ChainAmplify (값>0이면 ON).
- `WeaponUpgradeTable.cs`: WeaponModifier(param,mode,value), WeaponUpgradeStep(title,description,Modifiers), WeaponUpgradeTable(StepCount, GetStep(level), RewardWeight, SetSteps).
- Multiply = 기본값 대비 비율(0.2=+20%, 비율끼리 합산), Add = 그대로 가산.

## Weapon 런타임 수정치 (ComabtSystem/Attackable/Projectile/Weapon.cs)
UpgradeLevel/UpgradeVersion, UpgradeTable, WeaponPrefab, ApplyUpgrade(step), AddModifier, SetModifier, ResetUpgrades, GetParamAdd/Multiply, GetParam(param[,base]), GetParamInt, HasFlag. 각 *Projectile이 발사 시 GetParam으로 수치를 읽음.

## 보상 연동 (RewardSystem/)
- `Effects/WeaponUpgradeRewardEffect.cs`: static CreateRewardData(table,prefab,name,sprite..), ResolveManager(ctx), Apply: 선택 시 해당 무기 ApplyUpgrade -> RemoveFor 현재단계 -> 다음 단계 AddFor(10단계 이후 없음). Table/WeaponPrefab/Level.
- `RewardManager`: ExcludeFor/IsExcludedFor/AddFor/RemoveFor/GetPoolFor/ClearFor(side 단위, 플레이어별 숨김·전용 풀 `WeaponUpgrade`), GetRandomUnique(count,types,side,result) side 버전(공유 풀+플레이어별 반영), GetWeapon(prefab). 무기 획득(WeaponRewardEffect) 시 ExcludeFor + 1단계 AddFor.
- `RewardContext`: Side, Manager 필드 추가. `RewardData.SetEffect(effect)`(런타임 생성용). `UI/SelectPanelGroup.cs`가 side 버전 GetRandomUnique 호출.

## IOneHitProjectileHook.cs (Projectile/)
- IHitDamageModifier.GetHitDamageRate(proj,hitIndex): 타격 배율, 여러 개면 곱. OneHitProjectile.OwnerWeapon, SetHitCount 추가.
- IProjectileHitListener.OnProjectileHit(proj,target,hitIndex): 타격 직후 알림.

## 무기별 (Weapons/*) ★ (5·10단계)
- Boomerang/BoomerangUpgrade: IHitDamageModifier, FlagReturnDamage면 ReturnMovement.IsReturning 중 2배.
- Chain/ChainAmplifier(IHitDamageModifier, FlagChainAmplify: 전이마다 +15% 누적), Chain/ChainLightningLine(IProjectileHitListener, 번개 표시). 무기 기본: Repeat, dmg10, 공속0.8, 사거리7, chainCount3, chainRange3; ChainMovement+OneHitProjectile, 프리팹 prefab/weapon/Chain/.
- Explosive: FlagSecondaryExplosion(0.3s 뒤 반경60%/데미지50%). Mine: FlagChainExplosion(범위 내 활성 지뢰 연쇄). PoisonZone: FlagSlowZone(틱마다 monster.Context.ApplySlow, 기본 -40%). Shockwave: FlagContractWave(소멸 후 수축 파동 50%). SplitShot: FlagResplit(SplitFragmentResplit.Arm/OnProjectileHit, 조각 첫 타격 위치에서 3개). Sweep: FlagSlashWave(formation 0이 조준 방향 관통 검기 60%). Drone/Laser: 파라미터 수정치 사용(드론 탄 Child*, 레이저 FanAngle/LaserWidth).

## 기타
- `MoveSystem/ReturnMovement.cs`: 되돌아서는 순간 출발지 방향 1회 고정, 끝까지 직진, catchDistance 제거(닿아도 안 사라짐, 소멸=Lifetime), IsReturning 공개.
- 몬스터 둔화: `MonsterSystem/MonsterContext.cs` ApplySlow(rate,duration)/ClearSlow()/SlowMultiplier, MoveSpeed에 곱함. MonsterBrain이 재활성 시 ctx.ClearSlow().
- `StatSystem/PlayerStats.cs`: 변경 있음(세부 미기록).

## 리뷰 수정사항
- `WeaponUpgradeRewardEffect.RemoveFromPool` 추가(풀에서 제거).
- Shockwave: BeginContract에서 재초기화.
- Mine: 지역 연쇄 리스트 사용.
- `WeaponParamUtils.CommonParams` 추가.

## 빌더
- `Assets/Editor/Builders/WeaponUpgradeBuilder.cs`
- `Assets/Editor/Builders/Visual/WeaponChainSweepVisual.cs`

## 에셋
- `prefab/Upgrade/WeaponUpgrade/*UpgradeTable.asset` 10개
- `prefab/weapon/Chain/` (Chain, ChainLightning)
- `prefab/weapon/Sweep/SweepSlash.prefab`
- ChainReward/ChainRewardEffect

## 테스트
- `Assets/Tests/EditMode/WeaponUpgradeTests.cs`
- `Assets/Tests/PlayMode/ReturnMovementPlayTests.cs`
