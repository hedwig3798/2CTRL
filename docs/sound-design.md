# 사운드 기획서 (초안 2026-11-09, 승인 대기)

## 1. 한 줄 요약

**"왼쪽 일은 왼쪽 귀에서, 오른쪽 일은 오른쪽 귀에서."**
한 사람이 두 캐릭터를 조종하는 게임이므로, 눈이 한쪽에 가 있을 때 다른 쪽 상황을 귀로 알려 주는 것을 사운드의 첫 번째 역할로 삼는다.

규모: 효과음 70종, BGM 9개 파일. 음색 방향은 픽셀 아트에 맞춘 16비트풍 레트로 합성음을 제안한다 (13장 D1).

## 2. 현재 상태 조사

| 항목 | 상태 | 근거 |
|---|---|---|
| 음원 | 없음 (wav/ogg/mp3/mixer 0개) | `Assets/` 검색, README 5장 |
| 재생 코드 | 없음. AudioSource·AudioClip·PlayOneShot 사용처 0 | 코드 검색 |
| 볼륨 설정 | 완성. Master / Effect / BGM + 음소거, 설정 UI까지 있음 | `GameSettings`, `VolumeSettingRow` |
| AudioListener | **GameScene에 없음.** Title·DimensionSelect·WeaponTest 카메라에만 있음 | 씬 파일, `FollowCamera.prefab` |
| 프로젝트 오디오 설정 | Real Voices 32, Virtual 512, DSP 버퍼 1024 (기본값) | `ProjectSettings/AudioManager.asset` |
| 합성 도구 | Python 3.11 + numpy 1.26 + scipy 1.16 있음, ffmpeg·sox 없음 | 로컬 확인 |

### 구현 전에 반드시 처리할 문제

1. **GameScene에 리스너가 없어 그대로면 소리가 나지 않는다.** 리스너를 씬 전환 후에도 유지되는 `SoundManager`로 옮긴다. 이때 `TitleSceneBuilder.cs:58`, `DimensionSelectSceneBuilder.cs:88`, `WeaponTestSceneBuilder.cs:192`의 `typeof(AudioListener)`도 빼야 빌더를 다시 돌려도 중복되지 않는다.
2. **`Time.timeScale = 0`은 AudioSource를 멈추지 않는다.** 화염 방사 같은 루프음이 레벨업 화면(게임당 약 50회) 동안 계속 나게 된다. 7.3절로 해결한다.
3. **발사체에 AudioSource를 붙이면 소리가 잘린다.** 풀로 돌아갈 때 `SetActive(false)`가 되면서 재생이 끊긴다. 재생은 `SoundManager`의 자체 풀에서 한다.
4. **믹서 스냅샷이 일시정지 중 멈춘다.** `AudioMixer.updateMode`를 `UnscaledTime`으로 설정해야 하고, BGM 페이드 코루틴도 unscaled 시간을 써야 한다.
5. **주석이 서로 어긋난다.** `AudioChannel.cs:4`는 `GetEffectiveVolume`을 쓰라고 하지만, `GameSettings.cs:18-19`는 그러면 Master가 두 번 곱해진다고 경고한다. `GetChannelVolume`이 맞으므로 `AudioChannel.cs` 주석을 고친다.
6. **무기 수가 문서와 다르다.** README는 17종이지만 `DimensionDataBuilder`와 보상 데이터에는 SpinBlade, StormOrb가 더 있다. 이 기획서는 19종 기준이며, 두 무기가 빠질 예정이면 해당 행을 뺀다.

## 3. 사운드 원칙

| # | 원칙 | 구체적으로 |
|---|---|---|
| 1 | 좌우가 곧 정보 | 전투음은 진영에 따라 좌/우로 고정 패닝한다. 위험음일수록 어느 쪽인지 분명하게 들려야 한다 |
| 2 | 차원은 음색으로 구분 | 소리만 듣고도 어느 차원의 무기인지 알 수 있게 재료를 나눈다 (아래 표) |
| 3 | 위험 > 보상 > 내 공격 > 적 | 화면에 500마리가 있어도 내 피격음과 자폭 예고음은 묻히지 않는다 |
| 4 | 30분을 견디는 소리 | 자주 나는 소리일수록 짧고, 작고, 고음을 줄인다. 강한 소리는 드물게만 쓴다 |

### 차원별 음색

| 차원 | 재료 | 파형·처리 | 감정 |
|---|---|---|---|
| 숲 | 나무, 풀, 물기, 현을 튕기는 소리 | 삼각파, 짧은 감쇠의 FM(마림바), 필터 노이즈 | 부드럽고 유기적 |
| 공장 | 금속, 모터, 증기, 전자음 | 사각파, 비트 크러시, 링 모듈레이션 | 단단하고 기계적 |
| 폭풍 | 전기, 바람, 천둥 | 톱니파, 고속 랜덤 피치, 대역 노이즈 스윕 | 날카롭고 불안정 |

## 4. 믹스 구조

### 4.1 믹서 그룹과 목표 음량

피크 기준의 초기값이며 S4 믹스 단계에서 실제로 들어 보고 조정한다.

| 그룹 | 채널 | 내용 | 목표 | 우선순위 |
|---|---|---|---|---|
| Alert | Effect | 내 피격, 저체력, 사망, 자폭 예고, 웨이브 알림 | −6 dB | 최상 (AudioSource.priority 10) |
| Reward | Effect | 레벨업, 경험치 | −10 dB | 높음 (40) |
| UI | Effect | 버튼, 커서, 패널 | −12 dB | 높음 (40) |
| Weapon | Effect | 무기 발사·폭발·루프 | −14 dB | 중간 (128) |
| Hit | Effect | 적 타격 | −22 dB | 낮음 (200) |
| Monster | Effect | 적 사망, 행동 | −20 dB | 낮음 (180) |
| BGM | BGM | 음악 | −16 dB | 고정 (0) |

- **구현 메모 (S0, 2026-11-10):** 에디터 스크립트로 AudioMixer와 그룹을 만드는 공개 API가 없어, 그룹 음량은 `SoundLibrary.groupGains`(dB)로 코드에서 곱한다. 리미터·덕킹·일시정지 스냅샷은 아직 없으며, S4에서 믹서를 수동으로 만들지 코드로 처리할지 정한다.
- 사용자 볼륨은 기존 규약대로 `AudioSource.volume = GetChannelVolume(채널) × 이벤트 음량`으로 적용한다. 믹서는 그룹 밸런스, 덕킹, 리미터만 맡는다.
- Effect 합산 뒤에 리미터를 둔다. 후반 물량에서 생기는 클리핑을 막는다.
- Alert가 재생되면 Hit·Monster를 200 ms 동안 −6 dB 낮춘다 (사이드체인 덕킹).
- 레벨업·일시정지 스냅샷: BGM −6 dB + 저역 통과 필터, 게임 복귀 시 0.2초에 걸쳐 원복.

### 4.2 스테레오

| 소리 | 패닝 |
|---|---|
| 왼쪽 진영의 전투음 | 기본 −0.45 + 화면 내 x 위치 보정 ±0.15 |
| 오른쪽 진영의 전투음 | 기본 +0.45 + 화면 내 x 위치 보정 ±0.15 |
| 양쪽 공유 이벤트 (레벨업, 웨이브, UI) | 0 |
| 차원 선택 커서·확정 | 입력한 진영 쪽으로 ±0.6 |

- 탑다운 2D이므로 3D 거리 감쇠는 쓰지 않는다 (`spatialBlend = 0`).
- 몬스터 스폰 거리(11~13)는 화면 밖이므로, 화면 밖 사건의 소리는 재생하지 않는다.
- 끝까지 왼쪽(−1)으로 보내지 않는 이유: 스피커로 들을 때 한쪽만 울리면 어색하고, 헤드폰에서는 귀가 쉽게 피로해진다. −0.45에서 구분이 부족하면 S4에서 넓힌다.

### 4.3 보이스 예산 (Real Voices 32)

| 용도 | 수 |
|---|---|
| BGM (베이스 + 좌 차원 + 우 차원 + 교체용) | 4 |
| 루프 효과음 (진영당 3) | 6 |
| UI / Alert | 4 |
| 일반 원샷 | 16 |
| 예비 | 2 |

### 4.4 호출 제한

추정 부하는 최종 웨이브에서 진영당 약 14마리/초 처치, 양쪽 합 약 28회/초이며(`balance-design.md` 7장 기준), 타격은 그보다 훨씬 많다. 호출을 제한하지 않으면 소리가 뭉쳐 잡음이 되므로 모든 이벤트에 아래 제한을 둔다.

| 이벤트 | 최소 간격 | 동시 재생 | 넘칠 때 |
|---|---|---|---|
| 적 타격 HIT_* (진영별) | 45 ms | 3 | 버림 |
| 적 사망 MON_DIE_S (진영별) | 60 ms | 3 | 버림 |
| 무기 발사 (무기별, 진영별) | 60 ms | 2 | 버림 |
| 경험치 EXP_PICKUP | 50 ms | 2 | 버림, 콤보 음높이는 유지 |
| 내 피격 PLR_HIT (진영별) | 150 ms | 1 | 버림 |
| 폭발류 | 100 ms | 3 | 가장 오래된 것 교체 |
| 몬스터 행동 (종류별) | 120 ms | 2 | 버림 |

- 잦은 소리는 변형 2~3개 + 피치 ±5~8% 랜덤으로 반복감을 줄인다.
- 다발 발사(`fireCount` 여러 개)는 한 번의 공격에 소리를 1번만 내고, `fireDelay`로 나뉜 무기만 발마다 낸다.

## 5. BGM

### 5.1 구조: 공통 리듬 + 진영별 차원 레이어

좌우가 서로 다른 차원을 고른다는 점을 음악에도 반영한다. 같은 템포·조로 만든 레이어를 동시에 재생하고, **왼쪽 진영의 차원 레이어는 왼쪽, 오른쪽 진영의 차원 레이어는 오른쪽으로** 보낸다. 3개 차원에서 서로 다른 2개를 고르는 조합이 전부 같은 파일로 해결된다.

- 128 BPM, A단조 계열, 32마디 루프 (60초)
- 레이어는 `AudioSource.PlayScheduled`로 같은 DSP 시각에 시작한다
- 강도 변경은 마디 경계에서 2마디에 걸쳐 크로스페이드한다

| 파일 | 내용 | 쓰는 곳 |
|---|---|---|
| BGM_TITLE | 세 차원의 음색이 섞인 잔잔한 테마 | 타이틀 |
| BGM_SELECT | 타이틀 테마의 리듬 변주 | 차원 선택 |
| BGM_BASE_LOW / MID / HIGH | 드럼 + 베이스 3단계 | 전투 |
| BGM_LAYER_FOREST / FACTORY / STORM | 차원별 멜로디·아르페지오 | 전투, 진영별 패닝 |
| BGM_RUSH | 20:00 러시 전용 변주 | 웨이브 11 |

승리·패배는 BGM을 멈추고 스팅어(RESULT_*)로 처리한다.

**구현 메모 (S3, 2026-11-10):** `BgmDirector`가 전투 베이스 4종(LOW/MID/HIGH/RUSH)과 차원 레이어 2개를 같은 DSP 시각에 동시 재생하고, 강도는 볼륨만 등전력 크로스페이드로 바꾼다(다음 마디 경계, 2마디). 레이어 패닝 ±0.6. BGM은 자체 소스 8개를 쓰므로 효과음 루프 풀을 6으로 줄였다(원샷 16 + 루프 6 + BGM 8 = 30 ≤ 32). 아래 표의 "절정 구간 필터 개방"은 아직 구현하지 않았다. 음원은 `tools/audio/bgm.py`로 합성한다.

### 5.2 30분 구성

| 구간 | 웨이브 | 시간 | 구성 | 의도 |
|---|---|---|---|---|
| 도입 | 1~3 | 00:00~02:30 | LOW + 차원 레이어 | 무기 소리를 듣는 시간 |
| 전개 | 4~6 | 02:30~10:00 | MID + 차원 레이어 | 리듬이 살아남 |
| 압박 | 7~10 | 10:00~20:00 | HIGH + 차원 레이어 | 밀도 상승 |
| 러시 | 11 | 20:00~22:30 | RUSH + 차원 레이어 | 분위기 전환, 몰아치는 쾌감 |
| 절정 | 12~14 | 22:30~30:00 | HIGH + 차원 레이어 + 필터 개방 | 마지막 버티기 |

## 6. 효과음 목록

P0 = S1 슬라이스, P1 = 정식 포함, P2 = 여유가 있으면. 진입점의 ★는 새 코드가 필요하다는 뜻이다.

### 6.1 UI·메뉴 (12종)

| ID | 시점 | 진입점 | 톤 | 길이 | 우선 |
|---|---|---|---|---|---|
| UI_HOVER | 버튼 올리기 | ★ `UIButtonSound` | 아주 작은 틱 | 30 ms | P2 |
| UI_CLICK | 버튼 클릭 | ★ `UIButtonSound` | 부드러운 탁 | 60 ms | P0 |
| UI_BACK | 창 닫기, ESC | 설정·일시정지 창 | 낮게 내려가는 탁 | 80 ms | P1 |
| UI_DENIED | 잠긴 차원 확정 시도 | ★ `ToggleConfirm` 실패 분기 | 둔탁한 2연음 | 120 ms | P1 |
| UI_SLIDER | 효과음 슬라이더 조작 | `VolumeSettingRow` | 현재 음량 미리듣기 | 60 ms | P1 |
| UI_START | 게임 시작 | `TitleMenu` | 상승 스윕 | 500 ms | P1 |
| DIM_CURSOR | 커서 이동 | `OnCursorChanged` | 진영별 패닝 틱 | 50 ms | P1 |
| DIM_CONFIRM_FOREST / FACTORY / STORM | 차원 확정 | `OnConfirmChanged(true)` | 차원 음색의 짧은 화음 | 400 ms | P1 |
| DIM_CANCEL | 확정 취소 | `OnConfirmChanged(false)` | 하강 틱 | 120 ms | P1 |
| DIM_READY | 양쪽 확정 | `OnBothConfirmed` | 라이저 + 임팩트 | `loadDelay`에 맞춤 | P1 |

### 6.2 레벨업·진행 (11종)

레벨업은 약 36초마다 한 번꼴이므로 스팅어를 1초 이내로 짧게 유지한다.

| ID | 시점 | 진입점 | 톤 | 길이 | 우선 |
|---|---|---|---|---|---|
| LVL_UP | 레벨업 | `LevelUpUI.HandleLevelUp` | 밝은 3음 상승 | 700 ms | P0 |
| LVL_SELECT | 카드 선택 | `SelectPanelGroup.OnSelectionChanged` | 카드 넘기는 탁 | 80 ms | P0 |
| LVL_WEAPON | 무기 획득 확정 | ★ `LevelUpUI` 확정 처리 | 금속 울림 + 화음 | 400 ms | P1 |
| LVL_UPGRADE | 업그레이드 확정 | ★ 동일 | 상승 2음 | 250 ms | P1 |
| LVL_STAR | ★(5·10단계) 확정 | ★ 동일 | LVL_UPGRADE + 반짝임 | 500 ms | P1 |
| LVL_BUFF | 패시브 확정 (고른 쪽) | ★ 동일, 고른 쪽 패닝 | 밝은 상승 | 250 ms | P1 |
| LVL_DEBUFF | 패시브 확정 (상대 쪽) | ★ 동일, 반대쪽 패닝 | 어두운 하강 | 250 ms | P1 |
| PAUSE_OPEN / PAUSE_CLOSE (2종) | 일시정지 | `PauseMenu` | 필터가 닫히고 열리는 소리 | 150 ms | P1 |

LVL_BUFF와 LVL_DEBUFF를 동시에 양쪽으로 내면 "나는 강해지고 상대는 약해진다"는 규칙이 소리로 전달된다.

| ID | 시점 | 진입점 | 톤 | 길이 | 우선 |
|---|---|---|---|---|---|
| RESULT_WIN | 승리 | `GameOverManager.EndGame(true)` | 장조 팡파르 | 3 s | P1 |
| RESULT_LOSE | 패배 | `GameOverManager.EndGame(false)` | 단조 하강 | 2.5 s | P1 |

### 6.3 플레이어·웨이브 (8종)

| ID | 시점 | 진입점 | 톤 | 길이 | 우선 |
|---|---|---|---|---|---|
| PLR_HIT | 피격 | `OnPlayerDamaged` | 저음 쿵 + 짧은 노이즈, 진영 패닝 | 150 ms | P0 |
| PLR_LOW_HP | HP 25% 이하 | `HealthSystem.OnHPChanged` | 느린 심장 박동 루프, 진영 패닝 | 루프 | P1 |
| PLR_DEATH | 사망 | `OnPlayerDeath` | 큰 임팩트 + 꼬리 | 1.2 s | P0 |
| EXP_PICKUP | 경험치 획득 | `OnExpGained` | 맑은 틱, 연속 획득 시 음높이 상승 | 60 ms | P0 |
| WAVE_START | 웨이브 전환 | ★ `Spawner.OnWaveChanged` | 낮은 호른풍 | 800 ms | P1 |
| WAVE_RUSH | 20:00 | ★ 동일 | 경보 + 드럼 필 | 1.5 s | P1 |
| WAVE_FINAL | 27:30 | ★ 동일 | WAVE_RUSH의 강한 판 | 2 s | P1 |
| WAVE_TICK | 마지막 10초 | `WaveTimerUI` | 시계 틱 | 60 ms | P2 |

**웨이브 전환은 무음으로 한다 (2026-11-10 결정).** WAVE_START / WAVE_RUSH / WAVE_FINAL 은 음원·이벤트를 만들지 않는다. 연결 코드(`FlowSoundBinder`)는 남아 있어 이벤트만 다시 만들면 되살릴 수 있다. 웨이브 흐름은 BGM 강도 변화(5.2)로만 전달한다.

발소리는 넣지 않는다. 쉬지 않고 나는 소리인데 전달하는 정보가 없고, 조작감은 다른 소리로 충분히 낼 수 있다.

### 6.4 무기 (19무기, 26종)

| ID | 무기 (방식, 기본 공속) | 시점 | 톤 | 길이 | 우선 |
|---|---|---|---|---|---|
| WPN_BOOMERANG | 부메랑 (Repeat 0.7) | 발사 | 나무가 공기를 가르는 휙 | 250 ms | P0 |
| WPN_POISON_THROW | 독장판 (Repeat 0.5) | 발사 | 병 던지는 휙 | 150 ms | P1 |
| WPN_POISON_LAND | | 착지 | 물 튀김 + 지글거림 | 350 ms | P1 |
| WPN_SWEEP | 휩쓸기 (Repeat 1.2) | 발사 | 풀을 베는 소리 | 180 ms | P1 |
| WPN_SPLIT_FIRE | 분열탄 (Repeat 0.9) | 발사 | 씨앗 퐁 | 100 ms | P1 |
| WPN_SPLIT_BURST | | 터짐 | 꼬투리가 터지는 팍 | 180 ms | P1 |
| WPN_VINE | 덩굴 (Repeat 4) | 실제로 조각을 깔 때 | 아주 작은 잎 소리 | 80 ms | P2 |
| WPN_SPIRIT_SPAWN | 정령 (Capped 0.5) | 생성 | 맑은 벨 | 300 ms | P1 |
| WPN_SPIRIT_HIT | | 타격 | 작은 벨 | 80 ms | P2 |
| WPN_ROOT | 뿌리 속박 (Repeat 0.4) | 생성 | 나무가 삐걱이며 솟음 | 400 ms | P1 |
| WPN_LASER | 레이저 (Repeat 0.8) | 발사 | 하강하는 사각파 피융 | 180 ms | P0 |
| WPN_DRONE | 드론 (Persistent 2대, 대당 1.5) | 드론 발사 | 작은 전자음 | 60 ms | P1 |
| WPN_MINE_SET | 지뢰 (Repeat 0.5) | 설치 | 금속 철컥 | 120 ms | P1 |
| WPN_MINE_BOOM | | 폭발 | 금속성 폭발 | 500 ms | P1 |
| WPN_SAW_LOOP | 톱날 (Wave 1.0, 2개) | 이동 중 루프 | 회전 금속음 | 루프 | P1 |
| WPN_FLAME_LOOP | 화염 방사 (Capped 2.22) | 분사 중 루프 | 저역 노이즈 불꽃 | 루프 | P1 |
| WPN_SPINBLADE_TELL | SpinBlade (Repeat, 60초 주기) | 예고 | 모터 가속 | 예고 시간에 맞춤 | P1 |
| WPN_SPINBLADE_LOOP | | 회전 중 루프 | 고속 회전음 | 루프 | P1 |
| WPN_CHAIN | 체인 번개 (Repeat 0.8) | 발사 | 전기 튀김 | 200 ms | P0 |
| WPN_SHOCKWAVE | 충격파 (Repeat 0.35) | 발사 | 저역 퍼지는 웅 | 500 ms | P1 |
| WPN_EXPLOSIVE_FIRE | 폭발 (Repeat 0.6) | 발사 | 뭉툭한 퐁 | 120 ms | P1 |
| WPN_EXPLOSIVE_BOOM | | 폭발 | 천둥 섞인 폭발 | 500 ms | P1 |
| WPN_TORNADO_LOOP | 회오리 (Capped 0.33) | 존재 중 루프 | 대역 노이즈 스윕 | 루프 | P1 |
| WPN_LIGHTNING_MARK | 낙뢰 (Repeat 0.7) | 표식 (낙뢰 0.5초 전) | 전기가 모이는 소리 | 400 ms | P1 |
| WPN_LIGHTNING_STRIKE | | 낙뢰 | 짧은 천둥 | 300 ms | P1 |
| WPN_STORMORB_LOOP | StormOrb (Persistent 2개) | 존재 중 작은 루프 | 잔잔한 전기 험 | 루프 | P2 |

- 루프 무기는 무기(진영)당 소스 1개만 쓰고, 발사체 수가 늘면 음량을 조금만 올린다.
- 장판형 무기의 틱 타격음은 HIT_*의 제한을 그대로 받는다.
- 업그레이드로 공속이 올라도 제한(4.4) 덕분에 소리가 뭉치지 않는다.

### 6.5 타격 (3종)

`OnWeaponDamage(진영, 무기, 양)` 이벤트 하나로 모든 무기를 처리하며, 무기가 속한 차원의 소리를 쓴다.

| ID | 톤 | 길이 | 변형 | 우선 |
|---|---|---|---|---|
| HIT_ORGANIC | 퍽 | 50 ms | 3 | P0 |
| HIT_METAL | 챙 | 50 ms | 3 | P0 |
| HIT_SHOCK | 칙 | 50 ms | 3 | P0 |

### 6.6 몬스터 (10종)

| ID | 대상 | 시점 | 진입점 | 톤 | 우선 |
|---|---|---|---|---|---|
| MON_DIE_S | 박쥐, 미니슬라임, 슬라임 등 | 사망 | `OnEnemyDeath` | 짧은 퍽 (변형 3) | P0 |
| MON_DIE_M | 봄버, 레이스, 샤먼 | 사망 | 동일 | 조금 더 무거운 퍽 | P1 |
| MON_DIE_L | 골렘 | 사망 | 동일 | 돌이 무너지는 소리 | P1 |
| MON_BOMBER_TELL | 봄버 (돌진·자폭) | 예고 | `MonsterBrain.OnActionTelegraph` | 빨라지는 삐 소리 (**Alert**) | P1 |
| MON_BOMBER_BOOM | 봄버 | 자폭 | `SelfDestructKeyword` | 폭발 (**Alert**) | P1 |
| MON_TELEPORT | 레이스 | 순간이동 | 키워드 런타임 | 슉 | P1 |
| MON_SUMMON | 샤먼 | 소환 | 키워드 런타임 | 낮은 주문 | P1 |
| MON_SPLIT | 슬라임 | 분열 | 키워드 런타임 | 물컹 | P2 |
| MON_REVIVE | 봄버, 골렘 | 부활 | `MonsterBrain` 부활 처리 | 역재생 스윕 | P1 |
| MON_GOLEM_ENTER | 골렘 | 화면 진입 | ★ 판정 추가 | 묵직한 발소리 1회 | P2 |

- 슬라임류와 박쥐의 이동음은 넣지 않는다.
- `OnEnemyDeath`는 진영 정보가 없으므로 몬스터의 레이어(`LMonster`/`RMonster`)로 패닝을 정한다.

## 7. 구현 설계

### 7.1 구성

| 요소 | 역할 |
|---|---|
| `SoundEvent` (SO) | 클립 배열, 음량, 피치 범위, 그룹, 우선순위, 최소 간격, 동시 재생 수, 루프 여부, 일시정지 무시 여부 |
| `SoundManager` | `EventManager`와 같은 DontDestroyOnLoad 싱글턴. AudioListener, AudioSource 풀, 제한, 패닝, 볼륨 반영 |
| `BgmDirector` | 레이어 동기 재생, 웨이브별 강도 전환 |
| `CombatSoundBinder` | 이미 있는 전역 이벤트를 구독해 PLR_*, EXP, HIT, MON_DIE를 재생 |
| `WeaponSoundTable` (SO) | 무기 키 → 발사·타격·루프 SoundEvent |
| `UIButtonSound` | 버튼에 붙이는 클릭·호버 컴포넌트 |
| `SoundDataBuilder` | 믹서, SO, 임포트 설정 생성 (`Assets/Editor/Builders/`) |

호출 형태: `SoundManager.Play(event, side, worldPos)` / `루프 = SoundManager.StartLoop(event, side, owner)`

무기 프리팹에 직접 연결하지 않고 `WeaponSoundTable`로 분리한다. 무기 프리팹을 다시 만드는 빌더가 있어(CLAUDE.md 실수 기록 참고) 프리팹에 직접 넣으면 값이 덮어써질 수 있다.

### 7.2 코드 수정 지점

| 파일 | 변경 |
|---|---|
| `Weapon.cs` `FireOne` / `UpdateWave` | 공격당 1회 발사음 |
| 루프 무기의 발사체 (`Flame`, `Saw`, `Tornado`, `SpinBlade`, `StormOrb`) | 활성화 시 `StartLoop`, 비활성화 시 정지 |
| `LightningProjectile`, `MineProjectile`, `ExplosiveProjectile`, `SplitShotProjectile` 등 | 표식·폭발·터짐 시점 재생 |
| `Spawner.cs` | `OnWaveChanged(int)` 이벤트 추가 (진영별 Spawner가 2개이므로 한 번만 처리) |
| `MonsterBrain` / 키워드 | 예고·순간이동·소환·분열·부활 |
| `LevelUpUI.cs` | 선택 확정 시 보상 종류별 재생 |
| Title·Pause·Settings·GameOver 빌더 | 버튼에 `UIButtonSound` 부착 |
| 3개 씬 빌더 + 씬 | AudioListener 제거 |
| `AudioChannel.cs` | 주석 수정 |

### 7.3 일시정지 동작

`GamePause.OnPauseChanged`를 구독해 `AudioListener.pause`를 켜고 끈다. 멈췄던 전투음은 복귀할 때 이어서 재생된다. 일시정지 메뉴에서 타이틀로 나가는 경우를 위해 씬 로드 시에는 항상 해제한다.

| 소리 | `ignoreListenerPause` |
|---|---|
| 전투음·루프 | false (같이 멈춤) |
| UI, BGM, LVL_*, RESULT_* | true |
| **PLR_DEATH** | true (`EndGame`이 즉시 멈추므로 예외 처리 필요) |

## 8. 제작과 파일 규칙

| 항목 | 내용 |
|---|---|
| 제작 스크립트 | `tools/audio/*.py` (아트의 `tools/art/`와 같은 방식, CC0) |
| 효과음 위치 | `Assets/Audio/SFX/{UI,Player,Weapon,Hit,Monster}/` |
| BGM 위치 | `Assets/Audio/BGM/` |
| SO 위치 | `Assets/Resources/Data/Sound/` |
| 형식 | 44.1 kHz, 16-bit WAV (손실 압축은 Unity가 수행) |
| 파일 이름 | ID 그대로, 변형은 `_01` 접미사 (`HIT_METAL_01.wav`) |

| 종류 | Force Mono | Load Type | 압축 |
|---|---|---|---|
| 짧은 효과음 (< 1 s) | 켬 | Decompress On Load | ADPCM |
| 긴 효과음·스팅어 | 켬 (팡파르 제외) | Compressed In Memory | Vorbis 70 |
| 루프 효과음 | 켬 | Compressed In Memory | Vorbis 70 |
| BGM 레이어 | 끔 | Compressed In Memory | Vorbis 60 |

BGM에 Streaming 대신 Compressed In Memory를 쓰는 이유는 레이어 간 박자 동기를 안정적으로 유지하기 위해서다. 1분짜리 9개 파일이면 메모리는 대략 10 MB 수준으로 예상한다.

완성 후 README 5장("현재 게임에는 사운드 에셋이 없습니다")과 `CREDITS.md`를 갱신한다.

## 9. 작업 단계

| 단계 | 내용 | 완료 기준 |
|---|---|---|
| S0 기반 | `SoundManager`, 리스너 이전, 일시정지, 볼륨 반영, 제한 | EditMode/PlayMode 테스트 통과 |
| S1 슬라이스 | P0 효과음 13종 (시작 무기 3종, 타격 3종, 사망, 피격, 경험치, 레벨업, 카드, 클릭 등) | **사용자가 직접 듣고 음색 방향 승인** |
| S2 효과음 | P1 효과음 전체, 무기·몬스터 연결 | 전 무기 WeaponTest 씬에서 확인 |
| S3 BGM | 전투 BGM 레이어, 타이틀, 웨이브 연동 | 사용자 승인, 루프 이음새 확인 |
| S4 믹스 | 27:30 웨이브 실측 조정, P2 선별 | 11장 기준 통과 |

음색이 정해지기 전에 70종을 다 만들면 전부 다시 만들 위험이 크므로, S1에서 반드시 멈추고 들어 본다.

## 10. 위험 요소

| 위험 | 대응 |
|---|---|
| 제작자(Claude)는 소리를 들을 수 없다 | 수치 검사로 기술 오류를 잡고, 음색·밸런스는 S1·S3·S4에서 사용자가 판단 |
| 코드 합성 BGM의 음악적 완성도가 가장 불확실 | S3 초반에 30초 시안으로 판단. 부족하면 D2의 대안으로 전환 |
| 외부 음악은 차원 레이어 구조를 쓰기 어렵다 | 전투곡 1곡 + 코드로 만든 차원 환경음(진영별 패닝)으로 대체 |
| 후반 물량에서 잡음화 | 4.4 제한 + 리미터, S4 실측 |
| 빌더 재실행으로 연결 유실 | `WeaponSoundTable` 분리, UI는 빌더 안에서 부착 |

## 11. 검증 기준

**수치 검사 (스크립트로 자동화)**
- 모든 파일: 피크 ≤ −1 dBFS, 앞쪽 무음 ≤ 5 ms, 끝 처리 시 클릭 노이즈 없음
- 루프: 이음새의 샘플 값·기울기 차이가 임계값 이하
- 같은 그룹 내 음량(LUFS) 편차 ±3 LU 이내

**테스트 (`Game.Tests.*`)**
- 일시정지 시 전투 루프 정지, UI는 재생 유지
- 최소 간격·동시 재생 제한 동작
- `GetChannelVolume` 규약 준수, 볼륨 변경이 재생 중 소스에 반영
- 씬마다 AudioListener가 정확히 1개

**직접 플레이 (사람이 확인)**
- 눈을 한쪽 화면에 둔 채 다른 쪽 피격을 소리로 알아챌 수 있는가
- 27:30 웨이브에서 PLR_HIT와 MON_BOMBER_TELL이 들리는가
- 30분 플레이 후 귀가 피로하지 않은가
- 레벨업 50회를 반복해도 스팅어가 거슬리지 않는가
- 헤드폰과 스피커 양쪽에서 균형이 맞는가

## 12. 범위 밖

보이스, 3D 공간음, 몬스터 스킨별 다른 소리, 발소리, 몬스터 이동음, 음악 생성 AI 도입.

## 13. 결정이 필요한 사항

| # | 질문 | 제안 | 대안 |
|---|---|---|---|
| D1 | 효과음 음색 방향 | 16비트풍 레트로 합성음 | 묵직하고 사실적인 타격감 (합성만으로는 어려워 외부 음원 필요) |
| D2 | BGM 제작 방식 | 코드 합성으로 차원 레이어 구조 시도 | 외부 CC0 음악 사용. 프로젝트에 쓰인 Ninja Adventure 팩에 음악·효과음이 포함된 것으로 기억하지만 **미확인이며, 받을 때 내용과 라이선스를 확인해야 한다** |
| D3 | SpinBlade, StormOrb 포함 여부 | 데이터에 있으므로 포함 | 사용하지 않을 무기라면 제외 (3종 감소) |
| D4 | 좌우 패닝 강도 | ±0.45에서 시작 | S1에서 들어 보고 조정 |
