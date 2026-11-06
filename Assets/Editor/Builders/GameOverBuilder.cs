using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// GameScene 에 통계·게임 오버를 배치한다 (멱등: 있으면 갱신, 없으면 생성)
    /// - 루트 StatisticsManager (기본값 유지)
    /// - 루트 GameOverManager (gameOverUI, statisticsManager 연결)
    /// - 루트 GameOverCanvas (Screen Space Overlay, sortingOrder 100 → LevelUpUI(0) 보다 위)
    ///   └ GameOverUI ─ Panel(반투명 검정 전체 화면) ─ Title / LeftLabel / RightLabel
    /// - 비주얼(색·크기·레이아웃 다듬기)은 visual-designer 담당. 여기서는 플레이스홀더 값만 둔다.
    /// </summary>
    public static class GameOverBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string FONT_PATH = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
        private const int SORTING_ORDER = 100;
        private const string WEAPON_REWARD_FOLDER = "Assets/prefab/Upgrade/Weapon";
        // 카드 아래쪽 무기 아이콘 칸 영역 (2열, 무기 보유 상한 4 → 2줄)
        private const float WEAPON_LIST_HEIGHT = 130f;
        private const float WEAPON_LIST_BOTTOM = 26f;
        private static readonly Vector2 WEAPON_CELL = new Vector2(340f, 56f);
        private static readonly Vector2 WEAPON_SPACING = new Vector2(24f, 10f);

        [MenuItem("Tools/Builders/GameOver")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[GameOverBuilder]");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            LocalizationBuilderUtil.Reload();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (null == font)
            {
                sb.AppendLine("경고: 폰트 없음 " + FONT_PATH);
            }

            // 1. StatisticsManager
            GameObject statsGo = FindOrCreateRoot(scene, "StatisticsManager");
            StatisticsManager stats = GetOrAdd<StatisticsManager>(statsGo);

            // 2. Canvas
            GameObject canvasGo = FindOrCreateRoot(scene, "GameOverCanvas");
            canvasGo.layer = LayerMask.NameToLayer("UI");
            EnsureRect(canvasGo);
            Canvas canvas = GetOrAdd<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasGo);

            // 3. GameOverUI (항상 켜져 있는 루트, panel 만 켜고 끈다)
            GameObject uiGo = FindOrCreateChild(canvasGo.transform, "GameOverUI");
            Stretch(uiGo.GetComponent<RectTransform>());
            GameOverUI ui = GetOrAdd<GameOverUI>(uiGo);

            GameObject panelGo = FindOrCreateChild(uiGo.transform, "Panel");
            Stretch(panelGo.GetComponent<RectTransform>());
            Image bg = GetOrAdd<Image>(panelGo);
            bg.color = new Color(0.03f, 0.03f, 0.05f, 1f);
            bg.raycastTarget = true; // 뒤쪽 UI 클릭 차단

            // 상단 디버그 IMGUI 박스(720p 기준 약 210px = 29%)를 피해 제목은 아래로
            TMP_Text title = MakeLabel(panelGo.transform, panelGo.transform, "Title", font, 96f, TextAlignmentOptions.Center,
                new Vector2(0f, 0.58f), new Vector2(1f, 0.70f), LocalizationBuilderUtil.Ko("gameover.title"));
            title.fontStyle = FontStyles.Bold;
            // 제목 / 통계 라벨은 GameOverUI 가 키로 채운다 (LocalizedText 를 붙이지 않음)
            LocalizationBuilderUtil.Unbind(title);

            Transform leftCard = MakeCard(panelGo.transform, "LeftCard", 0.07f, 0.48f);
            Transform rightCard = MakeCard(panelGo.transform, "RightCard", 0.52f, 0.93f);
            TMP_Text left = MakeCardLabel(panelGo.transform, leftCard, "LeftLabel", font, LocalizationBuilderUtil.Ko("gameover.left_header"));
            TMP_Text right = MakeCardLabel(panelGo.transform, rightCard, "RightLabel", font, LocalizationBuilderUtil.Ko("gameover.right_header"));
            LocalizationBuilderUtil.Unbind(left);
            LocalizationBuilderUtil.Unbind(right);

            // 무기별 데미지 아이콘 칸 (카드 아래쪽). 통계 텍스트는 그 위로 줄인다
            RectTransform leftList = MakeWeaponList(leftCard, left);
            RectTransform rightList = MakeWeaponList(rightCard, right);
            GameOverWeaponRow rowTemplate = MakeWeaponRowTemplate(panelGo.transform, font);
            RewardData[] weaponRewards = LoadWeaponRewards();

            Button restart = MakeButton(panelGo.transform, "RestartButton", font, string.Empty, 0.30f, 0.48f);
            Button toTitle = MakeButton(panelGo.transform, "TitleButton", font, string.Empty, 0.52f, 0.70f);
            LocalizationBuilderUtil.BindChildLabel(restart, "gameover.restart");
            LocalizationBuilderUtil.BindChildLabel(toTitle, "gameover.to_title");

            // 에디터에서 패널은 꺼 둔다 (런타임 Awake 에서도 끈다)
            panelGo.SetActive(false);

            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("panel").objectReferenceValue = panelGo;
            uiSo.FindProperty("titleLabel").objectReferenceValue = title;
            uiSo.FindProperty("leftLabel").objectReferenceValue = left;
            uiSo.FindProperty("rightLabel").objectReferenceValue = right;
            uiSo.FindProperty("restartButton").objectReferenceValue = restart;
            uiSo.FindProperty("titleButton").objectReferenceValue = toTitle;
            uiSo.FindProperty("leftWeaponList").objectReferenceValue = leftList;
            uiSo.FindProperty("rightWeaponList").objectReferenceValue = rightList;
            uiSo.FindProperty("weaponRowTemplate").objectReferenceValue = rowTemplate;
            uiSo.FindProperty("weaponValueFormatKey").stringValue = "gameover.weapon_value";
            SerializedProperty rewardsProp = uiSo.FindProperty("weaponRewards");
            rewardsProp.arraySize = weaponRewards.Length;
            for (int i = 0; i < weaponRewards.Length; i++)
            {
                rewardsProp.GetArrayElementAtIndex(i).objectReferenceValue = weaponRewards[i];
            }
            // 싱글 게임: 좌우는 플레이어가 아니라 화면 구분 (StringTable 키)
            uiSo.FindProperty("leftHeaderKey").stringValue = "gameover.left_header";
            uiSo.FindProperty("rightHeaderKey").stringValue = "gameover.right_header";
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. GameOverManager
            GameObject goMgrGo = FindOrCreateRoot(scene, "GameOverManager");
            GameOverManager goMgr = GetOrAdd<GameOverManager>(goMgrGo);
            var mgrSo = new SerializedObject(goMgr);
            mgrSo.FindProperty("statisticsManager").objectReferenceValue = stats;
            mgrSo.FindProperty("gameOverUI").objectReferenceValue = ui;
            mgrSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine("씬: " + SCENE_PATH);
            sb.AppendLine("StatisticsManager / GameOverManager / GameOverCanvas(order " + SORTING_ORDER + ")/GameOverUI/Panel/{Title,LeftLabel,RightLabel,WeaponList x2,WeaponRowTemplate,RestartButton,TitleButton}");
            sb.AppendLine("무기 보상(아이콘) " + weaponRewards.Length + "개 연결");
            return sb.ToString();
        }

        private static GameObject FindOrCreateRoot(Scene _scene, string _name)
        {
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (root.name == _name)
                {
                    return root;
                }
            }

            var go = new GameObject(_name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }

        private static Transform MakeCard(Transform _panel, string _name, float _xMin, float _xMax)
        {
            GameObject go = FindOrCreateChild(_panel, _name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(_xMin, 0.19f);
            rt.anchorMax = new Vector2(_xMax, 0.56f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            Image img = GetOrAdd<Image>(go);
            img.color = new Color(1f, 1f, 1f, 0.035f);
            img.raycastTarget = false;
            return go.transform;
        }

        /// <summary>
        /// 카드 아래 버튼 한 줄 (스프라이트는 ArtResourceBuilder 가 입힌다)
        /// </summary>
        private static Button MakeButton(Transform _panel, string _name, TMP_FontAsset _font, string _text, float _xMin, float _xMax)
        {
            GameObject go = FindOrCreateChild(_panel, _name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(_xMin, 0.05f);
            rt.anchorMax = new Vector2(_xMax, 0.14f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            Image img = GetOrAdd<Image>(go);
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            img.raycastTarget = true;
            Button button = GetOrAdd<Button>(go);
            button.targetGraphic = img;

            TMP_Text label = MakeLabel(go.transform, go.transform, "Label", _font, 40f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, _text);
            label.raycastTarget = false;
            return button;
        }

        private static TMP_Text MakeCardLabel(Transform _panel, Transform _card, string _name, TMP_FontAsset _font, string _text)
        {
            TMP_Text t = MakeLabel(_panel, _card, _name, _font, 36f, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, _text);
            RectTransform rt = t.rectTransform;
            rt.offsetMin = new Vector2(40f, 30f);
            rt.offsetMax = new Vector2(-40f, -30f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 18f;
            t.fontSizeMax = 40f;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        /// <summary>
        /// 카드 아래쪽에 무기 칸을 담을 2열 그리드. _label 은 그 위로 줄인다
        /// </summary>
        private static RectTransform MakeWeaponList(Transform _card, TMP_Text _label)
        {
            GameObject go = FindOrCreateChild(_card, "WeaponList");
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(40f, WEAPON_LIST_BOTTOM);
            rt.offsetMax = new Vector2(-40f, WEAPON_LIST_BOTTOM + WEAPON_LIST_HEIGHT);
            rt.localScale = Vector3.one;

            GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(go);
            grid.cellSize = WEAPON_CELL;
            grid.spacing = WEAPON_SPACING;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            // 빌더 재실행 시 런타임에 남은 칸이 있으면 지운다 (에디터에서는 비어 있어야 함)
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            }

            if (null != _label)
            {
                RectTransform lrt = _label.rectTransform;
                lrt.offsetMin = new Vector2(lrt.offsetMin.x, WEAPON_LIST_BOTTOM + WEAPON_LIST_HEIGHT + 8f);
            }
            return rt;
        }

        /// <summary>
        /// 꺼 둔 무기 칸 템플릿: Icon(왼쪽 정사각) + Value(아이콘 오른쪽 글자)
        /// </summary>
        private static GameOverWeaponRow MakeWeaponRowTemplate(Transform _panel, TMP_FontAsset _font)
        {
            GameObject go = FindOrCreateChild(_panel, "WeaponRowTemplate");
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = WEAPON_CELL;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            GameObject iconGo = FindOrCreateChild(go.transform, "Icon");
            RectTransform irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.sizeDelta = new Vector2(WEAPON_CELL.y, WEAPON_CELL.y);
            irt.anchoredPosition = Vector2.zero;
            irt.localScale = Vector3.one;
            Image icon = GetOrAdd<Image>(iconGo);
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text value = MakeLabel(go.transform, go.transform, "Value", _font, 36f, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, Vector2.one, "0");
            value.rectTransform.offsetMin = new Vector2(WEAPON_CELL.y + 8f, 0f);
            // 아이콘이 없어 "이름 : 수치" 로 대체될 때 긴 이름도 들어가도록 줄인다
            value.enableAutoSizing = true;
            value.fontSizeMin = 24f;
            value.fontSizeMax = 36f;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Ellipsis;
            LocalizationBuilderUtil.Unbind(value);

            GameOverWeaponRow row = GetOrAdd<GameOverWeaponRow>(go);
            var so = new SerializedObject(row);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("valueText").objectReferenceValue = value;
            so.FindProperty("textLeftWithIcon").floatValue = WEAPON_CELL.y + 8f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return row;
        }

        private static RewardData[] LoadWeaponRewards()
        {
            var list = new System.Collections.Generic.List<RewardData>();
            foreach (string guid in AssetDatabase.FindAssets("t:RewardData", new[] { WEAPON_REWARD_FOLDER }))
            {
                RewardData reward = AssetDatabase.LoadAssetAtPath<RewardData>(AssetDatabase.GUIDToAssetPath(guid));
                if (null != reward)
                {
                    list.Add(reward);
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        private static GameObject FindOrCreateChild(Transform _parent, string _name)
        {
            Transform t = _parent.Find(_name);
            GameObject go;
            if (null != t)
            {
                go = t.gameObject;
            }
            else
            {
                go = new GameObject(_name, typeof(RectTransform));
                go.transform.SetParent(_parent, false);
            }

            go.layer = _parent.gameObject.layer;
            EnsureRect(go);
            return go;
        }

        private static void EnsureRect(GameObject _go)
        {
            if (null == _go.GetComponent<RectTransform>())
            {
                _go.AddComponent<RectTransform>();
            }
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            T c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }

        private static void Stretch(RectTransform _rt)
        {
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
            _rt.localScale = Vector3.one;
        }

        private static TMP_Text MakeLabel(Transform _panel, Transform _parent, string _name, TMP_FontAsset _font, float _size,
            TextAlignmentOptions _align, Vector2 _anchorMin, Vector2 _anchorMax, string _text)
        {
            Transform existing = _panel.Find(_name);
            if (null == existing)
            {
                existing = _parent.Find(_name);
            }
            if (null != existing && existing.parent != _parent)
            {
                existing.SetParent(_parent, false);
            }
            GameObject go = FindOrCreateChild(_parent, _name);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = _anchorMin;
            rt.anchorMax = _anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            TextMeshProUGUI tmp = GetOrAdd<TextMeshProUGUI>(go);
            if (null != _font)
            {
                tmp.font = _font;
            }
            tmp.fontSize = _size;
            tmp.alignment = _align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.text = _text;
            return tmp;
        }
    }
}
