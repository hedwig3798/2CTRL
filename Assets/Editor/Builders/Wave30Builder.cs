using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 차원별 30분 서바이벌 웨이브 14개씩을 생성·갱신한다 (멱등). 수치는 docs/balance-design.md 7장 / 7.2장 (진영당 Spawner 1개 기준)
    /// - 경로: WaveData/Dimension/(Forest|Factory|Storm)/(차원)_01~14 (예전 에셋은 지우지 않는다)
    /// - 웨이브 길이 / 데미지 배율 / 속도 배율은 7장 표 (모든 차원 공통), HP 배율과 몬스터별 spawnInterval 은 7.2장 차원별 표
    /// - 스폰 거리는 모두 11~13. 마지막 웨이브는 길이가 끝나도 계속 유지된다 (Spawner 동작)
    /// - 씬 Spawner 는 건드리지 않는다. 웨이브는 DimensionData.waves 로 연결되고 DimensionApplier 가 상대 스포너에 넣는다
    /// - DimensionData 가 이미 있으면 waves 도 연결한다 (DimensionDataBuilder 가 이 빌더를 먼저 호출하고 LoadWaves 로 다시 연결)
    /// </summary>
    public static class Wave30Builder
    {
        public const string WAVE_ROOT = "Assets/Resources/Script/SpawnSystem/WaveData/Dimension";
        private const string MONSTER_DIR = "Assets/prefab/Monster";
        private const string DIMENSION_DIR = "Assets/Resources/Data/Dimension";

        // 스폰 거리 (화면 밖 대각선 10.6 보다 바깥)
        private static readonly Vector2 SPAWN_RANGE = new Vector2(11f, 13f);

        // 출현하지 않음
        private const float NO = 0f;

        /// <summary>
        /// 차원 이름 (DimensionDataBuilder 와 같은 이름)
        /// </summary>
        public static readonly string[] DIMENSIONS = { "Forest", "Factory", "Storm" };

        /// <summary>
        /// 모든 차원 공통 (7장 표): 웨이브 길이(초), 데미지 배율, 속도 배율
        /// </summary>
        private static readonly float[] WAVE_TIME = { 50f, 50f, 50f, 150f, 150f, 150f, 150f, 150f, 150f, 150f, 150f, 150f, 150f, 150f };
        private static readonly float[] DAMAGE_RATE = { 1.00f, 1.05f, 1.10f, 1.15f, 1.30f, 1.45f, 1.60f, 1.75f, 1.90f, 2.05f, 2.20f, 2.35f, 2.50f, 2.65f };
        private static readonly float[] SPEED_RATE = { 1.000f, 1.008f, 1.017f, 1.025f, 1.050f, 1.075f, 1.100f, 1.125f, 1.150f, 1.175f, 1.200f, 1.225f, 1.250f, 1.275f };

        /// <summary>
        /// 차원별 표 (7.2장). monsters 열 순서의 spawnInterval, NO 면 출현하지 않음
        /// </summary>
        private struct DimensionTable
        {
            public string[] monsters;
            public float[] hpRate;
            public float[][] intervals;
        }

        [MenuItem("Tools/Builders/Wave30")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Wave30Builder]");

            if (false == ValidateCommon(sb))
            {
                return sb.ToString();
            }

            var prefabs = new Dictionary<string, Spawnable>();
            foreach (string dim in DIMENSIONS)
            {
                DimensionTable table = Table(dim);
                if (false == ValidateTable(dim, table, sb) || false == LoadPrefabs(table.monsters, prefabs, sb))
                {
                    return sb.ToString();
                }
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
                else
                {
                    sb.AppendLine(dim + ".asset 없음 (DimensionDataBuilder 가 LoadWaves 로 연결)");
                }
            }

            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>
        /// 차원 웨이브 에셋 (없는 번호는 건너뜀). DimensionDataBuilder 에서 사용
        /// </summary>
        public static WaveData[] LoadWaves(string _dimension)
        {
            var list = new List<WaveData>();
            for (int i = 1; i <= WAVE_TIME.Length; i++)
            {
                var w = AssetDatabase.LoadAssetAtPath<WaveData>(WavePath(_dimension, i));
                if (null != w)
                {
                    list.Add(w);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 차원별 구성 (docs/balance-design.md 7.2장, 줄 = 웨이브, 주석은 시작 시각)
        /// </summary>
        private static DimensionTable Table(string _dimension)
        {
            switch (_dimension)
            {
                // 숲: 박쥐·미니 슬라임 물량 + 슬라임(분열) + 골렘
                case "Forest":
                    return new DimensionTable
                    {
                        monsters = new[] { "bat", "MiniSlime", "Slime", "Golem" },
                        hpRate = new[] { 1.00f, 1.90f, 2.81f, 3.09f, 3.39f, 3.73f, 3.85f, 3.97f, 4.08f, 4.21f, 4.33f, 4.46f, 4.60f, 4.73f },
                        intervals = new[]
                        {
                            new[] { 1.11f, 3.33f, NO,    NO    }, // 1  00:00
                            new[] { 1.96f, 1.70f, 5.58f, NO    }, // 2  00:50
                            new[] { 1.89f, 2.18f, 2.66f, NO    }, // 3  01:40
                            new[] { 1.82f, 2.22f, 2.20f, NO    }, // 4  02:30
                            new[] { 1.57f, 2.10f, 1.53f, NO    }, // 5  05:00
                            new[] { 1.35f, 1.89f, 1.15f, NO    }, // 6  07:30
                            new[] { 1.03f, 0.82f, 3.57f, 8.13f }, // 7  10:00 골렘 등장
                            new[] { 0.85f, 0.72f, 2.62f, 4.94f }, // 8  12:30
                            new[] { 0.71f, 0.63f, 1.96f, 3.13f }, // 9  15:00
                            new[] { 0.60f, 0.54f, 1.55f, 2.27f }, // 10 17:30
                            new[] { 0.32f, 0.41f, 0.35f, NO    }, // 11 20:00 물량 러시
                            new[] { 0.42f, 0.40f, 0.96f, 1.19f }, // 12 22:30
                            new[] { 0.35f, 0.34f, 0.77f, 0.89f }, // 13 25:00
                            new[] { 0.29f, 0.29f, 0.62f, 0.68f }, // 14 27:30 최종 (계속 유지)
                        },
                    };
                // 공장: 미니 슬라임·슬라임 물량, 봄버(돌진·자폭) 압박, 골렘
                case "Factory":
                    return new DimensionTable
                    {
                        monsters = new[] { "MiniSlime", "Slime", "Bomber", "Golem" },
                        hpRate = new[] { 1.00f, 1.90f, 2.81f, 3.09f, 3.39f, 3.73f, 3.85f, 3.97f, 4.08f, 4.21f, 4.33f, 4.46f, 4.60f, 4.73f },
                        intervals = new[]
                        {
                            new[] { 0.93f, 8.33f, NO,    NO    }, // 1  00:00
                            new[] { 0.97f, 4.04f, NO,    NO    }, // 2  00:50
                            new[] { 1.09f, 2.24f, NO,    NO    }, // 3  01:40
                            new[] { 1.34f, 2.86f, 2.80f, NO    }, // 4  02:30 봄버 등장
                            new[] { 1.26f, 1.99f, 2.14f, NO    }, // 5  05:00
                            new[] { 1.14f, 1.49f, 1.69f, NO    }, // 6  07:30
                            new[] { 0.57f, 3.17f, 2.33f, 9.29f }, // 7  10:00 골렘 등장
                            new[] { 0.51f, 2.31f, 1.80f, 5.55f }, // 8  12:30
                            new[] { 0.45f, 1.72f, 1.41f, 3.46f }, // 9  15:00
                            new[] { 0.38f, 1.36f, 1.14f, 2.49f }, // 10 17:30
                            new[] { 0.24f, 0.46f, 0.47f, NO    }, // 11 20:00 물량 러시
                            new[] { 0.29f, 0.85f, 0.75f, 1.28f }, // 12 22:30
                            new[] { 0.24f, 0.68f, 0.61f, 0.96f }, // 13 25:00
                            new[] { 0.21f, 0.55f, 0.50f, 0.73f }, // 14 27:30 최종 (계속 유지)
                        },
                    };
                // 폭풍: 박쥐 물량 + 레이스(순간이동·장판) + 샤먼(선회·소환)
                default:
                    return new DimensionTable
                    {
                        monsters = new[] { "bat", "Wraith", "Shaman" },
                        hpRate = new[] { 0.90f, 1.90f, 2.81f, 3.09f, 3.39f, 3.73f, 4.05f, 4.41f, 4.80f, 5.26f, 5.78f, 6.38f, 7.07f, 7.89f },
                        intervals = new[]
                        {
                            new[] { 0.83f, NO,    NO    }, // 1  00:00
                            new[] { 0.92f, 5.21f, NO    }, // 2  00:50 레이스 등장
                            new[] { 1.42f, 1.51f, NO    }, // 3  01:40
                            new[] { 1.61f, 1.20f, NO    }, // 4  02:30
                            new[] { 1.95f, 0.80f, NO    }, // 5  05:00
                            new[] { 2.27f, 0.59f, NO    }, // 6  07:30
                            new[] { 0.69f, 1.62f, 1.91f }, // 7  10:00 샤먼 등장
                            new[] { 0.67f, 1.25f, 1.18f }, // 8  12:30
                            new[] { 0.66f, 1.01f, 0.77f }, // 9  15:00
                            new[] { 0.57f, 0.83f, 0.60f }, // 10 17:30
                            new[] { 0.16f, 0.45f, NO    }, // 11 20:00 물량 러시
                            new[] { 0.43f, 0.57f, 0.37f }, // 12 22:30
                            new[] { 0.35f, 0.47f, 0.31f }, // 13 25:00
                            new[] { 0.28f, 0.38f, 0.26f }, // 14 27:30 최종 (계속 유지)
                        },
                    };
            }
        }

        private static bool ValidateCommon(StringBuilder _sb)
        {
            if (WAVE_TIME.Length != DAMAGE_RATE.Length || WAVE_TIME.Length != SPEED_RATE.Length)
            {
                _sb.AppendLine("오류: 공통 표(길이 / 데미지 / 속도) 의 웨이브 수가 다릅니다.");
                return false;
            }
            return true;
        }

        private static bool ValidateTable(string _dimension, DimensionTable _table, StringBuilder _sb)
        {
            if (WAVE_TIME.Length != _table.hpRate.Length || WAVE_TIME.Length != _table.intervals.Length)
            {
                _sb.AppendLine("오류: " + _dimension + " 표의 웨이브 수가 공통 표(" + WAVE_TIME.Length + ") 와 다릅니다.");
                return false;
            }

            for (int i = 0; i < _table.intervals.Length; i++)
            {
                if (_table.intervals[i].Length != _table.monsters.Length)
                {
                    _sb.AppendLine("오류: " + _dimension + " " + (i + 1) + "웨이브 칸 수가 몬스터 수와 다릅니다.");
                    return false;
                }
            }
            return true;
        }

        private static bool LoadPrefabs(string[] _monsters, Dictionary<string, Spawnable> _prefabs, StringBuilder _sb)
        {
            foreach (string name in _monsters)
            {
                if (true == _prefabs.ContainsKey(name))
                {
                    continue;
                }

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(MONSTER_DIR + "/" + name + "/" + name + ".prefab");
                Spawnable sa = null != go ? go.GetComponent<Spawnable>() : null;
                if (null == sa)
                {
                    _sb.AppendLine("오류: Spawnable 프리팹 없음: " + name);
                    return false;
                }
                _prefabs[name] = sa;
            }
            return true;
        }

        private static string WavePath(string _dimension, int _number)
        {
            return WAVE_ROOT + "/" + _dimension + "/" + _dimension + "_" + _number.ToString("00") + ".asset";
        }

        private static WaveData[] BuildDimension(string _dimension, DimensionTable _table, Dictionary<string, Spawnable> _prefabs, StringBuilder _sb)
        {
            EnsureFolder(WAVE_ROOT + "/" + _dimension);

            var waves = new WaveData[WAVE_TIME.Length];
            float start = 0f;
            for (int i = 0; i < WAVE_TIME.Length; i++)
            {
                var w = LoadOrCreate<WaveData>(WavePath(_dimension, i + 1));
                w.waveID = _dimension + "_" + (i + 1).ToString("00");
                w.time = WAVE_TIME[i];
                w.hpRate = _table.hpRate[i];
                w.damageRate = DAMAGE_RATE[i];
                w.speedRate = SPEED_RATE[i];

                var spawns = new List<SpawnData>();
                for (int m = 0; m < _table.monsters.Length; m++)
                {
                    float interval = _table.intervals[i][m];
                    if (interval <= 0f)
                    {
                        continue;
                    }
                    spawns.Add(Make(_prefabs[_table.monsters[m]], interval));
                }
                w.spwanArray = spawns.ToArray();

                EditorUtility.SetDirty(w);
                waves[i] = w;
                _sb.AppendLine(w.waveID + " @" + FormatTime(start) + ": " + Describe(w));
                start += WAVE_TIME[i];
            }
            _sb.AppendLine(_dimension + " total=" + FormatTime(start) + " (마지막 웨이브는 계속 유지)");
            return waves;
        }

        private static SpawnData Make(Spawnable _prefab, float _interval)
        {
            return new SpawnData
            {
                spawnObject = _prefab,
                count = new Vector2Int(1, 1),
                spawnRange = SPAWN_RANGE,
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
            sb.Append("time=" + _w.time + " hp×" + _w.hpRate + " dmg×" + _w.damageRate + " spd×" + _w.speedRate + " [");
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
