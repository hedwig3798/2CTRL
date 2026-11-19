# spawn / item / tile / exp
## SpawnSystem
- `Spawner`(MonoBehaviour): SpawnModifierType {Count,HP,Damage,Speed}, WaveData(SO) 기반 웨이브.
- `Spawnable`, `SpawnableInitData`, `SpawnData`(struct), `IPoolable`(빈 마커 인터페이스), `DespawnOutOfCamera`, `Lifetime`(신규, Initializable, lifetime 키).
## ItemDropManager
- `DropManager`, `Item`, `ExpItem`(+Initializable), `ItemCollector`, `ItemTrigger`.
- `ItemCollector`: Awake 에서 ownerStats(owner 또는 부모의 PlayerStats) 캐시, EXP 수집 시 EventManager.OnExpGained(Side, exp) 발행 ([statistics.md](statistics.md)).
## ExpSystem
- `ExpSystem`(싱글턴 Instance/HasInstance): currLevel/currExp/maxExp, LevelTable(SO), GetExp(float). 이벤트 `levelUpAction`, `OnExpChanged(float,float)`; 레벨업 시 EventManager.OnLevelUp 발행(ExpSystem.cs:83).
## Tile
- `TileData`, `TileSet`(SO), `TileSocket`(SO), `WFCChunkLoader`(+CellState, TileWeightPair): WFC 청크 생성.

## 몬스터 시스템 연동 변경 (2026-10-29)

### Spawner (`SpawnSystem/Spawner.cs`)
- 풀: `poolDict[Spawnable prefab] -> ObjectPool`. 웨이브 초기화 시 `GetPool` 호출로 미리 생성.
- `private GetPool(Spawnable)`: 없으면 지연 생성(웨이브에 없는 소환/분열 몬스터용). CreateObject도 GetPool 사용.
- `public Spawnable SpawnAt(Spawnable prefab, Vector2 pos)`: 풀에서 꺼내 위치 지정, BlackBoard 세팅(dropManager, spawner=this, HPRate/moveSpeedRate/damageRate 현재 보정값, moveTarget=target) 후 `blackBoardHandler.Initialize()`. 실패 시 null(풀 반환).
- `Spawn(SpawnData)` 코루틴: 위치 계산 후 SpawnAt 호출 (기존 인라인 로직 제거).
- CreateObject에서도 `data.spawner = this` 세팅.
- 사용처: SummonerKeyword, SplitKeyword ([monster.md](monster.md)).

### 30분 웨이브 (2026-10-29)
- 빌더 `Assets/Editor/Builders/Wave30Builder.cs` (메뉴 `Tools/Builders/Wave30`, 멱등): 차원별 웨이브 `SpawnSystem/WaveData/Dimension/<차원>/` 생성(14웨이브: 50초x3 + 150초x11, 웨이브별 hp/damage/speedRate는 balance.md). 씬 스포너 waveArray 덮어쓰기는 제거됨(웨이브는 DimensionApplier가 SetWaves로 주입).
- 구성 표는 빌더 안 `Table()`(DimensionTable: monsters/hpRate/intervals, NO=0은 미출현), 스폰거리 11~13. spawnInterval은 Spawner 1개 기준. 마지막 웨이브는 30분 이후에도 계속 돈다.
- MonsterKeywordBuilder도 씬 Spawner 덮어쓰기 제거됨(재실행 시 웨이브 되돌림 문제 해소).

## 차원 연동 (Spawner, 미커밋)
- `SetWaves(WaveData[])`: waveArray 교체 + PreparePools(Awake의 풀 생성 분리). Start 전에 호출해야 첫 웨이브부터 적용.
- `SetMonsterSkins(MonsterSkin[])`: MonsterData->MonsterSkin skinMap 갱신(중복은 경고 후 덮어씀). 이후 스폰부터 적용.
- `SpawnAt`에서 `ApplySkin(sa)`: SkinTarget(원본 컨트롤러/스프라이트 저장)으로 Animator 있으면 컨트롤러, 없으면 스프라이트 교체. 스킨 없으면 원본 복구. 상세: dimension.md

- 웨이브 시간 API(IsStarted/RemainingTime 등) 추가: [hud.md](hud.md)
- (2026-11-06) 디버그: `Spawner.DebugSkipWave()`(`#if UNITY_EDITOR || DEVELOPMENT_BUILD`). 그 조건에서 Wave() 대기는 WaitForSeconds 대신 `Time.time < currentWaveStartTime+currentWaveTime` 루프(yield null) + 스킵 플래그. 릴리스 빌드는 기존 WaitForSeconds.

## 특수 드랍 아이템 (자석·무적, 2026-11-19, 프리팹 미작성)
- `ItemDropManager/PowerUpItem.cs`: enum `POWER_UP_TYPE{Magnet,Invincible}`. 필드 type/invincibleDuration(5)/magnetPullSpeed(20)/magnetPullAcceleration(40)/owner. `Collect()`(최초 true, owner 비활성), `Layer`. 프리팹은 보석과 같은 구성(tag item+Spawnable+SlingshotMovement+BlackBoardHandler) 전제.
- `DropManager`: `specialItems`(Spawnable[]), `specialDropChance`=0.001(종류 합산, 균등 선택). `DropItem`(일반 처치) 끝에 `TryDropSpecialItem`; 보스 `DropExp` 제외. 프리팹별 별도 풀(`specialItemPools`), `CreateObject(prefab,pool)`로 시그니처 변경. static `RollSpecialItem(chance,count,roll,pick)`->인덱스/-1 (테스트용). 드랍 레이어=DropManager 레이어.
- `ItemCollector.ApplyPowerUp`: Magnet -> static `PullGems(layer,target,speed,accel)`: `ExpItem.CollectActive` 중 같은 레이어 보석에 `SlingshotMovement.Pull` (즉시 흡수 X, 도착 시 기존 트리거 Absorb). Invincible -> 플레이어 하위 `InvincibleBuff.Activate(duration)` (없으면 경고).
- `SlingshotMovement.Pull(target,maxSpeed,accel)`: 속도/가속은 원래 값과 큰 쪽, 한 프레임 이동량을 남은 거리로 제한. Initialize 때 pull 값 리셋.
- (2026-11-19 통합 완료) `DropManager` specialItems 는 null 제외 압축 후 사용. 빌더 `Editor/Builders/SpecialDropBuilder.cs`(Tools/Builders/SpecialDrop, 멱등): magnetItem/invincibleItem 프리팹(expGem 기반+PowerUpItem), DropManager.prefab specialItems·확률 0.001, Player.prefab damaePipeline 에 InvincibleBuff(blinkRenderers=visual). 실행 후 ArtResourceBuilder.Build() 재실행 필요(아이콘 덮어씀). 테스트: EditMode/SpecialDropEditTests.cs(21), PlayMode/SpecialDropPlayTests.cs(20; PlayMode 동기 실행은 도메인 리로드로 끊김 -> --async_tests + test status).
