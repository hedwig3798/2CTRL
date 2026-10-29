# 몬스터 키워드 / 상태머신 설계

몬스터 행동을 **키워드(ScriptableObject)** 조합으로 정의한다. 키워드가 비어 있으면 그 기능은 없고, 기본 추적 이동 + 접촉 데미지만 한다.
예) `돌진 + 자폭 + 부활` → 돌진하고, 가까우면 자폭하고, 죽으면 한 번 부활하는 몬스터.

## 폴더
- 런타임: `Assets/Resources/Script/MonsterSystem/` (키워드는 `MonsterSystem/Keywords/`)
- SO 에셋: `Assets/Resources/Data/Monster/` (MonsterData), `Assets/Resources/Data/Monster/Keywords/` (키워드 SO)
- 프리팹: `Assets/prefab/Monster/<이름>/`

## 메인이 미리 바꾼 공유 코드 (그대로 사용, 수정 금지)
- `HealthSystem`
  - `Func<bool> DeathInterceptor`: HP 0 시 OnDeath 전에 호출. true면 OnDeath 없이 다운 상태가 된다(`isDead=true`, `IsDowned=true`, 콜라이더 꺼짐).
  - `Revive(float hpRatio)`: 다운 상태에서 되살린다.
  - `Kill()`: 즉시 사망한다. DeathInterceptor를 거친다.
- `Spawner.SpawnAt(Spawnable prefab, Vector2 pos)`: 풀을 지연 생성한다. 현재 보정값으로 BlackBoard를 세팅한 뒤 Initialize하고, 스폰된 Spawnable(또는 null)을 반환한다.
- `BlackBoard.spawner`: Spawner가 스폰 시 넣어준다. 소환과 분열에 쓴다.

## 구성
| 타입 | 종류 | 역할 |
|---|---|---|
| `MonsterData` | SO | baseMoveSpeed, targetLayer(플레이어 LayerMask), `MonsterKeyword[] keywords`. Move 카테고리 키워드가 2개 이상이면 OnValidate에서 경고한다. |
| `MonsterKeyword` | abstract SO | `Category`(Move/Attack/Death) 프로퍼티. 수치 필드를 가진다. `abstract KeywordRuntime CreateRuntime()`. |
| `KeywordRuntime` | abstract C# 클래스 | 몬스터 개체별 상태. 아래 훅을 가진다. SO는 공유되므로 개체 상태는 여기에만 둔다. |
| `MonsterContext` | C# 클래스 | 루트 Transform, HealthSystem, Knockback, SpriteRenderer, target, moveSpeedRate, damageRate, spawner, data, brain, 이동 헬퍼(`Move(Vector2 delta)` + flipX). |
| `StateMachine` / `IMonsterState` | C# | 범용 상태머신(Enter/Tick/Exit). |
| `MonsterBrain` | MonoBehaviour + Initializable | 몬스터 루트에 붙는다. 키워드 런타임을 Awake에서 한 번 생성하고 Initialize마다 리셋한다. 상태머신을 구동한다. 기존 StraightMovement를 대체한다. |

### KeywordRuntime 훅 (모두 virtual, 기본은 아무것도 안 함)
- `OnInitialize(ctx)`: 스폰 또는 재사용 시 리셋.
- `bool TryMove(ctx, dt)`: 이번 프레임 이동을 직접 처리했으면 true. 리스트 순서상 첫 true가 이긴다. 전부 false면 Brain이 기본 추적을 한다.
- 행동(Action):
  - `bool CanStartAction(ctx)`.
  - `float TelegraphTime`.
  - `OnTelegraphStart / OnTelegraphTick(ctx, dt)`.
  - `OnActionStart(ctx)`.
  - `bool TickAction(ctx, dt)`: 끝나면 true.
  - `OnActionEnd(ctx)`: 쿨다운을 시작한다.
- `Tick(ctx, dt)`: 살아 있는 동안 매 프레임 호출(패시브).
- `bool InterceptDeath(ctx)`: true면 사망을 가로챈다(부활).
- `OnDeath(ctx)`: 최종 사망 시 호출(분열, 사망 시 폭발).
- `OnDespawn(ctx)`: OnDisable 정리.

### 상태
`Move` → (행동 가능 키워드 발견) `Telegraph` → `Action` → `Move`
`Downed`: 부활 대기. `Dead`: 최종 사망.

- `Move`:
  - 넉백 중(`Knockback.IsKnockedBack`)이면 이동하지 않는다.
  - 행동 키워드를 리스트 순서대로 검사한다.
- `Telegraph`, `Action`: 넉백을 무시하고 진행한다. 자폭과 돌진이 끊기지 않게 하기 위해서다.
- `isDead`이면 모든 이동과 행동을 중지한다.
- Brain은 `event Action<MonsterStateId> OnStateChanged`와 `event Action<KeywordRuntime> OnActionTelegraph`를 노출한다. 시각 연출 담당이 애니메이터와 색에 연결하는 용도다.
- 부활 흐름:
  1. `HealthSystem.DeathInterceptor`를 Brain이 소유한다.
  2. 키워드 중 하나라도 InterceptDeath가 true면 `Downed` 상태로 간다.
  3. 대기 시간 후 Revive를 호출하고 `Move` 상태로 돌아간다.
- 최종 사망: `HealthSystem.OnDeath` 구독 → `Dead` 상태로 가고 키워드의 OnDeath를 호출한다.

## 키워드 10종
| 카테고리 | 키워드 | 종류 | 수치(SO) | 동작 |
|---|---|---|---|---|
| Move | 돌진 Dash | Action | triggerRange, telegraphTime, dashSpeed, dashDuration, cooldown | 사거리 안이면 멈춰서 예고한다. 예고 시작 시 방향을 고정한 뒤 직선으로 돌진한다. |
| Move | 선회 Orbit | TryMove | startRadius, minRadius, shrinkSpeed, angularSpeed | startRadius 밖이면 접근한다. 안이면 타깃 주위를 돌며 반경을 minRadius까지 줄인다. 방향은 개체마다 랜덤이다. |
| Move | 추적 Predict | TryMove | leadTime, maxLeadDistance | 타깃 위치 + 추정 속도 × leadTime 지점으로 이동한다(최대 거리 제한). 타깃 속도는 위치 변화로 추정한다. |
| Move | 순간이동 Teleport | Action | minDistance, telegraphTime, appearRadius(min,max), postDelay, cooldown | 멀면 예고(사라짐)한 뒤 타깃 주변 링으로 이동하고, postDelay 동안 정지한다. |
| Attack | 자폭 SelfDestruct | Action (+OnDeath 옵션) | triggerRange, telegraphTime, radius, damage, knockbackForce, explodeOnDeath, inflateScale | 가까우면 부풀어 오른 뒤 폭발한다. targetLayer의 반경 내 DamagePipeline에 데미지 × damageRate를 준다. 이후 `Kill()`을 호출한다. explodeOnDeath면 처치될 때도 폭발한다(중복 방지). 부활 시 스케일을 원복한다. |
| Attack | 장판 HazardTrail | Tick | zonePrefab(HazardZone), interval, radius, duration, tickInterval, damagePerTick | 살아 있는 동안 interval마다 발밑에 장판을 남긴다. 장판은 정적 공유 풀을 쓴다. |
| Attack | 소환사 Summoner | Action (+TryMove 옵션) | minionPrefab(Spawnable), count, maxAlive, spawnRadius, telegraphTime, cooldown, keepDistance(0이면 사용 안 함) | `ctx.spawner.SpawnAt`으로 주변에 소환한다. 살아 있는 소환체 수가 maxAlive 이상이면 소환하지 않는다. keepDistance>0이면 그 거리를 유지하도록 이동한다. |
| Death | 분열 Split | OnDeath | childPrefab(Spawnable), count, scatterRadius | 사망 위치 주변에 자식 몬스터를 스폰한다. 자식에는 분열 키워드를 넣지 않는다(무한 방지). |
| Death | 부활 Revive | InterceptDeath | reviveCount, reviveDelay, hpRatio | 남은 횟수가 있으면 다운 → 대기 → 부활한다. |
| Death | 넉백 KnockbackResist | OnInitialize | resistance(0~1) | Knockback.resistance를 설정한다. 디스폰 시 원래 값으로 되돌린다. |

## 장판 HazardZone
- 몬스터 전용 경량 컴포넌트다. Projectile이나 Weapon에 의존하지 않는다.
- `Activate(pos, radius, duration, tickInterval, damage, targetLayer)`.
- tickInterval마다 OverlapCircle을 돌려, 같은 대상에게는 한 틱에 1회만 `DamagePipeline.ProcessDamage`를 호출한다.
- 수명이 끝나면 풀로 반환한다.

## 공통 규칙
- 모든 몬스터 프리팹에는 접촉 데미지(`ContactDamage`가 붙은 `attack` 자식)가 있어야 한다. 없으면 Brain이 Initialize에서 LogError를 남긴다.
- 이동은 transform 직접 조작(기존 방식). 속도 = baseMoveSpeed × moveSpeedRate(0 이하면 1).
- 풀 재사용 환경이다. OnEnable이 Initialize보다 먼저 실행되므로 모든 리셋은 Initialize에서, 정리는 OnDisable에서 한다.

## 예시 몬스터
| 이름 | 키워드 | 비고 |
|---|---|---|
| Bomber (자폭병) | 돌진, 자폭, 부활 | 사용자 예시 |
| Slime (슬라임) | 추적, 분열 → MiniSlime | |
| MiniSlime | (없음) | 기본 추적 + 접촉 데미지 |
| Wraith (망령) | 순간이동, 장판 | |
| Shaman (주술사) | 선회, 소환사 → MiniSlime | |
| Golem (골렘) | 넉백(저항 1), 부활 | 느림, 고HP |
