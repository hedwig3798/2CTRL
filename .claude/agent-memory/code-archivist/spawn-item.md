# spawn / item / tile / exp
## SpawnSystem
- `Spawner`(MonoBehaviour): SpawnModifierType {Count,HP,Damage,Speed}, WaveData(SO) 기반 웨이브.
- `Spawnable`, `SpawnableInitData`, `SpawnData`(struct), `IPoolable`(빈 마커 인터페이스), `DespawnOutOfCamera`, `Lifetime`(신규, Initializable, lifetime 키).
## ItemDropManager
- `DropManager`, `Item`, `ExpItem`(+Initializable), `ItemCollector`, `ItemTrigger`.
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
- 빌더 `Assets/Editor/Builders/Wave30Builder.cs` (메뉴 `Tools/Builders/Wave30`, 멱등): `WaveData/Survival30/S30_01~S30_12` (각 150초, 합 30분) 생성 후 씬의 모든 Spawner(SpawnManager, SpawnManger) waveArray에 연결.
- 구성 표는 빌더 안 `table`. spawnInterval은 Spawner 1개 기준. 마지막 웨이브는 30분 이후에도 계속 돈다.
- 주의: MonsterKeywordBuilder 재실행 시 Spawner가 1_1~1_3으로 되돌아가므로 Wave30Builder를 다시 실행해야 함.
