using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 무기 업그레이드 테이블 10개, 기존 무기 연결, Sweep 검기 / Chain 무기 프리팹과 보상 에셋을 생성·갱신한다 (멱등)
    /// 수치는 docs/weapon-upgrade-design.md 표를 따른다
    /// </summary>
    public static class WeaponUpgradeBuilder
    {
        private const string TABLE_DIR = "Assets/prefab/Upgrade/WeaponUpgrade";
        private const string EFFECT_DIR = "Assets/prefab/Upgrade/Effects";
        private const string REWARD_DIR = "Assets/prefab/Upgrade/Weapon";
        private const string WEAPON_DIR = "Assets/prefab/weapon";
        private const string CHAIN_DIR = "Assets/prefab/weapon/Chain";

        private const string CHAIN_SRC = WEAPON_DIR + "/bullet/ChainProjectile.prefab";
        private const string CHAIN_PROJECTILE = CHAIN_DIR + "/ChainLightning.prefab";
        private const string CHAIN_WEAPON = CHAIN_DIR + "/Chain.prefab";
        private const string CHAIN_REWARD = REWARD_DIR + "/ChainReward.asset";
        private const string CHAIN_EFFECT = EFFECT_DIR + "/ChainRewardEffect.asset";
        private const string SWEEP_SLASH = WEAPON_DIR + "/Sweep/SweepSlash.prefab";
        private const string REWARD_MANAGER = "Assets/prefab/Managers/RewardManager.prefab";

        private const StatModifierMode ADD = StatModifierMode.Add;
        private const StatModifierMode MUL = StatModifierMode.Multiply;

        [MenuItem("Tools/Builders/WeaponUpgrade")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            EnsureFolder(TABLE_DIR);
            EnsureFolder(CHAIN_DIR);

            // 1. 업그레이드 테이블
            var tables = new Dictionary<string, WeaponUpgradeTable>();
            foreach (var kv in Tables())
            {
                var table = LoadOrCreate<WeaponUpgradeTable>($"{TABLE_DIR}/{kv.Key}UpgradeTable.asset");
                table.SetSteps(kv.Value, 1f);
                EditorUtility.SetDirty(table);
                tables[kv.Key] = table;
            }
            sb.AppendLine($"tables {tables.Count}");

            // 2. 기존 무기 RewardEffect 연결
            string[] existing = { "Boomerang", "Drone", "Explosive", "Laser", "Mine", "PoisonZone", "Shockwave", "SplitShot", "Sweep" };
            foreach (string w in existing)
            {
                var effect = AssetDatabase.LoadAssetAtPath<WeaponRewardEffect>($"{EFFECT_DIR}/{w}RewardEffect.asset");
                if (null == effect)
                {
                    sb.AppendLine($"missing effect {w}");
                    continue;
                }
                var so = new SerializedObject(effect);
                so.FindProperty("upgradeTable").objectReferenceValue = tables[w];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 무기 프리팹 기본값 (0 이면 설정)
            EnsureWeaponDefaults(sb, "Explosive/Explosive", 1.8f, 0f);
            EnsureWeaponDefaults(sb, "Mine/Mine", 2f, 15f);
            EnsureWeaponDefaults(sb, "PoisonZone/PoisonZone", 1.5f, 3f);
            EnsureWeaponDefaults(sb, "Shockwave/Shockwave", 3.5f, 0f);
            EditPrefab(WEAPON_DIR + "/PoisonZone/PoisonZoneProjectile.prefab", root =>
            {
                var s = root.GetComponent<ScaleOverLifetime>();
                if (null != s)
                {
                    s.useAreaRadius = true;
                }
            });

            // 부메랑 / 조각 훅
            EditPrefab(WEAPON_DIR + "/Boomerang/BoomerangProjectile.prefab", root => GetOrAdd<BoomerangUpgrade>(root));
            EditPrefab(WEAPON_DIR + "/SplitShot/SplitShotFragment.prefab", root => GetOrAdd<SplitFragmentResplit>(root));

            // Sweep 검기
            GameObject slash = BuildSweepSlash();
            EditPrefab(WEAPON_DIR + "/Sweep/SweepProjectile.prefab", root =>
            {
                var so = new SerializedObject(root.GetComponent<SweepProjectile>());
                so.FindProperty("slashPrefab").objectReferenceValue = slash.GetComponent<OneHitProjectile>();
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            sb.AppendLine("sweep slash " + SWEEP_SLASH);

            // 3. Chain
            GameObject chainProjectile = BuildChainProjectile();
            GameObject chainWeapon = BuildChainWeapon(chainProjectile);

            var chainEffect = LoadOrCreate<WeaponRewardEffect>(CHAIN_EFFECT);
            {
                var so = new SerializedObject(chainEffect);
                so.FindProperty("weaponPrefab").objectReferenceValue = chainWeapon.GetComponent<Weapon>();
                so.FindProperty("upgradeTable").objectReferenceValue = tables["Chain"];
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var chainReward = LoadOrCreate<RewardData>(CHAIN_REWARD);
            {
                var reference = AssetDatabase.LoadAssetAtPath<RewardData>(REWARD_DIR + "/SweepReward.asset");
                var so = new SerializedObject(chainReward);
                if (null != reference)
                {
                    so.FindProperty("image").objectReferenceValue = new SerializedObject(reference).FindProperty("image").objectReferenceValue;
                }
                so.FindProperty("rewardName").stringValue = "체인 라이트닝";
                so.FindProperty("hasKey").boolValue = false;
                so.FindProperty("hasValue").boolValue = false;
                so.FindProperty("explain1").stringValue = "가장 가까운 적에게 번개를 쏘고, 주변의 다른 적에게 차례로 전이하며 타격하는 무기를 얻습니다.";
                so.FindProperty("explain2").stringValue = string.Empty;
                so.FindProperty("effect").objectReferenceValue = chainEffect;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // RewardManager Weapon 풀
            EditPrefab(REWARD_MANAGER, root =>
            {
                var so = new SerializedObject(root.GetComponent<RewardManager>());
                var pools = so.FindProperty("pools");
                for (int i = 0; i < pools.arraySize; ++i)
                {
                    var slot = pools.GetArrayElementAtIndex(i);
                    if ((int)RewardPoolType.Weapon != slot.FindPropertyRelative("type").intValue)
                    {
                        continue;
                    }
                    var entries = slot.FindPropertyRelative("pool.entries");
                    for (int j = 0; j < entries.arraySize; ++j)
                    {
                        if (entries.GetArrayElementAtIndex(j).FindPropertyRelative("item").objectReferenceValue == chainReward)
                        {
                            return;
                        }
                    }
                    int n = entries.arraySize;
                    entries.InsertArrayElementAtIndex(n);
                    var e = entries.GetArrayElementAtIndex(n);
                    e.FindPropertyRelative("item").objectReferenceValue = chainReward;
                    e.FindPropertyRelative("weight").floatValue = 1f;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            sb.AppendLine("chain " + CHAIN_WEAPON);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }

        // ---------------- prefabs ----------------

        private static void EnsureWeaponDefaults(StringBuilder _sb, string _path, float _areaRadius, float _lifetime)
        {
            EditPrefab($"{WEAPON_DIR}/{_path}.prefab", root =>
            {
                var w = root.GetComponent<Weapon>();
                if (w.areaRadius <= 0f)
                {
                    w.areaRadius = _areaRadius;
                    _sb.AppendLine($"{_path} areaRadius={_areaRadius}");
                }
                if (_lifetime > 0f && w.lifetime <= 0f)
                {
                    w.lifetime = _lifetime;
                    _sb.AppendLine($"{_path} lifetime={_lifetime}");
                }
            });
        }

        private static GameObject BuildSweepSlash()
        {
            var fragment = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON_DIR + "/SplitShot/SplitShotFragment.prefab");
            var fragmentRenderer = null != fragment ? fragment.GetComponentInChildren<SpriteRenderer>(true) : null;

            var root = new GameObject("SweepSlash");
            try
            {
                var col = root.AddComponent<CapsuleCollider2D>();
                col.isTrigger = true;
                col.direction = CapsuleDirection2D.Vertical;
                col.size = new Vector2(0.35f, 1.4f);

                var straight = root.AddComponent<Straight>();
                var sso = new SerializedObject(straight);
                SetFloat(sso, "speed", 12f);
                var rot = sso.FindProperty("rotateMode");
                if (null != rot)
                {
                    rot.enumValueIndex = (int)SPRITE_ROTATE_MODE.ROTATE;
                }
                var left = sso.FindProperty("isDefaultLeft");
                if (null != left)
                {
                    left.boolValue = true;
                }
                sso.ApplyModifiedPropertiesWithoutUndo();

                var lifetime = root.AddComponent<Lifetime>();
                lifetime.lifetime = 0.8f;
                root.AddComponent<OneHitProjectile>();
                root.AddComponent<BlackBoardHandler>();

                var visual = new GameObject("visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(0.35f, 1.4f, 1f);
                var sr = visual.AddComponent<SpriteRenderer>();
                if (null != fragmentRenderer)
                {
                    sr.sprite = fragmentRenderer.sprite;
                    sr.sharedMaterial = fragmentRenderer.sharedMaterial;
                    sr.sortingLayerID = fragmentRenderer.sortingLayerID;
                    sr.sortingOrder = fragmentRenderer.sortingOrder;
                }

                return PrefabUtility.SaveAsPrefabAsset(root, SWEEP_SLASH);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildChainProjectile()
        {
            if (null == AssetDatabase.LoadAssetAtPath<GameObject>(CHAIN_PROJECTILE))
            {
                AssetDatabase.CopyAsset(CHAIN_SRC, CHAIN_PROJECTILE);
                AssetDatabase.ImportAsset(CHAIN_PROJECTILE);
            }

            EditPrefab(CHAIN_PROJECTILE, root =>
            {
                root.name = "ChainLightning";
                var col = root.GetComponent<Collider2D>();
                if (null != col)
                {
                    col.isTrigger = true;
                }

                var lifetime = GetOrAdd<Lifetime>(root);
                lifetime.lifetime = 3f;

                var amp = GetOrAdd<ChainAmplifier>(root);
                var aso = new SerializedObject(amp);
                SetFloat(aso, "amplifyRate", 0.15f);
                aso.ApplyModifiedPropertiesWithoutUndo();

                Transform lineT = root.transform.Find("line");
                if (null == lineT)
                {
                    lineT = new GameObject("line").transform;
                    lineT.SetParent(root.transform, false);
                }
                var lr = lineT.GetComponent<LineRenderer>();
                if (null == lr)
                {
                    lr = lineT.gameObject.AddComponent<LineRenderer>();
                    lr.startWidth = 0.08f;
                    lr.endWidth = 0.08f;
                    lr.numCapVertices = 2;
                    var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                    if (null != mat)
                    {
                        lr.sharedMaterial = mat;
                    }
                }
                lr.useWorldSpace = true;
                lr.positionCount = 0;

                var line = GetOrAdd<ChainLightningLine>(root);
                var lso = new SerializedObject(line);
                lso.FindProperty("line").objectReferenceValue = lr;
                lso.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(CHAIN_PROJECTILE);
        }

        private static GameObject BuildChainWeapon(GameObject _projectile)
        {
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON_DIR + "/Boomerang/Boomerang.prefab");
            var refWeapon = null != reference ? reference.GetComponent<Weapon>() : null;

            var root = new GameObject("Chain");
            try
            {
                if (null != reference)
                {
                    root.layer = reference.layer;
                }
                var w = root.AddComponent<Weapon>();
                w.weaponType = WEAPON_TYPE.Repeat;
                w.preLoadCount = 5;
                w.projectile = _projectile.GetComponent<Projectile>();
                w.targetLayer = null != refWeapon ? refWeapon.targetLayer : (LayerMask)0;
                w.damage = 10f;
                w.attackSpeed = 0.8f;
                w.range = 7f;
                w.fireCount = 1;
                w.speedRate = 1f;
                w.hitCount = 99;
                w.fireDelay = 0.1f;
                w.spreadRange = 0f;
                w.lifetime = 0f;
                w.knockbackForce = 0f;
                w.chainCount = 3;
                w.chainRange = 3f;
                w.areaRadius = 0f;
                return PrefabUtility.SaveAsPrefabAsset(root, CHAIN_WEAPON);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------------- tables ----------------

        private static WeaponModifier M(WeaponParam _p, StatModifierMode _m, float _v) => new WeaponModifier(_p, _m, _v);

        private static WeaponUpgradeStep S(string _title, string _desc, params WeaponModifier[] _mods) => new WeaponUpgradeStep(_title, _desc, _mods);

        private static WeaponUpgradeStep Dmg() => S("데미지 +20%", "데미지 +20%", M(WeaponParam.Damage, MUL, 0.2f));
        private static WeaponUpgradeStep As() => S("공속 +15%", "공격 속도 +15%", M(WeaponParam.AttackSpeed, MUL, 0.15f));
        private static WeaponUpgradeStep Count(int _n, string _what = "개수") => S($"{_what} +{_n}", $"{_what} +{_n}", M(WeaponParam.FireCount, ADD, _n));
        private static WeaponUpgradeStep Flag(WeaponParam _p, string _title, string _desc) => S("★ " + _title, _desc, M(_p, ADD, 1f));

        private static Dictionary<string, WeaponUpgradeStep[]> Tables()
        {
            var t = new Dictionary<string, WeaponUpgradeStep[]>();

            t["Boomerang"] = new[]
            {
                Dmg(), Count(1),
                S("투사체 속도 +20%", "투사체 속도 +20%", M(WeaponParam.SpeedRate, MUL, 0.2f)),
                Dmg(),
                S("★ 크기 +40%, 타격 수 +10", "부메랑 크기 +40%, 타격 수 +10", M(WeaponParam.ProjectileScale, MUL, 0.4f), M(WeaponParam.HitCount, ADD, 10f)),
                As(), Count(1), Dmg(),
                S("사거리 +1.5", "사거리 +1.5", M(WeaponParam.Range, ADD, 1.5f)),
                Flag(WeaponParam.FlagReturnDamage, "귀환 강타", "돌아올 때 데미지 2배"),
            };

            t["Drone"] = new[]
            {
                S("드론 탄 데미지 +20%", "드론 탄 데미지 +20%", M(WeaponParam.ChildDamage, MUL, 0.2f)),
                S("드론 탄 공속 +15%", "드론 탄 공격 속도 +15%", M(WeaponParam.ChildAttackSpeed, MUL, 0.15f)),
                Count(1, "드론"),
                S("드론 탄 데미지 +20%", "드론 탄 데미지 +20%", M(WeaponParam.ChildDamage, MUL, 0.2f)),
                S("★ 드론 탄 2연발", "드론이 탄을 2연발로 쏜다", M(WeaponParam.ChildFireCount, ADD, 1f)),
                S("공전 반경 +0.4, 드론 탄 사거리 +1.5", "드론 공전 반경 +0.4, 드론 탄 사거리 +1.5", M(WeaponParam.OrbitRadius, ADD, 0.4f), M(WeaponParam.ChildRange, ADD, 1.5f)),
                Count(1, "드론"),
                S("드론 탄 데미지 +20%", "드론 탄 데미지 +20%", M(WeaponParam.ChildDamage, MUL, 0.2f)),
                S("드론 탄 공속 +15%", "드론 탄 공격 속도 +15%", M(WeaponParam.ChildAttackSpeed, MUL, 0.15f)),
                S("★ 드론 탄 관통 +2", "드론 탄 관통 +2", M(WeaponParam.ChildHitCount, ADD, 2f)),
            };

            t["Explosive"] = new[]
            {
                Dmg(),
                S("폭발 반경 +20%", "폭발 반경 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                As(), Dmg(),
                S("★ 개수 +1", "폭탄 개수 +1", M(WeaponParam.FireCount, ADD, 1f)),
                S("넉백 +30%", "넉백 +30%", M(WeaponParam.Knockback, MUL, 0.3f)),
                S("폭발 반경 +20%", "폭발 반경 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                Dmg(), As(),
                Flag(WeaponParam.FlagSecondaryExplosion, "2차 폭발", "0.3초 뒤 같은 자리에 반경 60%, 데미지 50% 폭발"),
            };

            t["Laser"] = new[]
            {
                Dmg(),
                S("관통 +2", "관통 +2", M(WeaponParam.HitCount, ADD, 2f)),
                As(),
                S("두께 +30%", "광선 두께 +30%", M(WeaponParam.LaserWidth, MUL, 0.3f)),
                S("★ 개수 +1", "광선 +1 (부채꼴)", M(WeaponParam.FireCount, ADD, 1f)),
                Dmg(),
                S("관통 +3", "관통 +3", M(WeaponParam.HitCount, ADD, 3f)),
                S("사거리 +1.5", "사거리 +1.5", M(WeaponParam.Range, ADD, 1.5f)),
                Dmg(),
                S("★ 개수 +2, 부채꼴 각도 +5", "광선 +2, 부채꼴 각도 +5", M(WeaponParam.FireCount, ADD, 2f), M(WeaponParam.FanAngle, ADD, 5f)),
            };

            t["Mine"] = new[]
            {
                Dmg(),
                S("폭발 반경 +20%", "폭발 반경 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                Count(1, "지뢰"),
                S("활성화 지연 -50%", "활성화 지연 -50%", M(WeaponParam.ArmDelay, MUL, -0.5f)),
                S("★ 폭발 반경 +40%, 넉백 +50%", "폭발 반경 +40%, 넉백 +50%", M(WeaponParam.AreaRadius, MUL, 0.4f), M(WeaponParam.Knockback, MUL, 0.5f)),
                As(), Dmg(),
                S("지뢰 수명 +5초", "지뢰 수명 +5초", M(WeaponParam.Lifetime, ADD, 5f)),
                Count(1, "지뢰"),
                Flag(WeaponParam.FlagChainExplosion, "연쇄 폭발", "폭발 범위 안의 활성 지뢰도 터진다"),
            };

            t["PoisonZone"] = new[]
            {
                Dmg(),
                S("장판 크기 +20%", "장판 크기 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                S("지속 +1초", "지속 시간 +1초", M(WeaponParam.Lifetime, ADD, 1f)),
                S("틱 간격 -20%", "피해 간격 -20%", M(WeaponParam.TickInterval, MUL, -0.2f)),
                S("★ 개수 +1", "장판 개수 +1", M(WeaponParam.FireCount, ADD, 1f)),
                Dmg(),
                S("장판 크기 +20%", "장판 크기 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                S("지속 +1초", "지속 시간 +1초", M(WeaponParam.Lifetime, ADD, 1f)),
                Dmg(),
                Flag(WeaponParam.FlagSlowZone, "둔화 장판", "장판 위 적 이동속도 -40%"),
            };

            t["Shockwave"] = new[]
            {
                Dmg(),
                S("반경 +15%", "파동 반경 +15%", M(WeaponParam.AreaRadius, MUL, 0.15f)),
                As(),
                S("넉백 +25%", "넉백 +25%", M(WeaponParam.Knockback, MUL, 0.25f)),
                S("★ 2연속 파동", "파동을 2번 연속으로 낸다", M(WeaponParam.FireCount, ADD, 1f), M(WeaponParam.FireDelay, ADD, 0.25f)),
                Dmg(),
                S("반경 +15%", "파동 반경 +15%", M(WeaponParam.AreaRadius, MUL, 0.15f)),
                As(), Dmg(),
                Flag(WeaponParam.FlagContractWave, "수축 파동", "최대 반경에서 다시 줄어들며 데미지 50%로 한 번 더 타격"),
            };

            t["SplitShot"] = new[]
            {
                Dmg(),
                S("조각 +2", "조각 수 +2", M(WeaponParam.FragmentCount, ADD, 2f)),
                As(),
                S("조각 데미지 비율 +0.15", "조각 데미지 비율 +0.15", M(WeaponParam.FragmentDamageRate, ADD, 0.15f)),
                S("★ 본탄 개수 +1", "본탄 개수 +1", M(WeaponParam.FireCount, ADD, 1f)),
                S("조각 거리 +30%", "조각 비행 거리 +30%", M(WeaponParam.FragmentDistance, MUL, 0.3f)),
                S("조각 +2", "조각 수 +2", M(WeaponParam.FragmentCount, ADD, 2f)),
                Dmg(),
                S("조각 데미지 비율 +0.15", "조각 데미지 비율 +0.15", M(WeaponParam.FragmentDamageRate, ADD, 0.15f)),
                Flag(WeaponParam.FlagResplit, "재분열", "조각이 처음 맞힌 적 자리에서 3개로 1회 재분열"),
            };

            t["Sweep"] = new[]
            {
                Dmg(),
                S("칼날 길이 +20%", "칼날 길이 +20%", M(WeaponParam.Reach, MUL, 0.2f)),
                As(),
                S("넉백 +25%", "넉백 +25%", M(WeaponParam.Knockback, MUL, 0.25f)),
                S("★ 양방향", "반대 방향으로도 동시에 휘두른다", M(WeaponParam.FireCount, ADD, 1f)),
                Dmg(),
                S("칼날 길이 +20%", "칼날 길이 +20%", M(WeaponParam.Reach, MUL, 0.2f)),
                As(), Dmg(),
                Flag(WeaponParam.FlagSlashWave, "검기", "휘두를 때 조준 방향으로 데미지 60% 관통 검기 발사"),
            };

            t["Chain"] = new[]
            {
                Dmg(),
                S("전이 +1", "전이 횟수 +1", M(WeaponParam.ChainCount, ADD, 1f)),
                As(),
                S("전이 범위 +20%", "전이 범위 +20%", M(WeaponParam.ChainRange, MUL, 0.2f)),
                S("★ 개수 +1", "번개 개수 +1", M(WeaponParam.FireCount, ADD, 1f)),
                Dmg(),
                S("전이 +2", "전이 횟수 +2", M(WeaponParam.ChainCount, ADD, 2f)),
                As(), Dmg(),
                Flag(WeaponParam.FlagChainAmplify, "증폭", "전이할 때마다 데미지 +15% 누적"),
            };

            return t;
        }

        // ---------------- utils ----------------

        private static void SetFloat(SerializedObject _so, string _name, float _v)
        {
            var p = _so.FindProperty(_name);
            if (null != p)
            {
                p.floatValue = _v;
            }
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            var c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }

        private static void EditPrefab(string _path, System.Action<GameObject> _edit)
        {
            var root = PrefabUtility.LoadPrefabContents(_path);
            try
            {
                _edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, _path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T LoadOrCreate<T>(string _path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(_path);
            if (null == a)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, _path);
            }
            return a;
        }

        private static void EnsureFolder(string _path)
        {
            if (AssetDatabase.IsValidFolder(_path))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(_path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(_path));
        }
    }
}
