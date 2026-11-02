# statistics (게임 오버 통계, 2026-11-02)
경로: `Assets/Resources/Script/StatisticsSystem/`, `UI/GameOverUI.cs`, 테스트 `Assets/Tests/EditMode/StatisticsManagerTests.cs`

## 클래스
- `STAT_TYPE` {Kill, DamageTaken, WeaponDamage, Exp}, `StatRecord`(struct: type, side, value, weaponKey).
- `PlayerCombatStats`(순수 클래스, 플레이어 1명분): Kills, DamageTaken, Exp, WeaponDamage(무기키->누적), TotalWeaponDamage, GetSortedWeaponDamage(list), Apply(record), Reset().
- `StatisticsManager`(MonoBehaviour 싱글턴 Instance/HasInstance): 큐(pending) + 코루틴이 프레임당 maxRecordsPerFrame(64) 처리. Left/Right PlayerCombatStats.
  - API: Get(side), Enqueue(record), EnqueueKill(GameObject 몬스터; 레이어 LMonster->Left, RMonster->Right 로 귀속), EnqueueWeaponDamage(side, Weapon, dmg), Flush()(남은 큐 즉시 반영+OnRecorded), ResetAll(), GetWeaponKey(Weapon)(DescriptionHolder.GetName, 없으면 오브젝트명에서 "(Clone)" 제거; 캐시), PendingCount/IsProcessing. 이벤트 `OnRecorded`.
  - 이벤트 구독부(OnEnable/OnDisable)는 파일 후반, 아래 흐름 참조.
- `GameOverManager`(MonoBehaviour): EventManager.OnPlayerDeath 구독 -> TriggerGameOver(side): 1회만, GamePause.Pause(this), stats.Flush(), gameOverUI.Show(stats), `OnGameOver` 발행. IsGameOver/DeadSide. OnDestroy 에서 GamePause.Resume.
- `GameOverUI`(UI/GameOverUI.cs): Show(StatisticsManager), Hide(), IsShown (panel 토글).

## 이벤트 흐름
- 킬: MonsterBrain.HandleDeath(부활 인터셉트 통과 후, 레이어 변경 전) -> EventManager.OnEnemyDeath(go) -> StatisticsManager.EnqueueKill.
- 무기 데미지: Projectile.SetupDamage 에서 damageMassage.weapon=_weapon -> HealthSystem(적).PublishDamage -> OnWeaponDamage(weapon.OwnerStats.Side, weapon, applied) -> EnqueueWeaponDamage. 무기 없는 데미지(접촉/장판)는 미발행.
- 피격: HealthSystem(playerStats 있음).PublishDamage -> OnPlayerDamaged(side, applied). applied>0 일 때만(무적 무관 실제 감소량).
- EXP: ItemCollector(Awake 에서 ownerStats 탐색) -> OnExpGained(side, exp).
- 사망: HealthSystem.Die -> playerStats 있으면 OnPlayerDeath(side) -> GameOverManager.

## 씬 배치 (2026-11-02)
- 빌더 `Assets/Editor/Builders/GameOverBuilder.cs` (메뉴 Tools/Builders/GameOver, 멱등): GameScene 을 열어 갱신.
- 루트: StatisticsManager, GameOverManager(statisticsManager, gameOverUI 연결), GameOverCanvas(Overlay, sortingOrder 100, Scaler 1920x1080) > GameOverUI > Panel(불투명 검정, 기본 꺼짐) > Title, LeftCard>LeftLabel, RightCard>RightLabel.
- Linear 색공간이라 반투명 오버레이가 많이 비침 → Panel alpha 1, 카드 alpha 0.035.
- IMGUI 디버그(PlayerStatDebugUI 등)는 패널 위에 그려짐.
