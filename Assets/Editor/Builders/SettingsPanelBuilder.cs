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
    ///            ├ Content : [화면] 화면 크기 드롭다운
    ///            │           [정보 표시] 적 체력바 표시 / 데미지 표시 토글
    ///            │           [소리] 전체 음량 / 효과음 / 배경음 (슬라이더 + % + 음소거 버튼, VolumeSettingRow)
    ///            └ CloseButton "닫기"
    /// - 기준 해상도 1920x1080 좌표. 텍스트는 갈무리 픽셀 폰트, 크기는 12 의 배수
    /// - 씬 배치는 TitleSceneBuilder(SettingsCanvas) / PauseMenuBuilder(PauseCanvas) 가 InstantiateInto 로 한다.
    /// </summary>
    public static class SettingsPanelBuilder
    {
        public const string PREFAB_PATH = "Assets/prefab/UI/SettingsPanel.prefab";

        private static readonly Vector2 WINDOW_SIZE = new Vector2(1080f, 900f);
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

                TextMeshProUGUI title = SettingsUIKit.MakeLabel("Title", window.transform, "설정", TITLE_SIZE, TextAlignmentOptions.Center);
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

                MakeHeader(content, "ScreenHeader", "화면");
                RectTransform screenRow = MakeRow(content, "ScreenModeRow", "화면 크기");
                TMP_Dropdown dropdown = MakeDropdown(screenRow);

                MakeHeader(content, "InfoHeader", "정보 표시");
                Toggle hpToggle = MakeToggle(MakeRow(content, "EnemyHPBarRow", "적 체력바 표시"));
                Toggle damageToggle = MakeToggle(MakeRow(content, "DamageTextRow", "데미지 표시"));

                MakeHeader(content, "SoundHeader", "소리");
                var rows = new List<VolumeSettingRow>
                {
                    MakeVolumeRow(content, "MasterVolumeRow", "전체 음량", AudioChannel.Master),
                    MakeVolumeRow(content, "EffectVolumeRow", "효과음", AudioChannel.Effect),
                    MakeVolumeRow(content, "BGMVolumeRow", "배경음", AudioChannel.BGM),
                };

                // 4. 닫기
                Button close = SettingsUIKit.MakeButton("CloseButton", window.transform, "닫기", 300f, 84f, TEXT_SIZE);
                RectTransform closeRt = close.GetComponent<RectTransform>();
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0f);
                closeRt.pivot = new Vector2(0.5f, 0f);
                closeRt.anchoredPosition = new Vector2(0f, 32f);
                SettingsUIKit.StyleButton(close);

                // 5. 연결
                SettingsPanel panel = rootGo.AddComponent<SettingsPanel>();
                var so = new SerializedObject(panel);
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

        private static void MakeHeader(Transform _content, string _name, string _text)
        {
            TextMeshProUGUI header = SettingsUIKit.MakeLabel(_name, _content, _text, HEADER_SIZE, TextAlignmentOptions.MidlineLeft);
            header.color = SettingsUIKit.ACCENT_COLOR;
            SettingsUIKit.SetLayoutSize(header.gameObject, LABEL_WIDTH * 3f, HEADER_HEIGHT);
        }

        /// <summary>
        /// 왼쪽 이름 + 오른쪽 컨트롤 한 줄
        /// </summary>
        private static RectTransform MakeRow(Transform _content, string _name, string _label)
        {
            RectTransform row = SettingsUIKit.MakeRect(_name, _content);
            SettingsUIKit.AddHorizontal(row.gameObject, ROW_SPACING, TextAnchor.MiddleLeft);
            SettingsUIKit.SetLayoutSize(row.gameObject, -1f, ROW_HEIGHT);
            row.GetComponent<LayoutElement>().minWidth = -1f;
            row.GetComponent<LayoutElement>().flexibleWidth = 1f;

            TextMeshProUGUI label = SettingsUIKit.MakeLabel("Label", row, _label, TEXT_SIZE, TextAlignmentOptions.MidlineLeft);
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

        private static TMP_Dropdown MakeDropdown(Transform _row)
        {
            GameObject go = TMP_DefaultControls.CreateDropdown(TmpResources());
            go.name = "ScreenModeDropdown";
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
                template.sizeDelta = new Vector2(0f, ROW_HEIGHT * 4f + 12f);
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
            var options = new List<string>();
            for (int i = 0; i < ScreenModes.Count; ++i)
            {
                options.Add(ScreenModes.GetLabel(i));
            }
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(ScreenModes.DEFAULT_INDEX);
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

        private static VolumeSettingRow MakeVolumeRow(Transform _content, string _name, string _label, AudioChannel _channel)
        {
            RectTransform row = MakeRow(_content, _name, _label);

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

            Button mute = SettingsUIKit.MakeButton("MuteButton", row, "켜짐", MUTE_WIDTH, ROW_HEIGHT, TEXT_SIZE);
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
