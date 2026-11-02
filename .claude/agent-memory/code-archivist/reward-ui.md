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
- LevelUpUI/SelectPanelGroup: 게임 시작 무기 보상(grantStartReward, OpenStartReward, RefreshStartRewards 등) 제거. 시작 무기는 DimensionApplier 가 지급. 상세: dimension.md
