# 문자열 테이블 / 다국어 (2026-11-05, 미커밋)

## 개요
한국어(ko)/영어(en) 문자열을 CSV 한 장으로 관리. 언어는 GameSettings.Language(PlayerPrefs `Settings.Language`, 기본 한국어)에 저장.
SO/프리팹 필드는 "키 필드"가 되고, 표시 시점에 StringTable 로 해석한다.

## 파일
- `Assets/Resources/Localization/StringTable.csv` : 헤더 `key,ko,en`, 201키. `#` 시작 key 줄 = 주석, 빈 줄 무시. 쉼표/줄바꿈 칸은 "..." 로 감쌈. 영어 빈 칸이면 ko 대체.
- `Assets/Resources/Script/Localization/Language.cs` : enum Language{Korean,English}, LanguageInfo(DEFAULT, Count, Clamp, GetCode "ko"/"en", GetNativeName "한국어"/"English").
- `.../Localization/StringTable.cs` : static. Resources.Load 1회 캐시.
- `.../Localization/LocalizedText.cs` : TMP_Text 에 붙는 고정 라벨용. 직렬화 필드 `key`, Key 프로퍼티, Apply(). OnEnable/언어변경 시 text 덮어씀 (코드가 text 를 직접 바꾸는 라벨에는 붙이지 말 것).
- `Assets/Editor/Builders/LocalizationBuilderUtil.cs` (public static, namespace Builders).

## StringTable API
- Get(key[,lang]) : 누락 시 key 그대로 + 에디터 1회 경고. Format(key, args) : Get + string.Format. FormatText(format, args) : 이미 번역된 포멧에 적용(잘못된 포멧이면 원본).
- TryGet(key[,lang], out) 경고 없음. HasKey. Resolve(keyOrText) : 키면 번역, 아니면 원문 그대로 (키/원문 혼용 데이터 필드용).
- CurrentLanguage(=GameSettings.Language), 이벤트 OnLanguageChanged(Language)(=GameSettings 이벤트), Count, Reload(), LoadFromText(csv)(테스트), Parse, ParseCsv.

## 키 네이밍 (영역.항목)
ui.* (공통/타이틀/설정/일시정지/레벨업, 23), gameover.* (12), hud.*, dimension.*(3), stat.* (스탯 이름, ValueType), reward.*(44; reward.weapon.<snake_id>.name/.explain, reward.hp.explain1 등),
upgrade.* (113; 무기 업그레이드 단계 문구. 빌더가 한국어 원문으로 키 역조회).

## 키 필드로 바뀐 것
- ObjectDescription: objectName/description 이 키(Resolve). GetNameKey 추가, DescriptionHolder.GetNameKey.
- ValueType 확장: ToStringKey(), 이름 변환이 StringTable.TryGet 사용(없으면 enum 이름).
- RewardData: rewardName 키(Resolve), 설명 포멧 1/2 는 키 또는 포멧 문자열({0}=key,{1}=value,{2}=name), GetNameKey, 업그레이드 이름은 UPGRADE_NAME_KEY 포멧.
- WeaponRewardEffect/WeaponUpgradeRewardEffect: 이름을 GetNameKey 로, 업그레이드 문구 키면 그대로/아니면 EscapeFormat.
- DimensionData: displayNameKey (DisplayName=Resolve, DisplayNameKey).
- GameOverUI: titleKey/victoryKey/leftHeaderKey/.../expFormatKey 등 키 필드(기본값 gameover.*), 언어 변경 시 재렌더. StatisticsManager 는 무기 이름 키 보관.
- WeaponSlotUI: levelFormatKey(hud.weapon_slot.level), fallbackNameKey; WeaponHUD 는 reward.GetNameKey 전달.
- LevelUpUI: resumeLabelKey(ui.levelup.resume). SelectPanel, DimensionSelectView: 언어 변경 구독.
- ScreenModes: 전체 화면 라벨 키 (창 크기 숫자는 번역 안 함).
- SettingsPanel: 언어 드롭다운(LanguageInfo 고정 표기), VolumeSettingRow 라벨 키화.

## LocalizationBuilderUtil
Reload() (CSV 재임포트+StringTable.Reload, 빌더 시작 시 호출), Ko(key), Bind(text,key) (LocalizedText 부착 멱등+한국어 미리보기), BindChildLabel(root,key), Unbind(text),
WeaponRewardNameKey/ExplainKey(id), ToSnake(id), KeyOfKorean(원문) (CSV 첫 ko 일치 키, 없으면 경고+원문 반환).
수정된 빌더: BalanceData/DimensionData/ExtraWeapon/GameOver/PauseMenu/SettingsPanel/TitleScene/WeaponUpgrade.

## 새 텍스트 추가 절차
1. CSV 에 키 추가 (ko, en; 영역.항목 규칙).
2. 고정 라벨: LocalizedText(빌더에서 Bind). 코드가 넣는 텍스트: StringTable.Get/Format 후 OnLanguageChanged 구독/해제(OnEnable/OnDisable)로 재적용.
3. 데이터 SO 필드: 키를 저장하고 표시 시 StringTable.Resolve.
4. 빌더는 LocalizationBuilderUtil 사용 후 씬/프리팹 빌더 재실행 (ArtResourceBuilder 도 재실행 주의 - CLAUDE.md).
