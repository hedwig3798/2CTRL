using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// docs/balance-design.md 10장 체크리스트의 데이터 값을 프리팹 / SO / 씬에 반영한다 (멱등)
    /// - 플레이어 HP, 몬스터 HP / 접촉 데미지 / 쿨타임, 키워드, EXP, 레벨 공식, 무기 데미지, 패시브, 보상 가중치
    /// - 업그레이드 표 / 웨이브는 WeaponUpgradeBuilder / Wave30Builder 가 담당
    /// </summary>
    public static class BalanceDataBuilder
    {
        private const string MONSTER_DIR = "Assets/prefab/Monster";
        private const string WEAPON_DIR = "Assets/prefab/weapon";
        private const string GAME_SCENE = "Assets/Scenes/GameScene.unity";
        private const string PERCENT_EFFECT = "Assets/prefab/Upgrade/Effects/StatPercentEffect.asset";

        private struct MonsterSpec
        {
            public string name;
            public float hp;
            public float damage;
            public MonsterSpec(string _name, float _hp, float _damage) { name = _name; hp = _hp; damage = _damage; }
        }

        private static readonly MonsterSpec[] Monsters =
        {
            new MonsterSpec("bat", 9f, 4f),
            new MonsterSpec("MiniSlime", 5f, 3f),
            new MonsterSpec("Slime", 21f, 6f),
            new MonsterSpec("Bomber", 12f, 4f),
            new MonsterSpec("Wraith", 20f, 5f),
            new MonsterSpec("Shaman", 35f, 4f),
            new MonsterSpec("Golem", 140f, 15f),
        };

        private const float CONTACT_COOLTIME = 0.5f;

        private static readonly Dictionary<string, float> MonsterExp = new Dictionary<string, float>
        {
            { "MiniSlime", 1f }, { "Slime", 2f }, { "Bomber", 3f }, { "Wraith", 3f }, { "Shaman", 5f }, { "Golem", 20f },
        };

        // 무기 프리팹 경로 -> 데미지 (3.2장). 드론은 DroneProjectile 자식 무기
        private static readonly Dictionary<string, float> WeaponDamage = new Dictionary<string, float>
        {
            { "Chain/Chain.prefab", 8f },
            { "Drone/DroneProjectile.prefab", 9f },
            { "Explosive/Explosive.prefab", 14f },
            { "Laser/Laser.prefab", 13f },
            { "Mine/Mine.prefab", 30f },
            { "PoisonZone/PoisonZone.prefab", 3.5f },
            { "Shockwave/Shockwave.prefab", 8f },
            { "SplitShot/SplitShot.prefab", 15f },
            { "Sweep/Sweep.prefab", 13f },
            { "Boomerang/Boomerang.prefab", 12f },
        };

        private struct PassiveSpec
        {
            public string path;
            public float value;
            public string explain; // StringTable 키
            public bool isNerf;
            public string nameKey; // StringTable 키 (비우면 이름 없음)
            public PassiveSpec(string _path, float _value, string _explain, bool _isNerf, string _nameKey = "") { path = _path; value = _value; explain = _explain; isNerf = _isNerf; nameKey = _nameKey; }
        }

        // 버프는 "+5%", 너프는 음수 값을 절대값 % 로 표시 (StringTable 키. 원문 "{0}가 {1:0.#%} 증가합니다." 등)
        private const string BUFF_FMT_GA = "reward.passive.buff_ga";
        private const string BUFF_FMT_I = "reward.passive.buff_i";
        private const string NERF_FMT_GA = "reward.passive.nerf_ga";
        private const string NERF_FMT_I = "reward.passive.nerf_i";

        private static readonly PassiveSpec[] Passives =
        {
            new PassiveSpec("Assets/prefab/Upgrade/PassiveBuff/DamageUpgrade.asset", 0.05f, BUFF_FMT_GA, false, "reward.passive.damage.name"),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveBuff/ASUpgrade.asset", 0.05f, BUFF_FMT_GA, false, "reward.passive.attack_speed.name"),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveBuff/SpeedUpgrade.asset", 0.05f, BUFF_FMT_GA, false, "reward.passive.move_speed.name"),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveBuff/RangeUpgrade.asset", 0.10f, BUFF_FMT_GA, false, "reward.passive.range.name"),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveBuff/HPUpgrade.asset", 0.10f, BUFF_FMT_I, false, "reward.passive.hp.name"),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveNuff/DamageNuff.asset", -0.03f, NERF_FMT_GA, true),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveNuff/AttackNuff.asset", -0.03f, NERF_FMT_GA, true),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveNuff/MovespeedNuff.asset", -0.03f, NERF_FMT_GA, true),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveNuff/RangeNuff.asset", -0.06f, NERF_FMT_GA, true),
            new PassiveSpec("Assets/prefab/Upgrade/PassiveNuff/HPNuff.asset", -0.06f, NERF_FMT_I, true),
        };

        private const float WEAPON_POOL_WEIGHT = 1.5f;
        private const float PASSIVE_POOL_WEIGHT = 1f;

        [MenuItem("Tools/Builders/BalanceData")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();

            // 1. 플레이어
            SetHealth("Assets/prefab/Player/Player.prefab", 100f, sb);

            // 2. 몬스터
            foreach (MonsterSpec spec in Monsters)
            {
                string path = $"{MONSTER_DIR}/{spec.name}/{spec.name}.prefab";
                SetHealth(path, spec.hp, sb);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                ContactDamage contact = null != prefab ? prefab.GetComponentInChildren<ContactDamage>(true) : null;
                if (null == contact)
                {
                    sb.AppendLine("WARN ContactDamage 없음: " + path);
                    continue;
                }
                var so = new SerializedObject(contact);
                so.FindProperty("baseDamage").floatValue = spec.damage;
                so.FindProperty("coolTime").floatValue = CONTACT_COOLTIME;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine($"{spec.name}: contact={spec.damage} cool={CONTACT_COOLTIME}");
            }

            SetFloat("Assets/Resources/Data/Monster/Keywords/SelfDestruct.asset", "damage", 20f, sb);
            SetFloat("Assets/Resources/Data/Monster/Keywords/HazardTrail.asset", "damagePerTick", 3f, sb);

            // 3. EXP
            foreach (KeyValuePair<string, float> pair in MonsterExp)
            {
                SetFloat($"Assets/Resources/Data/Monster/{pair.Key}Data.asset", "expValue", pair.Value, sb);
            }
            GameObject gem = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Item/expGem.prefab");
            ExpItem expItem = null != gem ? gem.GetComponentInChildren<ExpItem>(true) : null;
            if (null != expItem)
            {
                var so = new SerializedObject(expItem);
                so.FindProperty("baseExp").floatValue = 1f;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("expGem baseExp=1");
            }
            else
            {
                sb.AppendLine("WARN expGem ExpItem 없음");
            }

            // 4. 레벨 공식
            var table = AssetDatabase.LoadAssetAtPath<LevelTable>("Assets/Resources/Script/ExpSystem/DefaultLevelTable.asset");
            if (null != table)
            {
                var so = new SerializedObject(table);
                so.FindProperty("baseExp").floatValue = 9f;
                so.FindProperty("coef").floatValue = 17.5f;
                so.FindProperty("power").floatValue = 1.2f;
                so.FindProperty("maxLevel").intValue = 99;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(table);
                sb.AppendLine("LevelTable 9/17.5/1.2 max99");
            }

            // 5. 무기 데미지
            sb.AppendLine(ApplyWeaponDamage());

            // 7. 패시브
            var percent = AssetDatabase.LoadAssetAtPath<RewardEffect>(PERCENT_EFFECT);
            foreach (PassiveSpec spec in Passives)
            {
                var data = AssetDatabase.LoadAssetAtPath<RewardData>(spec.path);
                if (null == data)
                {
                    sb.AppendLine("WARN 패시브 없음: " + spec.path);
                    continue;
                }
                var so = new SerializedObject(data);
                so.FindProperty("effect").objectReferenceValue = percent;
                so.FindProperty("hasValue").boolValue = true;
                so.FindProperty("value").floatValue = spec.value;
                so.FindProperty("rewardName").stringValue = spec.nameKey;
                so.FindProperty("explain1").stringValue = spec.explain;
                if (true == spec.isNerf)
                {
                    so.FindProperty("explain2").stringValue = spec.explain;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                sb.AppendLine($"{data.name}: {spec.value} -> \"{data.GetFormattedExplain1()}\"");
            }

            // 8. 보상 가중치 (프리팹)
            GameObject rmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Managers/RewardManager.prefab");
            if (null != rmPrefab)
            {
                sb.AppendLine("RewardManager prefab: " + SetPoolWeights(rmPrefab.GetComponent<RewardManager>()));
                EditorUtility.SetDirty(rmPrefab);
            }

            AssetDatabase.SaveAssets();

            // 8. 씬 (DimensionApplier, RewardManager 인스턴스, 9. Spawner 확인)
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != GAME_SCENE)
            {
                scene = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Single);
            }
            foreach (DimensionApplier applier in Object.FindObjectsByType<DimensionApplier>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(applier);
                so.FindProperty("weaponRewardWeight").floatValue = WEAPON_POOL_WEIGHT;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine($"{applier.name}.weaponRewardWeight={WEAPON_POOL_WEIGHT}");
            }
            foreach (RewardManager rm in Object.FindObjectsByType<RewardManager>(FindObjectsInactive.Include))
            {
                sb.AppendLine("RewardManager scene: " + SetPoolWeights(rm));
            }
            foreach (Spawner spawner in Object.FindObjectsByType<Spawner>(FindObjectsInactive.Include))
            {
                sb.AppendLine($"Spawner {spawner.name}: hp={spawner.HPRate} speed={spawner.speedRate} dmg={spawner.damagerate}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            return sb.ToString();
        }

        /// <summary>
        /// 무기 프리팹 10종 데미지만 다시 적용한다 (다른 빌더 실행 후 재적용용)
        /// </summary>
        public static string ApplyWeaponDamage()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, float> pair in WeaponDamage)
            {
                string path = $"{WEAPON_DIR}/{pair.Key}";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (null == prefab)
                {
                    sb.AppendLine("WARN 무기 없음: " + path);
                    continue;
                }
                // 드론 탄은 자식 무기, 나머지는 루트 무기
                Weapon weapon = prefab.GetComponent<Weapon>();
                if (null == weapon)
                {
                    weapon = prefab.GetComponentInChildren<Weapon>(true);
                }
                if (null == weapon)
                {
                    sb.AppendLine("WARN Weapon 없음: " + path);
                    continue;
                }
                var so = new SerializedObject(weapon);
                so.FindProperty("damage").floatValue = pair.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
                sb.Append($"{pair.Key}({weapon.name})={pair.Value} ");
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static void SetHealth(string _path, float _hp, StringBuilder _sb)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            HealthSystem health = null != prefab ? prefab.GetComponentInChildren<HealthSystem>(true) : null;
            if (null == health)
            {
                _sb.AppendLine("WARN HealthSystem 없음: " + _path);
                return;
            }
            var so = new SerializedObject(health);
            so.FindProperty("maxHP").floatValue = _hp;
            SerializedProperty curr = so.FindProperty("currHP");
            if (null != curr)
            {
                curr.floatValue = _hp;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            _sb.AppendLine($"{prefab.name}: maxHP={_hp}");
        }

        private static void SetFloat(string _path, string _field, float _value, StringBuilder _sb)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(_path);
            if (null == asset)
            {
                _sb.AppendLine("WARN 에셋 없음: " + _path);
                return;
            }
            var so = new SerializedObject(asset);
            SerializedProperty prop = so.FindProperty(_field);
            if (null == prop)
            {
                _sb.AppendLine($"WARN 필드 없음: {_path}.{_field}");
                return;
            }
            prop.floatValue = _value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            _sb.AppendLine($"{asset.name}.{_field}={_value}");
        }

        /// <summary>
        /// RewardManager.pools 의 Weapon 풀 항목은 1.5, PassiveBuff 풀 항목은 1 로 맞춘다
        /// </summary>
        private static string SetPoolWeights(RewardManager _manager)
        {
            if (null == _manager)
            {
                return "없음";
            }
            var so = new SerializedObject(_manager);
            SerializedProperty pools = so.FindProperty("pools");
            var sb = new StringBuilder();
            for (int i = 0; i < pools.arraySize; ++i)
            {
                SerializedProperty slot = pools.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = null;
                SerializedProperty entries = null;
                SerializedProperty it = slot.Copy();
                SerializedProperty end = slot.GetEndProperty();
                it.NextVisible(true);
                while (false == SerializedProperty.EqualContents(it, end))
                {
                    if (null == typeProp && SerializedPropertyType.Enum == it.propertyType)
                    {
                        typeProp = it.Copy();
                    }
                    if ("entries" == it.name && true == it.isArray)
                    {
                        entries = it.Copy();
                    }
                    it.NextVisible(true);
                }
                if (null == typeProp || null == entries)
                {
                    continue;
                }
                string typeName = typeProp.enumNames[typeProp.enumValueIndex];
                float weight;
                if ("Weapon" == typeName)
                {
                    weight = WEAPON_POOL_WEIGHT;
                }
                else if ("PassiveBuff" == typeName)
                {
                    weight = PASSIVE_POOL_WEIGHT;
                }
                else
                {
                    continue;
                }
                for (int e = 0; e < entries.arraySize; ++e)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                    // PassiveRewardBuilder 가 정한 가중치(투사체 수 0.3 등) 는 유지한다
                    float entryWeight = PassiveRewardBuilder.TryGetWeight(entry.FindPropertyRelative("item").objectReferenceValue, out float custom) ? custom : weight;
                    entry.FindPropertyRelative("weight").floatValue = entryWeight;
                }
                sb.Append($"{typeName} x{entries.arraySize}={weight} ");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return sb.ToString();
        }
    }
}
