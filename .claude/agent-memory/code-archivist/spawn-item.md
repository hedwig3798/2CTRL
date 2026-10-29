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
