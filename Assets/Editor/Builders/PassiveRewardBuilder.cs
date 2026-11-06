using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 추가 패시브(몬스터 스폰율·체력, 투사체 수, 지속 시간, 체력 재생) 보상 에셋을 만들고 RewardManager 풀에 등록한다 (멱등)
    /// - 몬스터 감소 = 버프(내 쪽 몬스터), 몬스터 증가 = 디버프(상대 쪽 몬스터). 스폰율(Count) 과 체력(HP) 을 함께 바꾼다
    /// - 투사체 수는 버프만 있다 (디버프 없음). 가중치가 낮다 (BalanceDataBuilder.SetPoolWeights 도 GetWeight 를 따른다)
    /// - 지속 시간은 무기 태그 Duration 이 있는 무기에만 적용되며, 최초 지속 시간 × Weapon.MinDurationRate 아래로 줄지 않는다
    /// - 아이콘은 아직 없다 (image 비움)
    /// </summary>
    public static class PassiveRewardBuilder
    {
        private const string BUFF_DIR = "Assets/prefab/Upgrade/PassiveBuff";
        private const string NERF_DIR = "Assets/prefab/Upgrade/PassiveNuff";
        private const string EFFECT_DIR = "Assets/prefab/Upgrade/Effects";
        private const string ADD_EFFECT = EFFECT_DIR + "/StatAddEffect.asset";
        private const string PERCENT_EFFECT = EFFECT_DIR + "/StatPercentEffect.asset";
        private const string MONSTER_EFFECT = EFFECT_DIR + "/MonsterCountHPEffect.asset";
        private const string RM_PREFAB = "Assets/prefab/Managers/RewardManager.prefab";
        private const string GAME_SCENE = "Assets/Scenes/GameScene.unity";

        public const float MONSTER_BUFF = -0.05f;
        public const float MONSTER_NERF = 0.03f;
        public const float DURATION_BUFF = 0.10f;
        public const float DURATION_NERF = -0.06f;
        public const float PROJECTILE_BUFF = 1f;
        public const float PROJECTILE_WEIGHT = 0.3f;
        // 초당 체력 회복 (고정값, 기본 재생 0. 최종 재생은 0 미만으로 내려가지 않는다)
        public const float REGEN_BUFF = 0.5f;
        public const float REGEN_NERF = -0.3f;
        private const float DEFAULT_WEIGHT = 1f;

        private struct Spec
        {
            public string path;
            public RewardPoolType pool;
            public bool hasKey;
            public ValueType key;
            public float value;
            public string explain;
            public string nameKey;
            public string effect;
            public float weight;
        }

        private static readonly Spec[] Specs =
        {
            new Spec { path = BUFF_DIR + "/MonsterDecrease.asset", pool = RewardPoolType.PassiveBuff, value = MONSTER_BUFF,
                explain = "reward.passive.monster_down", nameKey = "reward.passive.monster.name", effect = MONSTER_EFFECT, weight = DEFAULT_WEIGHT },
            new Spec { path = BUFF_DIR + "/DurationUpgrade.asset", pool = RewardPoolType.PassiveBuff, hasKey = true, key = ValueType.Duration, value = DURATION_BUFF,
                explain = "reward.passive.buff_i", nameKey = "reward.passive.duration.name", effect = PERCENT_EFFECT, weight = DEFAULT_WEIGHT },
            new Spec { path = BUFF_DIR + "/ProjectileUpgrade.asset", pool = RewardPoolType.PassiveBuff, hasKey = true, key = ValueType.ProjectileCount, value = PROJECTILE_BUFF,
                explain = "reward.passive.count_up", nameKey = "reward.passive.projectile_count.name", effect = ADD_EFFECT, weight = PROJECTILE_WEIGHT },
            new Spec { path = BUFF_DIR + "/HPRegenUpgrade.asset", pool = RewardPoolType.PassiveBuff, hasKey = true, key = ValueType.HPRegen, value = REGEN_BUFF,
                explain = "reward.passive.regen_up", nameKey = "reward.passive.hp_regen.name", effect = ADD_EFFECT, weight = DEFAULT_WEIGHT },
            new Spec { path = NERF_DIR + "/HPRegenNuff.asset", pool = RewardPoolType.PassiveNerf, hasKey = true, key = ValueType.HPRegen, value = REGEN_NERF,
                explain = "reward.passive.regen_down", nameKey = "", effect = ADD_EFFECT, weight = DEFAULT_WEIGHT },
            new Spec { path = NERF_DIR + "/MonsterIncreaseNuff.asset", pool = RewardPoolType.PassiveNerf, value = MONSTER_NERF,
                explain = "reward.passive.monster_up", nameKey = "", effect = MONSTER_EFFECT, weight = DEFAULT_WEIGHT },
            new Spec { path = NERF_DIR + "/DurationNuff.asset", pool = RewardPoolType.PassiveNerf, hasKey = true, key = ValueType.Duration, value = DURATION_NERF,
                explain = "reward.passive.nerf_i", nameKey = "", effect = PERCENT_EFFECT, weight = DEFAULT_WEIGHT },
        };

        /// <summary>
        /// 이 빌더가 정한 풀 가중치 (해당 없으면 false). BalanceDataBuilder 가 가중치를 일괄 적용할 때 참고한다
        /// </summary>
        public static bool TryGetWeight(Object _item, out float _weight)
        {
            string path = null != _item ? AssetDatabase.GetAssetPath(_item) : null;
            foreach (Spec spec in Specs)
            {
                if (spec.path == path)
                {
                    _weight = spec.weight;
                    return true;
                }
            }
            _weight = 0f;
            return false;
        }

        [MenuItem("Tools/Builders/PassiveReward")]
        public static string Build()
        {
            StringBuilder log = new StringBuilder();

            BuildMonsterEffect(log);

            List<(Spec spec, RewardData data)> created = new List<(Spec, RewardData)>();
            foreach (Spec spec in Specs)
            {
                RewardData data = BuildData(spec, log);
                if (null != data)
                {
                    created.Add((spec, data));
                }
            }
            AssetDatabase.SaveAssets();

            GameObject rmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RM_PREFAB);
            if (null != rmPrefab && rmPrefab.TryGetComponent(out RewardManager prefabManager))
            {
                log.AppendLine("RewardManager prefab: " + Register(prefabManager, created));
                EditorUtility.SetDirty(rmPrefab);
                AssetDatabase.SaveAssets();
            }
            else
            {
                log.AppendLine("[경고] RewardManager 프리팹 없음: " + RM_PREFAB);
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != GAME_SCENE)
            {
                // 저장 안 한 씬 변경을 묻지 않고 버리지 않도록 (취소하면 씬 등록은 건너뛴다)
                if (false == EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    log.AppendLine("[중단] 씬 저장이 취소되어 GameScene 풀 등록을 건너뜁니다.");
                    Debug.Log("[PassiveRewardBuilder]\n" + log);
                    return log.ToString();
                }
                scene = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Single);
            }
            foreach (RewardManager rm in Object.FindObjectsByType<RewardManager>(FindObjectsInactive.Include))
            {
                log.AppendLine($"RewardManager scene ({rm.name}): " + Register(rm, created));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PassiveRewardBuilder]\n" + log);
            return log.ToString();
        }

        private static void BuildMonsterEffect(StringBuilder _log)
        {
            SpawnerRewardEffect effect = AssetDatabase.LoadAssetAtPath<SpawnerRewardEffect>(MONSTER_EFFECT);
            if (null == effect)
            {
                effect = ScriptableObject.CreateInstance<SpawnerRewardEffect>();
                AssetDatabase.CreateAsset(effect, MONSTER_EFFECT);
            }

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("type").enumValueIndex = (int)SpawnModifierType.Count;
            SerializedProperty extras = so.FindProperty("extraTypes");
            extras.arraySize = 1;
            extras.GetArrayElementAtIndex(0).enumValueIndex = (int)SpawnModifierType.HP;
            so.FindProperty("mode").enumValueIndex = (int)StatModifierMode.Add;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
            _log.AppendLine("effect: " + MONSTER_EFFECT + " (Count + HP, Add)");
        }

        private static RewardData BuildData(Spec _spec, StringBuilder _log)
        {
            RewardEffect effect = AssetDatabase.LoadAssetAtPath<RewardEffect>(_spec.effect);
            if (null == effect)
            {
                _log.AppendLine("[경고] 효과 에셋 없음: " + _spec.effect);
                return null;
            }

            RewardData data = AssetDatabase.LoadAssetAtPath<RewardData>(_spec.path);
            if (null == data)
            {
                data = ScriptableObject.CreateInstance<RewardData>();
                AssetDatabase.CreateAsset(data, _spec.path);
            }

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("rewardName").stringValue = _spec.nameKey;
            so.FindProperty("hasKey").boolValue = _spec.hasKey;
            so.FindProperty("key").enumValueIndex = (int)_spec.key;
            so.FindProperty("hasValue").boolValue = true;
            so.FindProperty("value").floatValue = _spec.value;
            so.FindProperty("explain1").stringValue = _spec.explain;
            so.FindProperty("explain2").stringValue = _spec.explain;
            so.FindProperty("effect").objectReferenceValue = effect;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            _log.AppendLine($"{_spec.path}: {_spec.pool} value={_spec.value} weight={_spec.weight}");
            return data;
        }

        /// <summary>
        /// 풀에 없으면 추가, 있으면 가중치만 맞춘다
        /// </summary>
        private static string Register(RewardManager _manager, List<(Spec spec, RewardData data)> _items)
        {
            SerializedObject so = new SerializedObject(_manager);
            SerializedProperty pools = so.FindProperty("pools");
            StringBuilder sb = new StringBuilder();

            foreach ((Spec spec, RewardData data) in _items)
            {
                SerializedProperty entries = FindEntries(pools, spec.pool);
                if (null == entries)
                {
                    sb.Append($"[경고] {spec.pool} 풀 없음 ");
                    continue;
                }

                SerializedProperty entry = null;
                for (int i = 0; i < entries.arraySize; ++i)
                {
                    SerializedProperty e = entries.GetArrayElementAtIndex(i);
                    if (data == e.FindPropertyRelative("item").objectReferenceValue)
                    {
                        entry = e;
                        break;
                    }
                }

                if (null == entry)
                {
                    entries.arraySize += 1;
                    entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("item").objectReferenceValue = data;
                    sb.Append($"+{data.name} ");
                }
                entry.FindPropertyRelative("weight").floatValue = spec.weight;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return sb.Length > 0 ? sb.ToString() : "변경 없음(가중치만 갱신)";
        }

        private static SerializedProperty FindEntries(SerializedProperty _pools, RewardPoolType _type)
        {
            for (int i = 0; i < _pools.arraySize; ++i)
            {
                SerializedProperty slot = _pools.GetArrayElementAtIndex(i);
                if ((int)_type == slot.FindPropertyRelative("type").enumValueIndex)
                {
                    return slot.FindPropertyRelative("pool").FindPropertyRelative("entries");
                }
            }
            return null;
        }
    }
}
