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
    /// Assets/Scenes/DimensionWeaponTest.unity 를 차원 무기 테스트 씬으로 구성한다 (멱등)
    /// - 실게임 GameScene 과 같은 분할 화면: 좌 / 우 플레이어 (0,0), 진영 카메라 2대 (rect 좌 / 우 절반, 자기 진영 레이어만)
    ///   + 각 진영 카메라 스택의 URP Overlay 카메라 2대 + StageSpace (stageWorldOrigin (0,500))
    /// - 카메라는 플레이어를 따라가지 않고 (0,0) 에 고정. 맵 로딩(WFCChunkLoader) 은 끈다
    /// - Canvas + EventSystem, DimensionWeaponTestController 참조 연결 후 누락 참조를 결과 문자열로 돌려준다
    /// - 이름으로 찾아 교체 / 재사용하므로 재실행해도 중복되지 않는다. Build Settings 에는 등록하지 않는다
    /// - Game.Editor 에 URP 참조가 없으므로 UniversalAdditionalCameraData 는 리플렉션 타입 + SerializedObject 로 다룬다
    /// </summary>
    public static class DimensionWeaponTestSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/DimensionWeaponTest.unity";
        private const string PLAYER_PREFAB = "Assets/prefab/Player/Player.prefab";
        private const string DUMMY_PREFAB = "Assets/prefab/Monster/Dummy/Dummy.prefab";
        private const string PASSIVE_BUFF_DIR = "Assets/prefab/Upgrade/PassiveBuff";
        private const string PASSIVE_NUFF_DIR = "Assets/prefab/Upgrade/PassiveNuff";
        // 무기 테스트와 무관한 몬스터 수 증감 패시브는 제외 (WeaponTestSceneBuilder 와 같은 규칙)
        private static readonly string[] EXCLUDED_PASSIVES = { "MonsterDecrease", "MonsterIncreaseNuff" };
        private const string FONT_PATH = "Assets/Fonts/NotoSansKR-Regular SDF.asset";

        private const string CANVAS_NAME = "DimensionWeaponTestCanvas";
        private const string EVENT_SYSTEM_NAME = "EventSystem";
        private const string CONTROLLER_NAME = "DimensionWeaponTestController";
        private const string LEFT_PLAYER_NAME = "LeftPlayer";
        private const string RIGHT_PLAYER_NAME = "RightPlayer";
        private const string LEFT_CAMERA_NAME = "LeftCamera";
        private const string RIGHT_CAMERA_NAME = "RightCamera";
        private const string LEFT_OVERLAY_NAME = "LeftOverlayCamera";
        private const string RIGHT_OVERLAY_NAME = "RightOverlayCamera";
        private const string STAGE_SPACE_NAME = "StageSpace";

        // GameScene 진영 카메라와 같은 값
        private const float CAMERA_SIZE = 8f;
        private const float CAMERA_Z = -10f;
        private static readonly Vector2 STAGE_WORLD_ORIGIN = new Vector2(0f, 500f);
        private static readonly Color CAMERA_BG = new Color(0.16f, 0.17f, 0.2f, 1f);

        // GameScene 진영 카메라 cullingMask 와 같은 레이어 (Stage / Boss 는 오버레이가 그린다)
        private static readonly string[] LEFT_LAYERS = { "LBG", "LPlayer", "LMonster", "LAttack", "LItem", "LUI" };
        private static readonly string[] RIGHT_LAYERS = { "RBG", "RPlayer", "RMonster", "RAttack", "RItem", "RUI" };
        private const string STAGE_LAYER = "Stage";
        private const string BOSS_LAYER = "Boss";

        private const string URP_CAMERA_DATA = "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";

        private static TMP_FontAsset font;

        [MenuItem("Tools/Builders/DimensionWeaponTestScene")]
        public static void BuildMenu()
        {
            // 메뉴 실행 시에만 확인 (script eval 에서는 모달이 뜨면 에디터가 멈춘다)
            if (false == EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[DimensionWeaponTestSceneBuilder]");

            GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DUMMY_PREFAB);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (null == dummyPrefab || null == playerPrefab)
            {
                return sb.AppendLine("에러: Dummy/Player 프리팹 없음").ToString();
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (null == font)
            {
                sb.AppendLine("경고: 폰트 없음 " + FONT_PATH);
            }

            Scene scene = OpenOrCreateScene(sb);

            // 1. 기존 플레이어 / 더미 인스턴스와 빌더 생성물(Canvas, 컨트롤러, EventSystem) 삭제. 카메라 / StageSpace 는 재사용
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                bool isPrefabInstance = (source == dummyPrefab) || (source == playerPrefab);
                bool isPlayer = null != root.GetComponent<PlayerStats>();
                bool isOwned = root.name == CANVAS_NAME || root.name == CONTROLLER_NAME || null != root.GetComponent<EventSystem>();
                if (true == isPrefabInstance || true == isPlayer || true == isOwned)
                {
                    Object.DestroyImmediate(root);
                }
            }

            // 2. 플레이어 2명
            PlayerStats leftPlayer = CreatePlayer(scene, playerPrefab, LEFT_PLAYER_NAME, PlayerSide.Left, sb);
            PlayerStats rightPlayer = CreatePlayer(scene, playerPrefab, RIGHT_PLAYER_NAME, PlayerSide.Right, sb);

            // 3. 진영 카메라 + 오버레이 카메라
            Camera leftCamera = BuildSideCamera(scene, LEFT_CAMERA_NAME, new Rect(0f, 0f, 0.5f, 1f), LEFT_LAYERS, true, sb);
            Camera rightCamera = BuildSideCamera(scene, RIGHT_CAMERA_NAME, new Rect(0.5f, 0f, 0.5f, 1f), RIGHT_LAYERS, false, sb);
            Camera leftOverlay = BuildOverlayCamera(scene, LEFT_OVERLAY_NAME, leftCamera, sb);
            Camera rightOverlay = BuildOverlayCamera(scene, RIGHT_OVERLAY_NAME, rightCamera, sb);

            // 4. StageSpace
            StageSpace stageSpace = BuildStageSpace(scene, leftCamera, rightCamera, leftOverlay, rightOverlay, sb);

            // 5. EventSystem
            var es = new GameObject(EVENT_SYSTEM_NAME, typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(es, scene);

            // 6. Canvas + UI
            UIRefs ui = BuildCanvas(scene);

            // 7. Controller
            var controllerGo = new GameObject(CONTROLLER_NAME);
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            var controller = controllerGo.AddComponent<DimensionWeaponTestController>();

            DimensionFusionRegistry registry = LoadRegistry(sb);
            List<RewardData> passives = LoadPassives();

            var so = new SerializedObject(controller);
            so.FindProperty("leftPlayer").objectReferenceValue = leftPlayer;
            so.FindProperty("rightPlayer").objectReferenceValue = rightPlayer;
            so.FindProperty("leftCamera").objectReferenceValue = leftCamera;
            so.FindProperty("rightCamera").objectReferenceValue = rightCamera;
            so.FindProperty("registry").objectReferenceValue = registry;
            so.FindProperty("dummyPrefab").objectReferenceValue = dummyPrefab;
            SerializedProperty fontProp = so.FindProperty("meterSettings.font");
            if (null != fontProp)
            {
                fontProp.objectReferenceValue = font;
            }
            so.FindProperty("weaponButtonTemplate").objectReferenceValue = ui.weaponTemplate;
            so.FindProperty("weaponButtonContainer").objectReferenceValue = ui.weaponContainer;
            so.FindProperty("layoutButtonTemplate").objectReferenceValue = ui.layoutTemplate;
            so.FindProperty("layoutButtonContainer").objectReferenceValue = ui.layoutContainer;
            so.FindProperty("resetButton").objectReferenceValue = ui.reset;
            so.FindProperty("weaponNameText").objectReferenceValue = ui.weaponNameText;
            so.FindProperty("dpsText").objectReferenceValue = ui.dpsText;

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

            // 참조 검증
            var missing = new List<string>();
            var check = new SerializedObject(controller);
            string[] refs = { "leftPlayer", "rightPlayer", "leftCamera", "rightCamera", "registry", "dummyPrefab", "meterSettings.font",
                "weaponButtonTemplate", "weaponButtonContainer", "layoutButtonTemplate", "layoutButtonContainer", "resetButton",
                "weaponNameText", "dpsText", "passiveRowTemplate", "passiveRowContainer", "passiveResetButton", "statSummaryText" };
            missing.AddRange(refs.Where(r => null == check.FindProperty(r) || null == check.FindProperty(r).objectReferenceValue));
            SerializedProperty checkPassives = check.FindProperty("passives");
            if (null == checkPassives || 0 == checkPassives.arraySize)
            {
                missing.Add("passives");
            }
            if (null == registry || 0 == registry.Recipes.Count)
            {
                missing.Add("registry.Recipes");
            }

            var sso = new SerializedObject(stageSpace);
            foreach (string r in new[] { "leftCamera", "rightCamera", "leftOverlayCamera", "rightOverlayCamera" })
            {
                if (null == sso.FindProperty(r).objectReferenceValue)
                {
                    missing.Add("StageSpace." + r);
                }
            }
            if (false == IsOverlayInStack(leftCamera, leftOverlay))
            {
                missing.Add(LEFT_CAMERA_NAME + " 스택의 " + LEFT_OVERLAY_NAME);
            }
            if (false == IsOverlayInStack(rightCamera, rightOverlay))
            {
                missing.Add(RIGHT_CAMERA_NAME + " 스택의 " + RIGHT_OVERLAY_NAME);
            }
            sb.AppendLine("누락 참조: " + (0 == missing.Count ? "없음" : string.Join(", ", missing)));

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, SCENE_PATH);
            sb.AppendLine("저장: " + (true == saved ? SCENE_PATH : "실패"));

            sb.AppendLine("최종 루트: " + string.Join(", ", scene.GetRootGameObjects().Select(g => g.name)));
            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        // ---------- 씬 ----------
        private static Scene OpenOrCreateScene(StringBuilder _sb)
        {
            if (true == File.Exists(Path.GetFullPath(SCENE_PATH)))
            {
                Scene opened = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
                _sb.AppendLine("기존 루트: " + string.Join(", ", opened.GetRootGameObjects().Select(g => g.name)));
                return opened;
            }

            Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _sb.AppendLine("새 씬 생성: " + SCENE_PATH);
            return created;
        }

        private static GameObject FindOrCreateRoot(Scene _scene, string _name)
        {
            GameObject found = _scene.GetRootGameObjects().FirstOrDefault(g => g.name == _name);
            if (null != found)
            {
                return found;
            }

            var go = new GameObject(_name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            T c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }

        // ---------- 플레이어 ----------
        /// <summary>
        /// Player 프리팹 인스턴스 (0,0). side 를 지정하고, 오른쪽은 L* 레이어를 R* 로 바꾼다 (GameScene 과 같이 진영 카메라에만 보이도록)
        /// </summary>
        private static PlayerStats CreatePlayer(Scene _scene, GameObject _prefab, string _name, PlayerSide _side, StringBuilder _sb)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(_prefab, _scene);
            go.name = _name;
            go.transform.position = Vector3.zero;

            PlayerStats stats = go.GetComponent<PlayerStats>();
            if (null == stats)
            {
                stats = go.GetComponentInChildren<PlayerStats>(true);
            }
            if (null != stats)
            {
                var so = new SerializedObject(stats);
                so.FindProperty("side").enumValueIndex = (int)_side;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (PlayerSide.Right == _side)
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    string layerName = LayerMask.LayerToName(t.gameObject.layer);
                    if (true == layerName.StartsWith("L"))
                    {
                        int mirrored = LayerMask.NameToLayer("R" + layerName.Substring(1));
                        if (0 <= mirrored)
                        {
                            t.gameObject.layer = mirrored;
                        }
                    }
                }
            }

            // 오른쪽 플레이어는 GameScene 과 같이 방향키 (프리팹 기본은 WASD 라 두 플레이어가 같이 움직인다)
            int arrows = 0;
            if (PlayerSide.Right == _side)
            {
                foreach (ArrowMovemnt move in go.GetComponentsInChildren<ArrowMovemnt>(true))
                {
                    var mso = new SerializedObject(move);
                    mso.FindProperty("up").intValue = (int)UnityEngine.InputSystem.Key.UpArrow;
                    mso.FindProperty("down").intValue = (int)UnityEngine.InputSystem.Key.DownArrow;
                    mso.FindProperty("left").intValue = (int)UnityEngine.InputSystem.Key.LeftArrow;
                    mso.FindProperty("right").intValue = (int)UnityEngine.InputSystem.Key.RightArrow;
                    mso.ApplyModifiedPropertiesWithoutUndo();
                    arrows++;
                }
                _sb.AppendLine($"{_name}: 방향키 지정 {arrows}");
            }

            // 맵 로딩 끄기 (프리팹에 없으면 아무 일도 하지 않는다)
            int loaders = 0;
            foreach (WFCChunkLoader loader in go.GetComponentsInChildren<WFCChunkLoader>(true))
            {
                loader.enabled = false;
                loaders++;
            }

            _sb.AppendLine($"{_name}: side {_side}, PlayerStats {(null != stats ? "OK" : "없음")}, 레이어 {LayerMask.LayerToName(go.layer)}, WFCChunkLoader 끔 {loaders}");
            return stats;
        }

        // ---------- 카메라 ----------
        private static int MaskOf(string[] _layers)
        {
            int mask = 0;
            foreach (string name in _layers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (0 <= layer)
                {
                    mask |= 1 << layer;
                }
            }
            return mask;
        }

        private static SerializedObject UrpData(GameObject _go)
        {
            System.Type t = System.Type.GetType(URP_CAMERA_DATA);
            if (null == t)
            {
                return null;
            }
            Component c = _go.GetComponent(t);
            if (null == c)
            {
                c = _go.AddComponent(t);
            }
            return new SerializedObject(c);
        }

        /// <summary>
        /// 진영 Base 카메라: (0,0,-10) 고정, ortho 8, 자기 진영 레이어만 (Stage / Boss 제외)
        /// </summary>
        private static Camera BuildSideCamera(Scene _scene, string _name, Rect _rect, string[] _layers, bool _hasListener, StringBuilder _sb)
        {
            GameObject go = FindOrCreateRoot(_scene, _name);
            go.transform.SetPositionAndRotation(new Vector3(0f, 0f, CAMERA_Z), Quaternion.identity);
            Camera cam = GetOrAdd<Camera>(go);
            cam.orthographic = true;
            cam.orthographicSize = CAMERA_SIZE;
            cam.rect = _rect;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CAMERA_BG;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.depth = 0f;

            int mask = MaskOf(_layers) & ~MaskOf(new[] { STAGE_LAYER, BOSS_LAYER });
            cam.cullingMask = mask;

            AudioListener listener = go.GetComponent<AudioListener>();
            if (true == _hasListener && null == listener)
            {
                go.AddComponent<AudioListener>();
            }
            else if (false == _hasListener && null != listener)
            {
                Object.DestroyImmediate(listener);
            }

            SerializedObject uso = UrpData(go);
            if (null != uso)
            {
                uso.FindProperty("m_CameraType").intValue = 0; // Base
                SerializedProperty pp = uso.FindProperty("m_RenderPostProcessing");
                if (null != pp)
                {
                    pp.boolValue = false;
                }
                uso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                _sb.AppendLine("경고: UniversalAdditionalCameraData 타입 없음");
            }

            _sb.AppendLine($"{_name}: Base, rect {_rect}, mask " + string.Join("|", _layers.Where(l => 0 <= LayerMask.NameToLayer(l))));
            return cam;
        }

        /// <summary>
        /// 진영 카메라 스택에 붙는 URP Overlay 카메라 (Boss | Stage). 위치 / ortho / mask 는 런타임에 StageSpace 가 다시 맞춘다
        /// - FinalBossBuilder.BuildOverlayCamera 와 같은 방식. Base 로 만들면 아래 화면을 통째로 덮으므로 반드시 Overlay
        /// </summary>
        private static Camera BuildOverlayCamera(Scene _scene, string _name, Camera _base, StringBuilder _sb)
        {
            GameObject go = FindOrCreateRoot(_scene, _name);
            // 무대 오브젝트는 z=0 이므로 음수 z 보장 (진영 카메라 z 를 복사하지 않는다)
            go.transform.SetPositionAndRotation(new Vector3(STAGE_WORLD_ORIGIN.x, STAGE_WORLD_ORIGIN.y, CAMERA_Z), Quaternion.identity);
            Camera cam = GetOrAdd<Camera>(go);
            cam.orthographic = true;
            cam.orthographicSize = null != _base ? _base.orthographicSize : CAMERA_SIZE;
            cam.cullingMask = MaskOf(new[] { BOSS_LAYER, STAGE_LAYER });
            cam.clearFlags = CameraClearFlags.Depth;
            // 스택 Overlay 는 자기 rect 로 aspect 를 계산하므로 Base 와 같은 rect 여야 화면 비율이 맞는다
            cam.rect = null != _base ? _base.rect : new Rect(0f, 0f, 1f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            var listener = go.GetComponent<AudioListener>();
            if (null != listener)
            {
                Object.DestroyImmediate(listener);
            }

            SerializedObject uso = UrpData(go);
            if (null == uso)
            {
                _sb.AppendLine("경고: UniversalAdditionalCameraData 타입 없음");
                return cam;
            }
            uso.FindProperty("m_CameraType").intValue = 1; // Overlay
            SerializedProperty pp = uso.FindProperty("m_RenderPostProcessing");
            if (null != pp)
            {
                pp.boolValue = false;
            }
            uso.ApplyModifiedPropertiesWithoutUndo();

            if (null != _base)
            {
                SerializedObject bso = UrpData(_base.gameObject);
                SerializedProperty stack = bso.FindProperty("m_Cameras");
                bool has = false;
                for (int i = stack.arraySize - 1; i >= 0; --i)
                {
                    Object o = stack.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (null == o)
                    {
                        stack.DeleteArrayElementAtIndex(i);
                    }
                    else if (o == cam)
                    {
                        has = true;
                    }
                }
                if (false == has)
                {
                    stack.arraySize += 1;
                    stack.GetArrayElementAtIndex(stack.arraySize - 1).objectReferenceValue = cam;
                }
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
            _sb.AppendLine(_name + ": Overlay, mask Boss|Stage, 스택=" + (null != _base ? _base.name : "없음"));
            return cam;
        }

        private static bool IsOverlayInStack(Camera _base, Camera _overlay)
        {
            if (null == _base || null == _overlay)
            {
                return false;
            }
            SerializedObject bso = UrpData(_base.gameObject);
            SerializedObject oso = UrpData(_overlay.gameObject);
            if (null == bso || null == oso || 1 != oso.FindProperty("m_CameraType").intValue)
            {
                return false;
            }
            SerializedProperty stack = bso.FindProperty("m_Cameras");
            for (int i = 0; i < stack.arraySize; i++)
            {
                if (stack.GetArrayElementAtIndex(i).objectReferenceValue == _overlay)
                {
                    return true;
                }
            }
            return false;
        }

        private static StageSpace BuildStageSpace(Scene _scene, Camera _left, Camera _right, Camera _leftOverlay, Camera _rightOverlay, StringBuilder _sb)
        {
            GameObject go = FindOrCreateRoot(_scene, STAGE_SPACE_NAME);
            StageSpace space = GetOrAdd<StageSpace>(go);
            var so = new SerializedObject(space);
            so.FindProperty("leftCamera").objectReferenceValue = _left;
            so.FindProperty("rightCamera").objectReferenceValue = _right;
            so.FindProperty("leftOverlayCamera").objectReferenceValue = _leftOverlay;
            so.FindProperty("rightOverlayCamera").objectReferenceValue = _rightOverlay;
            so.FindProperty("stageWorldOrigin").vector2Value = STAGE_WORLD_ORIGIN;
            so.ApplyModifiedPropertiesWithoutUndo();
            _sb.AppendLine("StageSpace: origin " + STAGE_WORLD_ORIGIN);
            return space;
        }

        // ---------- 에셋 ----------
        private static DimensionFusionRegistry LoadRegistry(StringBuilder _sb)
        {
            string[] guids = AssetDatabase.FindAssets("t:DimensionFusionRegistry");
            if (0 == guids.Length)
            {
                _sb.AppendLine("경고: DimensionFusionRegistry 에셋 없음");
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (1 < guids.Length)
            {
                _sb.AppendLine($"경고: DimensionFusionRegistry {guids.Length}개, 첫 번째 사용 ({path})");
            }
            var registry = AssetDatabase.LoadAssetAtPath<DimensionFusionRegistry>(path);
            if (null != registry)
            {
                _sb.AppendLine($"registry {path}, recipes {registry.Recipes.Count}: " +
                    string.Join(", ", registry.Recipes.Select(r => null != r ? r.name + (null != r.ResultPrefab ? "" : "(결과 없음)") : "null")));
            }
            return registry;
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

        // ---------- UI ----------
        private struct UIRefs
        {
            public Button weaponTemplate;
            public Transform weaponContainer;
            public Button layoutTemplate;
            public Transform layoutContainer;
            public Button reset;
            public TMP_Text weaponNameText;
            public TMP_Text dpsText;
            public DimensionWeaponTestPassiveRow passiveRowTemplate;
            public Transform passiveRowContainer;
            public Button passiveReset;
            public TMP_Text statSummaryText;
        }

        private const float PANEL_ALPHA = 0.3f;
        private const float WEAPON_PANEL_WIDTH = 220f;
        private const float PASSIVE_ROW_HEIGHT = 36f;
        private const float PASSIVE_PANEL_WIDTH = 420f;
        private const float PASSIVE_BUTTON_SIZE = 32f;
        private const float PASSIVE_STACK_WIDTH = 44f;

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

            // 좌측 무기 목록 (왼쪽 진영 화면 왼쪽 위)
            RectTransform weaponPanel = CreatePanel("WeaponListPanel", canvasGo.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -90f), new Vector2(WEAPON_PANEL_WIDTH, 420f));
            var vlg = weaponPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vlg);
            refs.weaponContainer = weaponPanel;
            refs.weaponTemplate = CreateButton("WeaponButton", weaponPanel, "무기", WEAPON_PANEL_WIDTH - 20f, 48f);
            refs.weaponTemplate.gameObject.SetActive(false);

            // 상단 가운데 배치 버튼 (두 화면 경계)
            RectTransform layoutPanel = CreatePanel("LayoutPanel", canvasGo.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(720f, 64f));
            var hlg = layoutPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(hlg);
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            refs.layoutContainer = layoutPanel;
            refs.layoutTemplate = CreateButton("LayoutButton", layoutPanel, "배치", 160f, 46f);
            refs.layoutTemplate.gameObject.SetActive(false);

            // 하단 가운데 무기 이름 / DPS / 초기화 (두 화면 경계)
            RectTransform controlPanel = CreatePanel("ControlPanel", canvasGo.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(340f, 230f));
            var cvlg = controlPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(cvlg);
            cvlg.padding = new RectOffset(8, 8, 8, 8);
            cvlg.spacing = 6f;
            refs.weaponNameText = CreateText("WeaponNameText", controlPanel, "-", 28f, 44f);
            TMP_Text dps = CreateText("DpsText", controlPanel, "L DPS 0\nR DPS 0\n합계 0", 22f, 90f);
            dps.color = new Color(1f, 0.85f, 0.3f);
            refs.dpsText = dps;
            refs.reset = CreateButton("ResetButton", controlPanel, "초기화", 300f, 48f);

            // 우측 패시브 (오른쪽 진영 화면 오른쪽)
            BuildPassivePanel(canvasGo.transform, ref refs);

            // 패시브 패널이 오른쪽 진영 화면의 더미를 가리므로 토글로 열고 닫는다 (기본 닫힘)
            Transform passivePanel = canvasGo.transform.Find("PassivePanel");
            if (null != passivePanel)
            {
                BuildPassiveToggle(canvasGo.transform, passivePanel.gameObject);
            }

            return refs;
        }

        /// <summary>
        /// 우상단 "패시브" 토글. onValueChanged → 패널 SetActive (영구 리스너라 런타임 코드 없이 동작)
        /// </summary>
        private static void BuildPassiveToggle(Transform _canvas, GameObject _panel)
        {
            var go = new GameObject("PassiveToggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.layer = _canvas.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvas, false);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            rt.sizeDelta = new Vector2(180f, 50f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.25f, 0.25f, 0.3f, 1f);

            // 켜졌을 때 표시 (테두리 강조)
            var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.layer = go.layer;
            var checkRt = checkGo.GetComponent<RectTransform>();
            checkRt.SetParent(rt, false);
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;
            var check = checkGo.GetComponent<Image>();
            check.color = new Color(0.35f, 0.45f, 0.7f, 1f);
            check.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.layer = go.layer;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            SetupText(label, "패시브", 24f);
            label.raycastTarget = false;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            toggle.isOn = false;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(toggle.onValueChanged, _panel.SetActive);

            // 패널은 토글 아래로 내리고 기본 닫힘
            var panelRt = _panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, -35f);
            panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, -90f);
            _panel.SetActive(false);
        }

        private static void BuildPassivePanel(Transform _canvas, ref UIRefs _refs)
        {
            RectTransform panel = CreatePanel("PassivePanel", _canvas,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(PASSIVE_PANEL_WIDTH, -20f));
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(vlg);
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;

            // 열 제목 (이름 / 좌 / 우)
            CreatePassiveHeader(panel);

            // 스크롤 영역 (행이 다 안 들어가므로 ScrollRect)
            var scrollGo = new GameObject("PassiveScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.layer = panel.gameObject.layer;
            scrollGo.transform.SetParent(panel, false);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
            var scrollLe = scrollGo.GetComponent<LayoutElement>();
            scrollLe.preferredHeight = 520f;
            scrollLe.flexibleHeight = 1f;

            const float SCROLLBAR_WIDTH = 12f;
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
            cvlg.padding = new RectOffset(2, 2, 2, 2);
            cvlg.spacing = 3f;
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

            _refs.passiveReset = CreateButton("PassiveResetButton", panel, "패시브 초기화", PASSIVE_PANEL_WIDTH - 16f, 42f);

            // 무기 최종 수치 1줄 + 스탯 요약 9줄
            TMP_Text summary = CreateText("StatSummaryText", panel, "", 15f, 210f);
            summary.GetComponent<LayoutElement>().preferredWidth = PASSIVE_PANEL_WIDTH - 16f;
            summary.alignment = TextAlignmentOptions.TopLeft;
            summary.textWrappingMode = TextWrappingModes.NoWrap;
            summary.overflowMode = TextOverflowModes.Truncate;
            _refs.statSummaryText = summary;
        }

        private static void CreatePassiveHeader(Transform _parent)
        {
            HorizontalLayoutGroup hlg = CreateRowObject("PassiveHeader", _parent, 26f, out GameObject go);
            hlg.padding = new RectOffset(4, 4 + 14, 0, 0); // 스크롤바 폭만큼 오른쪽 여백

            TMP_Text nameText = CreateText("NameHeader", go.transform, "패시브", 16f, 26f);
            var nameLe = nameText.GetComponent<LayoutElement>();
            nameLe.preferredWidth = 120f;
            nameLe.flexibleWidth = 1f;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;

            float sideWidth = PASSIVE_BUTTON_SIZE * 2f + PASSIVE_STACK_WIDTH + 8f;
            TMP_Text left = CreateText("LeftHeader", go.transform, "좌", 16f, 26f);
            left.GetComponent<LayoutElement>().preferredWidth = sideWidth;
            TMP_Text right = CreateText("RightHeader", go.transform, "우", 16f, 26f);
            right.GetComponent<LayoutElement>().preferredWidth = sideWidth;
        }

        private static HorizontalLayoutGroup CreateRowObject(string _name, Transform _parent, float _height, out GameObject _go)
        {
            _go = new GameObject(_name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            _go.layer = _parent.gameObject.layer;
            _go.transform.SetParent(_parent, false);
            _go.GetComponent<LayoutElement>().preferredHeight = _height;
            var hlg = _go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            return hlg;
        }

        private static DimensionWeaponTestPassiveRow CreatePassiveRow(Transform _parent)
        {
            CreateRowObject("PassiveRow", _parent, PASSIVE_ROW_HEIGHT, out GameObject go);
            var row = go.AddComponent<DimensionWeaponTestPassiveRow>();

            TMP_Text nameText = CreateText("NameText", go.transform, "패시브", 16f, PASSIVE_ROW_HEIGHT);
            var nameLe = nameText.GetComponent<LayoutElement>();
            nameLe.preferredWidth = 120f;
            nameLe.flexibleWidth = 1f;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 11f;
            nameText.fontSizeMax = 16f;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            Button leftMinus = CreateButton("LeftMinusButton", go.transform, "-", PASSIVE_BUTTON_SIZE, PASSIVE_BUTTON_SIZE);
            TMP_Text leftStack = CreateText("LeftStackText", go.transform, "x0", 16f, PASSIVE_ROW_HEIGHT);
            leftStack.GetComponent<LayoutElement>().preferredWidth = PASSIVE_STACK_WIDTH;
            Button leftPlus = CreateButton("LeftPlusButton", go.transform, "+", PASSIVE_BUTTON_SIZE, PASSIVE_BUTTON_SIZE);

            Button rightMinus = CreateButton("RightMinusButton", go.transform, "-", PASSIVE_BUTTON_SIZE, PASSIVE_BUTTON_SIZE);
            TMP_Text rightStack = CreateText("RightStackText", go.transform, "x0", 16f, PASSIVE_ROW_HEIGHT);
            rightStack.GetComponent<LayoutElement>().preferredWidth = PASSIVE_STACK_WIDTH;
            Button rightPlus = CreateButton("RightPlusButton", go.transform, "+", PASSIVE_BUTTON_SIZE, PASSIVE_BUTTON_SIZE);

            // 좌 / 우 구분용 색
            leftMinus.GetComponent<Image>().color = new Color(0.22f, 0.3f, 0.42f, 1f);
            leftPlus.GetComponent<Image>().color = new Color(0.22f, 0.3f, 0.42f, 1f);
            rightMinus.GetComponent<Image>().color = new Color(0.42f, 0.24f, 0.24f, 1f);
            rightPlus.GetComponent<Image>().color = new Color(0.42f, 0.24f, 0.24f, 1f);

            var so = new SerializedObject(row);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("leftStackText").objectReferenceValue = leftStack;
            so.FindProperty("leftMinusButton").objectReferenceValue = leftMinus;
            so.FindProperty("leftPlusButton").objectReferenceValue = leftPlus;
            so.FindProperty("rightStackText").objectReferenceValue = rightStack;
            so.FindProperty("rightMinusButton").objectReferenceValue = rightMinus;
            so.FindProperty("rightPlusButton").objectReferenceValue = rightPlus;
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
            // 반쪽 화면을 많이 가리므로 배경을 옅게 (뒤의 더미 / 미터가 보이도록)
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, PANEL_ALPHA);
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
            SetupText(tmp, _label, Mathf.Min(24f, _height * 0.6f));
            tmp.raycastTarget = false;

            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateText(string _name, Transform _parent, string _text, float _fontSize, float _height)
        {
            var go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 300f;
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
    }
}
