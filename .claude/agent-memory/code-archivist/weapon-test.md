# 무기 테스트 디버그 UI (Assets/Resources/Script/UI/Debug/WeaponTest/, 커밋 전 신규)
무기 교체/강화/더미 배치/DPS 측정용 테스트 씬 도구. 전부 Game.Runtime.

- `WeaponTestController.cs` : MonoBehaviour. 직렬화 필드 약 25개(템플릿 버튼, 컨테이너, Dummy 프리팹, 레이아웃 등).
  public: CurrentWeapon, CurrentWeaponIndex, CurrentLayoutIndex, Meters(IReadOnlyList<WeaponTestDamageMeter>),
  SelectWeapon(int)->bool, UpgradeOnce()->bool, UpgradeMax()->int, SelectLayout(int)->bool, ResetMeters(), ResetAll().
  무기 목록은 WeaponRewardEffect 리스트에서 WeaponPrefab/UpgradeTable로 얻음. PlayerStats.AddWeapon은 제거 불가라 쓰지 않고 직접 생성/파괴.
  강화: TryApplyNextUpgrade가 table.GetStep(UpgradeLevel+1) (GetStep은 1부터)을 ApplyUpgrade. 테이블 없으면 버튼 비활성.
  버튼은 템플릿 복제로 런타임 생성(라벨=자식 TMP_Text). 무기교체/강화/초기화 시 전 더미 미터 리셋. 투사체 정리(ClearProjectiles).
  내부 클래스(Dummy 정보): root, body(Rigidbody2D), position, meter.
- `WeaponTestDamageMeter.cs` : [Serializable] WeaponTestDamageMeterSettings(font, fontSize, offsets, colors, 포맷 "HIT {0:0.#}"/"DPS {0:0.#}", minDpsWindow, refreshInterval, sortingOrder=1000);
  WeaponTestDamageMeter(MonoBehaviour): Bind(HealthSystem, settings), ResetMeter(), Target/LastDamage/TotalDamage/HitCount/Dps.
  HealthSystem의 데미지 이벤트 구독(HandleDamaged), 월드 TextMeshPro 2개를 LateUpdate에서 갱신.
- `WeaponTestDummyLayout.cs` : [Serializable] displayName, Vector2[] offsets, Count; static CreateDefaults(), Ring(center,radius,count), Cluster(center,spacing,count), Line(start,step,count).
의존: WeaponRewardEffect, WeaponUpgradeTable, Weapon, Projectile, HealthSystem, TMPro, UnityEngine.UI.
- `WeaponTestPassiveRow.cs` : [Serializable] WeaponTestPassiveRow, 패시브 한 줄 UI(라벨/스택 표시, +/- 버튼)를 담는 행 클래스.
- 패시브 스택(WeaponTestController): 직렬화 필드 passives(RewardData[], StatRewardEffect만 유효), maxPassiveStack=99, rebuildWeaponOnPassiveChange=true(변경 시 무기 재생성),
  passiveRowTemplate/passiveRowContainer/passiveResetButton, 포맷 passiveLabelFormat/passiveStackFormat/invalidPassiveFormat.
  내부: passiveStacks/passiveValid/passiveDeltaLabels[] (passives와 동일 인덱스), InitPassives, BuildPassiveRows, ApplyPassives(PlayerStats.ResetModifiers 후 스택만큼 RewardData.Apply(RewardContext{Player,Side}) 재적용 → 무기 재생성), OnPassivesChanged.
  public API: PassiveCount, GetPassiveStack(i), SetPassiveStack(i,stack)->bool (0~max clamp), AddPassiveStack(i,delta)->bool, ResetPassives(); ResetAll()도 패시브 스택 초기화+행 갱신.
- 빌더(2026-11-06): WeaponTestSceneBuilder 가 오른쪽 위 PassivePanel(ScrollRect, PassiveRow 템플릿 비활성, PassiveResetButton, StatSummaryText) 생성, passives 15개(PassiveBuff 먼저 이름순, MonsterDecrease/MonsterIncreaseNuff 제외) 연결. 빌더 재실행 후 WeaponTestUiVisual 도 재실행. passiveLabelFormat 기본 "{1}"(변화량만, 예 "데미지 +5%").
- 테스트: PlayMode WeaponTestPassivePlayTests(8), EditMode PlayerStatsResetModifiersTests(2).
