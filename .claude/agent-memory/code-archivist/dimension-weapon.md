# 차원 무기 (화면 공용 합성 무기) — 2026-11-17 기록
설계서: docs/dimension-weapon-design.md. 숲 6종 + 공장 6종 10레벨 재료 1:1 합성 -> 화면 공용 무기 6종. Root(뿌리 속박) 무기/RootProjectile 삭제됨.
경로 접두 S = Assets/Resources/Script/. 전부 커밋됨(~7436ee0).

## StageSystem (S/StageSystem/, 신규)
- StageSpace: 게임 시작부터 BossSpace 월드를 화면 공간으로 사용. [DefaultExecutionOrder(-500)]로 매 프레임 무기 Update 전 쿼드트리 재구성. static Instance/Targets(준비 안 됐으면 null)/StageLayer(레이어 "Stage"). IsReady, Space(BossSpace), Tree, StageRect, StageCenter, WorldToStage/StageToWorld(PlayerSide,Vector2), IsOnScreen. FinalBossDirector가 오버레이 배치를 재사용.
- StageQuadTree: 노드풀·배열풀(GC 0), 용량 8/깊이 6. Clear(Rect)/Insert(StageTarget), QueryCircle/QueryRect/QuerySegment(a,b,halfWidth)/QueryNearest(c,maxR,filter|exclude set,out), static SegmentDistSq. StageTarget struct(stagePos,radius,side,health,IsAlive).
- StageTargets: HealthSystem.ActiveEnemies(+BossHitProxy)를 StageTarget으로 변환(반지름 콜라이더 캐시), Rebuild(tree, BossSpace). 화면 밖 적 제외.
- StageDamage(static): Apply(HealthSystem|StageTarget, dmg, source)로 DamagePipeline 경유 데미지, Execute(즉사, 면역이면 fallbackDamage). DamageMassage.dimensionWeapon 세팅.

## 공통 베이스 (S/Weapons/Dimension/)
- DimensionWeapon(abstract MonoBehaviour, Weapon 비상속): Initialize(DimensionWeaponData), abstract Fire(), OnActivated(), Update(쿨타임). Final* 스탯(Damage/AttackSpeed/CoolTime/Range/FireCount/Lifetime) = 양쪽 PlayerStats 합산(ApplyCombinedStat), TryGetPlayerStagePos/GetPlayerStagePos(side), StatTags, RemainingCoolTime.
- DimensionWeaponData(SO): NameKey/ExplainKey/Icon/MaterialA(숲)/MaterialB(공장)/FallbackRequiredLevel/ResultPrefab, IsValid, DisplayNameKey, SetRecipe/SetDisplay(빌더용).
- DimensionFusion(static): FindCandidate(s)/IsCandidate/PickRandom/IsMaxLevel/GetRequiredLevel/FindUpgradeTable (양쪽 재료 10레벨 판정, 소유 중이면 제외).
- DimensionFusionRegistry(SO): Recipes, FindCandidate(), SetRecipes. DimensionStatMath: CombinedMultiplier(max(min,1+ΣL+ΣR))/Apply/RoundCount.
- WeaponParam.cs, PoisonZoneProjectile.cs 소폭 수정.

## 6종 (S/Weapons/Dimension/<Name>/, 모두 DimensionWeapon 파생)
SpiritSquadron(정령 추격+드론 사격; 정령 대상 규칙=일반 SpiritProjectile와 동일: 무기 위치 기준 FinalRange 내 타 정령이 미추적인 최근접 적, 보스 MidBoss/BossHitProxy 공유, 죽거나 FinalRange+spiritLeashMargin(2) 밖까지 고정, 대상 없으면 spiritScanInterval(0.1s)마다 탐색; 공개 GetSpiritTarget(int); PlayMode 테스트 SpiritSquadron_SpiritsPickDistinctTargets_AndKeepThem @DimensionWeaponTestToolPlayTests.cs), ThornSaw(톱날 3배+덩굴 장판), PhantomWhirl(회전검+주기 잔상 즉사), PrismLaser(+PrismBranchPlanner<T>/PrismLink/IPrismTargetSource, 레이저 분기), PlagueMine(+PlagueMineBomb/PlagueMineMath/PlagueZone, 지뢰 추적·끌어당김·독 장판), BlazeBoomerang(+BlazeBoomerangProjectile/BoomerangFlight struct, 지속 화염).

## 보상·UI·연동
- S/RewardSystem/Effects/DimensionFusionEffect.cs: RewardEffect 파생. static CreateReward(recipe), Apply: 재료 RemoveWeapon -> 양쪽 AddDimensionWeapon(공유 인스턴스), 보상 풀 정리, IsApplied.
- S/UI/DimensionFusionCard.cs: 중앙 합성 카드(Show/Hide/SetCursor(left,right), OnClicked(PlayerSide), IsLeft/RightSelected). DimensionFusionCursor: 순수 로직(Begin/End/MoveToCard/MoveToGroup/HandleInput/ShouldApplyFusion/CanResume/SideFromClick/FusionContextSide).
- LevelUpUI 변경: fusionRegistry/fusionCard 필드, SetupFusion(isBoss 라운드 제외), 키 D(왼쪽)/<-(오른쪽)=카드로, A/->=복귀, 카드 선택 상태 Space = 합성만 적용하고 라운드 종료(ApplyFusionReward), HasFusionCard/FusionCandidate/IsFusionSelected/SelectFusionCard 공개.
- PlayerStats: OnWeaponsChanged 이벤트(OnWeaponAdded 대체 구독처 WeaponHUD), RemoveWeapon(prefab), AddDimensionWeapon(inst,data)/HasDimensionWeapon/DimensionWeapons/DimensionWeaponDatas, MinMultiplier, WeaponCount/IsWeaponFull에 차원 무기 포함.
- WeaponHUD: 일반 무기 뒤에 차원 무기 슬롯 채움. WeaponSlotUI.SetDimensionWeapon(inst, icon, nameKey).
- 통계: DamageMassage.dimensionWeapon 있으면 HealthSystem이 EventManager.OnDimensionWeaponDamage(DimensionWeapon, float) 발행 후 return (기존 무기별 통계 경로 우회).
- 테스트(EditMode): DimensionFusionTests, StageQuadTreeTests, DimensionFusionCursorTests, DimensionWeapons_{MineBoomerang,SpiritThorn,WhirlPrism}Tests.

## 수정 루프 API (7436ee0까지)
- DimensionFusionEffect: IsFused(recipe)/TryApply(재료·중복 검증 후 적용 여부 반환). DimensionFusion: 진영별 FindUpgradeTable/GetRequiredLevel(side별 재료 기준). LevelUpUI.AbandonFusion(합성 카드 포기/복귀).
- Knockback: ResolvedRoot/Generation(루트 해석 캐시 및 세대 카운터). DimensionStatMath.ScaledTickInterval(공격속도 배율 반영 틱 간격).
- StageSpace: 오버레이 카메라 z 보장(ResolveOverlayZ: 진영 카메라 z<0이면 그 값, 아니면 오버레이 z<0, 아니면 -10). 무대 오브젝트 z=0이라 음수 필수.
- WeaponSlotUI: SetLevelBadgeActive, "LevelBadge"+levelText를 일반 무기는 표시/차원 무기는 숨김.

## 에디터 빌더 (Game.Editor)
- Assets/Editor/Builders/DimensionWeaponBuilder.cs (Builders.DimensionWeaponBuilder.Build()): Root(뿌리 속박) 에셋 정리, 레이어 Stage(19번) 등록, 차원 무기 프리팹/DimensionWeaponData SO/DimensionFusionRegistry 생성, LevelUpUI 합성 카드 배치, StageSpace 배치.
- Assets/Editor/Builders/Visual/DimensionWeaponVisual.cs (Build()): 차원 무기 비주얼 적용. ArtResourceBuilder.Build() 끝에서 호출됨(다른 빌더 재실행 후 ArtResourceBuilder 재실행 규칙에 포함).
- 에셋 위치: Assets/prefab/weapon/Dimension/(+Parts/), Assets/prefab/Upgrade/Dimension/. Stage 레이어=19.

- BlazeBoomerang 수명(2026-11-20): 프리팹 lifetime 0, defaultFlightLifetime 5(코드 기본값도 5, 원본 BoomerangProjectile 과 동일). 무기 lifetime 2초=발사간격 2초라 공중에서 사라지던 문제 수정. 돌아온 뒤에도 계속 날아가 화면 밖에서 종료, 진영당 동시 비행 1개->2~3개. 빌더 DimensionWeaponBuilder Def lifetime 0.
- BlazeBoomerang 화염 분출구(2026-11-20): DimensionWeaponVisual.BuildBlazeBoomerang 이 CreateBlazeJet x BLAZE_JET_COUNT=4 추가. 회전 visual 의 자식, 90° 간격, 로컬 바깥쪽 원뿔 분출(월드 시뮬레이션 -> 회전 따라 나선형 사방 분출, 도달 ≈ 화염 반지름 1.2). BlazeBoomerangProjectile.particles = 기존 flame 궤적 + jet 4개. 데미지 로직 무변화. ArtResourceBuilder 재실행으로 BlazeBoomerangProjectile.prefab 갱신.
