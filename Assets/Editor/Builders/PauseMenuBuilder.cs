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
    /// GameScene 에 ESC 일시정지 메뉴를 배치한다 (멱등: 기존 PauseCanvas 를 지우고 다시 만든다)
    /// - 루트 PauseCanvas (Screen Space Overlay, sortingOrder 90 → HUD/레벨업(0) 위, GameOverCanvas(100) 아래)
    ///   └ PauseMenu (항상 켜짐, PauseMenu 컴포넌트)
    ///      ├ Panel (반투명 검정, 꺼진 상태) ─ Window ─ Title "일시정지" / Buttons [계속하기, 설정, 타이틀로]
    ///      └ SettingsPanel (프리팹 인스턴스, 꺼진 상태)
    /// - GameOverUI 가 씬에 있으면 연결 (게임 오버 화면 중에는 열리지 않음)
    /// - EventSystem 이 없으면 만든다 (InputSystemUIInputModule)
    /// - 버튼 스프라이트 · 폰트 마감은 ArtResourceBuilder.Build() 를 다시 실행해 맞춘다.
    /// </summary>
    public static class PauseMenuBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string CANVAS_NAME = "PauseCanvas";
        private const int SORTING_ORDER = 90;
        private const float TITLE_SIZE = 72f;
        private const float BUTTON_TEXT_SIZE = 48f;
        private static readonly Vector2 WINDOW_SIZE = new Vector2(640f, 600f);
        private static readonly Vector2 BUTTON_SIZE = new Vector2(420f, 96f);

        [MenuItem("Tools/Builders/PauseMenu")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PauseMenuBuilder]");
            SettingsUIKit.ResetCache();

            // 프리팹 먼저 (씬을 연 뒤 만들면 임시 오브젝트가 씬에 섞일 수 있음)
            if (null == SettingsPanelBuilder.GetOrBuildPrefab())
            {
                return sb.AppendLine("오류: 설정 창 프리팹 생성 실패").ToString();
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            // 1. 기존 것 제거 후 Canvas
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (CANVAS_NAME == root.name)
                {
                    Object.DestroyImmediate(root);
                }
            }

            var canvasGo = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvasGo.layer = SettingsUIKit.UI_LAYER;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 2. PauseMenu (항상 켜진 루트)
            RectTransform menuRt = SettingsUIKit.MakeRect("PauseMenu", canvasGo.transform);
            SettingsUIKit.Stretch(menuRt);
            PauseMenu menu = menuRt.gameObject.AddComponent<PauseMenu>();

            Image panel = SettingsUIKit.MakeImage("Panel", menuRt, SettingsUIKit.DIM_COLOR, true);
            SettingsUIKit.Stretch(panel.rectTransform);

            Image window = SettingsUIKit.MakeImage("Window", panel.transform, SettingsUIKit.WINDOW_COLOR, true);
            window.sprite = SettingsUIKit.BuiltinSprite("UISprite.psd");
            window.type = Image.Type.Sliced;
            SettingsUIKit.Slice(window, "PanelDark", Color.white);
            SettingsUIKit.Center(window.rectTransform, WINDOW_SIZE, Vector2.zero);

            LocalizationBuilderUtil.Reload();
            TextMeshProUGUI title = SettingsUIKit.MakeLabel("Title", window.transform, string.Empty, TITLE_SIZE, TextAlignmentOptions.Center);
            LocalizationBuilderUtil.Bind(title, "ui.pause.title");
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 120f);
            titleRt.anchoredPosition = new Vector2(0f, -32f);

            RectTransform buttons = SettingsUIKit.MakeRect("Buttons", window.transform);
            buttons.anchorMin = new Vector2(0f, 0f);
            buttons.anchorMax = new Vector2(1f, 1f);
            buttons.offsetMin = new Vector2(40f, 48f);
            buttons.offsetMax = new Vector2(-40f, -176f);
            SettingsUIKit.AddVertical(buttons.gameObject, 24f, TextAnchor.UpperCenter);

            Button resume = SettingsUIKit.MakeButton("ResumeButton", buttons, string.Empty, BUTTON_SIZE.x, BUTTON_SIZE.y, BUTTON_TEXT_SIZE);
            Button settings = SettingsUIKit.MakeButton("SettingsButton", buttons, string.Empty, BUTTON_SIZE.x, BUTTON_SIZE.y, BUTTON_TEXT_SIZE);
            Button toTitle = SettingsUIKit.MakeButton("TitleButton", buttons, string.Empty, BUTTON_SIZE.x, BUTTON_SIZE.y, BUTTON_TEXT_SIZE);
            LocalizationBuilderUtil.BindChildLabel(resume, "ui.pause.resume");
            LocalizationBuilderUtil.BindChildLabel(settings, "ui.pause.settings");
            LocalizationBuilderUtil.BindChildLabel(toTitle, "ui.pause.to_title");

            SettingsUIKit.StyleButton(resume);
            SettingsUIKit.StyleButton(settings);
            SettingsUIKit.StyleButton(toTitle);

            panel.gameObject.SetActive(false);

            // 3. 설정 창 (일시정지 패널 위에 그려지도록 뒤 순서)
            SettingsPanel settingsPanel = SettingsPanelBuilder.InstantiateInto(menuRt);
            if (null == settingsPanel)
            {
                sb.AppendLine("경고: 설정 창 인스턴스 생성 실패");
            }

            // 4. 연결
            GameOverUI gameOverUI = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
            var so = new SerializedObject(menu);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("titleButton").objectReferenceValue = toTitle;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("gameOverUI").objectReferenceValue = gameOverUI;
            so.FindProperty("titleSceneName").stringValue = "TitleScene";
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("GameOverUI 연결: " + (null != gameOverUI ? "예" : "없음 (GameOverBuilder 먼저 실행 권장)"));

            // 5. EventSystem
            if (null == Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include))
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(es, scene);
                sb.AppendLine("EventSystem 생성");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine("씬: " + SCENE_PATH);
            sb.AppendLine(CANVAS_NAME + "(order " + SORTING_ORDER + ")/PauseMenu/{Panel/Window/{Title,Buttons/{ResumeButton,SettingsButton,TitleButton}}, SettingsPanel}");
            return sb.ToString();
        }
    }
}
