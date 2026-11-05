using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// 재사용 설정 창 프리팹 Assets/prefab/UI/SettingsPanel.prefab 을 새로 만든다 (멱등, 매번 덮어씀)
    /// - SettingsPanel (전체 화면 반투명 검정, 뒤 클릭 차단)
    ///   └ Window ─ Title "설정"
    ///            ├ Content : [일반] 언어 드롭다운 (항목은 "한국어" / "English" 고정)
    ///            │           [화면] 화면 크기 드롭다운
    ///            │           [정보 표시] 적 체력바 표시 / 데미지 표시 토글
    ///            │           [소리] 전체 음량 / 효과음 / 배경음 (슬라이더 + % + 음소거 버튼, VolumeSettingRow)
    ///            └ CloseButton "닫기"
    /// - 기준 해상도 1920x1080 좌표. 텍스트는 갈무리 픽셀 폰트, 크기는 12 의 배수
    /// - 고정 라벨은 LocalizedText(key) 로 StringTable 을 쓴다 (미리보기 글자는 한국어)
    /// - 씬 배치는 TitleSceneBuilder(SettingsCanvas) / PauseMenuBuilder(PauseCanvas) 가 InstantiateInto 로 한다.
    /// </summary>
    public static class SettingsPanelBuilder
    {
        public const string PREFAB_PATH = "Assets/prefab/UI/SettingsPanel.prefab";

        // 언어 줄(헤더 + 한 줄)을 넣으며 높이를 900 에서 1008 로 늘림
        private static readonly Vector2 WINDOW_SIZE = new Vector2(1080f, 1008f);
        private const int DROPDOWN_VISIBLE_ROWS = 4;
        private const float TITLE_SIZE = 60f;
        private const float HEADER_SIZE = 36f;
        private const float TEXT_SIZE = 36f;
        private const float ROW_HEIGHT = 60f;
        private const float HEADER_HEIGHT = 48f;
        private const float LABEL_WIDTH = 264f;
        private const float DROPDOWN_WIDTH = 480f;
        private const float TOGGLE_SIZE = 48f;
        private const float SLIDER_WIDTH = 360f;
        private const float VALUE_WIDTH = 108f;
        private const float MUTE_WIDTH = 156f;
        private const float ROW_SPACING = 20f;
        private static readonly Color DROPDOWN_TEXT_COLOR = Color.white;

        [MenuItem("Tools/Builders/SettingsPanel")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>
        /// 프리팹이 없으면 만들어서 돌려준다
        /// </summary>
        public static GameObject GetOrBuildPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            if (null == prefab)
            {
                Debug.Log(Build());
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            }
            return prefab;
        }

        /// <summary>
        /// 프리팹 인스턴스를 _parent 아래에 전체 화면으로 꺼진 상태로 배치
        /// </summary>
        public static SettingsPanel InstantiateInto(Transform _parent)
        {
            GameObject prefab = GetOrBuildPrefab();
            if (null == prefab)
            {
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _parent);
            go.name = prefab.name;
            SettingsUIKit.Stretch(go.GetComponent<RectTransform>());
            go.transform.SetAsLastSibling();
            go.SetActive(false);
            return go.GetComponent<SettingsPanel>();
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SettingsPanelBuilder]");
            SettingsUIKit.ResetCache();
            LocalizationBuilderUtil.Reload();
            if (null == SettingsUIKit.Font)
            {
                sb.AppendLine("경고: 갈무리 폰트 생성 실패 (TMP 기본 폰트 사용)");
            }

            // 1. 루트 (반투명 검정 전체 화면)
            var rootGo = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            rootGo.layer = SettingsUIKit.UI_LAYER;
            try
            {
                var rootRt = rootGo.GetComponent<RectTransform>();
                SettingsUIKit.Stretch(rootRt);
                var dim = rootGo.GetComponent<Image>();
                dim.color = SettingsUIKit.DIM_COLOR;
                dim.raycastTarget = true; // 뒤쪽 UI 클릭 차단

                // 2. 창
                Image window = SettingsUIKit.MakeImage("Window", rootRt, SettingsUIKit.WINDOW_COLOR, true);
                window.sprite = SettingsUIKit.BuiltinSprite("UISprite.psd");
                window.type = Image.Type.Sliced;
                SettingsUIKit.Slice(window, "PanelDark", Color.white);
                SettingsUIKit.Center(window.rectTransform, WINDOW_SIZE, Vector2.zero);

                TextMeshProUGUI title = SettingsUIKit.MakeLabel("Title", window.transform, string.Empty, TITLE_SIZE, TextAlignmentOptions.Center);
                LocalizationBuilderUtil.Bind(title, "ui.settings.title");
                RectTransform titleRt = title.rectTransform;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(0f, 96f);
                titleRt.anchoredPosition = new Vector2(0f, -24f);

                // 3. 항목
                RectTransform content = SettingsUIKit.MakeRect("Content", window.transform);
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = new Vector2(60f, 132f);
                content.offsetMax = new Vector2(-60f, -132f);
                SettingsUIKit.AddVertical(content.gameObject, 12f, TextAnchor.UpperLeft);

                MakeHeader(content, "GeneralHeader", "ui.settings.header.general");
                RectTransform languageRow = MakeRow(content, "LanguageRow", "ui.settings.language");
                var languageOptions = new List<string>();
                for (int i = 0; i < LanguageInfo.Count; ++i)
                {
                    // 각 언어 고유 표기 (문자열 테이블을 거치지 않는다)
                    languageOptions.Add(LanguageInfo.GetNativeName((Language)i));
                }
                TMP_Dropdown languageDropdown = MakeDropdown(languageRow, "LanguageDropdown", languageOptions, (int)LanguageInfo.DEFAULT);

                MakeHeader(content, "ScreenHeader", "ui.settings.header.screen");
                RectTransform screenRow = MakeRow(content, "ScreenModeRow", "ui.settings.screen_mode");
                var screenOptions = new List<string>();
                for (int i = 0; i < ScreenModes.Count; ++i)
                {
                    screenOptions.Add(ScreenModes.GetLabel(i));
                }
                TMP_Dropdown dropdown = MakeDropdown(screenRow, "ScreenModeDropdown", screenOptions, ScreenModes.DEFAULT_INDEX);

                MakeHeader(content, "InfoHeader", "ui.settings.header.info");
                Toggle hpToggle = MakeToggle(MakeRow(content, "EnemyHPBarRow", "ui.settings.enemy_hp_bar"));
                Toggle damageToggle = MakeToggle(MakeRow(content, "DamageTextRow", "ui.settings.damage_text"));

                MakeHeader(content, "SoundHeader", "ui.settings.header.sound");
                var rows = new List<VolumeSettingRow>
                {
                    MakeVolumeRow(content, "MasterVolumeRow", "ui.settings.volume.master", AudioChannel.Master),
                    MakeVolumeRow(content, "EffectVolumeRow", "ui.settings.volume.effect", AudioChannel.Effect),
                    MakeVolumeRow(content, "BGMVolumeRow", "ui.settings.volume.bgm", AudioChannel.BGM),
                };

                // 4. 닫기
                Button close = SettingsUIKit.MakeButton("CloseButton", window.transform, string.Empty, 300f, 84f, TEXT_SIZE);
                LocalizationBuilderUtil.BindChildLabel(close, "ui.settings.close");
                RectTransform closeRt = close.GetComponent<RectTransform>();
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
                closeRt.pivot = new Vector2(0.5f, 0f);
                closeRt.anchoredPosition = new Vector2(0f, 32f);
                SettingsUIKit.StyleButton(close);

                // 5. 연결
                SettingsPanel panel = rootGo.AddComponent<SettingsPanel>();
                var so = new SerializedObject(panel);
                so.FindProperty("languageDropdown").objectReferenceValue = languageDropdown;
                so.FindProperty("screenModeDropdown").objectReferenceValue = dropdown;
                so.FindProperty("enemyHPBarToggle").objectReferenceValue = hpToggle;
                so.FindProperty("damageTextToggle").objectReferenceValue = damageToggle;
                so.FindProperty("closeButton").objectReferenceValue = close;
                SerializedProperty rowsProp = so.FindProperty("volumeRows");
                rowsProp.arraySize = rows.Count;
                for (int i = 0; i < rows.Count; ++i)
                {
                    rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                SettingsUIKit.SetLayerRecursive(rootGo, SettingsUIKit.UI_LAYER);

                PrefabUtility.SaveAsPrefabAsset(rootGo, PREFAB_PATH, out bool success);
                sb.AppendLine((success ? "저장: " : "저장 실패: ") + PREFAB_PATH);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }

            return sb.ToString();
        }

        /// <param name="_key">헤더 글자 StringTable 키</param>
        private static void MakeHeader(Transform _content, string _name, string _key)
        {
            TextMeshProUGUI header = SettingsUIKit.MakeLabel(_name, _content, string.Empty, HEADER_SIZE, TextAlignmentOptions.MidlineLeft);
            LocalizationBuilderUtil.Bind(header, _key);
            header.color = SettingsUIKit.ACCENT_COLOR;
            SettingsUIKit.SetLayoutSize(header.gameObject, LABEL_WIDTH * 3f, HEADER_HEIGHT);
        }

        /// <summary>
        /// 왼쪽 이름 + 오른쪽 컨트롤 한 줄
        /// </summary>
        /// <param name="_labelKey">왼쪽 이름 StringTable 키</param>
        private static RectTransform MakeRow(Transform _content, string _name, string _labelKey)
        {
            RectTransform row = SettingsUIKit.MakeRect(_name, _content);
            SettingsUIKit.AddHorizontal(row.gameObject, ROW_SPACING, TextAnchor.MiddleLeft);
            SettingsUIKit.SetLayoutSize(row.gameObject, -1f, ROW_HEIGHT);
            row.GetComponent<LayoutElement>().minWidth = -1f;
            row.GetComponent<LayoutElement>().flexibleWidth = 1f;

            TextMeshProUGUI label = SettingsUIKit.MakeLabel("Label", row, string.Empty, TEXT_SIZE, TextAlignmentOptions.MidlineLeft);
            LocalizationBuilderUtil.Bind(label, _labelKey);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = TEXT_SIZE;
            label.overflowMode = TextOverflowModes.Ellipsis;
            SettingsUIKit.SetLayoutSize(label.gameObject, LABEL_WIDTH, ROW_HEIGHT);
            return row;
        }

        private static TMP_DefaultControls.Resources TmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = SettingsUIKit.BuiltinSprite("UISprite.psd"),
                background = SettingsUIKit.BuiltinSprite("Background.psd"),
                inputField = SettingsUIKit.BuiltinSprite("InputFieldBackground.psd"),
                knob = SettingsUIKit.BuiltinSprite("Knob.psd"),
                checkmark = SettingsUIKit.BuiltinSprite("Checkmark.psd"),
                dropdown = SettingsUIKit.BuiltinSprite("DropdownArrow.psd"),
                mask = SettingsUIKit.BuiltinSprite("UIMask.psd"),
            };
        }

        private static DefaultControls.Resources UiResources()
        {
            return new DefaultControls.Resources
            {
                standard = SettingsUIKit.BuiltinSprite("UISprite.psd"),
                background = SettingsUIKit.BuiltinSprite("Background.psd"),
                inputField = SettingsUIKit.BuiltinSprite("InputFieldBackground.psd"),
                knob = SettingsUIKit.BuiltinSprite("Knob.psd"),
                checkmark = SettingsUIKit.BuiltinSprite("Checkmark.psd"),
                dropdown = SettingsUIKit.BuiltinSprite("DropdownArrow.psd"),
                mask = SettingsUIKit.BuiltinSprite("UIMask.psd"),
            };
        }

        /// <summary>
        /// 줄 오른쪽 드롭다운 (옵션 글자는 런타임에 SettingsPanel 이 다시 채운다)
        /// </summary>
        private static TMP_Dropdown MakeDropdown(Transform _row, string _name, List<string> _options, int _value)
        {
            GameObject go = TMP_DefaultControls.CreateDropdown(TmpResources());
            go.name = _name;
            go.transform.SetParent(_row, false);
            SettingsUIKit.SetLayoutSize(go, DROPDOWN_WIDTH, ROW_HEIGHT);

            // 기본 크기(160x30) 기준 자식들을 60 높이에 맞게 키운다
            SetOffsets(go.transform, "Label", new Vector2(20f, 4f), new Vector2(-64f, -4f));
            RectTransform arrow = Find(go.transform, "Arrow");
            if (null != arrow)
            {
                arrow.sizeDelta = new Vector2(36f, 36f);
                arrow.anchoredPosition = new Vector2(-30f, 0f);
            }
            RectTransform template = Find(go.transform, "Template");
            if (null != template)
            {
                int visibleRows = Mathf.Clamp(_options.Count, 1, DROPDOWN_VISIBLE_ROWS);
                template.sizeDelta = new Vector2(0f, ROW_HEIGHT * visibleRows + 12f);
            }
            RectTransform scrollbar = Find(go.transform, "Template/Scrollbar");
            if (null != scrollbar)
            {
                scrollbar.sizeDelta = new Vector2(24f, scrollbar.sizeDelta.y);
            }
            RectTransform contentRt = Find(go.transform, "Template/Viewport/Content");
            if (null != contentRt)
            {
                contentRt.sizeDelta = new Vector2(0f, ROW_HEIGHT + 8f);
            }
            RectTransform item = Find(go.transform, "Template/Viewport/Content/Item");
            if (null != item)
            {
                item.sizeDelta = new Vector2(0f, ROW_HEIGHT);
            }
            RectTransform check = Find(go.transform, "Template/Viewport/Content/Item/Item Checkmark");
            if (null != check)
            {
                check.sizeDelta = new Vector2(36f, 36f);
                check.anchoredPosition = new Vector2(28f, 0f);
            }
            SetOffsets(go.transform, "Template/Viewport/Content/Item/Item Label", new Vector2(56f, 2f), new Vector2(-20f, -2f));

            SettingsUIKit.ApplyFont(go, TEXT_SIZE, DROPDOWN_TEXT_COLOR);
            StyleDropdown(go);

            var dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(_options);
            dropdown.SetValueWithoutNotify(_value);
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static Toggle MakeToggle(Transform _row)
        {
            GameObject go = DefaultControls.CreateToggle(UiResources());
            go.name = "Toggle";
            go.transform.SetParent(_row, false);
            SettingsUIKit.SetLayoutSize(go, TOGGLE_SIZE, TOGGLE_SIZE);

            // 기본 라벨(레거시 Text)은 쓰지 않는다 (왼쪽에 TMP 라벨이 있음)
            Transform legacyLabel = go.transform.Find("Label");
            if (null != legacyLabel)
            {
                Object.DestroyImmediate(legacyLabel.gameObject);
            }

            RectTransform bg = Find(go.transform, "Background");
            if (null != bg)
            {
                SettingsUIKit.Stretch(bg);
            }
            RectTransform check = Find(go.transform, "Background/Checkmark");
            if (null != check)
            {
                check.anchorMin = Vector2.zero;
                check.anchorMax = Vector2.one;
                check.offsetMin = new Vector2(4f, 4f);
                check.offsetMax = new Vector2(-4f, -4f);
            }

            var toggle = go.GetComponent<Toggle>();
            StyleToggle(toggle);
            toggle.isOn = true;
            return toggle;
        }

        private static VolumeSettingRow MakeVolumeRow(Transform _content, string _name, string _labelKey, AudioChannel _channel)
        {
            RectTransform row = MakeRow(_content, _name, _labelKey);

            GameObject sliderGo = DefaultControls.CreateSlider(UiResources());
            sliderGo.name = "Slider";
            sliderGo.transform.SetParent(row, false);
            SettingsUIKit.SetLayoutSize(sliderGo, SLIDER_WIDTH, ROW_HEIGHT);
            RectTransform handle = Find(sliderGo.transform, "Handle Slide Area/Handle");
            if (null != handle)
            {
                handle.sizeDelta = new Vector2(32f, 0f);
            }
            var slider = sliderGo.GetComponent<Slider>();
            StyleSlider(slider);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;

            TextMeshProUGUI value = SettingsUIKit.MakeLabel("Value", row, "100%", TEXT_SIZE, TextAlignmentOptions.MidlineRight);
            SettingsUIKit.SetLayoutSize(value.gameObject, VALUE_WIDTH, ROW_HEIGHT);

            // 음소거 버튼 글자는 VolumeSettingRow 가 상태에 따라 넣는다 (LocalizedText 를 붙이지 않음)
            Button mute = SettingsUIKit.MakeButton("MuteButton", row, LocalizationBuilderUtil.Ko("ui.settings.volume.on"), MUTE_WIDTH, ROW_HEIGHT, TEXT_SIZE);
            SettingsUIKit.StyleButton(mute);
            TMP_Text muteLabel = mute.GetComponentInChildren<TMP_Text>(true);

            VolumeSettingRow comp = row.gameObject.AddComponent<VolumeSettingRow>();
            var so = new SerializedObject(comp);
            so.FindProperty("channel").intValue = (int)_channel;
            so.FindProperty("slider").objectReferenceValue = slider;
            so.FindProperty("muteButton").objectReferenceValue = mute;
            so.FindProperty("muteLabel").objectReferenceValue = muteLabel;
            so.FindProperty("valueLabel").objectReferenceValue = value;
            // 음소거 상태: 주황 버튼 위에서 흰 글자와 확실히 구분되는 어두운 글자
            so.FindProperty("mutedColor").colorValue = new Color(0.16f, 0.12f, 0.2f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return comp;
        }

        private static readonly Color DARK = new Color(0.17f, 0.16f, 0.22f, 1f);

        private static void SetImage(Transform _root, string _path, string _sprite, Color _color)
        {
            RectTransform rt = Find(_root, _path);
            if (null == rt)
            {
                return;
            }
            var image = rt.GetComponent<Image>();
            if (null != image)
            {
                SettingsUIKit.Slice(image, _sprite, _color);
            }
        }

        private static void StyleDropdown(GameObject _go)
        {
            var dd = _go.GetComponent<TMP_Dropdown>();
            SettingsUIKit.Slice(_go.GetComponent<Image>(), "Button_Normal", Color.white);
            dd.transition = Selectable.Transition.SpriteSwap;
            dd.spriteState = new SpriteState
            {
                highlightedSprite = SettingsUIKit.ArtSprite("Button_Hover"),
                selectedSprite = SettingsUIKit.ArtSprite("Button_Hover"),
                pressedSprite = SettingsUIKit.ArtSprite("Button_Pressed"),
            };

            RectTransform arrow = Find(_go.transform, "Arrow");
            if (null != arrow)
            {
                arrow.GetComponent<Image>().color = Color.white;
            }

            // 펼친 목록: 어두운 패널 + 항목 하이라이트
            SetImage(_go.transform, "Template", "PanelDark", Color.white);
            RectTransform item = Find(_go.transform, "Template/Viewport/Content/Item");
            if (null != item)
            {
                var toggle = item.GetComponent<Toggle>();
                RectTransform itemBg = Find(item, "Item Background");
                var bgImage = itemBg.GetComponent<Image>();
                bgImage.sprite = null;
                bgImage.color = Color.white;
                toggle.targetGraphic = bgImage;
                toggle.transition = Selectable.Transition.ColorTint;
                toggle.colors = new ColorBlock
                {
                    normalColor = new Color(1f, 1f, 1f, 0f),
                    highlightedColor = new Color(0.94f, 0.4f, 0.2f, 1f),
                    pressedColor = new Color(0.6f, 0.32f, 0.24f, 1f),
                    selectedColor = new Color(0.94f, 0.4f, 0.2f, 0.5f),
                    disabledColor = new Color(1f, 1f, 1f, 0f),
                    colorMultiplier = 1f,
                    fadeDuration = 0.05f,
                };
                RectTransform check = Find(item, "Item Checkmark");
                if (null != check)
                {
                    var ci = check.GetComponent<Image>();
                    ci.sprite = null;
                    ci.color = SettingsUIKit.ACCENT_COLOR;
                    check.sizeDelta = new Vector2(20f, 20f);
                    check.anchoredPosition = new Vector2(28f, 0f);
                }
            }
            RectTransform sb = Find(_go.transform, "Template/Scrollbar");
            if (null != sb)
            {
                sb.GetComponent<Image>().sprite = null;
                sb.GetComponent<Image>().color = DARK;
                RectTransform sbHandle = Find(sb, "Sliding Area/Handle");
                if (null != sbHandle)
                {
                    SettingsUIKit.Slice(sbHandle.GetComponent<Image>(), "Button_Normal", Color.white);
                }
            }
        }

        private static void StyleToggle(Toggle _toggle)
        {
            SetImage(_toggle.transform, "Background", "PanelDark", Color.white);
            RectTransform check = Find(_toggle.transform, "Background/Checkmark");
            if (null != check)
            {
                var ci = check.GetComponent<Image>();
                ci.sprite = null;
                ci.color = SettingsUIKit.ACCENT_COLOR;
                check.offsetMin = new Vector2(12f, 12f);
                check.offsetMax = new Vector2(-12f, -12f);
            }
        }

        private static void StyleSlider(Slider _slider)
        {
            Transform t = _slider.transform;
            RectTransform bg = Find(t, "Background");
            bg.anchorMin = new Vector2(0f, 0.25f);
            bg.anchorMax = new Vector2(1f, 0.75f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            SetImage(t, "Background", "PanelDark", Color.white);

            RectTransform fillArea = Find(t, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(12f, 0f);
            fillArea.offsetMax = new Vector2(-12f, 0f);
            RectTransform fill = Find(t, "Fill Area/Fill");
            fill.sizeDelta = Vector2.zero;
            SetImage(t, "Fill Area/Fill", "Bar_EXP_Fill", Color.white);

            RectTransform area = Find(t, "Handle Slide Area");
            area.anchorMin = new Vector2(0f, 0.1f);
            area.anchorMax = new Vector2(1f, 0.9f);
            area.offsetMin = new Vector2(12f, 0f);
            area.offsetMax = new Vector2(-12f, 0f);
            RectTransform handle = Find(t, "Handle Slide Area/Handle");
            handle.sizeDelta = new Vector2(24f, 0f);
            SetImage(t, "Handle Slide Area/Handle", "Button_Normal", Color.white);
            _slider.targetGraphic = handle.GetComponent<Image>();
        }

        private static RectTransform Find(Transform _root, string _path)
        {
            return _root.Find(_path) as RectTransform;
        }

        private static void SetOffsets(Transform _root, string _path, Vector2 _min, Vector2 _max)
        {
            RectTransform rt = Find(_root, _path);
            if (null == rt)
            {
                return;
            }
            rt.offsetMin = _min;
            rt.offsetMax = _max;
        }
    }
}
