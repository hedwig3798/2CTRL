using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 몬스터 키워드 SO, MonsterData, HazardZone/몬스터 프리팹, 웨이브 데이터를 생성·갱신한다 (멱등)
    /// </summary>
    public static class MonsterKeywordBuilder
    {
        private const string DATA_DIR = "Assets/Resources/Data/Monster";
        private const string KEYWORD_DIR = "Assets/Resources/Data/Monster/Keywords";
        private const string PREFAB_DIR = "Assets/prefab/Monster";
        private const string WAVE_DIR = "Assets/Resources/Script/SpawnSystem/WaveData";
        private const string BAT_PATH = "Assets/prefab/Monster/bat/bat.prefab";
        private const string DUMMY_PATH = "Assets/prefab/Monster/Dummy/Dummy.prefab";
        private const string CIRCLE_PATH = "Assets/Resources/Image/Dummy/DummyCircle.png";
        private const string SCENE_PATH = "Assets/Scenes/SampleScene.unity";

        private struct MonsterSpec
        {
            public string name;
            public float hp;
            public float speed;
            public float contactDamage;
            public float scale;
            public Color color;
            public MonsterKeyword[] keywords;
        }

        [MenuItem("Tools/Builders/MonsterKeyword")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            EnsureFolder(DATA_DIR);
            EnsureFolder(KEYWORD_DIR);

            // 1. 키워드 SO
            var dash = LoadOrCreate<DashKeyword>(KEYWORD_DIR + "/Dash.asset");
            dash.triggerRange = 3f; dash.telegraphTime = 0.6f; dash.dashSpeed = 8f; dash.dashDuration = 0.35f; dash.cooldown = 3f;

            var predict = LoadOrCreate<PredictKeyword>(KEYWORD_DIR + "/Predict.asset");
            predict.leadTime = 0.5f; predict.maxLeadDistance = 2.5f; predict.velocitySmoothing = 8f;

            var teleport = LoadOrCreate<TeleportKeyword>(KEYWORD_DIR + "/Teleport.asset");
            teleport.minDistance = 4.5f; teleport.telegraphTime = 0.6f; teleport.appearRadius = new Vector2(1.5f, 2.5f);
            teleport.postDelay = 0.6f; teleport.cooldown = 5f;

            var orbit = LoadOrCreate<OrbitKeyword>(KEYWORD_DIR + "/Orbit.asset");
            orbit.startRadius = 4f; orbit.minRadius = 2f; orbit.shrinkSpeed = 0.25f; orbit.angularSpeed = 50f;

            var selfDestruct = LoadOrCreate<SelfDestructKeyword>(KEYWORD_DIR + "/SelfDestruct.asset");
            selfDestruct.triggerRange = 1.2f; selfDestruct.telegraphTime = 0.8f; selfDestruct.radius = 1.5f;
            selfDestruct.damage = 3f; selfDestruct.knockbackForce = 6f; selfDestruct.explodeOnDeath = true; selfDestruct.inflateScale = 1.5f;

            var hazardTrail = LoadOrCreate<HazardTrailKeyword>(KEYWORD_DIR + "/HazardTrail.asset");
            hazardTrail.interval = 1.2f; hazardTrail.radius = 0.6f; hazardTrail.duration = 2.5f;
            hazardTrail.tickInterval = 0.5f; hazardTrail.damagePerTick = 1f;

            var summoner = LoadOrCreate<SummonerKeyword>(KEYWORD_DIR + "/Summoner.asset");
            summoner.count = 2; summoner.maxAlive = 4; summoner.spawnRadius = 1.2f; summoner.telegraphTime = 0.8f;
            summoner.cooldown = 6f; summoner.keepDistance = 4f; summoner.keepDistanceTolerance = 0.5f;

            var split = LoadOrCreate<SplitKeyword>(KEYWORD_DIR + "/Split.asset");
            split.count = 2; split.scatterRadius = 0.6f;

            var revive = LoadOrCreate<ReviveKeyword>(KEYWORD_DIR + "/Revive.asset");
            revive.reviveCount = 1; revive.reviveDelay = 1.5f; revive.hpRatio = 0.5f;

            var kbResist = LoadOrCreate<KnockbackResistKeyword>(KEYWORD_DIR + "/KnockbackResist.asset");
            kbResist.resistance = 1f;
            sb.AppendLine("keywords: 10");

            // 2. HazardZone 프리팹
            HazardZone zone = BuildHazardZone();
            hazardTrail.zonePrefab = zone;
            sb.AppendLine("hazardZone: " + AssetDatabase.GetAssetPath(zone));

            // 3. 몬스터 스펙
            var specs = new List<MonsterSpec>
            {
                new MonsterSpec { name = "MiniSlime", hp = 5f, speed = 1.4f, contactDamage = 1f, scale = 0.6f,
                    color = new Color(0.45f, 0.95f, 0.45f, 1f), keywords = new MonsterKeyword[0] },
                new MonsterSpec { name = "Slime", hp = 20f, speed = 1f, contactDamage = 1f, scale = 1f,
                    color = new Color(0.3f, 0.85f, 0.3f, 1f), keywords = new MonsterKeyword[] { predict, split } },
                new MonsterSpec { name = "Bomber", hp = 12f, speed = 1.3f, contactDamage = 1f, scale = 1f,
                    color = new Color(1f, 0.3f, 0.3f, 1f), keywords = new MonsterKeyword[] { dash, selfDestruct, revive } },
                new MonsterSpec { name = "Wraith", hp = 18f, speed = 1.1f, contactDamage = 1f, scale = 1f,
                    color = new Color(0.7f, 0.4f, 1f, 0.55f), keywords = new MonsterKeyword[] { teleport, hazardTrail } },
                new MonsterSpec { name = "Shaman", hp = 25f, speed = 0.9f, contactDamage = 1f, scale = 1.1f,
                    color = new Color(0.35f, 0.55f, 1f, 1f), keywords = new MonsterKeyword[] { orbit, summoner } },
                new MonsterSpec { name = "Golem", hp = 80f, speed = 0.6f, contactDamage = 2f, scale = 1.8f,
                    color = new Color(0.6f, 0.6f, 0.6f, 1f), keywords = new MonsterKeyword[] { kbResist, revive } },
            };

            int playerMask = (1 << LayerMask.NameToLayer("RPlayer")) | (1 << LayerMask.NameToLayer("LPlayer"));
            var prefabs = new Dictionary<string, Spawnable>();
            foreach (MonsterSpec spec in specs)
            {
                var data = LoadOrCreate<MonsterData>(DATA_DIR + "/" + spec.name + "Data.asset");
                data.baseMoveSpeed = spec.speed;
                data.targetLayer = playerMask;
                data.keywords = spec.keywords;
                EditorUtility.SetDirty(data);

                prefabs[spec.name] = BuildMonster(spec, data);
                sb.AppendLine(spec.name + ": hp=" + spec.hp + " speed=" + spec.speed + " dmg=" + spec.contactDamage);
            }

            split.childPrefab = prefabs["MiniSlime"];
            summoner.minionPrefab = prefabs["MiniSlime"];

            foreach (var k in new MonsterKeyword[] { dash, predict, teleport, orbit, selfDestruct, hazardTrail, summoner, split, revive, kbResist })
            {
                EditorUtility.SetDirty(k);
            }

            // 4. 웨이브
            sb.AppendLine(BuildWaves(prefabs));

            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static HazardZone BuildHazardZone()
        {
            string dir = PREFAB_DIR + "/HazardZone";
            EnsureFolder(dir);
            string path = dir + "/HazardZone.prefab";

            var go = new GameObject("HazardZone");
            var sr = go.AddComponent<SpriteRenderer>();
            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_PATH);
            sr.sprite = circle;
            sr.color = new Color(0.6f, 0.2f, 0.9f, 0.45f);
            sr.sortingOrder = -1;
            var zone = go.AddComponent<HazardZone>();
            if (null != circle)
            {
                var so = new SerializedObject(zone);
                so.FindProperty("unitRadius").floatValue = Mathf.Max(0.01f, circle.bounds.extents.x);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved.GetComponent<HazardZone>();
        }

        private static Spawnable BuildMonster(MonsterSpec _spec, MonsterData _data)
        {
            string dir = PREFAB_DIR + "/" + _spec.name;
            EnsureFolder(dir);
            string path = dir + "/" + _spec.name + ".prefab";

            GameObject root = PrefabUtility.LoadPrefabContents(BAT_PATH);
            try
            {
                root.name = _spec.name;
                root.transform.localScale = Vector3.one * _spec.scale;

                var straight = root.GetComponent<Straight>();
                if (null != straight)
                {
                    Object.DestroyImmediate(straight);
                }

                SpriteRenderer sr = root.transform.Find("visual").GetComponent<SpriteRenderer>();
                sr.color = _spec.color;

                var brain = root.GetComponent<MonsterBrain>();
                if (null == brain)
                {
                    brain = root.AddComponent<MonsterBrain>();
                }
                var bso = new SerializedObject(brain);
                bso.FindProperty("data").objectReferenceValue = _data;
                bso.FindProperty("spriteRenderer").objectReferenceValue = sr;
                bso.FindProperty("isDefaultLeft").boolValue = true;
                bso.ApplyModifiedPropertiesWithoutUndo();

                Transform pipeT = root.transform.Find("damagePipeline");
                var health = pipeT.GetComponent<HealthSystem>();
                var hso = new SerializedObject(health);
                hso.FindProperty("maxHP").floatValue = _spec.hp;
                hso.FindProperty("currHP").floatValue = _spec.hp;
                hso.ApplyModifiedPropertiesWithoutUndo();

                Transform attackT = root.transform.Find("attack");
                var contact = attackT.GetComponent<ContactDamage>();
                contact.owner = pipeT.GetComponent<DamagePipeline>();
                contact.baseDamage = _spec.contactDamage;

                // 데미지 텍스트: Dummy 설정을 복사
                Transform binderT = root.transform.Find("LocalEventBiner");
                var textBinder = binderT.GetComponent<DamageTextBinder>();
                if (null == textBinder)
                {
                    textBinder = binderT.gameObject.AddComponent<DamageTextBinder>();
                }
                GameObject dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DUMMY_PATH);
                var dummyBinder = null != dummy ? dummy.GetComponentInChildren<DamageTextBinder>(true) : null;
                if (null != dummyBinder)
                {
                    EditorUtility.CopySerialized(dummyBinder, textBinder);
                }
                var tso = new SerializedObject(textBinder);
                tso.FindProperty("target").objectReferenceValue = health;
                tso.FindProperty("spawnPoint").objectReferenceValue = root.transform;
                tso.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                return saved.GetComponent<Spawnable>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string BuildWaves(Dictionary<string, Spawnable> _prefabs)
        {
            var sb = new StringBuilder();
            var bat = AssetDatabase.LoadAssetAtPath<GameObject>(BAT_PATH).GetComponent<Spawnable>();
            Vector2 range = new Vector2(3f, 4f);

            // 1_1: 기존 항목 유지 + 약한 몬스터
            var w1 = AssetDatabase.LoadAssetAtPath<WaveData>(WAVE_DIR + "/1_1.asset");
            var list1 = new List<SpawnData>(w1.spwanArray ?? new SpawnData[0]);
            AddIfMissing(list1, _prefabs["MiniSlime"], range, 3f);
            AddIfMissing(list1, _prefabs["Slime"], range, 6f);
            w1.spwanArray = list1.ToArray();
            EditorUtility.SetDirty(w1);
            sb.AppendLine("1_1: " + Describe(w1));

            // 1_2: 중간 난이도
            var w2 = LoadOrCreate<WaveData>(WAVE_DIR + "/1_2.asset");
            w2.waveID = "1_2";
            w2.time = 100f;
            w2.spwanArray = new SpawnData[]
            {
                Make(bat, range, 1f),
                Make(_prefabs["Slime"], range, 4f),
                Make(_prefabs["MiniSlime"], range, 3f),
                Make(_prefabs["Bomber"], range, 6f),
                Make(_prefabs["Wraith"], range, 8f),
            };
            EditorUtility.SetDirty(w2);
            sb.AppendLine("1_2: " + Describe(w2));

            // 1_3: 후반
            var w3 = LoadOrCreate<WaveData>(WAVE_DIR + "/1_3.asset");
            w3.waveID = "1_3";
            w3.time = 300f;
            w3.spwanArray = new SpawnData[]
            {
                Make(bat, range, 1f),
                Make(_prefabs["Slime"], range, 4f),
                Make(_prefabs["Bomber"], range, 5f),
                Make(_prefabs["Wraith"], range, 7f),
                Make(_prefabs["Shaman"], range, 10f),
                Make(_prefabs["Golem"], range, 15f),
            };
            EditorUtility.SetDirty(w3);
            sb.AppendLine("1_3: " + Describe(w3));

            // 씬 Spawner 에 웨이브 연결
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }
            var waves = new WaveData[] { w1, w2, w3 };
            foreach (var spawner in Object.FindObjectsByType<Spawner>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(spawner);
                var arr = so.FindProperty("waveArray");
                arr.arraySize = waves.Length;
                for (int i = 0; i < waves.Length; i++)
                {
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spawner);
                sb.AppendLine("spawner " + spawner.name + " waves=1_1,1_2,1_3");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return sb.ToString();
        }

        private static void AddIfMissing(List<SpawnData> _list, Spawnable _prefab, Vector2 _range, float _interval)
        {
            foreach (SpawnData d in _list)
            {
                if (d.spawnObject == _prefab)
                {
                    return;
                }
            }
            _list.Add(Make(_prefab, _range, _interval));
        }

        private static SpawnData Make(Spawnable _prefab, Vector2 _range, float _interval)
        {
            return new SpawnData
            {
                spawnObject = _prefab,
                count = new Vector2Int(1, 1),
                spawnRange = _range,
                spawnInterval = _interval,
            };
        }

        private static string Describe(WaveData _w)
        {
            var sb = new StringBuilder();
            sb.Append("time=" + _w.time + " [");
            foreach (SpawnData d in _w.spwanArray)
            {
                sb.Append((null != d.spawnObject ? d.spawnObject.name : "null") + "@" + d.spawnInterval + "s ");
            }
            sb.Append("]");
            return sb.ToString();
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
            string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
        }
    }
}
