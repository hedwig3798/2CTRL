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
