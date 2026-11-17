# 차원 무기 (화면 공용 합성 무기) — 2026-11-17 기록
설계서: docs/dimension-weapon-design.md. 숲 6종 + 공장 6종 10레벨 재료 1:1 합성 -> 화면 공용 무기 6종. Root(뿌리 속박) 무기/RootProjectile 삭제됨.
경로 접두 S = Assets/Resources/Script/. 1076b3a 커밋 = 공통 베이스, 나머지(UI 카드·6종·테스트)는 미커밋(작성 시점).

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
SpiritSquadron(정령 추격+드론 사격), ThornSaw(톱날 3배+덩굴 장판), PhantomWhirl(회전검+주기 잔상 즉사), PrismLaser(+PrismBranchPlanner<T>/PrismLink/IPrismTargetSource, 레이저 분기), PlagueMine(+PlagueMineBomb/PlagueMineMath/PlagueZone, 지뢰 추적·끌어당김·독 장판), BlazeBoomerang(+BlazeBoomerangProjectile/BoomerangFlight struct, 지속 화염).

## 보상·UI·연동
- S/RewardSystem/Effects/DimensionFusionEffect.cs: RewardEffect 파생. static CreateReward(recipe), Apply: 재료 RemoveWeapon -> 양쪽 AddDimensionWeapon(공유 인스턴스), 보상 풀 정리, IsApplied.
- S/UI/DimensionFusionCard.cs: 중앙 합성 카드(Show/Hide/SetCursor(left,right), OnClicked(PlayerSide), IsLeft/RightSelected). DimensionFusionCursor: 순수 로직(Begin/End/MoveToCard/MoveToGroup/HandleInput/ShouldApplyFusion/CanResume/SideFromClick/FusionContextSide).
- LevelUpUI 변경: fusionRegistry/fusionCard 필드, SetupFusion(isBoss 라운드 제외), 키 D(왼쪽)/<-(오른쪽)=카드로, A/->=복귀, 카드 선택 상태 Space = 합성만 적용하고 라운드 종료(ApplyFusionReward), HasFusionCard/FusionCandidate/IsFusionSelected/SelectFusionCard 공개.
- PlayerStats: OnWeaponsChanged 이벤트(OnWeaponAdded 대체 구독처 WeaponHUD), RemoveWeapon(prefab), AddDimensionWeapon(inst,data)/HasDimensionWeapon/DimensionWeapons/DimensionWeaponDatas, MinMultiplier, WeaponCount/IsWeaponFull에 차원 무기 포함.
- WeaponHUD: 일반 무기 뒤에 차원 무기 슬롯 채움. WeaponSlotUI.SetDimensionWeapon(inst, icon, nameKey).
- 통계: DamageMassage.dimensionWeapon 있으면 HealthSystem이 EventManager.OnDimensionWeaponDamage(DimensionWeapon, float) 발행 후 return (기존 무기별 통계 경로 우회).
- 테스트(EditMode): DimensionFusionTests, StageQuadTreeTests, DimensionFusionCursorTests, DimensionWeapons_{MineBoomerang,SpiritThorn,WhirlPrism}Tests.
- 미반영(빌더/프리팹/SO/레이어 Stage 등록 등 에디터 작업은 이 기록 범위 밖, editor.md 미갱신).
