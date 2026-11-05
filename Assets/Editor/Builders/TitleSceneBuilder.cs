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
    /// Assets/Scenes/TitleScene.unity 를 새로 만든다 (멱등, 매번 빈 씬으로 재생성)
    /// - Main Camera, EventSystem, Canvas(배경 / "2CTRL" 로고 / 버튼 목록) + TitleMenu
    /// - 버튼은 당장 "게임 시작" 하나 (MenuButtons 세로 레이아웃에 추가하면 설정 등 확장 가능)
    /// - Build Settings: TitleScene(0), DimensionSelectScene(1), GameScene(2) (DimensionSelectSceneBuilder.RegisterBuildScenes)
    /// - 확인용으로 .claude/tmp/titlescene.png 캡처
    /// </summary>
    public static class TitleSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/TitleScene.unity";
        private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";
        // 임시 배경
        private const string BACKGROUND_PATH = "Assets/Resources/Textuer/fbg.png";
        private const string FONT_PATH = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
        private const string CAPTURE_PATH = ".claude/tmp/titlescene.png";
        // TitleCanvas(0) 위
        private const int SETTINGS_SORTING_ORDER = 50;

        private static TMP_FontAsset font;

        [MenuItem("Tools/Builders/TitleScene")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[TitleSceneBuilder]");

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (null == font)
            {
                sb.AppendLine("경고: 폰트 없음 " + FONT_PATH);
            }

            // 설정 창 프리팹은 새 씬을 열기 전에 준비 (빌드 중 임시 오브젝트가 새 씬에 남지 않게)
            SettingsPanelBuilder.GetOrBuildPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            System.Type urpCam = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (null != urpCam)
            {
                camGo.AddComponent(urpCam);
            }
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // 2. EventSystem
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 3. Canvas
            var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 배경 (스프라이트 임포트 여부와 무관하게 쓰도록 RawImage)
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            RectTransform bgRt = Stretch(bgGo, canvasGo.transform);
            var bg = bgGo.GetComponent<RawImage>();
            bg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BACKGROUND_PATH);
            bg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            bg.raycastTarget = false;
            sb.AppendLine("배경: " + (null != bg.texture ? BACKGROUND_PATH : "없음"));

            // 로고
            var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(TextMeshProUGUI));
            logoGo.layer = canvasGo.layer;
            var logoRt = logoGo.GetComponent<RectTransform>();
            logoRt.SetParent(canvasGo.transform, false);
            logoRt.anchorMin = logoRt.anchorMax = new Vector2(0.5f, 0.5f);
            logoRt.anchoredPosition = new Vector2(0f, 150f);
            logoRt.sizeDelta = new Vector2(1200f, 300f);
            var logo = logoGo.GetComponent<TextMeshProUGUI>();
            SetupText(logo, "2CTRL", 200f);
            logo.fontStyle = FontStyles.Bold;

            // 버튼 목록
            var menuGo = new GameObject("MenuButtons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            menuGo.layer = canvasGo.layer;
            var menuRt = menuGo.GetComponent<RectTransform>();
            menuRt.SetParent(canvasGo.transform, false);
            menuRt.anchorMin = menuRt.anchorMax = new Vector2(0.5f, 0.5f);
            menuRt.pivot = new Vector2(0.5f, 1f);
            menuRt.anchoredPosition = new Vector2(0f, -80f);
            menuRt.sizeDelta = new Vector2(400f, 300f);
            var vlg = menuGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            LocalizationBuilderUtil.Reload();
            Button startButton = CreateButton("StartButton", menuRt, string.Empty, 360f, 80f);
            Button settingsButton = CreateButton("SettingsButton", menuRt, string.Empty, 360f, 80f);
            LocalizationBuilderUtil.BindChildLabel(startButton, "ui.title.start");
            LocalizationBuilderUtil.BindChildLabel(settingsButton, "ui.title.settings");

            // 설정 창 : ArtResourceBuilder 가 TitleCanvas 에 이미지를 뒤에 추가해도 가리지 않도록 별도 캔버스(위 순서)
            var settingsCanvasGo = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            settingsCanvasGo.layer = canvasGo.layer;
            var settingsCanvas = settingsCanvasGo.GetComponent<Canvas>();
            settingsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            settingsCanvas.sortingOrder = SETTINGS_SORTING_ORDER;
            var settingsScaler = settingsCanvasGo.GetComponent<CanvasScaler>();
            settingsScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            settingsScaler.referenceResolution = new Vector2(1920f, 1080f);
            settingsScaler.matchWidthOrHeight = 0.5f;
            SettingsPanel settingsPanel = SettingsPanelBuilder.InstantiateInto(settingsCanvasGo.transform);
            sb.AppendLine("설정 창: " + (null != settingsPanel ? SettingsPanelBuilder.PREFAB_PATH : "생성 실패"));

            // 4. TitleMenu
            var menu = canvasGo.AddComponent<TitleMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("gameSceneName").stringValue = "DimensionSelectScene";
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            sb.AppendLine("저장: " + SCENE_PATH);

            sb.AppendLine(RegisterBuildScenes());
            sb.AppendLine(Capture(cam, canvasGo.GetComponent<Canvas>()));

            sb.AppendLine("루트: " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name)));
            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        /// <summary>
        /// Title(0) / DimensionSelect(1) / Game(2) 순서는 차원 선택 빌더와 같게 맞춘다
        /// </summary>
        private static string RegisterBuildScenes()
        {
            return DimensionSelectSceneBuilder.RegisterBuildScenes();
        }

        private static RectTransform Stretch(GameObject _go, Transform _parent)
        {
            _go.layer = _parent.gameObject.layer;
            var rt = _go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Button CreateButton(string _name, Transform _parent, string _label, float _width, float _height)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = _width;
            le.preferredHeight = _height;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            Stretch(labelGo, go.transform);
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            SetupText(tmp, _label, 40f);
            tmp.raycastTarget = false;

            return go.GetComponent<Button>();
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

        /// <summary>
        /// Overlay 캔버스는 카메라 렌더에 안 잡히므로 캡처 동안만 ScreenSpaceCamera 로 바꾼다 (저장 후라 씬에는 영향 없음)
        /// </summary>
        private static string Capture(Camera _cam, Canvas _canvas)
        {
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24);
            try
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _cam;
                _canvas.planeDistance = 5f;
                _cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                _cam.Render();

                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                string full = Path.GetFullPath(CAPTURE_PATH);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return "캡처: " + CAPTURE_PATH;
            }
            catch (System.Exception e)
            {
                return "캡처 실패: " + e.Message;
            }
            finally
            {
                _cam.targetTexture = null;
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.worldCamera = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
