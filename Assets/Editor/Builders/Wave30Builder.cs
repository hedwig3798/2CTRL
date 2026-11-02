using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 차원별 30분(1800초) 웨이브 데이터 12개씩을 생성·갱신한다 (멱등)
    /// - 경로: WaveData/Dimension/(Forest|Factory|Storm)/(차원)_01~12
    /// - 웨이브당 150초. 마지막 웨이브는 30분 이후에도 계속 유지된다 (Spawner 동작)
    /// - spawnInterval 은 Spawner 하나 기준. 강도 곡선은 기존 Survival30(S30) 을 따른다 (20:00 물량 러시, 27:30 최종)
    /// - 씬 Spawner 는 건드리지 않는다. 웨이브는 DimensionData.waves 로 연결되고 DimensionApplier 가 상대 스포너에 넣는다
    /// - DimensionData 가 이미 있으면 waves 도 연결한다 (DimensionDataBuilder 가 이 빌더를 먼저 호출)
    /// </summary>
    public static class Wave30Builder
    {
        public const string WAVE_ROOT = "Assets/Resources/Script/SpawnSystem/WaveData/Dimension";
        private const string MONSTER_DIR = "Assets/prefab/Monster";
        private const string DIMENSION_DIR = "Assets/Resources/Data/Dimension";
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

        /// <summary>
        /// 차원 이름 (DimensionDataBuilder 와 같은 이름)
        /// </summary>
        public static readonly string[] DIMENSIONS = { "Forest", "Factory", "Storm" };

        [MenuItem("Tools/Builders/Wave30")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Wave30Builder]");

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

            foreach (string dim in DIMENSIONS)
            {
                WaveData[] waves = BuildDimension(dim, Table(dim), prefabs, sb);
                var data = AssetDatabase.LoadAssetAtPath<DimensionData>(DIMENSION_DIR + "/" + dim + ".asset");
                if (null != data)
                {
                    var so = new SerializedObject(data);
                    var arr = so.FindProperty("waves");
                    arr.arraySize = waves.Length;
                    for (int i = 0; i < waves.Length; i++)
                    {
                        arr.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(data);
                    sb.AppendLine(dim + ".asset waves 연결 " + waves.Length);
                }
            }

            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>
        /// 차원 웨이브 에셋 (없으면 빈 배열). DimensionDataBuilder 에서 사용
        /// </summary>
        public static WaveData[] LoadWaves(string _dimension)
        {
            var list = new List<WaveData>();
            for (int i = 1; i <= 12; i++)
            {
                var w = AssetDatabase.LoadAssetAtPath<WaveData>(WAVE_ROOT + "/" + _dimension + "/" + _dimension + "_" + i.ToString("00") + ".asset");
                if (null != w)
                {
                    list.Add(w);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 차원별 구성 (줄 = 150초 구간, 주석은 시작 시각)
        /// </summary>
        private static Entry[][] Table(string _dimension)
        {
            switch (_dimension)
            {
                // 숲: 박쥐·미니 슬라임 물량 + 슬라임(분열) + 골렘
                case "Forest":
                    return new[]
                    {
                        new[] { E("bat", 1.4f), E("MiniSlime", 3.5f) },                                    // 00:00
                        new[] { E("bat", 1.1f), E("MiniSlime", 3f), E("Slime", 6f) },                      // 02:30
                        new[] { E("bat", 1f), E("MiniSlime", 2.5f), E("Slime", 4.5f) },                    // 05:00
                        new[] { E("bat", 0.9f), E("MiniSlime", 2.2f), E("Slime", 4f) },                    // 07:30
                        new[] { E("bat", 0.8f), E("MiniSlime", 2f), E("Slime", 3.5f), E("Golem", 30f) },   // 10:00 골렘 등장
                        new[] { E("bat", 0.9f), E("Slime", 3.5f), E("Golem", 25f) },                       // 12:30 숨 고르기
                        new[] { E("bat", 0.7f), E("MiniSlime", 1.6f), E("Slime", 3f) },                    // 15:00
                        new[] { E("bat", 0.6f), E("MiniSlime", 1.4f), E("Slime", 2.5f), E("Golem", 20f) }, // 17:30
                        new[] { E("bat", 0.3f), E("MiniSlime", 0.8f) },                                    // 20:00 물량 러시
                        new[] { E("bat", 0.5f), E("MiniSlime", 1.2f), E("Slime", 2f), E("Golem", 15f) },   // 22:30
                        new[] { E("bat", 0.45f), E("MiniSlime", 1f), E("Slime", 1.7f), E("Golem", 12f) },  // 25:00
                        new[] { E("bat", 0.4f), E("MiniSlime", 0.9f), E("Slime", 1.4f), E("Golem", 9f) },  // 27:30 최종
                    };
                // 공장: 슬라임이 기본 물량, 봄버(돌진·자폭) 압박, 골렘
                case "Factory":
                    return new[]
                    {
                        new[] { E("Slime", 2.5f) },                                      // 00:00
                        new[] { E("Slime", 2.2f), E("Bomber", 10f) },                    // 02:30 봄버 등장
                        new[] { E("Slime", 2f), E("Bomber", 8f) },                       // 05:00
                        new[] { E("Slime", 1.8f), E("Bomber", 6.5f) },                   // 07:30
                        new[] { E("Slime", 1.6f), E("Bomber", 5.5f), E("Golem", 30f) },  // 10:00 골렘 등장
                        new[] { E("Slime", 1.8f), E("Bomber", 5f), E("Golem", 25f) },    // 12:30 숨 고르기
                        new[] { E("Slime", 1.4f), E("Bomber", 4.5f), E("Golem", 22f) },  // 15:00
                        new[] { E("Slime", 1.2f), E("Bomber", 4f), E("Golem", 18f) },    // 17:30
                        new[] { E("Slime", 0.8f), E("Bomber", 2.5f) },                   // 20:00 물량 러시
                        new[] { E("Slime", 1f), E("Bomber", 3f), E("Golem", 14f) },      // 22:30
                        new[] { E("Slime", 0.85f), E("Bomber", 2.7f), E("Golem", 12f) }, // 25:00
                        new[] { E("Slime", 0.7f), E("Bomber", 2.2f), E("Golem", 9f) },   // 27:30 최종
                    };
                // 폭풍: 박쥐 물량 + 레이스(순간이동·장판) + 샤먼(선회·소환)
                default:
                    return new[]
                    {
                        new[] { E("bat", 1.4f) },                                        // 00:00
                        new[] { E("bat", 1.1f), E("Wraith", 10f) },                      // 02:30 레이스 등장
                        new[] { E("bat", 1f), E("Wraith", 8f) },                         // 05:00
                        new[] { E("bat", 0.9f), E("Wraith", 6.5f) },                     // 07:30
                        new[] { E("bat", 0.8f), E("Wraith", 5.5f), E("Shaman", 15f) },   // 10:00 샤먼 등장
                        new[] { E("bat", 0.9f), E("Wraith", 5f), E("Shaman", 14f) },     // 12:30 숨 고르기
                        new[] { E("bat", 0.7f), E("Wraith", 4.5f), E("Shaman", 12f) },   // 15:00
                        new[] { E("bat", 0.6f), E("Wraith", 4f), E("Shaman", 10f) },     // 17:30
                        new[] { E("bat", 0.3f), E("Wraith", 3f) },                       // 20:00 물량 러시
                        new[] { E("bat", 0.5f), E("Wraith", 3.5f), E("Shaman", 8f) },    // 22:30
                        new[] { E("bat", 0.45f), E("Wraith", 3f), E("Shaman", 7f) },     // 25:00
                        new[] { E("bat", 0.4f), E("Wraith", 2.5f), E("Shaman", 6f) },    // 27:30 최종
                    };
            }
        }

        private static WaveData[] BuildDimension(string _dimension, Entry[][] _table, Dictionary<string, Spawnable> _prefabs, StringBuilder _sb)
        {
            string dir = WAVE_ROOT + "/" + _dimension;
            EnsureFolder(dir);

            Vector2 range = new Vector2(3f, 4f);
            var waves = new WaveData[_table.Length];
            for (int i = 0; i < _table.Length; i++)
            {
                string id = _dimension + "_" + (i + 1).ToString("00");
                var w = LoadOrCreate<WaveData>(dir + "/" + id + ".asset");
                w.waveID = id;
                w.time = WAVE_TIME;
                w.spwanArray = new SpawnData[_table[i].Length];
                for (int j = 0; j < _table[i].Length; j++)
                {
                    w.spwanArray[j] = Make(_prefabs[_table[i][j].monster], range, _table[i][j].interval);
                }
                EditorUtility.SetDirty(w);
                waves[i] = w;
                _sb.AppendLine(id + " @" + FormatTime(i * WAVE_TIME) + ": " + Describe(w));
            }
            _sb.AppendLine(_dimension + " total=" + FormatTime(_table.Length * WAVE_TIME));
            return waves;
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
