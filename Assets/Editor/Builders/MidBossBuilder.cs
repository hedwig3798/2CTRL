using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 중간 보스 6종(10·20분 × 숲/공장/폭풍)의 키워드 SO, MonsterData, 프리팹, 예고 스타일을 만들고
    /// DimensionData.midBosses 에 연결한다 (멱등). 외형은 기존 몬스터 아트 재사용 + 차원 테마색 (임시)
    /// </summary>
    public static class MidBossBuilder
    {
        private const string GOLEM_PATH = "Assets/prefab/Monster/Golem/Golem.prefab";
        private const string ZONE_PATH = "Assets/prefab/Monster/HazardZone/HazardZone.prefab";
        private const string PREFAB_DIR = "Assets/prefab/Monster/Boss";
        private const string DATA_DIR = "Assets/Resources/Data/Monster/Boss";
        private const string KEYWORD_DIR = "Assets/Resources/Data/Monster/Keywords/Boss";
        private const string DIM_DIR = "Assets/Resources/Data/Dimension";
        private const string MONSTER_ART = "Assets/Resources/Textuer/Art/Monster";
        private const string OVERRIDE_DIR = "Assets/prefab/Art/Anim/Override";
        private const string SAW_SPRITE = "Assets/Resources/Textuer/Art/Weapon/D2_Weapon_Saw_Blade.png";
        private const string URP_UNLIT_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string UNLIT_SHADER = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const float BOSS_SCALE = 3f;
        private const float BLADE_DIAMETER = 1.1f;

        private struct BossSpec
        {
            public string name;
            public string strip;
            public Color tint;
            public float speed;
            public float hp;
            public float contact;
            public MonsterKeyword[] keywords;
        }

        [MenuItem("Tools/Builders/MidBoss")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            EnsureFolder(PREFAB_DIR);
            EnsureFolder(DATA_DIR);
            EnsureFolder(KEYWORD_DIR);

            BossTelegraphStyle style = BuildTelegraphStyle(sb);
            var zone = AssetDatabase.LoadAssetAtPath<GameObject>(ZONE_PATH)?.GetComponent<HazardZone>();
            if (null == zone)
            {
                sb.AppendLine("경고: HazardZone 프리팹 없음 " + ZONE_PATH);
            }

            // 키워드 SO
            var trail = LoadOrCreate<HazardTrailKeyword>(KEYWORD_DIR + "/ForestBoss10_HazardTrail.asset");
            trail.interval = 0.5f; trail.radius = 1.2f; trail.duration = 5f;
            trail.tickInterval = 0.5f; trail.damagePerTick = 4f; trail.slowRate = 0.4f; trail.zonePrefab = zone;

            var blades = LoadOrCreate<OrbitBladesKeyword>(KEYWORD_DIR + "/FactoryBoss10_OrbitBlades.asset");
            blades.bladePrefab = BuildBladePrefab(sb);

            var pushPull = LoadOrCreate<PushPullKeyword>(KEYWORD_DIR + "/StormBoss10_PushPull.asset");
            pushPull.telegraphStyle = style;

            var jump = LoadOrCreate<JumpSlamKeyword>(KEYWORD_DIR + "/ForestBoss20_JumpSlam.asset");
            jump.rootZonePrefab = zone; jump.telegraphStyle = style;

            var bounce = LoadOrCreate<ScreenBounceKeyword>(KEYWORD_DIR + "/FactoryBoss20_ScreenBounce.asset");
            bounce.telegraphStyle = style;

            var ring = LoadOrCreate<RingJudgeKeyword>(KEYWORD_DIR + "/StormBoss20_RingJudge.asset");
            ring.telegraphStyle = style;

            foreach (Object k in new Object[] { trail, blades, pushPull, jump, bounce, ring })
            {
                EditorUtility.SetDirty(k);
            }

            var specs = new[]
            {
                new BossSpec { name = "ForestBoss10", strip = "D1_Monster_Golem", tint = new Color(0.55f, 1f, 0.5f, 1f),
                    speed = 0.7f, hp = 5500f, contact = 18f, keywords = new MonsterKeyword[] { trail } },
                new BossSpec { name = "FactoryBoss10", strip = "D2_Monster_Golem", tint = new Color(1f, 0.75f, 0.4f, 1f),
                    speed = 0.8f, hp = 5500f, contact = 18f, keywords = new MonsterKeyword[] { blades } },
                new BossSpec { name = "StormBoss10", strip = "D3_Monster_Wraith", tint = new Color(0.55f, 0.75f, 1f, 1f),
                    speed = 1.2f, hp = 5000f, contact = 15f, keywords = new MonsterKeyword[] { pushPull } },
                new BossSpec { name = "ForestBoss20", strip = "D1_Monster_Golem", tint = new Color(0.4f, 0.7f, 0.3f, 1f),
                    speed = 1.0f, hp = 17500f, contact = 25f, keywords = new MonsterKeyword[] { jump } },
                new BossSpec { name = "FactoryBoss20", strip = "D2_Monster_Bomber", tint = new Color(1f, 0.5f, 0.25f, 1f),
                    speed = 1.0f, hp = 17500f, contact = 25f, keywords = new MonsterKeyword[] { bounce } },
                new BossSpec { name = "StormBoss20", strip = "D3_Monster_Shaman", tint = new Color(0.75f, 0.55f, 1f, 1f),
                    speed = 0.8f, hp = 16000f, contact = 20f, keywords = new MonsterKeyword[] { ring } },
            };

            int playerMask = (1 << LayerMask.NameToLayer("RPlayer")) | (1 << LayerMask.NameToLayer("LPlayer"));
            var prefabs = new System.Collections.Generic.Dictionary<string, Spawnable>();
            foreach (BossSpec spec in specs)
            {
                var data = LoadOrCreate<MonsterData>(DATA_DIR + "/" + spec.name + "Data.asset");
                data.baseMoveSpeed = spec.speed;
                data.targetLayer = playerMask;
                data.keywords = spec.keywords;
                data.expValue = 0f;
                EditorUtility.SetDirty(data);

                prefabs[spec.name] = BuildBoss(spec, data, sb);
            }

            // 차원 연결
            foreach (string dim in new[] { "Forest", "Factory", "Storm" })
            {
                var dd = AssetDatabase.LoadAssetAtPath<DimensionData>(DIM_DIR + "/" + dim + ".asset");
                if (null == dd)
                {
                    sb.AppendLine("경고: DimensionData 없음 " + dim);
                    continue;
                }
                var so = new SerializedObject(dd);
                SerializedProperty arr = so.FindProperty("midBosses");
                arr.arraySize = 2;
                SetEntry(arr.GetArrayElementAtIndex(0), prefabs[dim + "Boss10"], 600f, 3f, 0.7f, 60f, 12);
                SetEntry(arr.GetArrayElementAtIndex(1), prefabs[dim + "Boss20"], 1200f, 3f, 0.6f, 150f, 20);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dd);
                sb.AppendLine(dim + ".midBosses = [" + dim + "Boss10, " + dim + "Boss20]");
            }

            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static void SetEntry(SerializedProperty _e, Spawnable _prefab, float _time, float _lead, float _mul, float _exp, int _gems)
        {
            _e.FindPropertyRelative("prefab").objectReferenceValue = _prefab;
            _e.FindPropertyRelative("spawnTime").floatValue = _time;
            _e.FindPropertyRelative("warningLead").floatValue = _lead;
            _e.FindPropertyRelative("spawnCountMultiplier").floatValue = _mul;
            _e.FindPropertyRelative("expTotal").floatValue = _exp;
            _e.FindPropertyRelative("gemCount").intValue = _gems;
        }

        private static BossTelegraphStyle BuildTelegraphStyle(StringBuilder _sb)
        {
            var style = LoadOrCreate<BossTelegraphStyle>(DATA_DIR + "/BossTelegraphStyle.asset");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(URP_UNLIT_MAT);
            if (null == mat)
            {
                string matPath = DATA_DIR + "/M_BossTelegraph.mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (null == mat)
                {
                    mat = new Material(Shader.Find(UNLIT_SHADER));
                    AssetDatabase.CreateAsset(mat, matPath);
                }
            }
            style.material = mat;
            style.sortingLayerName = "attack";
            EditorUtility.SetDirty(style);
            _sb.AppendLine("telegraphStyle: mat=" + (null != mat ? AssetDatabase.GetAssetPath(mat) : "null"));
            return style;
        }

        private static GameObject BuildBladePrefab(StringBuilder _sb)
        {
            string dir = PREFAB_DIR + "/FactoryBoss10";
            EnsureFolder(dir);
            string path = dir + "/SawBlade.prefab";

            var go = new GameObject("SawBlade");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(SAW_SPRITE).OfType<Sprite>().OrderBy(s => s.name).FirstOrDefault();
                sr.sprite = sprite;
                sr.sortingLayerName = "Player";
                sr.sortingOrder = 2;
                float size = null != sprite ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
                go.transform.localScale = Vector3.one * (BLADE_DIAMETER / size);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
                _sb.AppendLine("blade: " + path + " sprite=" + (null != sprite ? sprite.name : "null"));
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Spawnable BuildBoss(BossSpec _spec, MonsterData _data, StringBuilder _sb)
        {
            string dir = PREFAB_DIR + "/" + _spec.name;
            EnsureFolder(dir);
            string path = dir + "/" + _spec.name + ".prefab";

            GameObject root = PrefabUtility.LoadPrefabContents(GOLEM_PATH);
            try
            {
                root.name = _spec.name;
                root.transform.localScale = Vector3.one * BOSS_SCALE;

                foreach (DespawnOutOfCamera d in root.GetComponentsInChildren<DespawnOutOfCamera>(true))
                {
                    Object.DestroyImmediate(d);
                }

                // 외형 (임시): 기존 몬스터 아트 + 테마색
                Transform visual = root.transform.Find("visual");
                var sr = visual.GetComponent<SpriteRenderer>();
                // 컨트롤러를 먼저 바꾼다 (나중에 바꾸면 Animator 리바인드가 이전 스프라이트로 되돌린다)
                var anim = visual.GetComponent<Animator>();
                var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OVERRIDE_DIR + "/" + _spec.strip + ".overrideController");
                if (null != anim && null != aoc)
                {
                    anim.runtimeAnimatorController = aoc;
                }
                Sprite first = AssetDatabase.LoadAllAssetsAtPath(MONSTER_ART + "/" + _spec.strip + "_Walk.png")
                    .OfType<Sprite>().OrderBy(s => s.name).FirstOrDefault();
                if (null != first)
                {
                    sr.sprite = first;
                }
                sr.color = _spec.tint;

                var brain = root.GetComponent<MonsterBrain>();
                var bso = new SerializedObject(brain);
                bso.FindProperty("data").objectReferenceValue = _data;
                bso.FindProperty("spriteRenderer").objectReferenceValue = sr;
                bso.ApplyModifiedPropertiesWithoutUndo();

                Transform pipeT = root.transform.Find("damagePipeline");
                var health = pipeT.GetComponent<HealthSystem>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHP").floatValue = _spec.hp;
                hso.FindProperty("currHP").floatValue = _spec.hp;
                hso.ApplyModifiedPropertiesWithoutUndo();

                var contact = root.transform.Find("attack").GetComponent<ContactDamage>();
                contact.owner = pipeT.GetComponent<DamagePipeline>();
                contact.baseDamage = _spec.contact;
                contact.coolTime = 0.5f;

                var boss = root.GetComponent<MidBoss>();
                if (null == boss)
                {
                    boss = root.AddComponent<MidBoss>();
                }
                var mso = new SerializedObject(boss);
                mso.FindProperty("displayNameKey").stringValue = "boss." + _spec.name;
                mso.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                _sb.AppendLine(_spec.name + ": hp=" + _spec.hp + " speed=" + _spec.speed + " contact=" + _spec.contact
                    + " art=" + _spec.strip + (null == aoc ? "(animator 없음)" : ""));
                return saved.GetComponent<Spawnable>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T LoadOrCreate<T>(string _path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(_path);
            if (null == asset)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, _path);
            }
            return asset;
        }

        private static void EnsureFolder(string _path)
        {
            if (AssetDatabase.IsValidFolder(_path))
            {
                return;
            }
            int slash = _path.LastIndexOf('/');
            string parent = _path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, _path.Substring(slash + 1));
        }
    }
}
