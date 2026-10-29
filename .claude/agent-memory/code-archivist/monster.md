# 몬스터 시스템 (MonsterSystem)

경로: `Assets/Resources/Script/MonsterSystem/` (+ `Keywords/`). 설계: `docs/monster-keyword-design.md`.
몬스터 행동을 키워드(SO) 조합으로 정의. 키워드 없으면 기본 추적 이동 + 접촉 데미지. StraightMovement 대체.

## 핵심 타입
- `MonsterData` (SO, `2CTRL/Monster/MonsterData`): baseMoveSpeed, targetLayer(LayerMask), `MonsterKeyword[] keywords`(순서대로 처리). Move 카테고리 2개 이상이면 OnValidate 경고.
- `MonsterKeyword` (abstract SO): `Category`(Move/Attack/Death), 수치만 보유, `CreateRuntime()`. `KeywordRuntime<T>` 제네릭 베이스(생성자로 SO 데이터 받음).
- `KeywordRuntime` (abstract, 개체별 상태). 훅(전부 virtual 기본 무동작):
  OnInitialize, TryMove(→bool 이동 처리함), CanStartAction, TelegraphTime, OnTelegraphStart/Tick,
  OnActionStart, TickAction(→true 종료), OnActionEnd, Tick(매 프레임), InterceptDeath(→bool),
  ReviveDelay, ReviveHPRatio, OnRevive, OnDeath, OnDespawn.
- `MonsterContext`: 루트/HealthSystem/Knockback/SpriteRenderer/target/moveSpeedRate/damageRate/spawner/data/brain. 헬퍼 DistanceToTarget, Move, MoveTowards, SetPosition, Face.
- `StateMachine` / `IMonsterState`, `MonsterStateId` {None, Move, Telegraph, Action, Downed, Dead}.
- `MonsterStates.cs`: MonsterMoveState, MonsterTelegraphState, MonsterActionState, MonsterDownedState, MonsterDeadState.
- `MonsterBrain` (MonoBehaviour, `Initializable`): 런타임 Awake에서 1회 생성, Initialize(BlackBoard)마다 리셋. 흐름 Move -> (CanStartAction) Telegraph -> Action -> Move; 사망 가로채기 시 Downed -> ReviveFromDowned; 최종 Dead.
  public: Initialize, StartAction(rt), ChangeToMove, ChangeToAction, EndActiveAction, RaiseActionTelegraph(rt), ReviveFromDowned. 이벤트 `OnStateChanged(MonsterStateId)`.
  HealthSystem.DeathInterceptor에 키워드 InterceptDeath를 연결.
- `HazardZone` (풀링 장판 MonoBehaviour): SetPool, Activate(...), static Spawn(...). 일정 간격 범위 데미지.
- `MonsterAreaDamage` (static): `Apply(center, radius, layer, damage, knockbackForce, attacker)` -> 맞은 수. DamagePipeline로 데미지, 중심에서 바깥 넉백.

## 키워드 10종 (Keywords/, 메뉴 `2CTRL/Monster/Keyword/*`)
| 키워드 | 훅 | 주요 필드 |
|---|---|---|
| Dash | CanStartAction/Telegraph/TickAction | triggerRange, telegraphTime, dashSpeed, dashDuration, cooldown |
| Orbit (Move) | TryMove | startRadius, minRadius, shrinkSpeed, angularSpeed |
| Predict (Move) | Tick/TryMove | leadTime, maxLeadDistance, velocitySmoothing |
| Teleport | Action | minDistance, telegraphTime, postDelay, cooldown, fadeOnTelegraph |
| Summoner | Tick/TryMove/Action | minionPrefab, count, maxAlive, spawnRadius, telegraphTime, cooldown, keepDistance(+Tolerance); spawner.SpawnAt 사용 |
| SelfDestruct | Telegraph/Action/OnDeath | triggerRange, telegraphTime, radius, damage, knockbackForce, explodeOnDeath, inflateScale; MonsterAreaDamage 사용 |
| HazardTrail | Tick | zonePrefab, interval, radius, duration, tickInterval, damagePerTick |
| KnockbackResist | OnInitialize/OnDespawn | resistance |
| Revive (Death) | InterceptDeath | reviveCount, reviveDelay, hpRatio |
| Split (Death) | OnDeath | childPrefab, count, scatterRadius; spawner.SpawnAt 사용 |

## 의존
HealthSystem(DeathInterceptor/Revive/Kill), Spawner.SpawnAt, BlackBoard.spawner, DamagePipeline, Knockback. 자세한 공유 변경은 combat.md, spawn-item.md, init-event.md 참고.
SO 에셋 위치(설계): `Assets/Resources/Data/Monster/`(+`Keywords/`), 프리팹 `Assets/prefab/Monster/<이름>/`.

## 후속 변경 (2026-10-29)
- `MonsterContext.TargetLayer` = data.targetLayer & `Physics2D.GetLayerCollisionMask(root 레이어)`. 좌·우 진영 분리(LMonster↔LPlayer, RMonster↔RPlayer). Default 레이어는 아무것과도 충돌 안 하므로 Spawner 밖에서 만든 몬스터는 대상 없음(테스트는 레이어 27/30 충돌을 켜서 씀).
- `MonsterBrain.OnDespawned` 이벤트(OnDisable 첫 줄). Summoner가 소환체 추적에 사용. ContactDamage 누락 검사는 Awake 1회.
- `MonsterSpawnGraph.WarnIfCycle(keyword, start)`: Split/Summoner/MonsterData OnValidate에서 프리팹 순환 경고. `MonsterKeyword.SpawnTarget` 가상 프로퍼티.
- HealthSystem.Die: isDowned를 인터셉터 호출 전에 세움, 인터셉터 안에서 Revive됐으면 OnDeath 생략.
- 연출: `MonsterSystem/Visual/MonsterVisualFX`(예고 깜빡임·Downed 회색, 알파는 곱연산, UI 자식 역스케일 보정), `HazardZoneVisual`(RemainRate 페이드).
- 빌더: `Assets/Editor/Builders/MonsterKeywordBuilder.cs`(SO·프리팹·웨이브 1_1~1_3) → 그 뒤 반드시 `Builders/Visual/MonsterVisual.cs` 재실행(안 하면 연출 사라짐).
- 에셋: `Resources/Data/Monster/`(MonsterData 6종, Keywords/ 10종), `prefab/Monster/{Bomber,Slime,MiniSlime,Wraith,Shaman,Golem,HazardZone}`, `prefab/Monster/Bomber/BomberExplosion.prefab`.
