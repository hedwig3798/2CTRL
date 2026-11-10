# SoundSystem (Assets/Resources/Script/SoundSystem/, Game.Runtime) - 2026-11-10
설계 근거: docs/sound-design.md

## 구성
- SoundId.cs: enum 사운드 ID (UI_CLICK, LVL_SELECT, LVL_UP, PLR_HIT, PLR_DEATH, EXP_PICKUP, MON_DIE_S 등 약 80개)
- SoundGroup.cs: enum Alert/Reward/UI/Weapon/Hit/Monster/BGM + 확장 ToChannel() -> AudioChannel
- SoundEvent.cs: 직렬화 클래스 (id, clips[], group, volume, pitchMin/Max, minInterval, maxVoices, ignorePause, priority, loop; HasClip, PickClipIndex)
- SoundLibrary.cs: ScriptableObject. RESOURCE_PATH="Data/Sound/SoundLibrary". events, weapons(WeaponSoundEntry: weaponKey/fire/hit), 그룹 dB(SoundGroupGain), oneShotVoices/loopVoices, ExpCombo(Window/StepSemitones/MaxSemitones). TryGetEvent/TryGetWeapon/GetGroupGainDb/Linear/DbToLinear/InvalidateCache
- SoundThrottle.cs: 키(MakeKey(id, side)) 별 minInterval/maxVoices 제한. TryAcquire/Release/GetVoices/Clear
- SoundStats.cs: struct played/throttled/noVoice/missing
- SoundManager.cs: MonoBehaviour 싱글턴. [RuntimeInitializeOnLoadMethod BeforeSceneLoad] 자동 생성 + DontDestroyOnLoad, AudioListener 보유, Library 를 Resources.Load.
  - static API: Play(id, side?, pitchScale?), StartLoop(id, side?)->handle, StopLoop(handle), StopAllLoops, GetStats/GetAllStats/ResetStats/BuildStatsReport, GetPan(side) (LEFT -0.45/RIGHT 0.45/CENTER 0). Instance/HasInstance/Library/ActiveOneShotCount/ActiveLoopCount
  - 음량: source.volume = GameSettings.GetChannelVolume(채널) x event.volume x 그룹 dB 선형. Master 는 AudioListener.volume 이 곱하므로 GetEffectiveVolume 사용 금지(이중 곱).
  - 일시정지: GamePause.OnPauseChanged -> AudioListener.pause. ignorePause 이벤트는 ignoreListenerPause.
  - 씬 로드 때 UIButtonSound 를 버튼/SelectPanel 에 자동 부착. SubsystemRegistration 에서 static 초기화.
- CombatSoundBinder.cs: SoundManager 가 같은 오브젝트에 부착. EventManager 이벤트 구독(sceneLoaded 마다 재구독, 없으면 대기): OnPlayerDamaged->PLR_HIT, OnPlayerDeath->PLR_DEATH, OnLevelUp->LVL_UP, OnExpGained->EXP_PICKUP(콤보 반음), OnEnemyDeath->MON_DIE_S(몬스터 레이어로 진영), OnWeaponDamage->무기 hit, Weapon.Fired->무기 fire. static GetWeaponKey(instanceName).
- UIButtonSound.cs: 버튼 클릭 시 SoundId(기본 UI_CLICK) 재생. SelectPanel.OnClicked 우선, 없으면 Button.onClick. SetSoundId.

## 연동 변경
- Weapon.cs: `public static event Action<Weapon> Fired` 추가. 발행 여부는 호출부가 FireOne(_index,_count,_notifyFired)로 결정: Fire() 즉시 루프·UpdateWave 는 첫 발만, FireRoutine(fireDelay 분할)·UpdateCapped 는 발마다. Persistent 보충/FireSub 은 미발행. SubsystemRegistration 에서 Fired=null 초기화(ResetStatics).
- Settings/AudioChannel.cs: 주석만 변경 (Effect/BGM 은 GetChannelVolume 사용).

- 리뷰 반영(2026-11-10): Single 씬 로드 시 루프 전부 + ignorePause=false 원샷 정지. Load 직후 library.InvalidateCache(). loop/원샷 이벤트를 반대 API에 넘기면 이벤트별 경고 1회 후 거부(missing 통계). SoundThrottle.Cancel(key): 보이스 확보 실패 시 개수·lastTime 되돌림. AddStat 은 UNITY_EDITOR||DEVELOPMENT_BUILD 에서만 기록.

## S2 변경 (2026-11-10)
- SoundLayerSide.cs (static): GetSide(layer)->PlayerSide?(L*=Left/R*=Right/그 외 null), GetMonsterLayer(side). LMonster/RMonster 상수. 32칸 표 캐시.
- FlowSoundBinder.cs (SoundManager 와 같은 오브젝트): Spawner.OnWaveChanged -> 웨이브음(0번 무음, RushWaveIndex=WAVE_RUSH, 마지막=WAVE_FINAL, 그 외 WAVE_START; 진영 중복 방지 lastWaveIndex). DimensionSelectController 이벤트 -> DIM_CURSOR/DIM_CANCEL/DIM_READY/UI_DENIED, 확정 시 SoundLibrary.TryGetDimension(에셋명).confirm. static GetWaveSound(spawner,index). sceneLoaded 재바인딩.
- RewardSound.cs (static): PlayConfirm(RewardData, side). WeaponReward->LVL_WEAPON, WeaponUpgrade->LVL_UPGRADE(별 단계면 LVL_STAR), StatReward->LVL_BUFF(고른 쪽)+LVL_DEBUFF(반대쪽), 그 외 LVL_UPGRADE. LevelUpUI 가 호출.
- SoundLibrary: WeaponSoundEntry 에 loop 추가. 신규 struct MonsterSoundEntry(monsterKey,die)/EffectSoundEntry(effectKey,sound)/DimensionSoundEntry(dimensionKey,confirm); Monsters/Effects/Dimensions, TryGetMonster/TryGetEffect/TryGetDimension, 기본 몬스터 사망음, RushWaveIndex(기본 10), LoopVoices, CullViewportMargin.
- SoundManager: Play(id, side, Vector3 worldPos) 오버로드 - 진영 카메라 뷰포트+여백 밖이면 미재생(stat culled; side null/카메라 없으면 컬링 안 함). SoundStats.culled 추가.
- CombatSoundBinder 재작성(581줄): 기존 구독 + MonsterBrain.AnyActionTelegraph/AnyActionStarted/AnyRevived(몬스터 행동음), PooledEffect.Spawned(연출음), Weapon.Enabled/Disabled + ActiveProjectileCount 로 무기 루프(UpdateWeaponLoops), 저체력 루프(UpdateLowHp), 몬스터 사망음(GetMonsterDieId).
- UIButtonSound: static Assign(Button, SoundId) 추가.

## 게임 코드 훅 (static 이벤트는 SubsystemRegistration 에서 null 초기화)
- Weapon: static Enabled/Disabled(Action<Weapon>), int ActiveProjectileCount (풀 CountActive, FireSub 제외).
- PooledEffect: static Spawned(Action<PooledEffect, Vector3, int layer>), Spawn 시 발행.
- Spawner: Action<Spawner,int> OnWaveChanged(웨이브 시작, 0부터), int WaveCount.
- MonsterBrain: static AnyActionTelegraph/AnyActionStarted(Action<MonsterBrain,KeywordRuntime>), AnyRevived(Action<MonsterBrain>), RaiseActionStarted(rt) - MonsterStates 행동 상태 진입(OnActionStart 직후) 호출.
- DimensionSelectController: event Action<PlayerSide> OnConfirmDenied.
- [SerializeField] SoundId 필드 추가: LightningProjectile, PoisonZoneLandMovement, SpinBladeTelegraph, PauseMenu, SettingsPanel, VolumeSettingRow, TitleMenu, GameOverManager.
## S3 BGM (2026-11-10)
- BgmDirector.cs: SoundManager 가 Awake 에서 AddComponent. 자식 AudioSource+AudioLowPassFilter 8개(Single 2 / 베이스 4 / 레이어 2).
  - 씬 이름 -> BgmMode(None/Single/Combat) 매핑(SoundLibrary.TryGetBgmScene).
  - Combat: sceneLoaded 다음 Update 에서 베이스 4종 + 레이어 2개를 같은 dspTime 에 PlayScheduled(전부 동일 길이라 동기 유지).
  - Spawner.OnWaveChanged -> 다음 마디 경계에서 강도 크로스페이드(GetBgmBaseForWave).
  - GamePause 시 -6dB + 로우패스. GameOverManager.OnGameOver 로 페이드아웃. BuildDebugState() 로 상태 덤프.
- SoundLibrary 추가: enum BgmMode, BgmSceneEntry, dimensions[].layer(차원별 레이어 BGM), bgm* 설정값, TryGetBgmScene, GetBgmBaseForWave.
- 음원 Assets/Audio/BGM/: 전투 7개 128BPM 32마디 동일 길이, 메뉴 2개 96BPM 16마디. 합성 스크립트 tools/audio/bgm.py.

- 웨이브 전환 무음(2026-11-10): WAVE_* 음원·이벤트 삭제, SoundDataBuilder 설정 표에서도 제거. FlowSoundBinder 경로는 유지(이벤트 없음 → missing 통계).
