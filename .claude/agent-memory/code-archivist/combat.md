# combat (ComabtSystem, Weapons)
경로: `Assets/Resources/Script/ComabtSystem/` (폴더명 오타 Comabt, 유지), `Weapons/`
## 데미지 (Damageable/)
- `IDamageable.ProcessDamage(ref DamageMassage)` (struct 이름 오타 Massage). 구현: `HealthSystem`(+Initializable), `Knockback`(+Initializable, 신규).
- `DamagePipeline`(MonoBehaviour): DAMAGE_PIPELINE 순서로 처리. enum: INVINCIBLE, DODGE, FIXED, DEFENSE, SHIELD, HEALTH[Pipeline(HealthSystem)], KNOCKBACK[Pipeline(Knockback)], PAYBACK, END. `PipelineAttribute(Type)`로 단계-컴포넌트 매핑.
- 확장: 단계 추가 = enum 값 + [Pipeline] + IDamageable 구현.
## 공격 (Attackable/)
- `Weapon`(MonoBehaviour): WEAPON_TYPE {Repeat(쿨타임마다 fireCount 발사), Persistent(fireCount개 유지)}. 풀에서 Projectile 발사. 무기 스텟 + 주인 PlayerStats = Final* 값.
- `Projectile`(abstract, MonoBehaviour, `Projectile/Projectile.cs`): 필드 blackBoardHandler, weapon, damageMassage, formationIndex/Count. public: SetPool, MarkReturningToPool, SetFormation(i,n), GetBlackBoard, InitializeBlackBoard, Launch(Weapon), LaunchAt(Weapon,start,point,rate). 확장점: `abstract OnLaunch()`, `virtual OnLaunchAt()`, OnEnable/OnDisable. 헬퍼: ApplyDamage, ApplyAreaDamage, SendDamage, AimAtNearest, FindNearest, FindAll, IsAlive.
- 파생: `OneHitProjectile`(+Initializable), `AuraProjectile`(+struct AuraTarget), Weapons/ 아래: Drone, Explosive(+Init), LaserBeam(+Init), Mine, PoisonZone(+PoisonZoneLandMovement), Shockwave, SplitShot, Sweep(+SweepMovement).
- `TargetFinder`(static): 타겟 탐색. `ContactDamage`(+Initializable): 접촉 데미지.
의존: BlackBoard, MoveSystem, SpawnSystem(풀), StatSystem.

## 몬스터 시스템 연동 변경 (2026-10-29)

기록은 몬스터 연동 변경분만 있음. 나머지는 필요 시 코드 확인.

### HealthSystem (`ComabtSystem/Damageable/HealthSystem.cs`)
- 기존: OnDeath(GameObject), OnDamaged(float), isInvincibility, Initialize(BlackBoard)(HPRate 반영).
- 추가(몬스터 키워드용):
  - `Func<bool> DeathInterceptor`: HP<=0 시 OnDeath 전에 호출(단일 소유자, MonsterBrain). true면 OnDeath 없이 다운(`isDead=true`, `IsDowned=true`, 콜라이더 꺼짐).
  - `bool IsDowned`.
  - `Kill()`: 이미 죽었으면 무시, CurrHP=0 후 Die() (인터셉터 거침). 자폭 등.
  - `Revive(float hpRatio)`: 다운 상태에서만 동작. 콜라이더 복구, HP = max(1, MaxHP*ratio).
  - private `Die()`: isDead, 콜라이더 끄기, 인터셉터 -> 다운 또는 OnDeath.
  - Initialize에서 isDowned 리셋.
- 사용처: [monster.md](monster.md)
