# 설정 시스템 (2026-11-05 최종)

## 개요
PlayerPrefs 기반 게임 설정(화면 모드, 적 체력바/데미지 텍스트 표시, 채널별 볼륨/음소거) + 16:9 고정 레터박스 + 설정 창/일시정지 메뉴.

## 런타임 `Assets/Resources/Script/Settings/` (전역 네임스페이스)
- AudioChannel.cs: enum (Master 포함, Effect 등). 채널 수 = CHANNEL_COUNT.
- GameSettings.cs: static. 키 접두 "Settings.". 프로퍼티 ScreenModeIndex / ShowEnemyHPBar / ShowDamageText,
  SetScreenModeIndex(int, bool forceNotify) (forceNotify면 같은 값이어도 이벤트 발생),
  GetVolume/SetVolume/IsMuted/SetMuted, Save()(디스크 저장), Reload().
  GetChannelVolume(채널): Master 미포함, AudioSource용 (Master는 SettingsApplier가 AudioListener.volume으로 담당).
  GetEffectiveVolume: 표시·계산용(음소거면 0).
  이벤트 OnScreenModeChanged(int), OnShowEnemyHPBarChanged(bool), OnShowDamageTextChanged(bool), OnVolumeChanged(AudioChannel).
  값이 실제로 바뀔 때만 이벤트(forceNotify 예외). 구독은 OnEnable/OnDisable.
  DEFAULT_*: 체력바 true, 데미지텍스트 true, 볼륨 1, 음소거 false.
- ScreenModes.cs: static. 창 크기 목록(모니터 workArea 기준) + 마지막이 전체화면. Count, FullScreenIndex, DEFAULT_INDEX=3, Clamp/IsFullScreen/GetLabel/GetSize/Apply.
- Letterbox.cs: static. 16:9(TARGET_*). NormalizedRect/PixelRect, Map(Rect), Calculate(w,h). 프레임당 1회 계산.
- LetterboxCamera.cs: MonoBehaviour(RequireComponent Camera). 원래 rect를 baseRect로 기억 후 16:9 영역에 매핑. SetBaseRect(Rect) 사용(camera.rect 직접 변경 금지).
- SettingsApplier.cs: DontDestroyOnLoad 상주 "[SettingsApplier]", [DefaultExecutionOrder(-1000)], RuntimeInitializeOnLoadMethod(BeforeSceneLoad)로 자동 생성.
  시작 시 화면모드/Master 볼륨(AudioListener.volume) 적용, 이벤트로 즉시 반영. 검은 배경 카메라(최저 depth)로 영역 밖 clear;
  16:9로 꽉 차면 배경 카메라 끔.
  씬 로드마다 AttachLetterbox(): 모든 카메라(targetTexture 제외)에 LetterboxCamera, Overlay 루트 캔버스에 LetterboxCanvas. Instance/HasInstance/BackgroundCamera.

## UI
- UI/LetterboxCanvas.cs: Overlay 루트 캔버스의 자식을 "LetterboxRoot"로 옮겨 16:9 영역에 맞춤(씬 계층 불변). CanvasScaler 기준을 16:9 영역으로. 자체 Canvas 가진 자식은 제외. Root.
- UI/Settings/SettingsPanel.cs: 화면모드 TMP_Dropdown(같은 항목 재선택 시에도 강제 재적용), 체력바/데미지텍스트 Toggle, VolumeSettingRow[], closeButton. Open/Close/Refresh/IsOpen, 이벤트 OnClosed.
- UI/Settings/VolumeSettingRow.cs: channel, Slider, 음소거 Button, 라벨(켜짐/음소거, "{0:0}%"). Channel, Refresh().
- UI/Pause/PauseMenu.cs: GameScene ESC 메뉴(계속/설정/타이틀). Open/Close/OpenSettings/GoToTitle/IsOpen. GamePause.Pause(this)/Resume(this)로 timeScale 관리(비활성/파괴 시에도 Resume). 타 일시정지 중이거나 GameOverUI 표시 중이면 안 열림. 설정창 열려 있으면 ESC는 설정창만 닫음. titleSceneName="TitleScene".
- 수정 UI/Title/TitleMenu.cs: settingsButton, settingsPanel 필드 추가, ESC로 닫기(InputSystem).
- 수정 UI/Binder/HPSliderBinder.cs: GameSettings.ShowEnemyHPBar 반영(OnShowEnemyHPBarChanged 구독, ApplyVisibility). 숨김은 Canvas.enabled(hpBarCanvas)로 처리.
- 수정 UI/Binder/DamageTextBinder.cs: ShowDamageText 꺼지면 생성 안 함+기존 텍스트 제거, font/fontStyle 필드(PixelFontBuilder가 연결), ui.Setup 호출.
- 수정 UI/FloatingTextUI.cs: DefaultSortingLayerID()(Front UI -> attack -> 0), Setup(font, style, sortingLayerID, sortingOrder). 생성 시 기본 정렬 레이어 적용.

## 에디터 빌더 `Assets/Editor/Builders/` (namespace Builders)
- PixelFontBuilder: Tools/Builders/PixelFont. Galmuri11 TMP 폰트 에셋 생성(GetOrCreateRegular/Bold, 폴백 NotoSansKR), AssignDamageTextFont()로 데미지 텍스트 프리팹에 폰트 연결.
  생성: Assets/Fonts/Galmuri/Galmuri11-Bold Pixel.asset (Regular는 Galmuri11 Pixel.asset).
- DisplaySettingsBuilder: Tools/Builders/DisplaySettings. 디스플레이 관련 프로젝트 설정.
- SettingsUIKit: 빌더 공용 UI 헬퍼(색상 상수, Font, MakeRect/Image/Label/Button, 레이아웃 그룹, 스프라이트 슬라이스·버튼 헬퍼).
- SettingsPanelBuilder: Tools/Builders/SettingsPanel. 프리팹 Assets/prefab/UI/SettingsPanel.prefab. GetOrBuildPrefab/InstantiateInto(parent).
- PauseMenuBuilder: Tools/Builders/PauseMenu. GameScene에 "PauseCanvas" + PauseMenu 구성.
- 수정 TitleSceneBuilder: 설정 버튼 추가, 별도 "SettingsCanvas"(sortingOrder 50, 1920x1080, match 0.5)에 설정창 인스턴스, TitleMenu에 연결.
- 신규 Visual/GameViewHelper.cs (Assets/Editor/Builders/Visual/): 캡처용 Game 뷰 해상도 설정.
- 실행: internal 헬퍼 때문에 `unity command script eval "return Builders.X.Build();"` 형태로 호출.

## 빌더 실행 순서
- HUD 빌더(InGameHudBuilder -> InGameHudVisual -> ArtResourceBuilder) 순서: [hud.md](hud.md)
PixelFontBuilder -> DisplaySettingsBuilder -> SettingsPanelBuilder -> TitleSceneBuilder -> (GameOverBuilder) -> PauseMenuBuilder -> ArtResourceBuilder (아트 덮어쓰기 방지로 마지막).
생성 에셋: Galmuri11-Bold Pixel.asset, prefab/UI/SettingsPanel.prefab, TitleScene SettingsCanvas, GameScene PauseCanvas.

## 알려진 한계
- PlayerStatDebugUI(IMGUI 디버그)는 레터박스 밖 좌표 사용.
- 런타임 Instantiate된 Overlay 캔버스는 SettingsApplier.AttachLetterbox() 호출 필요.
- Screen.SetResolution은 에디터에서 미동작.

## 의존
SettingsApplier -> GameSettings/ScreenModes/Letterbox/LetterboxCamera/LetterboxCanvas. PauseMenu -> SettingsPanel, GameOverUI, GamePause. Binder들 -> GameSettings 이벤트.
