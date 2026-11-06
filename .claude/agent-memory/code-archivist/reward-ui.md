# reward / stat / description / ui
## RewardSystem
- `RewardEffect`(abstract SO): `Apply(RewardContext, RewardData)`. 파생(Effects/): `StatRewardEffect`, `WeaponRewardEffect`, `SpawnerRewardEffect`. 확장 = 새 SO 파생.
- `RewardData`(SO), `RewardContext`, `RewardPoolType` {Weapon, WeaponUpgrade, PassiveBuff, PassiveNerf}.
- `RewardManager`(싱글턴): ApplyReward(data, PlayerSide), 풀 관리(Add/Remove/SetWeight/Clear/Count), Get/TryGet/GetAny/TryGetAny, GetRandomUnique(count, list). 풀은 `RandomPool<RewardData>`(Utils).
## StatSystem
- `PlayerStats`(MonoBehaviour): Find(PlayerSide), Get/GetBase/GetAdd/GetMultiply(ValueType), AddModifier(type, StatModifierMode, v), AddWeapon/HasWeapon, event OnStatChanged. `PlayerSide`(+Extensions), `StatModifierMode`.
## DescriptionSystem
- `ValueType` {Damage,HP,MoveSpeed,AttackSpeed,Range}(+Extensions 한글명; 값 추가 시 이름도 추가), `ObjectDescription`(SO, StatEntry), `DescriptionHolder`.
## UI (UI/)
- `LevelUpUI`, `SelectPanelGroup`, `SelectPanel`: EventManager.OnLevelUp 구독(-= 후 +=). RewardManager.GetRandomUnique로 선택지.
- `SliderUI`(FILL_DIRECTION), Binder/: DamageTextBinder, ExpSliderBinder, HPSliderBinder. `FloatingTextUI`, Debug/`PlayerStatDebugUI`, 루트 `FollowUI.cs`.

## 차원 연동 변경 (미커밋)
- RewardManager: `SetPoolFor(side,type,items,weight)`(그 플레이어 type 풀을 전용 항목으로 교체, 공유 풀 미사용), `IsPoolOverriddenFor(side,type)`. SideState.overridden 추가. GetRandomUnique(side): overridden type 은 공유 풀 제외, `ExcludeFor` 는 전용 풀(side pool)에도 적용.
- LevelUpUI 열기/닫기: Awake 시점 직계 자식을 contentObjects 에 저장해 그것만 On/Off. LetterboxCanvas 가 런타임에 자식을 LetterboxRoot 아래로 옮겨도 MainPanel 을 직접 켠다 (전에는 직계 자식=LetterboxRoot 만 켜져 패널이 안 보였음).
- LevelUpUI/SelectPanelGroup: 게임 시작 무기 보상(grantStartReward, OpenStartReward, RefreshStartRewards 등) 제거. 시작 무기는 DimensionApplier 가 지급. 상세: dimension.md

## 밸런스 변경 (2026-11-02, 상세 balance.md)
- RewardManager: SideState.blocked, SetPoolBlockedFor/IsPoolBlockedFor/IsInPoolFor(side,type,item). 막힌 풀은 공유/전용 모두 제외.
- WeaponRewardEffect: 보유 상한(PlayerStats.IsWeaponFull) 도달 시 Weapon 풀 차단, 미보유일 때는 ExcludeFor 안 함.
- SelectPanelGroup: 디버프(Data2)는 PassiveBuff 패널에만(DebuffPairedPoolTypes, IsDebuffPaired). SelectPanel.debuffArea는 Data2 없으면 숨김. LevelUpUI는 Data2 null이면 상대 디버프 적용 생략.
- PlayerStats: minMultiplier 0.5, GetMultiplier, maxWeaponCount 4, WeaponCount/IsWeaponFull. PlayerStatDebugUI는 GetMultiplier 표기.

- PlayerStats.OnWeaponAdded/AcquiredWeapons 추가, 무기 HUD: [hud.md](hud.md)

## 추가 패시브 (2026-11-06, 미커밋)
- SpawnerRewardEffect.extraTypes: type 과 같은 value 로 함께 변경. 에셋 Upgrade/Effects/MonsterCountHPEffect.asset (Count+HP, Add).
- 보상 5개: PassiveBuff/MonsterDecrease(-5% 스폰율·체력, 내 쪽), DurationUpgrade(+10%, StatPercentEffect), ProjectileUpgrade(+1, StatAddEffect, 풀 가중치 0.3) / PassiveNuff/MonsterIncreaseNuff(+3%, 상대 쪽), DurationNuff(-6%). 투사체 수 디버프 없음. 아이콘 없음(image 비움).
- 빌더 Editor/Builders/PassiveRewardBuilder.cs (Tools/Builders/PassiveReward, 멱등): 에셋 생성 + RewardManager 프리팹·GameScene 풀 등록, TryGetWeight 제공.
- 테스트 Tests/EditMode/PassiveRewardTests.cs.
- 체력 재생 (2026-11-06): ValueType.HPRegen(끝에 추가, base 0, Get 이 0 미만을 0 으로). HealthSystem.ApplyRegen(rate, dt)(죽음/다운/최대치면 무시), Update 에서 playerStats 있을 때만 Get(HPRegen) 적용. 보상 PassiveBuff/HPRegenUpgrade(+0.5/s, StatAddEffect), PassiveNuff/HPRegenNuff(-0.3/s). StringTable stat.hp_regen, reward.passive.hp_regen.name/regen_up/regen_down. PassiveRewardBuilder 는 GameScene 열기 전 SaveCurrentModifiedScenesIfUserWantsTo.
- PlayerStats.ResetModifiers(): ΣAdd/ΣMultiply 배열 0으로 초기화 후 OnStatChanged 발생 (무기 테스트 패시브 스택 재적용용). 참고 [weapon-test.md](weapon-test.md)
