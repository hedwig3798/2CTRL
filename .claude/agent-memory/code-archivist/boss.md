# 중간 보스 (BossSystem) — 2026-11-11 최종, 기획: docs/boss-design.md
경로: Assets/Resources/Script/BossSystem/ (Game.Runtime). 보스 = MonsterBrain + 키워드 조합 몬스터.

## 클래스
- MidBoss: 보스 컴포넌트. Setup(MidBossEntry), Initialize(BlackBoard), Relocate(), SetAirborne(bool).
  프로퍼티 Brain/Health/Entry/Spawner/Target/Side/HasSide/IsAirborne/SideCamera/DisplayNameKey.
  이벤트 OnRelocated(boss,pos)/OnDefeated/OnDespawned/OnAirborneChanged. static FindSpawnPoint(target, movement, min, max, ...).
- MidBossEntry: 직렬화 데이터(prefab: Spawnable, 등장 시각 등). DimensionData.MidBosses 로 공급.
- MidBossScheduler: 엔트리별 등장 스케줄·경고(OnBossWarning)·스폰·격파 추적. SetEntries(), Entries, DebugSpawnMidBoss(int).
- BossTelegraph(+BossTelegraphStyle): 예고 원판/띠/링(RingCount=2) 표시. static Spawn(style,layer,center), Release,
  SetCenter, ShowDisc/HideDisc, ShowBand/HideBand, ShowRing(i,r,color,dashed)/HideRing, HideAll,
  ArmBlast(delay,radius,damage,targetLayer,attacker,color..) ; static event AnyBlast(pos,radius).
- PlayerForcedMove: 플레이어 강제 이동. static For(Transform), Push(delta,duration), Stop(), IsMoving.
- BossMath(static 순수함수, 테스트용): ForwardRingPoint, ViewRect, Bounce, DiagonalToward, IsRingDanger, PickRingSide,
  IsHPAtOrBelow, ClampPullDistance.
- BossHudUI: 보스 경고 배너·방향 화살표(SideView/Tracked)·보상 타이틀. BossAppearFX: 보스 등장 연출.
- MidBoss 추가 API: SetDamageTakenFactor(source,factor)/ClearDamageTakenFactor(source) (출처별 받는 피해 배율, 공중 시 0),
  ResetBody()(풀 재사용 시 몸 상태 초기화). MidBossScheduler: 스폰 실패 시 재시도.
- BossTelegraph: SceneManager.sceneUnloaded 에서 풀 비움(HandleSceneUnloaded).
- BossRewardRules(static): PoolTypes, GetPoolTypes(isWeaponFull, list), ShouldFallback(bossRewardCount).
- BossKeywordUtil(static): IsEnraged(ctx,ratio), FindTargetPipeline, DamageTarget, Layer(ctx).

## 새 키워드 (MonsterSystem/Keywords/, 패턴은 monster.md 참조)
- OrbitBladesKeyword (Attack, Tick/OnDeath/OnDespawn): 보스 주위 회전 칼날.
- PushPullKeyword (Move, TryMove+CanStartAction+텔레그래프): 플레이어 밀기/당기기(PlayerForcedMove).
- JumpSlamKeyword (Move, 웅크림 TelegraphTime=crouchTime, TickAction): 점프 내려찍기, SetAirborne.
- ScreenBounceKeyword: 화면 사각형 내 대각 바운스(BossMath.Bounce).
- RingJudgeKeyword: 링 안/밖 위험 판정(IsRingDanger/PickRingSide), 링 텔레그래프.

## 공유 파일 변경
- HealthSystem: IsExecuteImmune, IncomingDamageMultiplier (보스 즉사 면역·받는 피해 배율).
- EventManager: Action<PlayerSide,MidBoss> OnBossWarning/OnBossSpawned/OnBossDefeated.
- Spawner: 보스 웨이브 배율 1 고정, SetMidBosses(MidBossEntry[]), DropManager 프로퍼티 공개.
  AddBossCountMultiplier(m)/RemoveBossCountMultiplier(m)(bool)/BossCountMultiplier: 보스 등장 중 스폰 수 곱셈 보정(엔트리 spawnCountMultiplier 중첩 곱).
- DimensionData.MidBosses(midBosses) / DimensionApplier가 Spawner.SetMidBosses 로 전달.
- DropManager: DropExp(pos,totalExp,gemCount), static CalculateGemExpRate; ExpItem 소폭 변경.
- PlayerStats: ApplyTimedSlow(rate,duration), ClearTimedSlow(), TimedSlowRate/TimedSlowEndTime.
- LevelUpUI: pendingCount 대신 라운드 큐(rounds, isBoss/side). OpenBossReward(side), IsBossRound, BossRoundSide,
  PendingRoundCount, event OnRoundShown(bool isBoss, PlayerSide).
- SelectPanelGroup: AutoRefreshOnLevelUp(기본 true), RefreshBossRewards(), CountVisiblePanels().
- HazardZone: SlowRate(둔화), 레이어 인자; HazardTrailKeyword.slowRate.
- MonsterAreaDamage: ApplyWithCooldown(...), ApplyPlayerSlow(center,radius,layer,rate,duration).
- MonsterContext.slowEffectRate(기본 1). DespawnOutOfCamera.FindCameraForLayer(int) public static.
- SpinBladeProjectile 소폭 수정.

## 에셋·빌더·테스트
- 프리팹 Assets/prefab/Monster/Boss/{Forest,Storm,Factory}Boss{10,20}. 데이터 Assets/Resources/Data/Monster/Boss/(*BossNNData, BossTelegraphStyle), 키워드 SO Data/Monster/Keywords/Boss/. 화살표 Textuer/Hud/BossArrow.png.
- DimensionData.midBosses(엔트리: prefab, spawnTime, warningLead=3, spawnCountMultiplier, expTotal, gemCount): 각 차원 600초(x0.7, exp60, gem12)·1200초(x0.6, exp150, gem20).
- 빌더: Editor/Builders/MidBossBuilder.Build() (Tools/Builders/MidBoss), Builders/Visual/BossVisual.Build() (Tools/Builders/Visual/Boss; 빌더 재실행 후 ArtResourceBuilder 도 다시).
- 테스트: Tests/EditMode/BossEditTests, Tests/PlayMode/BossPlayTests, LevelUpRoundQueuePlayTests.

## 최종 보스 (2026-11-11, Assets/Resources/Script/BossSystem/Final/, 기획 docs/boss-design.md 4장)
구조: 보스는 별도 "보스 레이어"(레이어 18 Boss, ResolveBossLayer) 월드에서 진영별 URP Overlay 카메라 2개(BossOverlayCamera_Left/_Right; 각 진영 FollowCamera 스택, rect=진영 카메라와 동일)로 그려진다. 보스 월드 원점 bossWorldOrigin(0,500). BossSpace 가 보스 월드<->진영 월드를 평행 이동으로 변환한다. 진영별 BossHitProxy(투명 히트박스, 진영 몬스터 레이어)가 매 LateUpdate 보스 위치를 그 진영 월드로 옮기고 데미지를 공유 HP(FinalBoss.ApplyDamage)로 전달. 플레이어 피격은 보스 월드 좌표에서 계산(BossAttackResolver) 후 Director.DamagePlayer.
- BossSpace(순수 클래스; ctor(leftOverlay,rightOverlay,leftCam,rightCam)): GetCamera/GetOverlay(side), SideViewRect, TryToSideWorld(bossPos,out side,out world), ToSideWorld/FromSideWorld, SideRect, StageRect, SideScale. static: OrthoWorldRect, MapPoint, MapRect, Union, CircleOverlapsRect, Inset, ContainsInclusive, Translate, PixelsToWorld, OverlayCenters(origin,leftW,rightW,gap,out,out).
- BossAttackResolver(static): CircleContainsPoint, CircleOverlapsCircle, DistanceToSegment, CapsuleContainsPoint.
- FinalBossPatterns.cs: enum FinalBossPattern(LaserMeteor/DashMeteor는 P3 조합), struct LaserLine(a,b), static PhaseFor(ratio,p2,p3), TelegraphTime, PickPattern(last,phase,comboChance,rCombo,rPick), HasSafeSpot, RandomLine, GenerateLasers(안전지대 보장).
- FinalBossData(Serializable 수치): 페이즈 배열 [P1,P2,P3], telegraphStyle, static PhaseValue(float[]/int[],phase,fallback), Telegraph(base,phase).
- FinalBoss: Begin(director,data,bossLayer), ApplyDamage(amount), DebugSetHPRatio. 이벤트 OnPatternStarted/OnMeteorWindup/OnMeteorImpact(pos,r)/OnDashStarted/OnDashEnded/OnLaserFired/OnEntranceFinished/OnDamaged/OnDefeated. 패턴 예고는 BossTelegraph(ShowLine/HideLine 추가) 사용.
- FinalBossDirector(씬에 1개): static Active, Boss, Begin()->bool(참조 부족 시 false), static ResolveBossLayer, TryGetPlayerBossPos(side,out), DamagePlayer(side,dmg,bossPoint)(DamagePipeline, 공격자=진영 프록시), DebugBegin, DebugSetBossHPRatio. Begin 순서: Spawner.StopSpawning -> MidBossScheduler.StopAndRemoveAll/적 조용히 제거 -> ExpItem.CollectActive + ItemCollector.Absorb 로 EXP 흡수 -> 카메라 고정 -> LayoutOverlays(오버레이 두 대를 bossWorldOrigin 기준 배치) -> PlayerScreenClamp -> BgmDirector.PlayOverride -> 인트로 -> 보스 등장. 처치 시 EventManager.OnFinalBossDefeated -> GameOverManager.TriggerVictory. victoryDelay(기본 1.2 실시간초): 처치 후 GamePause 건 채 대기 -> Resume -> TriggerVictory (테스트 rig 는 0 이라 즉시). 필드 leftOverlayCamera/rightOverlayCamera/bossWorldOrigin.
- BossHitProxy: Setup(director,boss,side), Shutdown. 보스 몸체가 그 진영 SideRect 와 겹칠 때만 콜라이더/HealthSystem on. 무적(복구)+즉사 면역.
- PlayerScreenClamp: static Attach(target,camera,margin), Setup, static Clamp(point,rect).
- FinalBossHPBar: 화면 상단 공유 HP 바. OnFinalBossStart 전 알파 0, OnFinalBossHPChanged(cur,max) 갱신, 처치 시 숨김.
- 이벤트(EventManager): OnFinalBossStart, OnFinalBossHPChanged(float cur,float max), OnFinalBossDefeated.
- 공유 파일 변경: GameOverManager.HandleWavesFinished(Active Director.Begin 성공 시 승리 대신 보스전, 실패면 기존 승리), Spawner.StopSpawning, MidBossScheduler.StopAndRemoveAll, ExpItem(static ActiveItems 집합 + CollectActive(List), static Absorb(item,owner) 는 ItemCollector 쪽), ItemCollector.Absorb(static), BgmDirector(static PlayOverride(SoundId)/ClearOverride/HasOverride), BossTelegraph.ShowLine(from,to,width,color)/HideLine.
- 시각 컴포넌트(BossSystem/Final/): FinalBossIntroUI(등장 연출 UI), FinalBossDefeatFX(처치 연출), FinalBossLineGlow(레이저 선 글로우), FinalBossPulse. 빌더 Editor/Builders/FinalBossBuilder.cs, Builders/Visual/FinalBossVisual.cs.
- 에셋: Resources/Data/Monster/Boss/FinalBossData.asset(bossBgm=BGM_RUSH), FinalBossTelegraphStyle.asset(sortingOrder 12). 프리팹 Assets/prefab/Monster/Boss/FinalBoss/(FinalBoss, FinalBossMeteor, FinalBossMeteorShadow).
- 테스트: Tests/EditMode/FinalBossEditTests, Tests/PlayMode/FinalBossPlayTests.
