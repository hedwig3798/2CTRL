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
