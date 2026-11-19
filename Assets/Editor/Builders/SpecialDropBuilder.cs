using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Builders
{
    /// <summary>
    /// 특수 드랍 아이템(자석 · 무적) 구성 빌더 (멱등)
    /// - expGem 을 바탕으로 magnetItem / invincibleItem 프리팹 생성 (ExpItem -> PowerUpItem)
    /// - DropManager 프리팹 specialItems / specialDropChance 설정, 씬 인스턴스의 해당 오버라이드는 되돌린다
    /// - Player 프리팹 damaePipeline 에 InvincibleBuff 추가 (blinkRenderers = 몸통 visual)
    /// 아이콘은 임시 (expGem 스프라이트 + 색). 아트는 visual-designer 단계에서 교체
    /// </summary>
    public static class SpecialDropBuilder
    {
        private const string EXP_GEM = "Assets/prefab/Item/expGem.prefab";
        private const string MAGNET = "Assets/prefab/Item/magnetItem.prefab";
        private const string INVINCIBLE = "Assets/prefab/Item/invincibleItem.prefab";
        private const string DROP_MANAGER = "Assets/prefab/Managers/DropManager.prefab";
        private const string PLAYER = "Assets/prefab/Player/Player.prefab";
        private const float DROP_CHANCE = 0.001f;
        private const float INVINCIBLE_DURATION = 5f;

        private static readonly string[] SCENES =
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/WeaponTest.unity",
        };

        [MenuItem("Tools/Builders/SpecialDrop")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            StringBuilder sb = new StringBuilder();

            BuildItem(MAGNET, POWER_UP_TYPE.Magnet, new Color(0.45f, 0.75f, 1f, 1f), sb);
            BuildItem(INVINCIBLE, POWER_UP_TYPE.Invincible, new Color(1f, 0.95f, 0.55f, 1f), sb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Spawnable magnet = AssetDatabase.LoadAssetAtPath<Spawnable>(MAGNET);
            Spawnable invincible = AssetDatabase.LoadAssetAtPath<Spawnable>(INVINCIBLE);

            SetupDropManagerPrefab(magnet, invincible, sb);
            SetupPlayer(sb);
            AssetDatabase.SaveAssets();

            RevertSceneOverrides(sb);

            return sb.ToString();
        }

        private static void BuildItem(string _path, POWER_UP_TYPE _type, Color _tint, StringBuilder _sb)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(EXP_GEM);
            try
            {
                root.name = System.IO.Path.GetFileNameWithoutExtension(_path);

                ExpItem exp = root.GetComponent<ExpItem>();
                if (null != exp)
                {
                    Object.DestroyImmediate(exp);
                }

                PowerUpItem item = root.GetComponent<PowerUpItem>();
                if (null == item)
                {
                    item = root.AddComponent<PowerUpItem>();
                }

                SerializedObject so = new SerializedObject(item);
                so.FindProperty("owner").objectReferenceValue = root;
                so.FindProperty("type").enumValueIndex = (int)_type;
                so.FindProperty("invincibleDuration").floatValue = INVINCIBLE_DURATION;
                so.ApplyModifiedPropertiesWithoutUndo();

                Transform visual = root.transform.Find("visual");
                if (null != visual)
                {
                    SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
                    if (null != sr)
                    {
                        sr.color = _tint;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, _path);
                _sb.AppendLine($"item {_path} type={_type}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupDropManagerPrefab(Spawnable _magnet, Spawnable _invincible, StringBuilder _sb)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DROP_MANAGER);
            try
            {
                DropManager[] managers = root.GetComponentsInChildren<DropManager>(true);
                foreach (DropManager dm in managers)
                {
                    SerializedObject so = new SerializedObject(dm);
                    SerializedProperty items = so.FindProperty("specialItems");
                    items.arraySize = 2;
                    items.GetArrayElementAtIndex(0).objectReferenceValue = _magnet;
                    items.GetArrayElementAtIndex(1).objectReferenceValue = _invincible;
                    so.FindProperty("specialDropChance").floatValue = DROP_CHANCE;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, DROP_MANAGER);
                _sb.AppendLine($"DropManager prefab: {managers.Length}개 설정");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupPlayer(StringBuilder _sb)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER);
            try
            {
                DamagePipeline pipeline = root.GetComponentInChildren<DamagePipeline>(true);
                if (null == pipeline || null == pipeline.GetComponent<HealthSystem>())
                {
                    _sb.AppendLine("WARN Player: DamagePipeline+HealthSystem 오브젝트 없음");
                    return;
                }

                GameObject target = pipeline.gameObject;
                InvincibleBuff buff = target.GetComponent<InvincibleBuff>();
                if (null == buff)
                {
                    buff = target.AddComponent<InvincibleBuff>();
                }

                Transform visual = root.transform.Find("visual");
                SpriteRenderer body = null != visual ? visual.GetComponent<SpriteRenderer>() : null;

                SerializedObject so = new SerializedObject(buff);
                SerializedProperty renderers = so.FindProperty("blinkRenderers");
                renderers.arraySize = null != body ? 1 : 0;
                if (null != body)
                {
                    renderers.GetArrayElementAtIndex(0).objectReferenceValue = body;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PLAYER);
                _sb.AppendLine($"Player: InvincibleBuff on {target.name}, blink={(null != body ? body.name : "없음")}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 씬의 DropManager 인스턴스에 specialItems / specialDropChance 오버라이드가 있으면 되돌린다
        /// </summary>
        private static void RevertSceneOverrides(StringBuilder _sb)
        {
            string activePath = SceneManager.GetActiveScene().path;

            foreach (string scenePath in SCENES)
            {
                if (null == AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
                {
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool opened = false;
                if (false == scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    opened = true;
                }

                int count = 0;
                int reverted = 0;
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    foreach (DropManager dm in go.GetComponentsInChildren<DropManager>(true))
                    {
                        ++count;
                        if (false == PrefabUtility.IsPartOfPrefabInstance(dm))
                        {
                            _sb.AppendLine($"WARN {scenePath}: {dm.name} 는 프리팹 인스턴스가 아님");
                            continue;
                        }

                        SerializedObject so = new SerializedObject(dm);
                        foreach (string name in new[] { "specialItems", "specialDropChance" })
                        {
                            SerializedProperty prop = so.FindProperty(name);
                            if (true == prop.prefabOverride)
                            {
                                PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
                                ++reverted;
                            }
                        }
                    }
                }

                if (0 < reverted)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                _sb.AppendLine($"{scenePath}: DropManager {count}개, 오버라이드 되돌림 {reverted}");

                if (true == opened && scene.path != activePath)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
