# 코드 지도 목차 (초기 작성 2026-10-28, 선언부 위주)
- [combat.md](combat.md) — Projectile/Weapon/DamagePipeline/IDamageable, Weapons 파생
- [move.md](move.md) — MoveSystem 이동 컴포넌트
- [spawn-item.md](spawn-item.md) — Spawner, 풀, 드롭, ExpSystem, Tile(WFC)
- [reward-ui.md](reward-ui.md) — RewardEffect/Manager, PlayerStats, Description, UI
- [effect.md](effect.md) — EffectSystem
- [init-event.md](init-event.md) — Initializable/BlackBoard, EventManager, Utils
- [editor.md](editor.md) — Editor, asmdef
- [monster.md](monster.md) — MonsterBrain 상태머신, 키워드 SO/Runtime 훅, 키워드 10종, HazardZone
오타 폴더/클래스: ComabtSystem, DamageMassage, ArrowMovemnt, DissolveEffectContorller (그대로 유지). 파일명≠클래스명: StraightMovement.cs=Straight, FollowMovement.cs=Follow.
- [weapon-test.md](weapon-test.md) — UI/Debug/WeaponTest 무기 테스트 컨트롤러, DPS 미터, 더미 배치
- [weapon-upgrade.md](weapon-upgrade.md) — 무기 업그레이드, 플레이어별 보상 풀, Chain, 무기 ★, ReturnMovement, 몬스터 둔화

- [statistics.md](statistics.md) — StatisticsManager/GameOverManager/GameOverUI, 통계 이벤트 흐름
- [title.md](title.md) — TitleMenu, TitleSceneBuilder, 씬 순서 Title(0)/Game(1)
- [dimension.md](dimension.md) — DimensionData/Applier/Selection, 차원 선택 UI, Spawner/RewardManager 연동
- [balance.md](balance.md) — 웨이브 배율, 스탯 하한 0.5, 무기 상한 4, 몬스터 EXP, 레벨 공식, 디버프 규칙, 빌더
- [settings.md](settings.md) — GameSettings(PlayerPrefs)/16:9 Letterbox/SettingsApplier, 설정창·일시정지 UI, 설정 빌더 5종과 실행 순서
