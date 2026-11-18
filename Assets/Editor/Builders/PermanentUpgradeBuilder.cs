using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Builders
{
    /// <summary>
    /// 영구 강화(차원력) 에셋을 만들고 GameScene 에 PermanentUpgradeApplier 를 배치한다 (멱등)
    /// - 강화 9종 PermanentUpgradeData (docs/meta-upgrade-design.md 2장 표), PermanentUpgradeRegistry 1개, MetaRewardConfig 1개(기본값)
    /// - 아이콘은 패시브 버프 아이콘(Common_Icon_*Up) 재사용. 파일명은 ArtResourceBuilder.ICONS 접두사와 겹치지 않게 Permanent_* 로 둔다
    /// - GameScene 루트 "PermanentUpgradeApplier" (registry, players = 씬의 Left/Right PlayerStats)
    /// - GameOverManager.metaRewardConfig / GameOverUI.metaRewardLabel 연결은 GameOverBuilder 가 한다
    /// </summary>
    public static class PermanentUpgradeBuilder
    {
        public const string DIR = "Assets/prefab/Upgrade/Permanent";
        public const string REGISTRY_PATH = DIR + "/PermanentUpgradeRegistry.asset";
        public const string CONFIG_PATH = DIR + "/MetaRewardConfig.asset";
        private const string ICON_DIR = "Assets/Resources/Textuer/Art/Icon/Common_Icon_";
        private const string GAME_SCENE = "Assets/Scenes/GameScene.unity";
        private const string APPLIER_NAME = "PermanentUpgradeApplier";

        public const float COST_GROWTH = 1.5f;

        private struct Spec
        {
            public string id;
            public string file;
            public ValueType stat;
            public StatModifierMode mode;
            public float value;
            public int maxLevel;
            public int baseCost;
            public string icon;
        }

        private static readonly Spec[] Specs =
        {
            new Spec { id = "damage", file = "Damage", stat = ValueType.Damage, mode = StatModifierMode.Multiply, value = 0.02f, maxLevel = 10, baseCost = 20, icon = "DamageUp" },
            new Spec { id = "attack_speed", file = "AttackSpeed", stat = ValueType.AttackSpeed, mode = StatModifierMode.Multiply, value = 0.02f, maxLevel = 10, baseCost = 20, icon = "AttackSpeedUp" },
            new Spec { id = "range", file = "Range", stat = ValueType.Range, mode = StatModifierMode.Multiply, value = 0.04f, maxLevel = 5, baseCost = 20, icon = "RangeUp" },
            new Spec { id = "move_speed", file = "MoveSpeed", stat = ValueType.MoveSpeed, mode = StatModifierMode.Multiply, value = 0.02f, maxLevel = 5, baseCost = 20, icon = "SpeedUp" },
            new Spec { id = "hp", file = "HP", stat = ValueType.HP, mode = StatModifierMode.Multiply, value = 0.05f, maxLevel = 10, baseCost = 20, icon = "HPUp" },
            new Spec { id = "hp_regen", file = "HPRegen", stat = ValueType.HPRegen, mode = StatModifierMode.Add, value = 0.2f, maxLevel = 5, baseCost = 20, icon = "HPRegenUp" },
            new Spec { id = "pickup_range", file = "PickupRange", stat = ValueType.PickupRange, mode = StatModifierMode.Multiply, value = 0.05f, maxLevel = 5, baseCost = 20, icon = "PickupRangeUp" },
            new Spec { id = "duration", file = "Duration", stat = ValueType.Duration, mode = StatModifierMode.Multiply, value = 0.03f, maxLevel = 5, baseCost = 20, icon = "DurationUp" },
            new Spec { id = "projectile_count", file = "ProjectileCount", stat = ValueType.ProjectileCount, mode = StatModifierMode.Add, value = 1f, maxLevel = 1, baseCost = 300, icon = "ProjectileUp" },
        };

        [MenuItem("Tools/Builders/PermanentUpgrade")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var log = new StringBuilder();
            log.AppendLine("[PermanentUpgradeBuilder]");

            PermanentUpgradeRegistry registry = BuildAssets(log);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GAME_SCENE)
            {
                if (false == EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.AppendLine("[중단] 씬 저장이 취소되어 GameScene 배치를 건너뜁니다.");
                    return log.ToString();
                }
                scene = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Single);
            }

            GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == APPLIER_NAME);
            if (null == go)
            {
                go = new GameObject(APPLIER_NAME);
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            PermanentUpgradeApplier applier = go.GetComponent<PermanentUpgradeApplier>();
            if (null == applier)
            {
                applier = go.AddComponent<PermanentUpgradeApplier>();
            }

            PlayerStats[] players = Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include)
                .Where(p => p.gameObject.scene == scene)
                .OrderBy(p => (int)p.Side)
                .ToArray();

            var so = new SerializedObject(applier);
            so.FindProperty("registry").objectReferenceValue = registry;
            SerializedProperty playersProp = so.FindProperty("players");
            playersProp.arraySize = players.Length;
            for (int i = 0; i < players.Length; i++)
            {
                playersProp.GetArrayElementAtIndex(i).objectReferenceValue = players[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"GameScene/{APPLIER_NAME}: players {players.Length} ({string.Join(", ", players.Select(p => p.name + ":" + p.Side))})");

            Debug.Log(log.ToString());
            return log.ToString();
        }

        /// <summary>
        /// 강화 9종 + 레지스트리 + 보상 설정 (씬은 건드리지 않는다)
        /// </summary>
        public static PermanentUpgradeRegistry BuildAssets(StringBuilder _log)
        {
            EnsureFolder(DIR);

            var datas = new PermanentUpgradeData[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
            {
                Spec spec = Specs[i];
                string path = DIR + "/Permanent_" + spec.file + ".asset";
                PermanentUpgradeData data = GetOrCreate<PermanentUpgradeData>(path);
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(ICON_DIR + spec.icon + ".png");
                if (null == icon)
                {
                    _log?.AppendLine("[경고] 아이콘 없음: " + spec.icon);
                }
                data.Configure(spec.id, spec.stat, spec.mode, spec.value, spec.maxLevel, spec.baseCost, COST_GROWTH, "meta.upgrade." + spec.id, icon);
                EditorUtility.SetDirty(data);
                datas[i] = data;
            }

            PermanentUpgradeRegistry registry = GetOrCreate<PermanentUpgradeRegistry>(REGISTRY_PATH);
            registry.SetUpgrades(datas);
            EditorUtility.SetDirty(registry);

            // 계수는 인스펙터에서 조정하므로 새로 만들 때만 기본값을 넣는다
            MetaRewardConfig config = AssetDatabase.LoadAssetAtPath<MetaRewardConfig>(CONFIG_PATH);
            if (null == config)
            {
                config = GetOrCreate<MetaRewardConfig>(CONFIG_PATH);
                config.Configure(MetaRewardConfig.DEFAULT_WAVE_COEF, MetaRewardConfig.DEFAULT_KILL_COEF, MetaRewardConfig.DEFAULT_LEVEL_COEF,
                    MetaRewardConfig.DEFAULT_VICTORY_RATE, MetaRewardConfig.DEFAULT_DEFEAT_RATE);
                EditorUtility.SetDirty(config);
                _log?.AppendLine("MetaRewardConfig 생성 (기본값)");
            }

            AssetDatabase.SaveAssets();
            _log?.AppendLine($"에셋: {DIR} (강화 {datas.Length}종, 레지스트리, 보상 설정)");
            return registry;
        }

        public static PermanentUpgradeRegistry LoadRegistry()
        {
            PermanentUpgradeRegistry registry = AssetDatabase.LoadAssetAtPath<PermanentUpgradeRegistry>(REGISTRY_PATH);
            return null != registry ? registry : BuildAssets(null);
        }

        public static MetaRewardConfig LoadConfig()
        {
            MetaRewardConfig config = AssetDatabase.LoadAssetAtPath<MetaRewardConfig>(CONFIG_PATH);
            if (null == config)
            {
                BuildAssets(null);
                config = AssetDatabase.LoadAssetAtPath<MetaRewardConfig>(CONFIG_PATH);
            }
            return config;
        }

        private static T GetOrCreate<T>(string _path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(_path);
            if (null == asset)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, _path);
            }
            return asset;
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
