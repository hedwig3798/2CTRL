using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 30분(1800초) 웨이브 데이터 12개를 생성·갱신하고 씬의 모든 Spawner 에 연결한다 (멱등)
    /// - 웨이브당 150초. 마지막 웨이브는 30분 이후에도 계속 유지된다 (Spawner 동작)
    /// - spawnInterval 은 Spawner 하나 기준 (씬에 좌/우 Spawner 가 각각 돈다)
    /// - MonsterKeywordBuilder 를 다시 실행하면 Spawner 웨이브가 1_1~1_3 으로 돌아가므로 이 빌더를 다시 실행해야 한다
    /// </summary>
    public static class Wave30Builder
    {
        private const string WAVE_DIR = "Assets/Resources/Script/SpawnSystem/WaveData/Survival30";
        private const string MONSTER_DIR = "Assets/prefab/Monster";
        private const string SCENE_PATH = "Assets/Scenes/SampleScene.unity";
        private const float WAVE_TIME = 150f;

        private struct Entry
        {
            public string monster;
            public float interval;

            public Entry(string _monster, float _interval)
            {
                monster = _monster;
                interval = _interval;
            }
        }

        [MenuItem("Tools/Builders/Wave30")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            EnsureFolder(WAVE_DIR);

            var prefabs = new Dictionary<string, Spawnable>();
            foreach (string name in new[] { "bat", "MiniSlime", "Slime", "Bomber", "Wraith", "Shaman", "Golem" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(MONSTER_DIR + "/" + name + "/" + name + ".prefab");
                Spawnable sa = null != go ? go.GetComponent<Spawnable>() : null;
                if (null == sa)
                {
                    return "[Wave30Builder] Spawnable 프리팹 없음: " + name;
                }
                prefabs[name] = sa;
            }

            // 구간별 구성 (분:초는 시작 시각)
            Entry[][] table =
            {
                // 00:00 도입: 박쥐와 미니 슬라임만
                new[] { E("bat", 1.4f), E("MiniSlime", 3.5f) },
                // 02:30 슬라임(분열) 등장
                new[] { E("bat", 1.1f), E("MiniSlime", 3f), E("Slime", 6f) },
                // 05:00 레이스(순간이동·장판) 등장
                new[] { E("bat", 1f), E("Slime", 4.5f), E("Wraith", 8f) },
                // 07:30 봄버(돌진·자폭) 등장
                new[] { E("bat", 0.9f), E("Slime", 4f), E("Wraith", 6.5f), E("Bomber", 8f) },
                // 10:00 샤먼(선회·소환) 등장
                new[] { E("bat", 0.8f), E("Slime", 3.5f), E("Wraith", 5.5f), E("Bomber", 6f), E("Shaman", 15f) },
                // 12:30 골렘 첫 등장, 잡몹은 잠시 숨 고르기
                new[] { E("bat", 0.9f), E("Slime", 3.5f), E("Bomber", 5.5f), E("Golem", 30f) },
                // 15:00 중반 전체 구성
                new[] { E("bat", 0.7f), E("Slime", 3f), E("Wraith", 4.5f), E("Bomber", 5f), E("Shaman", 12f) },
                // 17:30 골렘 상시 등장
                new[] { E("bat", 0.6f), E("Slime", 2.5f), E("Wraith", 4f), E("Bomber", 4.5f), E("Shaman", 10f), E("Golem", 25f) },
                // 20:00 물량 러시: 약한 몹 대량 + 봄버
                new[] { E("bat", 0.3f), E("MiniSlime", 0.8f), E("Bomber", 3f) },
                // 22:30 후반
                new[] { E("bat", 0.5f), E("Slime", 2f), E("Wraith", 3.5f), E("Bomber", 3.5f), E("Shaman", 8f), E("Golem", 18f) },
                // 25:00 후반 강화
                new[] { E("bat", 0.45f), E("Slime", 1.7f), E("Wraith", 3f), E("Bomber", 3f), E("Shaman", 7f), E("Golem", 14f) },
                // 27:30 최종 구간 (30분 이후에도 유지)
                new[] { E("bat", 0.4f), E("Slime", 1.4f), E("Wraith", 2.5f), E("Bomber", 2.5f), E("Shaman", 6f), E("Golem", 10f) },
            };

            Vector2 range = new Vector2(3f, 4f);
            var waves = new WaveData[table.Length];
            for (int i = 0; i < table.Length; i++)
            {
                string id = "S30_" + (i + 1).ToString("00");
                var w = LoadOrCreate<WaveData>(WAVE_DIR + "/" + id + ".asset");
                w.waveID = id;
                w.time = WAVE_TIME;
                w.spwanArray = new SpawnData[table[i].Length];
                for (int j = 0; j < table[i].Length; j++)
                {
                    w.spwanArray[j] = Make(prefabs[table[i][j].monster], range, table[i][j].interval);
                }
                EditorUtility.SetDirty(w);
                waves[i] = w;
                sb.AppendLine(id + " @" + FormatTime(i * WAVE_TIME) + ": " + Describe(w));
            }
            sb.AppendLine("total=" + FormatTime(table.Length * WAVE_TIME));

            // 씬 Spawner 에 웨이브 연결
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }
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
                sb.AppendLine("spawner " + spawner.name + " waves=S30_01~S30_" + waves.Length.ToString("00"));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static Entry E(string _monster, float _interval)
        {
            return new Entry(_monster, _interval);
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

        private static string FormatTime(float _seconds)
        {
            int s = Mathf.RoundToInt(_seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
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
