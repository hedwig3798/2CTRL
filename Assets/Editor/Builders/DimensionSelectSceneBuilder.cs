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
    /// Assets/Scenes/DimensionSelectScene.unity 를 새로 만든다 (멱등, 매번 빈 씬으로 재생성)
    /// - Main Camera, EventSystem(InputSystemUIInputModule), Canvas
    ///   [좌·우 큰 카드(LeftCard / RightCard, DimensionSelectView: 테마 배경 / 미리보기 / 이름 / 확정 표시 / 잠금 표시),
    ///    중앙 격자(DimensionGrid, GridLayoutGroup 고정 열: 차원 수만큼 Cell_0… DimensionSelectView + DimensionGridCell),
    ///    하단 키 안내] + DimensionSelectController (dimensions / columns / gridCells 연결)
    /// - 칸 자식: LeftCursorFrame / RightCursorFrame (시작 비활성) / CellCard[Preview, Name] / LockedMark (시작 비활성)
    /// - 레이아웃은 기능 확인 수준 (연출은 visual-designer)
    /// - TitleScene 의 TitleMenu.gameSceneName 을 이 씬으로, Build Settings 를 Title(0) / DimensionSelect(1) / Game(2) 로 맞춘다
    /// - 확인용으로 .claude/tmp/dimensionselect.png 캡처
    /// </summary>
    public static class DimensionSelectSceneBuilder
    {
        public const string SCENE_PATH = "Assets/Scenes/DimensionSelectScene.unity";
        private const string TITLE_SCENE_PATH = "Assets/Scenes/TitleScene.unity";
        private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string FONT_PATH = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
        private const string CAPTURE_PATH = ".claude/tmp/dimensionselect.png";
        private static readonly string[] DIMENSIONS = { "Forest", "Factory", "Storm" };

        private const int COLUMNS = 3;
        private const float LEFT_ANCHOR_X = 0.14f;
        private const float RIGHT_ANCHOR_X = 0.86f;
        private const float CENTER_ANCHOR_Y = 0.55f;
        private static readonly Vector2 CARD_SIZE = new Vector2(400f, 600f);
        private static readonly Vector2 CARD_PREVIEW_SIZE = new Vector2(320f, 320f);
        private const float CARD_NAME_FONT = 56f;
        private const float CARD_NAME_WIDTH = 370f;
        private static readonly Vector2 GRID_SIZE = new Vector2(920f, 700f);
        private static readonly Vector2 CELL_SIZE = new Vector2(260f, 300f);
        private static readonly Vector2 CELL_SPACING = new Vector2(40f, 60f);
        private static readonly Vector2 CELL_PREVIEW_SIZE = new Vector2(200f, 200f);
        private const float CELL_NAME_FONT = 40f;
        private const float LEFT_FRAME_GROW = 40f;
        private const float RIGHT_FRAME_GROW = 20f;
        private static readonly Color LEFT_FRAME_COLOR = new Color(0.3f, 0.85f, 1f, 1f);
        private static readonly Color RIGHT_FRAME_COLOR = new Color(1f, 0.35f, 0.55f, 1f);

        private static TMP_FontAsset font;

        [MenuItem("Tools/Builders/DimensionSelectScene")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[DimensionSelectSceneBuilder]");

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (null == font)
            {
                sb.AppendLine("경고: 폰트 없음 " + FONT_PATH);
            }

            DimensionData[] dims = DIMENSIONS
                .Select(d => AssetDatabase.LoadAssetAtPath<DimensionData>(DimensionDataBuilder.DATA_DIR + "/" + d + ".asset"))
                .Where(d => null != d).ToArray();
            if (dims.Length < 2)
            {
                return sb.AppendLine("오류: DimensionData 부족 (" + dims.Length + "). DimensionDataBuilder 를 먼저 실행").ToString();
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 새 씬을 열면 쓰지 않는 에셋이 내려가므로 다시 불러온다
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            dims = DIMENSIONS
                .Select(d => AssetDatabase.LoadAssetAtPath<DimensionData>(DimensionDataBuilder.DATA_DIR + "/" + d + ".asset"))
                .Where(d => null != d).ToArray();

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
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);

            // 2. EventSystem
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 3. Canvas
            var canvasGo = new GameObject("DimensionSelectCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            DimensionSelectView leftView = CreateCard("LeftCard", canvasGo.transform, LEFT_ANCHOR_X, CARD_SIZE, CARD_PREVIEW_SIZE, CARD_NAME_FONT, CARD_NAME_WIDTH);
            DimensionSelectView rightView = CreateCard("RightCard", canvasGo.transform, RIGHT_ANCHOR_X, CARD_SIZE, CARD_PREVIEW_SIZE, CARD_NAME_FONT, CARD_NAME_WIDTH);
            RectTransform gridRt = CreateGrid(canvasGo.transform);
            var cells = new DimensionGridCell[dims.Length];
            for (int i = 0; i < dims.Length; i++)
            {
                cells[i] = CreateCell(i, gridRt);
            }

            // 하단 키 안내 (조작 키만)
            CreateKeyHint("LeftKeyHint", canvasGo.transform, LEFT_ANCHOR_X, "W A S D   ·   Space");
            CreateKeyHint("RightKeyHint", canvasGo.transform, RIGHT_ANCHOR_X, "↑ ← ↓ →   ·   Enter");

            // 4. Controller
            var controller = canvasGo.AddComponent<DimensionSelectController>();
            var so = new SerializedObject(controller);
            var arr = so.FindProperty("dimensions");
            arr.arraySize = dims.Length;
            for (int i = 0; i < dims.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = dims[i];
            }
            var cellArr = so.FindProperty("gridCells");
            cellArr.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                cellArr.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
            }
            so.FindProperty("columns").intValue = COLUMNS;
            so.FindProperty("gameSceneName").stringValue = "GameScene";
            so.FindProperty("leftView").objectReferenceValue = leftView;
            so.FindProperty("rightView").objectReferenceValue = rightView;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 캡처용 미리 표시 (실행 중에는 Controller 가 다시 그림)
            leftView.Show(dims[0], false, false);
            rightView.Show(dims[1], false, false);
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].GetComponent<DimensionSelectView>().Show(dims[i], false, false);
            }

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            sb.AppendLine("저장: " + SCENE_PATH + " 차원=" + string.Join(",", dims.Select(d => d.name)) + " 칸=" + cells.Length);

            // 커서 프레임은 시작 비활성·불투명으로 저장해야 하므로 저장 뒤에 켠다 (캡처 전용, 아래 씬 재로드로 사라짐)
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Show(dims[i], 0 == i, false, 1 == i, false);
            }

            // 칸의 위치·크기는 GridLayoutGroup 이 구동하는 값이라 씬에 저장되지 않으므로 캡처 전에 배치한다
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);

            sb.AppendLine(Capture(cam, canvasGo.GetComponent<Canvas>()));
            sb.AppendLine(RegisterBuildScenes());
            sb.AppendLine(SetTitleTarget());

            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        /// <summary>
        /// 카드 하나: 테마 배경(themeGraphic) / 미리보기 / 이름 / 확정 표시 / 잠금 표시
        /// </summary>
        private static DimensionSelectView CreateCard(string _name, Transform _parent, float _anchorX, Vector2 _size, Vector2 _previewSize, float _nameFontSize, float _nameWidth)
        {
            var cardGo = new GameObject(_name, typeof(RectTransform), typeof(Image));
            cardGo.layer = _parent.gameObject.layer;
            var rt = cardGo.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(_anchorX, CENTER_ANCHOR_Y);
            rt.sizeDelta = _size;
            var theme = cardGo.GetComponent<Image>();
            theme.color = Color.gray;
            theme.raycastTarget = false;

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            var prt = Child(previewGo, cardGo.transform);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.6f);
            prt.sizeDelta = _previewSize;
            var preview = previewGo.GetComponent<Image>();
            preview.preserveAspect = true;
            preview.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var nrt = Child(nameGo, cardGo.transform);
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.12f);
            nrt.sizeDelta = new Vector2(_nameWidth, 120f);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            SetupText(nameText, "", _nameFontSize);

            // 확정 표시: 카드 뒤의 노란 테두리 (플레이스홀더). 카드보다 먼저 그려야 하므로 카드의 형제로 둔다
            var confirmGo = new GameObject(_name + "_ConfirmedMark", typeof(RectTransform), typeof(Image));
            var crt = Child(confirmGo, _parent);
            crt.anchorMin = crt.anchorMax = rt.anchorMin;
            crt.sizeDelta = rt.sizeDelta + new Vector2(32f, 32f);
            confirmGo.transform.SetSiblingIndex(cardGo.transform.GetSiblingIndex());
            var cimg = confirmGo.GetComponent<Image>();
            cimg.color = new Color(1f, 0.9f, 0.3f, 1f);
            cimg.raycastTarget = false;
            confirmGo.SetActive(false);

            // 잠금 표시: 어두운 덮개 (상대가 확정한 차원)
            var lockGo = new GameObject("LockedMark", typeof(RectTransform), typeof(Image));
            var lrt = Child(lockGo, cardGo.transform);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var limg = lockGo.GetComponent<Image>();
            limg.color = new Color(0f, 0f, 0f, 0.7f);
            limg.raycastTarget = false;
            lockGo.SetActive(false);

            var view = cardGo.AddComponent<DimensionSelectView>();
            var so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("previewImage").objectReferenceValue = preview;
            so.FindProperty("themeGraphic").objectReferenceValue = theme;
            so.FindProperty("confirmedMark").objectReferenceValue = confirmGo;
            so.FindProperty("lockedMark").objectReferenceValue = lockGo;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>
        /// 중앙 격자. 열 수를 고정해 차원이 늘면 줄이 자동으로 늘어난다
        /// </summary>
        private static RectTransform CreateGrid(Transform _parent)
        {
            var go = new GameObject("DimensionGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            var rt = Child(go, _parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, CENTER_ANCHOR_Y);
            rt.sizeDelta = GRID_SIZE;
            var grid = go.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = COLUMNS;
            grid.cellSize = CELL_SIZE;
            grid.spacing = CELL_SPACING;
            grid.childAlignment = TextAnchor.MiddleCenter;
            return rt;
        }

        /// <summary>
        /// 격자 한 칸: 왼쪽·오른쪽 커서 프레임 / 카드(테마 배경, 미리보기, 이름) / 잠금 표시. 자식 순서가 곧 그리는 순서
        /// </summary>
        private static DimensionGridCell CreateCell(int _index, Transform _parent)
        {
            var cellGo = new GameObject("Cell_" + _index, typeof(RectTransform));
            Child(cellGo, _parent);

            Image leftFrame = CreateStretchImage("LeftCursorFrame", cellGo.transform, LEFT_FRAME_GROW, LEFT_FRAME_COLOR);
            leftFrame.gameObject.SetActive(false);
            Image rightFrame = CreateStretchImage("RightCursorFrame", cellGo.transform, RIGHT_FRAME_GROW, RIGHT_FRAME_COLOR);
            rightFrame.gameObject.SetActive(false);

            Image theme = CreateStretchImage("CellCard", cellGo.transform, 0f, Color.gray);

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            var prt = Child(previewGo, theme.transform);
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.6f);
            prt.sizeDelta = CELL_PREVIEW_SIZE;
            var preview = previewGo.GetComponent<Image>();
            preview.preserveAspect = true;
            preview.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var nrt = Child(nameGo, theme.transform);
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.13f);
            nrt.sizeDelta = new Vector2(CELL_SIZE.x - 20f, 70f);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            SetupText(nameText, "", CELL_NAME_FONT);

            Image locked = CreateStretchImage("LockedMark", cellGo.transform, 0f, new Color(0f, 0f, 0f, 0.7f));
            locked.gameObject.SetActive(false);

            var view = cellGo.AddComponent<DimensionSelectView>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("nameText").objectReferenceValue = nameText;
            viewSo.FindProperty("previewImage").objectReferenceValue = preview;
            viewSo.FindProperty("themeGraphic").objectReferenceValue = theme;
            viewSo.FindProperty("lockedMark").objectReferenceValue = locked.gameObject;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var cell = cellGo.AddComponent<DimensionGridCell>();
            var cellSo = new SerializedObject(cell);
            cellSo.FindProperty("view").objectReferenceValue = view;
            cellSo.FindProperty("leftFrame").objectReferenceValue = leftFrame;
            cellSo.FindProperty("rightFrame").objectReferenceValue = rightFrame;
            cellSo.ApplyModifiedPropertiesWithoutUndo();
            return cell;
        }

        /// <summary>
        /// 부모에 맞춰 늘어나는 Image. _grow 는 부모보다 커지는 전체 크기 (사방으로 절반씩)
        /// </summary>
        private static Image CreateStretchImage(string _name, Transform _parent, float _grow, Color _color)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
            var rt = Child(go, _parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float half = _grow * 0.5f;
            rt.offsetMin = new Vector2(-half, -half);
            rt.offsetMax = new Vector2(half, half);
            var img = go.GetComponent<Image>();
            img.color = _color;
            img.raycastTarget = false;
            return img;
        }

        private static void CreateKeyHint(string _name, Transform _parent, float _anchorX, string _text)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = Child(go, _parent);
            rt.anchorMin = rt.anchorMax = new Vector2(_anchorX, 0.07f);
            rt.sizeDelta = new Vector2(600f, 80f);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            SetupText(tmp, _text, 40f);
            tmp.color = new Color(1f, 1f, 1f, 0.75f);
        }

        private static RectTransform Child(GameObject _go, Transform _parent)
        {
            _go.layer = _parent.gameObject.layer;
            var rt = _go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            return rt;
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
            _tmp.raycastTarget = false;
        }

        /// <summary>
        /// Title(0) / DimensionSelect(1) / Game(2), 나머지 기존 등록은 뒤에 유지
        /// </summary>
        public static string RegisterBuildScenes()
        {
            string[] head = { TITLE_SCENE_PATH, SCENE_PATH, GAME_SCENE_PATH };
            var scenes = head.Where(p => File.Exists(p)).Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => false == head.Contains(s.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
            return "Build Settings: " + string.Join(", ", scenes.Select(s => s.path));
        }

        /// <summary>
        /// TitleScene 의 TitleMenu 가 이 씬을 로드하도록 (씬을 열어 값만 바꾸고 저장)
        /// </summary>
        private static string SetTitleTarget()
        {
            if (false == File.Exists(TITLE_SCENE_PATH))
            {
                return "경고: TitleScene 없음";
            }
            Scene title = EditorSceneManager.OpenScene(TITLE_SCENE_PATH, OpenSceneMode.Single);
            TitleMenu menu = Object.FindFirstObjectByType<TitleMenu>(FindObjectsInactive.Include);
            if (null == menu)
            {
                return "경고: TitleScene 에 TitleMenu 없음";
            }
            var so = new SerializedObject(menu);
            so.FindProperty("gameSceneName").stringValue = "DimensionSelectScene";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(title);
            EditorSceneManager.SaveScene(title);
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            return "TitleMenu.gameSceneName = DimensionSelectScene";
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
