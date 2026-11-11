using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// 최종 보스 통합 (재실행 가능)
    /// - 레이어 18 "Boss", BossOverlayCamera, FinalBossData, FinalBoss 프리팹(운석·그림자 포함),
    ///   진영 프록시 2개, FinalBossDirector, HUD 보스 HP 바, 마지막에 ArtResourceBuilder 재실행
    /// </summary>
    public static class FinalBossBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const int BOSS_LAYER = 18;
        private const string BOSS_LAYER_NAME = "Boss";
        private const string DATA_DIR = "Assets/Resources/Data/Monster/Boss";
        private const string DATA_PATH = DATA_DIR + "/FinalBossData.asset";
        private const string STYLE_PATH = DATA_DIR + "/BossTelegraphStyle.asset";
        private const string PREFAB_DIR = "Assets/prefab/Monster/Boss/FinalBoss";
        private const string BOSS_PREFAB = PREFAB_DIR + "/FinalBoss.prefab";
        private const string METEOR_PREFAB = PREFAB_DIR + "/FinalBossMeteor.prefab";
        private const string SHADOW_PREFAB = PREFAB_DIR + "/FinalBossMeteorShadow.prefab";
        private const string BOSS_SPRITE = "Assets/Resources/Textuer/Art/Monster/D3_Monster_Wraith_Walk.png";
        private const string DUMMY_PATH = "Assets/prefab/Monster/Dummy/Dummy.prefab";
        private const string URP_UNLIT_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string SORTING = "attack";

        [MenuItem("Tools/Builders/FinalBoss")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[FinalBossBuilder]");
            EnsureLayer(sb);
            EnsureFolder(DATA_DIR);
            EnsureFolder(PREFAB_DIR);

            FinalBossData data = BuildData(sb);
            FinalBoss bossPrefab = BuildBossPrefab(data, sb);

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            Camera left = null;
            Camera right = null;
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (c.name.StartsWith("BossOverlayCamera") || c.name.StartsWith("LetterboxBackground"))
                {
                    continue;
                }
                if (c.rect.x < 0.25f && c.rect.width < 0.75f)
                {
                    left = c;
                }
                else if (c.rect.x >= 0.25f)
                {
                    right = c;
                }
            }
            sb.AppendLine("좌 카메라: " + (left ? left.name : "없음") + ", 우 카메라: " + (right ? right.name : "없음"));

            RemoveOldOverlay(sb);
            Camera leftOverlay = BuildOverlayCamera("BossOverlayCamera_Left", left, sb);
            Camera rightOverlay = BuildOverlayCamera("BossOverlayCamera_Right", right, sb);

            PlayerStats leftPlayer = null;
            PlayerStats rightPlayer = null;
            int lPlayer = LayerMask.NameToLayer("LPlayer");
            foreach (PlayerStats p in Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include))
            {
                if (p.gameObject.layer == lPlayer)
                {
                    leftPlayer = p;
                }
                else
                {
                    rightPlayer = p;
                }
            }
            sb.AppendLine("좌 플레이어: " + (leftPlayer ? leftPlayer.name : "없음") + ", 우 플레이어: " + (rightPlayer ? rightPlayer.name : "없음"));

            GameObject root = FindOrCreate("FinalBossDirector", null);
            root.transform.position = Vector3.zero;
            BossHitProxy lProxy = BuildProxy("BossHitProxy_Left", root.transform, PlayerSide.Left);
            BossHitProxy rProxy = BuildProxy("BossHitProxy_Right", root.transform, PlayerSide.Right);

            FinalBossDirector director = GetOrAdd<FinalBossDirector>(root);
            var dso = new SerializedObject(director);
            dso.FindProperty("leftCamera").objectReferenceValue = left;
            dso.FindProperty("rightCamera").objectReferenceValue = right;
            dso.FindProperty("leftOverlayCamera").objectReferenceValue = leftOverlay;
            dso.FindProperty("rightOverlayCamera").objectReferenceValue = rightOverlay;
            dso.FindProperty("leftPlayer").objectReferenceValue = leftPlayer;
            dso.FindProperty("rightPlayer").objectReferenceValue = rightPlayer;
            dso.FindProperty("finalBoss").objectReferenceValue = bossPrefab;
            dso.FindProperty("data").objectReferenceValue = data;
            dso.FindProperty("leftProxy").objectReferenceValue = lProxy;
            dso.FindProperty("rightProxy").objectReferenceValue = rProxy;
            dso.FindProperty("gameOverManager").objectReferenceValue = Object.FindAnyObjectByType<GameOverManager>(FindObjectsInactive.Include);
            dso.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("FinalBossDirector 연결 완료 (프록시 2, finalBoss=프리팹)");

            BuildHpBar(sb);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine(Visual.ArtResourceBuilder.Build());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        // ---------- 레이어 ----------
        private static void EnsureLayer(StringBuilder _sb)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty slot = layers.GetArrayElementAtIndex(BOSS_LAYER);
            if (BOSS_LAYER_NAME != slot.stringValue)
            {
                if (false == string.IsNullOrEmpty(slot.stringValue))
                {
                    _sb.AppendLine("경고: 레이어 18 '" + slot.stringValue + "' 를 Boss 로 덮어씀");
                }
                slot.stringValue = BOSS_LAYER_NAME;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                _sb.AppendLine("레이어 18 Boss 추가");
            }
        }

        // ---------- 데이터 ----------
        private static FinalBossData BuildData(StringBuilder _sb)
        {
            var data = AssetDatabase.LoadAssetAtPath<FinalBossData>(DATA_PATH);
            if (null == data)
            {
                data = ScriptableObject.CreateInstance<FinalBossData>();
                AssetDatabase.CreateAsset(data, DATA_PATH);
                _sb.AppendLine("생성: " + DATA_PATH);
            }
            data.laserMaxAttempts = 12;
            data.telegraphStyle = AssetDatabase.LoadAssetAtPath<BossTelegraphStyle>(STYLE_PATH);
            data.bossBgm = SoundId.None;
            EditorUtility.SetDirty(data);
            if (null == data.telegraphStyle)
            {
                _sb.AppendLine("경고: BossTelegraphStyle 없음 (" + STYLE_PATH + ")");
            }
            return data;
        }

        // ---------- 프리팹 ----------
        private static FinalBoss BuildBossPrefab(FinalBossData _data, StringBuilder _sb)
        {
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(URP_UNLIT_MAT);
            float meteorDiameter = _data.meteorRadius * 2f;

            GameObject meteor = SaveCirclePrefab(METEOR_PREFAB, circle, mat, new Color(1f, 0.45f, 0.15f, 1f), meteorDiameter * 0.6f, 20);
            GameObject shadow = SaveCirclePrefab(SHADOW_PREFAB, circle, mat, new Color(0f, 0f, 0f, 0.45f), meteorDiameter, 5);

            Sprite body = AssetDatabase.LoadAllAssetsAtPath(BOSS_SPRITE).OfType<Sprite>().OrderBy(s => s.name).FirstOrDefault();
            var root = new GameObject("FinalBoss");
            try
            {
                root.layer = BOSS_LAYER;
                var visual = new GameObject("Visual");
                visual.layer = BOSS_LAYER;
                visual.transform.SetParent(root.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = null != body ? body : circle;
                sr.sharedMaterial = mat;
                sr.color = new Color(0.55f, 0.2f, 0.75f, 1f);
                sr.sortingLayerName = SORTING;
                sr.sortingOrder = 10;
                float size = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                float scale = size > 0f ? (_data.bodyRadius * 2f) / size : 1f;
                visual.transform.localScale = new Vector3(scale, scale, 1f);

                // 검정 테두리 느낌의 원 바닥
                var aura = new GameObject("Aura");
                aura.layer = BOSS_LAYER;
                aura.transform.SetParent(root.transform, false);
                var asr = aura.AddComponent<SpriteRenderer>();
                asr.sprite = circle;
                asr.sharedMaterial = mat;
                asr.color = new Color(0.05f, 0f, 0.08f, 0.6f);
                asr.sortingLayerName = SORTING;
                asr.sortingOrder = 9;
                float csize = circle.bounds.size.x;
                float cs = (_data.bodyRadius * 2f) / csize;
                aura.transform.localScale = new Vector3(cs, cs, 1f);

                FinalBoss boss = root.AddComponent<FinalBoss>();
                var so = new SerializedObject(boss);
                so.FindProperty("meteorVisualPrefab").objectReferenceValue = meteor;
                so.FindProperty("meteorShadowPrefab").objectReferenceValue = shadow;
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BOSS_PREFAB);
                _sb.AppendLine("프리팹: " + BOSS_PREFAB + " (sprite " + (body ? body.name : "원") + ", scale " + scale.ToString("F2") + ")");
                return saved.GetComponent<FinalBoss>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject SaveCirclePrefab(string _path, Sprite _sprite, Material _mat, Color _color, float _diameter, int _order)
        {
            var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(_path));
            try
            {
                go.layer = BOSS_LAYER;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.sharedMaterial = _mat;
                sr.color = _color;
                sr.sortingLayerName = SORTING;
                sr.sortingOrder = _order;
                float s = _diameter / Mathf.Max(0.01f, _sprite.bounds.size.x);
                go.transform.localScale = new Vector3(s, s, 1f);
                return PrefabUtility.SaveAsPrefabAsset(go, _path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- 카메라 ----------
        private static void RemoveOldOverlay(StringBuilder _sb)
        {
            foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if ("BossOverlayCamera" == g.name)
                {
                    Object.DestroyImmediate(g);
                    _sb.AppendLine("기존 BossOverlayCamera 제거");
                    break;
                }
            }
        }

        private static System.Type UrpType()
        {
            return System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        }

        private static SerializedObject UrpData(GameObject _go)
        {
            System.Type t = UrpType();
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

        /// <summary>진영 카메라 스택에 붙는 URP Overlay 카메라 (보스 레이어만)</summary>
        private static Camera BuildOverlayCamera(string _name, Camera _base, StringBuilder _sb)
        {
            GameObject go = FindOrCreate(_name, null);
            go.transform.SetPositionAndRotation(new Vector3(0f, 500f, -10f), Quaternion.identity);
            Camera cam = GetOrAdd<Camera>(go);
            cam.orthographic = true;
            cam.orthographicSize = null != _base ? _base.orthographicSize : 8f;
            cam.cullingMask = 1 << BOSS_LAYER;
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
            if (null != pp) { pp.boolValue = false; }
            uso.ApplyModifiedPropertiesWithoutUndo();

            if (null != _base)
            {
                _base.cullingMask &= ~(1 << BOSS_LAYER);
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
            _sb.AppendLine(_name + ": Overlay, mask Boss, 스택=" + (null != _base ? _base.name + "(rect x " + _base.rect.x + ")" : "없음"));
            return cam;
        }

        // ---------- 프록시 ----------
        private static BossHitProxy BuildProxy(string _name, Transform _parent, PlayerSide _side)
        {
            GameObject go = FindOrCreate(_name, _parent);
            go.layer = LayerMask.NameToLayer(PlayerSide.Left == _side ? "LMonster" : "RMonster");
            BossHitProxy proxy = GetOrAdd<BossHitProxy>(go);
            var body = go.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            go.GetComponent<CircleCollider2D>().isTrigger = true;
            var pso = new SerializedObject(proxy);
            pso.FindProperty("side").enumValueIndex = (int)_side;
            pso.ApplyModifiedPropertiesWithoutUndo();

            GameObject binderGo = FindOrCreate("LocalEventBiner", go.transform);
            binderGo.layer = go.layer;
            var binder = GetOrAdd<DamageTextBinder>(binderGo);
            GameObject dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DUMMY_PATH);
            var dummyBinder = null != dummy ? dummy.GetComponentInChildren<DamageTextBinder>(true) : null;
            if (null != dummyBinder)
            {
                EditorUtility.CopySerialized(dummyBinder, binder);
            }
            var bso = new SerializedObject(binder);
            bso.FindProperty("target").objectReferenceValue = go.GetComponent<HealthSystem>();
            bso.FindProperty("spawnPoint").objectReferenceValue = go.transform;
            bso.ApplyModifiedPropertiesWithoutUndo();
            return proxy;
        }

        // ---------- HUD ----------
        private static void BuildHpBar(StringBuilder _sb)
        {
            GameObject canvas = GameObject.Find("HUDCanvas");
            if (null == canvas)
            {
                _sb.AppendLine("경고: HUDCanvas 없음, HP 바 생략");
                return;
            }
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject barGo = FindOrCreate("FinalBossHPBar", canvas.transform);
            var rt = GetOrAdd<RectTransform>(barGo);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -120f);
            rt.sizeDelta = new Vector2(-160f, 28f);
            var bg = GetOrAdd<Image>(barGo);
            bg.sprite = uiSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.02f, 0.1f, 0.85f);
            bg.raycastTarget = false;
            var group = GetOrAdd<CanvasGroup>(barGo);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject fillGo = FindOrCreate("Fill", barGo.transform);
            var frt = GetOrAdd<RectTransform>(fillGo);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(4f, 4f);
            frt.offsetMax = new Vector2(-4f, -4f);
            var fill = GetOrAdd<Image>(fillGo);
            fill.sprite = uiSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = new Color(0.7f, 0.2f, 0.9f, 1f);
            fill.raycastTarget = false;

            var bar = GetOrAdd<FinalBossHPBar>(barGo);
            var so = new SerializedObject(bar);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            _sb.AppendLine("HUD: HUDCanvas/FinalBossHPBar/Fill (WaveTimer 아래, 전폭)");
        }

        // ---------- 유틸 ----------
        private static GameObject FindOrCreate(string _name, Transform _parent)
        {
            if (null != _parent)
            {
                Transform t = _parent.Find(_name);
                if (null != t)
                {
                    return t.gameObject;
                }
                var child = new GameObject(_name);
                child.transform.SetParent(_parent, false);
                return child;
            }
            foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (_name == g.name)
                {
                    return g;
                }
            }
            return new GameObject(_name);
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            T c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }

        private static void EnsureFolder(string _path)
        {
            if (true == AssetDatabase.IsValidFolder(_path))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(_path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(_path));
        }
    }
}
