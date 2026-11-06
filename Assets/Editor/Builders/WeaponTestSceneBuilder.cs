using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// Assets/Scenes/WeaponTest.unity 를 무기 테스트 씬으로 구성한다 (멱등)
    /// - 씬에 놓인 Dummy 인스턴스 삭제 (컨트롤러가 런타임에 생성)
    /// - Player 인스턴스 (0,0), Main Camera 정사영, Canvas + EventSystem, WeaponTestController 참조 연결
    /// - 이름으로 찾아 교체하므로 재실행해도 중복되지 않는다
    /// - 확인용으로 더미를 임시 배치해 메인 카메라를 .claude/tmp/weapontest_scene.png 로 렌더한 뒤 지운다
    /// </summary>
    public static class WeaponTestSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/WeaponTest.unity";
        private const string PLAYER_PREFAB = "Assets/prefab/Player/Player.prefab";
        private const string DUMMY_PREFAB = "Assets/prefab/Monster/Dummy/Dummy.prefab";
        private const string EFFECT_DIR = "Assets/prefab/Upgrade/Effects";
        private const string PASSIVE_BUFF_DIR = "Assets/prefab/Upgrade/PassiveBuff";
        private const string PASSIVE_NUFF_DIR = "Assets/prefab/Upgrade/PassiveNuff";
        // 무기 테스트와 무관한 몬스터 수 증감 패시브는 제외
        private static readonly string[] EXCLUDED_PASSIVES = { "MonsterDecrease", "MonsterIncreaseNuff" };
        private const string FONT_PATH = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
        private const string CAPTURE_PATH = ".claude/tmp/weapontest_scene.png";

        private const string CANVAS_NAME = "WeaponTestCanvas";
        private const string EVENT_SYSTEM_NAME = "EventSystem";
        private const string CONTROLLER_NAME = "WeaponTestController";
        private const string PLAYER_NAME = "Player";
        private const string CAMERA_NAME = "Main Camera";

        private const float CAMERA_SIZE = 8f;

        private static TMP_FontAsset font;

        [MenuItem("Tools/Builders/WeaponTestScene")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[WeaponTestSceneBuilder]");

            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            sb.AppendLine("기존 루트: " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name)));

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (null == font)
            {
                sb.AppendLine("경고: 폰트 없음 " + FONT_PATH);
            }

            GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DUMMY_PREFAB);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (null == dummyPrefab || null == playerPrefab)
            {
                return sb.AppendLine("에러: Dummy/Player 프리팹 없음").ToString();
            }

            // 1. 기존 Dummy 인스턴스, Player 인스턴스, 빌더 생성물 삭제
            int removedDummies = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                bool isDummy = (source == dummyPrefab);
                bool isPlayer = (source == playerPrefab) || (root.name == PLAYER_NAME && null != root.GetComponent<PlayerStats>());
                bool isOwned = root.name == CANVAS_NAME || root.name == CONTROLLER_NAME || null != root.GetComponent<EventSystem>();
                if (true == isDummy)
                {
                    removedDummies++;
                }
                if (true == isDummy || true == isPlayer || true == isOwned)
                {
                    Object.DestroyImmediate(root);
                }
            }
            sb.AppendLine($"Dummy 인스턴스 삭제: {removedDummies}");

            // 2. Player
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = PLAYER_NAME;
            player.transform.position = Vector3.zero;
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (null == playerStats)
            {
                playerStats = player.GetComponentInChildren<PlayerStats>(true);
            }
            sb.AppendLine("Player PlayerStats: " + (null != playerStats ? "OK" : "없음"));

            // 3. Camera
            Camera cam = SetupCamera(scene, sb);

            // 4. EventSystem
            var es = new GameObject(EVENT_SYSTEM_NAME, typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(es, scene);

            // 5. Canvas + UI
            UIRefs ui = BuildCanvas(scene);

            // 6. Controller
            var controllerGo = new GameObject(CONTROLLER_NAME);
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            var controller = controllerGo.AddComponent<WeaponTestController>();

            List<WeaponRewardEffect> effects = LoadWeaponEffects();
            sb.AppendLine($"weaponEffects {effects.Count}: " + string.Join(", ", effects.Select(e => e.name)));

            var so = new SerializedObject(controller);
            so.FindProperty("player").objectReferenceValue = playerStats;
            so.FindProperty("dummyPrefab").objectReferenceValue = dummyPrefab;
            SerializedProperty arr = so.FindProperty("weaponEffects");
            arr.arraySize = effects.Count;
            for (int i = 0; i < effects.Count; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
            }
            SerializedProperty fontProp = so.FindProperty("meterSettings.font");
            if (null != fontProp)
            {
                fontProp.objectReferenceValue = font;
            }
            so.FindProperty("weaponButtonTemplate").objectReferenceValue = ui.weaponTemplate;
            so.FindProperty("weaponButtonContainer").objectReferenceValue = ui.weaponContainer;
            so.FindProperty("layoutButtonTemplate").objectReferenceValue = ui.layoutTemplate;
            so.FindProperty("layoutButtonContainer").objectReferenceValue = ui.layoutContainer;
            so.FindProperty("upgradeOnceButton").objectReferenceValue = ui.upgradeOnce;
            so.FindProperty("upgradeMaxButton").objectReferenceValue = ui.upgradeMax;
            so.FindProperty("resetButton").objectReferenceValue = ui.reset;
            so.FindProperty("levelText").objectReferenceValue = ui.levelText;
            so.FindProperty("weaponNameText").objectReferenceValue = ui.weaponNameText;

            List<RewardData> passives = LoadPassives();
            SerializedProperty passiveArr = so.FindProperty("passives");
            passiveArr.arraySize = passives.Count;
            for (int i = 0; i < passives.Count; i++)
            {
                passiveArr.GetArrayElementAtIndex(i).objectReferenceValue = passives[i];
            }
            so.FindProperty("passiveRowTemplate").objectReferenceValue = ui.passiveRowTemplate;
            so.FindProperty("passiveRowContainer").objectReferenceValue = ui.passiveRowContainer;
            so.FindProperty("passiveResetButton").objectReferenceValue = ui.passiveReset;
            so.FindProperty("statSummaryText").objectReferenceValue = ui.statSummaryText;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine($"passives {passives.Count}: " + string.Join(", ", passives.Select(p => p.name)));
            sb.AppendLine("meterSettings.font: " + (null != fontProp ? "연결" : "필드 없음"));

            // 참조 검증
            var check = new SerializedObject(controller);
            string[] refs = { "player", "dummyPrefab", "weaponButtonTemplate", "weaponButtonContainer", "layoutButtonTemplate",
                "layoutButtonContainer", "upgradeOnceButton", "upgradeMaxButton", "resetButton", "levelText", "weaponNameText", "meterSettings.font",
                "passiveRowTemplate", "passiveRowContainer", "passiveResetButton", "statSummaryText" };
            var missing = refs.Where(r => null == check.FindProperty(r) || null == check.FindProperty(r).objectReferenceValue).ToList();
            SerializedProperty checkPassives = check.FindProperty("passives");
            if (null == checkPassives || 0 == checkPassives.arraySize)
            {
                missing.Add("passives");
            }
            sb.AppendLine("누락 참조: " + (0 == missing.Count ? "없음" : string.Join(", ", missing)));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 7. 확인 캡처 (저장 후 임시 더미 배치 → 렌더 → 삭제, 씬은 다시 저장하지 않음)
            sb.AppendLine(Capture(scene, cam, dummyPrefab));

            sb.AppendLine("최종 루트: " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name)));
            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        private static Camera SetupCamera(Scene _scene, StringBuilder _sb)
        {
            Camera cam = _scene.GetRootGameObjects()
                .Select(g => g.GetComponentInChildren<Camera>(true))
                .FirstOrDefault(c => null != c && (c.CompareTag("MainCamera") || c.name == CAMERA_NAME));
            if (null == cam)
            {
                var go = new GameObject(CAMERA_NAME, typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(go, _scene);
                go.tag = "MainCamera";
                cam = go.GetComponent<Camera>();
                // URP 추가 데이터 (Game.Editor 가 URP 를 참조하지 않으므로 이름으로 부착)
                System.Type t = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (null != t)
                {
                    go.AddComponent(t);
                }
                _sb.AppendLine("Main Camera 생성");
            }

            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.orthographic = true;
            cam.orthographicSize = CAMERA_SIZE;
            cam.cullingMask = ~0;

            string[] layers = { "LMonster", "LAttack", "RMonster", "RAttack" };
            _sb.AppendLine("카메라 ortho " + CAMERA_SIZE + ", culling Everything, 레이어 " +
                string.Join(", ", layers.Select(l => $"{l}={LayerMask.NameToLayer(l)}")));
            return cam;
        }

        private struct UIRefs
        {
            public Button weaponTemplate;
            public Transform weaponContainer;
            public Button layoutTemplate;
            public Transform layoutContainer;
            public Button upgradeOnce;
            public Button upgradeMax;
            public Button reset;
            public TMP_Text levelText;
            public TMP_Text weaponNameText;
            public WeaponTestPassiveRow passiveRowTemplate;
            public Transform passiveRowContainer;
            public Button passiveReset;
            public TMP_Text statSummaryText;
        }

        private static UIRefs BuildCanvas(Scene _scene)
        {
            var refs = new UIRefs();

            var canvasGo = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, _scene);
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 좌측 무기 리스트
            RectTransform weaponPanel = CreatePanel("WeaponListPanel", canvasGo.transform,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(260f, -40f));
            var vlg = weaponPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vlg);
            refs.weaponContainer = weaponPanel;
            refs.weaponTemplate = CreateButton("WeaponButton", weaponPanel, "무기", 240f, 48f);
            refs.weaponTemplate.gameObject.SetActive(false);

            // 상단 배치 모드
            RectTransform layoutPanel = CreatePanel("LayoutPanel", canvasGo.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(900f, 70f));
            var hlg = layoutPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(hlg);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            refs.layoutContainer = layoutPanel;
            refs.layoutTemplate = CreateButton("LayoutButton", layoutPanel, "배치", 160f, 50f);
            refs.layoutTemplate.gameObject.SetActive(false);

            // 우측 하단 조작 (우상단 원거리 더미와 겹치지 않게)
            RectTransform controlPanel = CreatePanel("ControlPanel", canvasGo.transform,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 20f), new Vector2(300f, 380f));
            var cvlg = controlPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(cvlg);
            refs.weaponNameText = CreateText("WeaponNameText", controlPanel, "-", 32f, 60f);
            refs.levelText = CreateText("LevelText", controlPanel, "Lv -", 28f, 50f);
            refs.upgradeOnce = CreateButton("UpgradeOnceButton", controlPanel, "강화 +1", 280f, 56f);
            refs.upgradeMax = CreateButton("UpgradeMaxButton", controlPanel, "최대 강화", 280f, 56f);
            refs.reset = CreateButton("ResetButton", controlPanel, "초기화", 280f, 56f);

            // 우측 상단 패시브 (LayoutPanel 오른쪽, ControlPanel 위)
            BuildPassivePanel(canvasGo.transform, ref refs);

            return refs;
        }

        private const float PASSIVE_ROW_HEIGHT = 40f;
        private const float PASSIVE_PANEL_WIDTH = 360f;

        private static void BuildPassivePanel(Transform _canvas, ref UIRefs _refs)
        {
            RectTransform panel = CreatePanel("PassivePanel", _canvas,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(PASSIVE_PANEL_WIDTH, 640f));
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vlg);
            vlg.childForceExpandWidth = true;

            // 스크롤 영역 (15행이 다 안 들어가므로 ScrollRect)
            var scrollGo = new GameObject("PassiveScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.layer = panel.gameObject.layer;
            scrollGo.transform.SetParent(panel, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
            var scrollLe = scrollGo.GetComponent<LayoutElement>();
            scrollLe.preferredHeight = 322f;
            scrollLe.flexibleHeight = 1f;

            const float SCROLLBAR_WIDTH = 14f;
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.layer = scrollGo.layer;
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 휠 스크롤 레이캐스트용
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(scrollGo.transform, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-SCROLLBAR_WIDTH - 2f, 0f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.layer = scrollGo.layer;
            var content = contentGo.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var cvlg = contentGo.GetComponent<VerticalLayoutGroup>();
            ConfigureLayout(cvlg);
            cvlg.padding = new RectOffset(4, 4, 4, 4);
            cvlg.spacing = 4f;
            cvlg.childForceExpandWidth = true;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            Scrollbar scrollbar = CreateVerticalScrollbar(scrollGo.transform, SCROLLBAR_WIDTH);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            _refs.passiveRowContainer = content;
            _refs.passiveRowTemplate = CreatePassiveRow(content);
            _refs.passiveRowTemplate.gameObject.SetActive(false);

            _refs.passiveReset = CreateButton("PassiveResetButton", panel, "패시브 초기화", PASSIVE_PANEL_WIDTH - 20f, 44f);

            // 스탯 요약 8줄
            TMP_Text summary = CreateText("StatSummaryText", panel, "", 18f, 230f);
            summary.GetComponent<LayoutElement>().preferredWidth = PASSIVE_PANEL_WIDTH - 20f;
            summary.alignment = TextAlignmentOptions.TopLeft;
            summary.textWrappingMode = TextWrappingModes.NoWrap;
            summary.overflowMode = TextOverflowModes.Truncate;
            _refs.statSummaryText = summary;
        }

        private static WeaponTestPassiveRow CreatePassiveRow(Transform _parent)
        {
            var go = new GameObject("PassiveRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(WeaponTestPassiveRow));
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = PASSIVE_ROW_HEIGHT;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            TMP_Text nameText = CreateText("NameText", go.transform, "패시브", 18f, PASSIVE_ROW_HEIGHT);
            var nameLe = nameText.GetComponent<LayoutElement>();
            nameLe.preferredWidth = 160f;
            nameLe.flexibleWidth = 1f;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 12f;
            nameText.fontSizeMax = 18f;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            Button minus = CreateButton("MinusButton", go.transform, "-", PASSIVE_ROW_HEIGHT, PASSIVE_ROW_HEIGHT);
            TMP_Text stackText = CreateText("StackText", go.transform, "x0", 18f, PASSIVE_ROW_HEIGHT);
            stackText.GetComponent<LayoutElement>().preferredWidth = 50f;
            Button plus = CreateButton("PlusButton", go.transform, "+", PASSIVE_ROW_HEIGHT, PASSIVE_ROW_HEIGHT);

            var row = go.GetComponent<WeaponTestPassiveRow>();
            var so = new SerializedObject(row);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("stackText").objectReferenceValue = stackText;
            so.FindProperty("minusButton").objectReferenceValue = minus;
            so.FindProperty("plusButton").objectReferenceValue = plus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return row;
        }

        private static Scrollbar CreateVerticalScrollbar(Transform _parent, float _width)
        {
            var go = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            go.layer = _parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(_width, 0f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);

            var areaGo = new GameObject("SlidingArea", typeof(RectTransform));
            areaGo.layer = go.layer;
            var area = areaGo.GetComponent<RectTransform>();
            area.SetParent(rt, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.layer = go.layer;
            var handle = handleGo.GetComponent<RectTransform>();
            handle.SetParent(area, false);
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            handleGo.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.65f, 1f);

            var bar = go.GetComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleGo.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            return bar;
        }

        private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup _group)
        {
            _group.padding = new RectOffset(10, 10, 10, 10);
            _group.spacing = 8f;
            _group.childAlignment = TextAnchor.UpperCenter;
            _group.childControlWidth = true;
            _group.childControlHeight = true;
            _group.childForceExpandWidth = false;
            _group.childForceExpandHeight = false;
        }

        private static RectTransform CreatePanel(string _name, Transform _parent, Vector2 _anchorMin, Vector2 _anchorMax,
            Vector2 _pivot, Vector2 _position, Vector2 _size)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
            go.layer = _parent.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            rt.anchorMin = _anchorMin;
            rt.anchorMax = _anchorMax;
            rt.pivot = _pivot;
            rt.anchoredPosition = _position;
            rt.sizeDelta = _size;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
            return rt;
        }

        private static Button CreateButton(string _name, Transform _parent, string _label, float _width, float _height)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = _width;
            le.preferredHeight = _height;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.layer = go.layer;
            var rt = labelGo.GetComponent<RectTransform>();
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            SetupText(tmp, _label, 24f);
            tmp.raycastTarget = false;

            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateText(string _name, Transform _parent, string _text, float _fontSize, float _height)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 280f;
            le.preferredHeight = _height;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            SetupText(tmp, _text, _fontSize);
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void SetupText(TextMeshProUGUI _tmp, string _text, float _size)
        {
            if (null != font)
            {
                _tmp.font = font;
            }
            _tmp.text = _text;
            _tmp.fontSize = _size;
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.color = Color.white;
        }

        private static List<WeaponRewardEffect> LoadWeaponEffects()
        {
            return AssetDatabase.FindAssets("t:ScriptableObject", new[] { EFFECT_DIR })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .OfType<WeaponRewardEffect>()
                .OrderBy(e => e.name)
                .ToList();
        }

        private static List<RewardData> LoadPassives()
        {
            var result = new List<RewardData>();
            foreach (string dir in new[] { PASSIVE_BUFF_DIR, PASSIVE_NUFF_DIR })
            {
                result.AddRange(AssetDatabase.FindAssets("t:ScriptableObject", new[] { dir })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                    .OfType<RewardData>()
                    .Where(r => false == EXCLUDED_PASSIVES.Contains(r.name))
                    .OrderBy(r => r.name));
            }
            return result;
        }

        private static string Capture(Scene _scene, Camera _cam, GameObject _dummyPrefab)
        {
            var temp = new List<GameObject>();
            try
            {
                // 가장 넓게 퍼진 배치 프리셋으로 임시 더미 배치
                List<WeaponTestDummyLayout> layouts = WeaponTestDummyLayout.CreateDefaults();
                WeaponTestDummyLayout widest = layouts
                    .OrderByDescending(l => null == l.offsets || 0 == l.offsets.Length ? 0f : l.offsets.Max(o => o.magnitude))
                    .FirstOrDefault();
                if (null != widest && null != widest.offsets)
                {
                    foreach (Vector2 o in widest.offsets)
                    {
                        var d = (GameObject)PrefabUtility.InstantiatePrefab(_dummyPrefab, _scene);
                        d.transform.position = new Vector3(o.x, o.y, 0f);
                        temp.Add(d);
                    }
                }

                const int W = 1920, H = 1080;
                var rt = new RenderTexture(W, H, 24);
                RenderTexture prevTarget = _cam.targetTexture;
                _cam.targetTexture = rt;
                _cam.Render();
                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                _cam.targetTexture = prevTarget;

                string full = Path.GetFullPath(CAPTURE_PATH);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                return $"캡처: {CAPTURE_PATH} (임시 더미 {temp.Count}개, 프리셋 '{widest?.displayName}')";
            }
            catch (System.Exception e)
            {
                return "캡처 실패: " + e.Message;
            }
            finally
            {
                foreach (GameObject d in temp)
                {
                    if (null != d)
                    {
                        Object.DestroyImmediate(d);
                    }
                }
            }
        }
    }
}
