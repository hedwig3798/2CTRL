# init / event / utils
## InitializeSystem
- `Initializable`: `void Initialize(BlackBoard)`. 구현: HealthSystem, Knockback, ContactDamage, OneHitProjectile, Lifetime, ExpItem, ScaleOverLifetime, Dissolve, 이동류 전부(Straight/Chasing/Chain/Return/Slingshot/Orbit/Follow), Explosive/LaserBeam Projectile, PoisonZoneLandMovement.
- `BlackBoard` + `DATA_TYPE` enum(BlackBoard.cs:4): startPosition,startPoint,moveTarget,moveSpeedRate,moveTargetPoint,HPRate,damageRate,expRate,formationIndex/Count,targetLayer,chainCount,chainRange,hitCount,lifetime,areaRadius. 키 추가 = enum 값 추가.
- `BlackBoardHandler`(MonoBehaviour): Projectile이 보유, Initializable들에 배포.
## EventSystem
- `EventManager`(MonoBehaviour, 자동생성 싱글턴 Instance, 씬 유지): `Action<GameObject> OnEnemyDeath`, `Action<GameObject,int> OnLevelUp`. 발행=직접 Invoke, 구독=+=/-=. 현재 OnLevelUp 발행: ExpSystem, 구독: LevelUpUI, SelectPanelGroup.
## Utils
- GamePause(static), LayerUtils, MathUtils, DIRECTION enum, PriorityQueue<,>, RandomPool<T>(ISerializationCallbackReceiver), SerializableInterface<T>(+PropertyDrawer).
