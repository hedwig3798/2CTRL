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
- `WeaponUpgradeTable.cs`: WeaponModifier(param,mode,value), WeaponUpgradeStep(title,description,Modifiers), WeaponUpgradeTable(StepCount, GetStep(level: 1부터, 범위 밖 null; 다음 단계 = UpgradeLevel+1), RewardWeight, SetSteps).
- Multiply = 기본값 대비 비율(0.2=+20%, 비율끼리 합산), Add = 그대로 가산.

## Weapon 런타임 수정치 (ComabtSystem/Attackable/Projectile/Weapon.cs)
UpgradeLevel/UpgradeVersion (UpgradeTable/WeaponPrefab은 Weapon에 없음 -> WeaponRewardEffect.WeaponPrefab/UpgradeTable 프로퍼티), ApplyUpgrade(step), AddModifier, SetModifier, ResetUpgrades, GetParamAdd/Multiply, GetParam(param[,base]), GetParamInt, HasFlag. 각 *Projectile이 발사 시 GetParam으로 수치를 읽음.

## 보상 연동 (RewardSystem/)
- `Effects/WeaponUpgradeRewardEffect.cs`: static CreateRewardData(table,prefab,name,sprite..), ResolveManager(ctx), Apply: 선택 시 해당 무기 ApplyUpgrade -> RemoveFor 현재단계 -> 다음 단계 AddFor(10단계 이후 없음). Table/WeaponPrefab/Level.
- `RewardManager`: ExcludeFor/IsExcludedFor/AddFor/RemoveFor/GetPoolFor/ClearFor(side 단위, 플레이어별 숨김·전용 풀 `WeaponUpgrade`), GetRandomUnique(count,types,side,result) side 버전(공유 풀+플레이어별 반영), GetWeapon(prefab). 무기 획득(WeaponRewardEffect) 시 ExcludeFor + 1단계 AddFor.
- `RewardContext`: Side, Manager 필드 추가. `RewardData.SetEffect(effect)`(런타임 생성용). `UI/SelectPanelGroup.cs`가 side 버전 GetRandomUnique 호출.

## IOneHitProjectileHook.cs (Projectile/)
- IHitDamageModifier.GetHitDamageRate(proj,hitIndex): 타격 배율, 여러 개면 곱. OneHitProjectile.OwnerWeapon, SetHitCount 추가.
- IProjectileHitListener.OnProjectileHit(proj,target,hitIndex): 타격 직후 알림.

## 무기별 (Weapons/*) ★ (5·10단계)
- Boomerang/BoomerangUpgrade: IHitDamageModifier, FlagReturnDamage면 ReturnMovement.IsReturning 중 returnDamageRate배(빌더 1.5, 이전 2배).
- Chain/ChainLightningProjectile (Projectile 직접 상속): OnLaunch 프레임에 FinalRange 내 최근접 적 -> chainRange 전이로 최대 min(1+chainCount, hitCount)명 즉시 타격(public StruckCount). 선(LineRenderer 자식 line)은 hold 0.1 + fade 0.3 뒤 풀 반환. ★FlagChainAmplify amplifyRate(전이마다 누적)를 내장. speedRate 효과 없음. (ChainAmplifier/ChainLightningLine 삭제됨)
- ChainLightning.prefab = Transform + ChainLightningProjectile + 자식 line. 콜라이더/Lifetime/BlackBoardHandler/visual 없음. 무기 기본: Repeat, dmg10, 공속0.8, 사거리7, chainCount3, chainRange3. 프리팹 prefab/weapon/Chain/.
- ChainMovement는 템플릿 prefab/weapon/bullet/ChainProjectile.prefab 전용(Chain 무기는 미사용). 빌더 WeaponUpgradeBuilder.BuildChainProjectile(RemoveIfExists 헬퍼). 테스트 PlayMode ChainLightningPlayTests(7). ArtResourceBuilder/WeaponChainSweepVisual은 Chain visual 처리 안 함.
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

## 밸런스 반영 (2026-11-02, 상세 balance.md)
- WeaponUpgradeBuilder: 업그레이드 보상 가중치 1.5, Chain 프리팹 damage 8, 부메랑 귀환 데미지 1.5배(returnDamageRate), PoisonZoneProjectile slowRate 0.3(둔화 -30%), 일부 ★ 수치 하향.

## 차원별 추가 무기 (커밋 6c6fa1a 베이스 + 미커밋 구현, 2026-11-03, 설계: docs/weapon-dimension-extra-design.md)
- WeaponParam 추가: ChaseSpeed=115, BurstDuration=116, PullForce=117, StrikeCount=118 / 플래그 FlagVineThorn=208, FlagRootChase=209, FlagSawBounce=210, FlagBurn=211, FlagTornadoPermanent=212, FlagStormLink=213. (FanAngle=108, ProjectileScale=100 재사용)
- 경로 `Script/Weapons/<이름>/`. 모두 Projectile 파생, 이동/수명 컴포넌트 없이 직접 처리하는 것이 많음(Spirit/Saw/Tornado/Lightning).
- Vine/VineProjectile(:PoisonZoneProjectile) 주인 위치에 조각 깔기, segmentSpacing 미만이면 생략, VineTrail(무기에 붙어 마지막 조각 기억). ★가시 FireSub. 프리팹에 PoisonZoneLandMovement 금지.
- Root/RootProjectile(:PoisonZone) 사거리 내 랜덤 위치에 지역, 둔화 항상. ★FlagRootChase 가장 가까운 적 추적(ChaseSpeed).
- Spirit/SpiritProjectile(Capped) 대기→추적(SPIRIT_STATE Idle/Chase), hitCount 타격 후 소멸, TickInterval/ChaseSpeed 업글.
- Saw/SawProjectile(Wave) 카메라 시야 좌/우 끝에서 가로 관통 이동, ProjectileScale, ★FlagSawBounce 화면 경계 튕김.
- Flame/FlameProjectile(Repeat, fireCount1) LastMoveDirection 방향 부채꼴 분사(WEAPON_TYPE Capped, 분사 끝난 뒤 쿨타임; 분사시간=ApplyDuration(BurstDuration), FanAngle), ★FlagBurn → Flame/FlameBurn(화상 DoT, static Apply).
- Tornado/TornadoProjectile(Capped) 떠돌며 MonsterContext.root 위치를 직접 끌어당김(PullForce, Knockback.resistance 반영), ★FlagTornadoPermanent 수명 무한+leash 복귀. 무기 knockbackForce 0.
- Lightning/LightningProjectile(Repeat, fireCount1) StrikeCount명 표식(markDelay) 후 낙뢰 범위 데미지(낙뢰 수 StrikeCount+FinalFireCount-1), ★FlagStormLink 지점 간 전기선(볼록껍질/선) 데미지. Lightning/LightningLineUtil(static: AddLink/Apply/Clear, LineRenderer 보조).

- 통합(2026-11-03): `Assets/Editor/Builders/ExtraWeaponBuilder.cs`(7종 프리팹 `prefab/weapon/<Name>/`, 업그레이드 테이블, 보상). `DimensionDataBuilder` 무기 목록 숲 7 / 공장 5 / 폭풍 5.
- 아트: `tools/art/extra_weapons.py`(Ninja Adventure + `tools/art/gen` 생성 원본) → `Textuer/Art/Weapon/D?_Weapon_<Name>_*.png`, `Icon/Common_Icon_<Name>.png`.
- 테스트: `Tests/EditMode/ExtraWeaponEditTests.cs`, `Tests/PlayMode/{WeaponTestKit,WeaponFireModePlayTests,ExtraWeaponPlayTests}.cs`.
