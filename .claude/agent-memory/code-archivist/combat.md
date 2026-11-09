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

## 통계 연동 (2026-11-02, [statistics.md](statistics.md))
- `DamageMassage.weapon`(Weapon, 통계 귀속용; 접촉/장판 데미지는 null). `Projectile.SetupDamage` 에서 `damageMassage.weapon = _weapon`(보조 발사체 포함).
- `HealthSystem.PublishDamage(ref msg, applied)`(private): applied>0 일 때 호출. playerStats 있으면 OnPlayerDamaged, 아니면 msg.weapon.OwnerStats 기준 OnWeaponDamage. `Die()` 에서 playerStats 있으면 OnPlayerDeath(Side).
- (2026-11-03) WEAPON_TYPE 추가: Capped(공격 간격마다 1개 발사, 살아 있는 발사체 >= fireCount 면 발사/타이머 정지), Wave(발사체가 모두 사라진 뒤 간격 후 fireCount개 일괄). Weapon.cs UpdateCapped/UpdateWave, 살아있는 발사체 목록으로 셈.
- PoisonZoneProjectile 상속 훅(protected virtual): LaunchZone(발사, 기본 AimAtNearest), IsSlowActive(기본 FlagSlowZone), OnLandedUpdate(착지 후 매 프레임), OnTick(radius, hitCount; ApplyAreaDamage가 hitCount 반환). 파생은 Awake/Update 선언 금지(가려짐). Vine/Root가 사용. 신규 무기 상세 weapon-upgrade.md.

## 무기 태그 (2026-11-06, [balance.md](balance.md))
- `Weapon.statTags`(기본 All), `Weapon.IsAffectedBy(ValueType)`. ApplyOwnerStat 이 태그 없는 수치는 플레이어 보정 무시. FinalCoolTime/FinalFireDelay 도 따라감.
- 2차: Weapon.FinalFireCount(fireCount<=0→0, 아니면 최소1), LaunchCount(Projectile.HandlesFireCount(virtual)면 1개만), FinalLifetime(<=0→0=발사체 기본값, BlackBoard lifetime), ApplyDuration(초). 발사 루프는 LaunchCount 사용. Lightning HandlesFireCount=true(낙뢰 수=StrikeCount+FinalFireCount-1).
- `DroneProjectile.SetupChildWeapon`: 자식 무기에 부모 statTags 복사, 단 ProjectileCount 제거(드론 수에만 적용) (Drone 프리팹 한 곳 관리).
- `PlayerStatDebugUI.FormatWeaponStat`: 태그 없으면 "태그 없음: 버프 무시" 표시. 발사 수(FinalFireCount)/지속 시간(FinalLifetime) 줄도 표시.
- Tornado 수명은 weapon.FinalLifetime (Duration 태그 있음). Flame 은 Capped(ExtraWeaponBuilder 도 Capped), 분사 시간을 공격 간격으로 자르지 않음(분사 끝난 뒤 쿨타임). Flame 공속 2.22(`ExtraWeaponBuilder.FLAME_ATTACK_SPEED`, 분사 0.8s+쿨 0.45s=주기 1.25s 로 이전 DPS 유지).
- `Editor/Builders/WeaponTagBuilder.cs` (Tools/Builders/WeaponTag, 멱등): 무기 프리팹 재생성 빌더 뒤에 다시 실행 필요.

## 추가 무기 후속 (2026-11-03)
- `Projectile.RefreshDamage()`(protected): weapon.FinalDamage × 발사 배율(LaunchAt damageRate)과 knockback 재기록. 오래 사는 발사체(Saw 튕김, Tornado 영구, Spirit)가 타격 직전 호출.
- `PoisonZoneProjectile.SlowRate`(protected virtual) 훅. Root 는 `rootSlowRate` 0.4 로 override.
- Saw: 카메라 탐색 실패 캐시(cameraRetryInterval 1초). Tornado: brainCacheLimit 256. Spirit: UpgradeVersion 변경 시 hitCount 증가분을 remainingHits 에 더함.

## SpinBlade/StormOrb 추가 (2026-11-09)
- HealthSystem: 정적 `ActiveEnemies`, `CollectAlive(mask, list, includeDowned)` (플레이어 제외, MonsterBrain 없는 bat 포함), `Execute(ref msg)` / `IsExecuted` (부활·분열·자폭 무시, MonsterDeadState 가 체크).
- Weapon: `RemainingCoolTime` 추가. AttackSpeed 태그가 없으면 FinalAttackSpeed 하한 0.1 미적용.
- SpinBlade (`Weapons/SpinBlade`): 공장 전용, Repeat 60초, 태그 Damage 만, 자식 SpinBladeTelegraph, ★10 FlagSpinBladeExecute 즉사.
- StormOrb (`Weapons/StormOrb`): 폭풍 전용, Persistent, OrbitSpeedRate, FlagStormOrbLink.
- 빌더: ExtraWeaponBuilder 9종, WeaponTagBuilder NoAttackSpeed, `ArtResourceBuilder.BuildSpinBladeStormOrb`(tools/art/spin_storm.py).
- 차원 무기 수: 숲 7 / 공장 6 / 폭풍 6 ([dimension.md](dimension.md)).
