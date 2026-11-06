using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 차원별 추가 무기 7종(Vine, Spirit, Root, Saw, Flame, Tornado, Lightning)의
    /// 무기 / 발사체 프리팹, 업그레이드 테이블, 보상 효과, 보상 에셋을 생성·갱신한다 (멱등)
    /// - 사양: docs/weapon-dimension-extra-design.md "통합 정보" + 차원별 10단계 표
    /// - 프리팹이 이미 있으면 내용을 열어 갱신한다 (GUID / fileID 유지, 다른 에셋의 참조가 끊기지 않게)
    /// - 렌더러 정렬 레이어는 attack. 스프라이트 / 보상 아이콘은 기존 무기 아트를 임시로 쓴다 (아트 단계에서 교체)
    /// </summary>
    public static class ExtraWeaponBuilder
    {
        private const string WEAPON_DIR = "Assets/prefab/weapon";

        // Flame 공속 (쿨타임 1 / 2.22 ≈ 0.45s, 분사 0.8s 와 합쳐 주기 1.25s)
        public const float FLAME_ATTACK_SPEED = 2.22f;
        private const string TABLE_DIR = "Assets/prefab/Upgrade/WeaponUpgrade";
        private const string EFFECT_DIR = "Assets/prefab/Upgrade/Effects";
        private const string REWARD_DIR = "Assets/prefab/Upgrade/Weapon";
        private const string ART = "Assets/Resources/Textuer/Art/Weapon/";
        private const string LINE_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string SORTING = "attack";

        private const StatModifierMode ADD = StatModifierMode.Add;
        private const StatModifierMode MUL = StatModifierMode.Multiply;

        // 업그레이드 보상 가중치 (docs/balance-design.md 4.3)
        private const float UPGRADE_REWARD_WEIGHT = 1.5f;

        // 이름 / 설명은 StringTable 키 reward.weapon.<id>.name / .explain (원문은 StringTable.csv)
        private struct RewardDef
        {
            public string name;
            public string iconFrom; // 임시 아이콘을 가져올 기존 보상
        }

        private static readonly RewardDef[] REWARDS =
        {
            new RewardDef { name = "Vine", iconFrom = "PoisonZone" },
            new RewardDef { name = "Spirit", iconFrom = "Drone" },
            new RewardDef { name = "Root", iconFrom = "PoisonZone" },
            new RewardDef { name = "Saw", iconFrom = "Boomerang" },
            new RewardDef { name = "Flame", iconFrom = "Laser" },
            new RewardDef { name = "Tornado", iconFrom = "Shockwave" },
            new RewardDef { name = "Lightning", iconFrom = "Chain" },
        };

        [MenuItem("Tools/Builders/ExtraWeapon")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ExtraWeaponBuilder]");
            LocalizationBuilderUtil.Reload();
            EnsureFolder(TABLE_DIR);
            EnsureFolder(EFFECT_DIR);
            EnsureFolder(REWARD_DIR);

            var refWeaponGo = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON_DIR + "/Boomerang/Boomerang.prefab");
            Weapon refWeapon = null != refWeaponGo ? refWeaponGo.GetComponent<Weapon>() : null;

            // 1. 발사체 / 무기 프리팹
            var projectiles = new Dictionary<string, GameObject>
            {
                { "Vine", BuildVine() },
                { "Spirit", BuildSpirit() },
                { "Root", BuildRoot() },
                { "Saw", BuildSaw() },
                { "Flame", BuildFlame() },
                { "Tornado", BuildTornado() },
                { "Lightning", BuildLightning() },
            };

            var weapons = new Dictionary<string, Weapon>();
            foreach (var kv in projectiles)
            {
                weapons[kv.Key] = BuildWeapon(kv.Key, kv.Value, refWeaponGo, refWeapon);
                sb.AppendLine($"{kv.Key}: weapon={(null != weapons[kv.Key])} projectile={kv.Value.name}");
            }

            // 2. 업그레이드 테이블
            var tables = new Dictionary<string, WeaponUpgradeTable>();
            foreach (var kv in Tables())
            {
                var table = LoadOrCreate<WeaponUpgradeTable>($"{TABLE_DIR}/{kv.Key}UpgradeTable.asset");
                table.SetSteps(kv.Value, UPGRADE_REWARD_WEIGHT);
                EditorUtility.SetDirty(table);
                tables[kv.Key] = table;
            }
            sb.AppendLine($"tables {tables.Count}");

            // 3. 보상 효과 / 보상
            foreach (RewardDef def in REWARDS)
            {
                var effect = LoadOrCreate<WeaponRewardEffect>($"{EFFECT_DIR}/{def.name}RewardEffect.asset");
                var eso = new SerializedObject(effect);
                eso.FindProperty("weaponPrefab").objectReferenceValue = weapons[def.name];
                eso.FindProperty("upgradeTable").objectReferenceValue = tables[def.name];
                eso.ApplyModifiedPropertiesWithoutUndo();

                var reward = LoadOrCreate<RewardData>($"{REWARD_DIR}/{def.name}Reward.asset");
                var rso = new SerializedObject(reward);
                var iconRef = AssetDatabase.LoadAssetAtPath<RewardData>($"{REWARD_DIR}/{def.iconFrom}Reward.asset");
                if (null != iconRef)
                {
                    rso.FindProperty("image").objectReferenceValue = new SerializedObject(iconRef).FindProperty("image").objectReferenceValue;
                }
                rso.FindProperty("rewardName").stringValue = LocalizationBuilderUtil.WeaponRewardNameKey(def.name);
                rso.FindProperty("hasKey").boolValue = false;
                rso.FindProperty("hasValue").boolValue = false;
                rso.FindProperty("explain1").stringValue = LocalizationBuilderUtil.WeaponRewardExplainKey(def.name);
                rso.FindProperty("explain2").stringValue = string.Empty;
                rso.FindProperty("effect").objectReferenceValue = effect;
                rso.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine($"reward {reward.name} -> {effect.name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }

        // ---------------- weapons ----------------

        private static Weapon BuildWeapon(string _name, GameObject _projectile, GameObject _refGo, Weapon _ref)
        {
            string path = $"{WEAPON_DIR}/{_name}/{_name}.prefab";
            EditOrCreatePrefab(path, _name, root =>
            {
                if (null != _refGo)
                {
                    root.layer = _refGo.layer;
                }
                var w = GetOrAdd<Weapon>(root);
                w.projectile = _projectile.GetComponent<Projectile>();
                w.targetLayer = null != _ref ? _ref.targetLayer : (LayerMask)0;
                w.speedRate = 1f;
                w.fireDelay = 0f;
                w.spreadRange = 0f;
                w.knockbackForce = 0f;
                w.chainCount = 0;
                w.chainRange = 0f;
                w.hitCount = 1;
                w.lifetime = 0f;
                w.areaRadius = 0f;
                w.range = 0f;
                w.fireCount = 1;

                switch (_name)
                {
                    case "Vine":
                        Set(w, WEAPON_TYPE.Repeat, 6f, 4f, 0f, 1, 16);
                        w.areaRadius = 0.6f;
                        w.lifetime = 3f;
                        break;
                    case "Spirit":
                        Set(w, WEAPON_TYPE.Capped, 8f, 0.5f, 4f, 3, 5);
                        w.hitCount = 6;
                        break;
                    case "Root":
                        Set(w, WEAPON_TYPE.Repeat, 5f, 0.4f, 6f, 1, 4);
                        w.areaRadius = 1.2f;
                        w.lifetime = 4f;
                        break;
                    case "Saw":
                        Set(w, WEAPON_TYPE.Wave, 15f, 1f, 0f, 2, 4);
                        break;
                    case "Flame":
                        // Capped: 분사가 끝난 뒤 쿨타임이 돈다 (FlameProjectile)
                        // 공속 2.22 = 쿨타임 0.45s. 분사 0.8s + 0.45s = 주기 1.25s 로 Repeat(공속 0.8) 시절 DPS 유지
                        Set(w, WEAPON_TYPE.Capped, 4f, FLAME_ATTACK_SPEED, 3f, 1, 2);
                        w.knockbackForce = 1f;
                        break;
                    case "Tornado":
                        Set(w, WEAPON_TYPE.Capped, 6f, 0.33f, 0f, 1, 2);
                        w.lifetime = 5f;
                        break;
                    case "Lightning":
                        Set(w, WEAPON_TYPE.Repeat, 25f, 0.7f, 8f, 1, 3);
                        w.areaRadius = 0.8f;
                        break;
                }
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Weapon>();
        }

        private static void Set(Weapon _w, WEAPON_TYPE _type, float _dmg, float _as, float _range, int _fire, int _preLoad)
        {
            _w.weaponType = _type;
            _w.damage = _dmg;
            _w.attackSpeed = _as;
            _w.range = _range;
            _w.fireCount = _fire;
            _w.preLoadCount = _preLoad;
        }

        // ---------------- projectiles ----------------

        private static GameObject BuildVine()
        {
            string dir = WEAPON_DIR + "/Vine";
            EnsureFolder(dir);

            // 가시 (★10)
            string thornPath = dir + "/VineThorn.prefab";
            EditOrCreatePrefab(thornPath, "VineThorn", root =>
            {
                var col = GetOrAdd<CircleCollider2D>(root);
                col.isTrigger = true;
                col.radius = 0.15f;
                var straight = new SerializedObject(GetOrAdd<Straight>(root));
                SetFloat(straight, "speed", 10f);
                straight.ApplyModifiedPropertiesWithoutUndo();
                GetOrAdd<Lifetime>(root).lifetime = 1f;
                GetOrAdd<OneHitProjectile>(root);
                GetOrAdd<BlackBoardHandler>(root);
                var sr = Visual(root, "visual", ART + "D1_Weapon_SplitShot_Fragment.png", 0.3f);
                sr.sortingOrder = 1;
            });

            string path = dir + "/VineProjectile.prefab";
            EditOrCreatePrefab(path, "VineProjectile", root =>
            {
                RemoveIfAny<PoisonZoneLandMovement>(root);
                var p = GetOrAdd<VineProjectile>(root);
                var bb = GetOrAdd<BlackBoardHandler>(root);
                p.blackBoardHandler = bb;
                GetOrAdd<Lifetime>(root);
                var scale = GetOrAdd<ScaleOverLifetime>(root);
                scale.useAreaRadius = true;
                var sr = ZoneVisual(root);

                var so = new SerializedObject(p);
                so.FindProperty("thornPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(thornPath).GetComponent<Projectile>();
                SetRef(so, "segmentRenderer", sr);
                SetString(so, "sortingLayerName", SORTING);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildRoot()
        {
            string dir = WEAPON_DIR + "/Root";
            EnsureFolder(dir);
            string path = dir + "/RootProjectile.prefab";
            EditOrCreatePrefab(path, "RootProjectile", root =>
            {
                RemoveIfAny<PoisonZoneLandMovement>(root);
                var p = GetOrAdd<RootProjectile>(root);
                p.blackBoardHandler = GetOrAdd<BlackBoardHandler>(root);
                GetOrAdd<Lifetime>(root);
                GetOrAdd<ScaleOverLifetime>(root).useAreaRadius = true;
                var sr = ZoneVisual(root);

                var so = new SerializedObject(p);
                SetFloat(so, "rootSlowRate", 0.4f);
                SetRef(so, "zoneRenderer", sr);
                SetString(so, "sortingLayerName", SORTING);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildSpirit()
        {
            string dir = WEAPON_DIR + "/Spirit";
            EnsureFolder(dir);
            string path = dir + "/SpiritProjectile.prefab";
            EditOrCreatePrefab(path, "SpiritProjectile", root =>
            {
                var p = GetOrAdd<SpiritProjectile>(root);
                var sr = GetOrAdd<SpriteRenderer>(root);
                sr.sprite = LoadSprite(ART + "D1_Weapon_SplitShot_Projectile.png");
                sr.sortingLayerName = SORTING;
                NormalizeScale(root.transform, sr.sprite, 0.6f);

                var so = new SerializedObject(p);
                SetRef(so, "spiritRenderer", sr);
                SetString(so, "sortingLayerName", SORTING);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildSaw()
        {
            string dir = WEAPON_DIR + "/Saw";
            EnsureFolder(dir);
            string path = dir + "/SawProjectile.prefab";
            EditOrCreatePrefab(path, "SawProjectile", root =>
            {
                var p = GetOrAdd<SawProjectile>(root);
                // 판정 반지름 0.5 = 지름 1
                var sr = Visual(root, "visual", ART + "D1_Weapon_Boomerang_Projectile.png", 1f);

                var so = new SerializedObject(p);
                SetRef(so, "scaleTarget", sr.transform);
                SetRef(so, "spinTarget", sr.transform);
                SetString(so, "sortingLayerName", SORTING);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildFlame()
        {
            string dir = WEAPON_DIR + "/Flame";
            EnsureFolder(dir);
            string path = dir + "/FlameProjectile.prefab";
            EditOrCreatePrefab(path, "FlameProjectile", root =>
            {
                var p = GetOrAdd<FlameProjectile>(root);
                Transform visualRoot = GetOrCreateChild(root.transform, "visual");
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localScale = Vector3.one;
                // +x 방향으로 길이 3 (visualBaseRange), 끝 폭은 60° 부채꼴 기준 약 3.4
                Transform spriteT = GetOrCreateChild(visualRoot, "flame");
                var sr = GetOrAdd<SpriteRenderer>(spriteT.gameObject);
                sr.sprite = LoadSprite(ART + "D1_Weapon_Sweep_Arc.png");
                sr.sortingLayerName = SORTING;
                spriteT.localPosition = new Vector3(1.5f, 0f, 0f);
                FitScale(spriteT, sr.sprite, 3f, 3.4f);

                var so = new SerializedObject(p);
                SetRef(so, "visualRoot", visualRoot);
                SetFloat(so, "visualBaseRange", 3f);
                SetString(so, "sortingLayerName", SORTING);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildTornado()
        {
            string dir = WEAPON_DIR + "/Tornado";
            EnsureFolder(dir);
            string path = dir + "/TornadoProjectile.prefab";
            EditOrCreatePrefab(path, "TornadoProjectile", root =>
            {
                var p = GetOrAdd<TornadoProjectile>(root);
                // 타격 반경 0.8 = 지름 1.6
                var sr = Visual(root, "visual", ART + "D3_Weapon_Shockwave_Ring.png", 1.6f);

                var so = new SerializedObject(p);
                SetRef(so, "scaleTarget", sr.transform);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildLightning()
        {
            string dir = WEAPON_DIR + "/Lightning";
            EnsureFolder(dir);
            string path = dir + "/LightningProjectile.prefab";
            var lineMat = AssetDatabase.LoadAssetAtPath<Material>(LINE_MAT);
            EditOrCreatePrefab(path, "LightningProjectile", root =>
            {
                var p = GetOrAdd<LightningProjectile>(root);
                // markerUnitRadius 0.5 = 지름 1
                var marker = Visual(root, "Marker", ART + "D1_Weapon_PoisonZone_Area.png", 1f);
                LineRenderer bolt = Line(root.transform, "Bolt", 0.15f, lineMat);
                LineRenderer link = Line(root.transform, "Link", 0.1f, lineMat);

                var so = new SerializedObject(p);
                SetRef(so, "markerTemplate", marker.gameObject);
                SetRef(so, "boltTemplate", bolt);
                SetRef(so, "linkLine", link);
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---------------- visual helpers (플레이스홀더) ----------------

        // 독 장판 조각과 같은 구성 (visual 자식, 크기 1) - ScaleOverLifetime 이 루트 크기를 areaRadius 로 맞춘다
        private static SpriteRenderer ZoneVisual(GameObject _root)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON_DIR + "/PoisonZone/PoisonZoneProjectile.prefab");
            var srcSr = null != src ? src.GetComponentInChildren<SpriteRenderer>(true) : null;
            Transform t = GetOrCreateChild(_root.transform, "visual");
            var sr = GetOrAdd<SpriteRenderer>(t.gameObject);
            if (null != srcSr)
            {
                t.localScale = srcSr.transform.localScale;
                sr.sprite = srcSr.sprite;
                sr.sharedMaterial = srcSr.sharedMaterial;
                sr.color = srcSr.color;
            }
            sr.sortingLayerName = SORTING;
            return sr;
        }

        private static SpriteRenderer Visual(GameObject _root, string _child, string _sprite, float _size)
        {
            Transform t = GetOrCreateChild(_root.transform, _child);
            t.localPosition = Vector3.zero;
            var sr = GetOrAdd<SpriteRenderer>(t.gameObject);
            sr.sprite = LoadSprite(_sprite);
            sr.sortingLayerName = SORTING;
            NormalizeScale(t, sr.sprite, _size);
            return sr;
        }

        private static LineRenderer Line(Transform _parent, string _name, float _width, Material _mat)
        {
            Transform t = GetOrCreateChild(_parent, _name);
            var lr = GetOrAdd<LineRenderer>(t.gameObject);
            lr.startWidth = _width;
            lr.endWidth = _width;
            lr.numCapVertices = 2;
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.sortingLayerName = SORTING;
            if (null != _mat)
            {
                lr.sharedMaterial = _mat;
            }
            return lr;
        }

        private static Sprite LoadSprite(string _path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(_path);
            if (null == s)
            {
                Debug.LogWarning("[ExtraWeaponBuilder] 스프라이트 없음 " + _path);
            }
            return s;
        }

        // 스프라이트 가로 길이가 _size 가 되도록 크기를 맞춘다
        private static void NormalizeScale(Transform _t, Sprite _s, float _size)
        {
            if (null == _s || _s.bounds.size.x <= 0f)
            {
                _t.localScale = Vector3.one * _size;
                return;
            }
            float k = _size / Mathf.Max(_s.bounds.size.x, _s.bounds.size.y);
            _t.localScale = new Vector3(k, k, 1f);
        }

        private static void FitScale(Transform _t, Sprite _s, float _w, float _h)
        {
            if (null == _s || _s.bounds.size.x <= 0f || _s.bounds.size.y <= 0f)
            {
                _t.localScale = new Vector3(_w, _h, 1f);
                return;
            }
            _t.localScale = new Vector3(_w / _s.bounds.size.x, _h / _s.bounds.size.y, 1f);
        }

        // ---------------- tables ----------------

        private static WeaponModifier M(WeaponParam _p, StatModifierMode _m, float _v) => new WeaponModifier(_p, _m, _v);

        // 표는 한국어 원문으로 적고, 단계에는 StringTable 키를 기록한다 (원문 → 키 역조회. 표에 없으면 경고 후 원문)
        private static WeaponUpgradeStep S(string _text, params WeaponModifier[] _mods) => Step(_text, _text, _mods);

        private static WeaponUpgradeStep Star(string _text, params WeaponModifier[] _mods) => Step("★ " + _text, _text, _mods);

        private static WeaponUpgradeStep Step(string _title, string _desc, WeaponModifier[] _mods)
            => new WeaponUpgradeStep(LocalizationBuilderUtil.KeyOfKorean(_title), LocalizationBuilderUtil.KeyOfKorean(_desc), _mods);

        private static WeaponUpgradeStep Dmg() => S("데미지 +20%", M(WeaponParam.Damage, MUL, 0.2f));

        private static WeaponUpgradeStep As() => S("공속 +15%", M(WeaponParam.AttackSpeed, MUL, 0.15f));

        private static WeaponUpgradeStep Flag(WeaponParam _p, string _text) => Star(_text, M(_p, ADD, 1f));

        private static Dictionary<string, WeaponUpgradeStep[]> Tables()
        {
            var t = new Dictionary<string, WeaponUpgradeStep[]>();

            t["Vine"] = new[]
            {
                Dmg(),
                S("범위 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                S("틱 간격 -10%", M(WeaponParam.TickInterval, MUL, -0.1f)),
                Dmg(),
                Star("덩굴 수명 +50%", M(WeaponParam.Lifetime, MUL, 0.5f)),
                Dmg(),
                S("범위 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                Dmg(), Dmg(),
                Flag(WeaponParam.FlagVineThorn, "가시 발사"),
            };

            t["Spirit"] = new[]
            {
                Dmg(), As(),
                S("추적 속도 +20%", M(WeaponParam.ChaseSpeed, MUL, 0.2f)),
                Dmg(),
                Star("정령 +2 (최대 5)", M(WeaponParam.FireCount, ADD, 2f)),
                Dmg(), As(), Dmg(),
                S("사거리 +1.5", M(WeaponParam.Range, ADD, 1.5f)),
                Star("공격 횟수 +4", M(WeaponParam.HitCount, ADD, 4f)),
            };

            t["Root"] = new[]
            {
                Dmg(),
                S("범위 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                As(), Dmg(),
                Star("지역 +1 (2개)", M(WeaponParam.FireCount, ADD, 1f)),
                Dmg(),
                S("수명 +1초", M(WeaponParam.Lifetime, ADD, 1f)),
                Dmg(), Dmg(),
                Flag(WeaponParam.FlagRootChase, "추적"),
            };

            t["Saw"] = new[]
            {
                Dmg(),
                S("크기 +20%", M(WeaponParam.ProjectileScale, MUL, 0.2f)),
                S("이동 속도 +20%", M(WeaponParam.SpeedRate, MUL, 0.2f)),
                Dmg(),
                Star("톱날 +2 (4개)", M(WeaponParam.FireCount, ADD, 2f)),
                Dmg(), As(), Dmg(), Dmg(),
                Flag(WeaponParam.FlagSawBounce, "튕김"),
            };

            t["Flame"] = new[]
            {
                Dmg(),
                S("사거리 +20%", M(WeaponParam.Range, MUL, 0.2f)),
                As(), Dmg(),
                Star("부채꼴 각도 +30°", M(WeaponParam.FanAngle, ADD, 30f)),
                Dmg(),
                S("분사 시간 +0.3초", M(WeaponParam.BurstDuration, ADD, 0.3f)),
                Dmg(), Dmg(),
                Flag(WeaponParam.FlagBurn, "화상"),
            };

            t["Tornado"] = new[]
            {
                Dmg(),
                S("크기 +20%", M(WeaponParam.ProjectileScale, MUL, 0.2f)),
                S("수명 +1초", M(WeaponParam.Lifetime, ADD, 1f)),
                Dmg(),
                Star("회오리 +1 (2개)", M(WeaponParam.FireCount, ADD, 1f)),
                Dmg(),
                S("끌어당김 +30%", M(WeaponParam.PullForce, MUL, 0.3f)),
                Dmg(), Dmg(),
                Flag(WeaponParam.FlagTornadoPermanent, "영구"),
            };

            t["Lightning"] = new[]
            {
                Dmg(),
                S("범위 +20%", M(WeaponParam.AreaRadius, MUL, 0.2f)),
                As(), Dmg(),
                Star("대상 +2 (최대 3)", M(WeaponParam.StrikeCount, ADD, 2f)),
                Dmg(),
                S("사거리 +1.5", M(WeaponParam.Range, ADD, 1.5f)),
                Dmg(), Dmg(),
                Flag(WeaponParam.FlagStormLink, "연결"),
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

        private static void SetString(SerializedObject _so, string _name, string _v)
        {
            var p = _so.FindProperty(_name);
            if (null != p)
            {
                p.stringValue = _v;
            }
        }

        private static void SetRef(SerializedObject _so, string _name, Object _v)
        {
            var p = _so.FindProperty(_name);
            if (null != p)
            {
                p.objectReferenceValue = _v;
            }
            else
            {
                Debug.LogWarning("[ExtraWeaponBuilder] 필드 없음 " + _so.targetObject.GetType().Name + "." + _name);
            }
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            var c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }

        private static void RemoveIfAny<T>(GameObject _go) where T : Component
        {
            var c = _go.GetComponent<T>();
            if (null != c)
            {
                Object.DestroyImmediate(c, true);
            }
        }

        private static Transform GetOrCreateChild(Transform _parent, string _name)
        {
            Transform t = _parent.Find(_name);
            if (null == t)
            {
                t = new GameObject(_name).transform;
                t.SetParent(_parent, false);
            }
            return t;
        }

        // 있으면 열어서 갱신, 없으면 새로 만든다
        private static void EditOrCreatePrefab(string _path, string _name, System.Action<GameObject> _edit)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(_path).Replace('\\', '/'));
            if (null == AssetDatabase.LoadAssetAtPath<GameObject>(_path))
            {
                var go = new GameObject(_name);
                try
                {
                    _edit(go);
                    PrefabUtility.SaveAsPrefabAsset(go, _path);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
                return;
            }

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
