using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// 차원 무기 통합 빌더 (멱등)
    /// 1. 뿌리 속박(Root) 에셋 제거 + GameScene RewardManager 풀 / WeaponTest 컨트롤러의 참조 정리
    /// 2. "Stage" 레이어 (빈 사용자 레이어 슬롯)
    /// 3. 차원 무기 프리팹 6종 (prefab/weapon/Dimension), DimensionWeaponData 6종 + DimensionFusionRegistry (prefab/Upgrade/Dimension)
    /// 4. LevelUpUI 프리팹에 중앙 합성 카드 계층 + LevelUpUI 필드 연결
    /// 5. GameScene 에 StageSpace (카메라 4대는 FinalBossDirector 와 동일)
    /// 후속: DimensionDataBuilder → WeaponTagBuilder → ArtResourceBuilder
    /// </summary>
    public static class DimensionWeaponBuilder
    {
        private const string GAME_SCENE = "Assets/Scenes/GameScene.unity";
        private const string WEAPON_TEST_SCENE = "Assets/Scenes/WeaponTest.unity";
        private const string WEAPON_DIR = "Assets/prefab/weapon";
        private const string RESULT_DIR = "Assets/prefab/weapon/Dimension";
        private const string REWARD_DIR = "Assets/prefab/Upgrade/Weapon";
        private const string DATA_DIR = "Assets/prefab/Upgrade/Dimension";
        private const string REGISTRY_PATH = DATA_DIR + "/DimensionFusionRegistry.asset";
        private const string LEVELUP_PREFAB = "Assets/prefab/UI/LevelUpUI.prefab";
        private const string FONT_PATH = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        private const string STAGE_LAYER = "Stage";
        private const string CARD_NAME = "DimensionFusionCard";
        private const float FUSION_GROUP_OFFSET = 200f;

        private static readonly string[] ROOT_ASSETS =
        {
            "Assets/prefab/Upgrade/Weapon/RootReward.asset",
            "Assets/prefab/Upgrade/Effects/RootRewardEffect.asset",
            "Assets/prefab/Upgrade/WeaponUpgrade/RootUpgradeTable.asset",
            "Assets/prefab/weapon/Root",
        };

        private struct Def
        {
            public string name;
            public Type type;
            public string snake;
            public string matA;
            public string matB;
            public float damage;
            public float attackSpeed;
            public int fireCount;
            public float range;     // < 0 이면 기본값 유지
            public float lifetime;
            public float areaRadius;
        }

        private static readonly Def[] DEFS =
        {
            new Def { name = "SpiritSquadron", type = typeof(SpiritSquadron), snake = "spirit_squadron", matA = "Spirit", matB = "Drone",
                damage = 39f, attackSpeed = 2f, fireCount = 3, range = 40f },
            new Def { name = "ThornSaw", type = typeof(ThornSaw), snake = "thorn_saw", matA = "Vine", matB = "Saw",
                damage = 31.2f, attackSpeed = 0.5f, fireCount = 2, range = -1f },
            new Def { name = "PhantomWhirl", type = typeof(PhantomWhirl), snake = "phantom_whirl", matA = "Sweep", matB = "SpinBlade",
                damage = 72f, attackSpeed = 4f, fireCount = 1, range = -1f },
            new Def { name = "PrismLaser", type = typeof(PrismLaser), snake = "prism_laser", matA = "SplitShot", matB = "Laser",
                damage = 234f, attackSpeed = 0.5f, fireCount = 2, range = 12f },
            new Def { name = "PlagueMine", type = typeof(PlagueMine), snake = "plague_mine", matA = "PoisonZone", matB = "Mine",
                damage = 234f, attackSpeed = 0.5f, fireCount = 2, range = 20f, lifetime = 3f, areaRadius = 1.5f },
            new Def { name = "BlazeBoomerang", type = typeof(BlazeBoomerang), snake = "blaze_boomerang", matA = "Boomerang", matB = "Flame",
                // lifetime 0 → 부메랑 수명은 defaultFlightLifetime(5초, 원본 BoomerangProjectile Lifetime 과 같음)
                damage = 46.8f, attackSpeed = 0.5f, fireCount = 1, range = 12f, lifetime = 0f, areaRadius = 1.2f },
        };

        [MenuItem("Tools/Builders/DimensionWeapon")]
        private static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            try
            {
                CleanupRoot(sb);
                int stageLayer = EnsureLayer(STAGE_LAYER, sb);
                EnsureFolder(RESULT_DIR);
                EnsureFolder(DATA_DIR);

                var datas = new List<DimensionWeaponData>();
                foreach (Def def in DEFS)
                {
                    DimensionWeapon prefab = BuildResultPrefab(def, stageLayer, sb);
                    DimensionWeaponData data = BuildData(def, prefab, sb);
                    if (null != data)
                    {
                        datas.Add(data);
                    }
                }
                DimensionFusionRegistry registry = BuildRegistry(datas, sb);
                BuildLevelUpCard(registry, sb);
                BuildScene(sb);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                sb.AppendLine("에러: " + e);
            }
            return sb.ToString();
        }

        #region 1. Root 정리
        private static void CleanupRoot(StringBuilder _sb)
        {
            var rootReward = AssetDatabase.LoadAssetAtPath<RewardData>("Assets/prefab/Upgrade/Weapon/RootReward.asset");
            var rootEffect = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/prefab/Upgrade/Effects/RootRewardEffect.asset");

            // GameScene RewardManager 풀
            var scene = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Single);
            int removed = 0;
            foreach (RewardManager rm in UnityEngine.Object.FindObjectsByType<RewardManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(rm);
                SerializedProperty pools = so.FindProperty("pools");
                for (int i = 0; i < pools.arraySize; i++)
                {
                    SerializedProperty entries = pools.GetArrayElementAtIndex(i).FindPropertyRelative("pool.entries");
                    if (null == entries)
                    {
                        continue;
                    }
                    for (int j = entries.arraySize - 1; j >= 0; j--)
                    {
                        UnityEngine.Object item = entries.GetArrayElementAtIndex(j).FindPropertyRelative("item").objectReferenceValue;
                        if (null == item || (null != rootReward && item == rootReward))
                        {
                            entries.DeleteArrayElementAtIndex(j);
                            removed++;
                        }
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            _sb.AppendLine("GameScene 보상 풀 Root/빈 항목 제거: " + removed);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // WeaponTest 컨트롤러
            if (null != AssetDatabase.LoadAssetAtPath<SceneAsset>(WEAPON_TEST_SCENE))
            {
                scene = EditorSceneManager.OpenScene(WEAPON_TEST_SCENE, OpenSceneMode.Single);
                removed = 0;
                foreach (var c in UnityEngine.Object.FindObjectsByType<WeaponTestController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var so = new SerializedObject(c);
                    SerializedProperty arr = so.FindProperty("weaponEffects");
                    for (int j = arr.arraySize - 1; j >= 0; j--)
                    {
                        UnityEngine.Object item = arr.GetArrayElementAtIndex(j).objectReferenceValue;
                        if (null == item || (null != rootEffect && item == rootEffect))
                        {
                            arr.GetArrayElementAtIndex(j).objectReferenceValue = null;
                            arr.DeleteArrayElementAtIndex(j);
                            removed++;
                        }
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                _sb.AppendLine("WeaponTest weaponEffects Root/빈 항목 제거: " + removed);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            foreach (string path in ROOT_ASSETS)
            {
                if (true == AssetDatabase.IsValidFolder(path) || null != AssetDatabase.LoadMainAssetAtPath(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    _sb.AppendLine("삭제: " + path);
                }
            }
        }
        #endregion

        #region 2. 레이어
        private static int EnsureLayer(string _name, StringBuilder _sb)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (_name == layers.GetArrayElementAtIndex(i).stringValue)
                {
                    _sb.AppendLine("레이어 " + _name + " = " + i + " (기존)");
                    return i;
                }
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (true == string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = _name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    _sb.AppendLine("레이어 " + _name + " = " + i + " (추가)");
                    return i;
                }
            }
            throw new InvalidOperationException("빈 사용자 레이어 슬롯 없음");
        }
        #endregion

        #region 3. 프리팹·SO
        private static DimensionWeapon BuildResultPrefab(Def _def, int _layer, StringBuilder _sb)
        {
            string path = RESULT_DIR + "/" + _def.name + ".prefab";
            bool exists = null != AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject root = true == exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(_def.name);
            try
            {
                root.transform.localScale = Vector3.one;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = _layer;
                }
                var weapon = root.GetComponent(_def.type) as DimensionWeapon;
                if (null == weapon)
                {
                    weapon = root.AddComponent(_def.type) as DimensionWeapon;
                }

                WeaponStatTag tags = WeaponTagBuilder.GetTags(_def.matA) | WeaponTagBuilder.GetTags(_def.matB);
                var so = new SerializedObject(weapon);
                so.FindProperty("damage").floatValue = _def.damage;
                so.FindProperty("attackSpeed").floatValue = _def.attackSpeed;
                so.FindProperty("fireCount").intValue = _def.fireCount;
                if (_def.range >= 0f)
                {
                    so.FindProperty("range").floatValue = _def.range;
                }
                so.FindProperty("lifetime").floatValue = _def.lifetime;
                so.FindProperty("areaRadius").floatValue = _def.areaRadius;
                so.FindProperty("statTags").intValue = (int)tags;
                // 정령 편대: 적이 정령의 대상뿐이면(최종 보스 등) 드론도 그 대상을 쏜다
                SerializedProperty droneFallback = so.FindProperty("droneFallbackToSpiritTarget");
                if (null != droneFallback)
                {
                    droneFallback.boolValue = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                _sb.AppendLine((exists ? "갱신 " : "생성 ") + path + " tags=" + tags);
                return saved.GetComponent(_def.type) as DimensionWeapon;
            }
            finally
            {
                if (true == exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static Weapon LoadMaterial(string _name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON_DIR + "/" + _name + "/" + _name + ".prefab");
            return null != go ? go.GetComponentInChildren<Weapon>(true) : null;
        }

        private static RewardData LoadReward(string _name)
        {
            return AssetDatabase.LoadAssetAtPath<RewardData>(REWARD_DIR + "/" + _name + "Reward.asset");
        }

        private static DimensionWeaponData BuildData(Def _def, DimensionWeapon _prefab, StringBuilder _sb)
        {
            Weapon a = LoadMaterial(_def.matA);
            Weapon b = LoadMaterial(_def.matB);
            if (null == a || null == b || null == _prefab)
            {
                _sb.AppendLine("경고: " + _def.name + " 재료/결과 없음 A=" + (null != a) + " B=" + (null != b) + " result=" + (null != _prefab));
                return null;
            }

            string path = DATA_DIR + "/" + _def.name + "Data.asset";
            var data = AssetDatabase.LoadAssetAtPath<DimensionWeaponData>(path);
            if (null == data)
            {
                data = ScriptableObject.CreateInstance<DimensionWeaponData>();
                AssetDatabase.CreateAsset(data, path);
            }
            RewardData rewardA = LoadReward(_def.matA);
            Sprite icon = null != rewardA ? rewardA.GetImage() : null;
            string key = "dimension_weapon." + _def.snake;
            data.SetRecipe(a, b, _prefab);
            data.SetDisplay(key + ".name", key + ".explain", icon);
            EditorUtility.SetDirty(data);
            _sb.AppendLine("SO " + path + " (" + _def.matA + "+" + _def.matB + ", icon=" + (null != icon ? icon.name : "없음") + ")");
            return data;
        }

        private static DimensionFusionRegistry BuildRegistry(List<DimensionWeaponData> _datas, StringBuilder _sb)
        {
            var registry = AssetDatabase.LoadAssetAtPath<DimensionFusionRegistry>(REGISTRY_PATH);
            if (null == registry)
            {
                registry = ScriptableObject.CreateInstance<DimensionFusionRegistry>();
                AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
            }
            registry.SetRecipes(_datas.ToArray());
            EditorUtility.SetDirty(registry);
            _sb.AppendLine("Registry " + REGISTRY_PATH + " 레시피 " + _datas.Count);
            return registry;
        }
        #endregion

        #region 4. LevelUpUI 합성 카드
        private static void BuildLevelUpCard(DimensionFusionRegistry _registry, StringBuilder _sb)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            GameObject root = PrefabUtility.LoadPrefabContents(LEVELUP_PREFAB);
            try
            {
                LevelUpUI ui = root.GetComponent<LevelUpUI>();
                if (null == ui)
                {
                    _sb.AppendLine("경고: LevelUpUI 프리팹에 LevelUpUI 없음");
                    return;
                }

                RectTransform card = GetOrCreateRect(root.transform, CARD_NAME);
                card.SetAsLastSibling();
                SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(400f, 540f));
                Image bg = GetOrAdd<Image>(card.gameObject);
                bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
                bg.raycastTarget = true; // 클릭 수신
                DimensionFusionCard fusion = GetOrAdd<DimensionFusionCard>(card.gameObject);

                RectTransform selectBg = GetOrCreateRect(card, "SelectBackground");
                selectBg.anchorMin = Vector2.zero;
                selectBg.anchorMax = Vector2.one;
                selectBg.offsetMin = new Vector2(-6f, -6f);
                selectBg.offsetMax = new Vector2(6f, 6f);
                selectBg.SetAsFirstSibling();
                Image selImg = GetOrAdd<Image>(selectBg.gameObject);
                selImg.color = new Color(1f, 0.85f, 0.2f, 0.35f);
                selImg.raycastTarget = false;
                selectBg.gameObject.SetActive(false);

                TMP_Text title = MakeText(card, "Title", font, 34f, new Vector2(0f, -36f), new Vector2(380f, 50f));
                LocalizationBuilderUtil.Bind(title, "ui.levelup.fusion.title");

                Image resultIcon = MakeImage(card, "ResultIcon", new Vector2(0f, -140f), new Vector2(128f, 128f));
                TMP_Text nameText = MakeText(card, "Name", font, 30f, new Vector2(0f, -236f), new Vector2(380f, 44f));

                Image matA = MakeImage(card, "MaterialIconA", new Vector2(-90f, -312f), new Vector2(72f, 72f));
                TMP_Text plus = MakeText(card, "Plus", font, 36f, new Vector2(0f, -312f), new Vector2(60f, 60f));
                plus.text = "+";
                Image matB = MakeImage(card, "MaterialIconB", new Vector2(90f, -312f), new Vector2(72f, 72f));

                TMP_Text explain = MakeText(card, "Explain", font, 22f, new Vector2(0f, -440f), new Vector2(360f, 150f));
                explain.textWrappingMode = TextWrappingModes.Normal;
                explain.alignment = TextAlignmentOptions.Top;

                GameObject leftCursor = MakeCursor(card, "LeftCursor", true);
                GameObject rightCursor = MakeCursor(card, "RightCursor", false);

                var cso = new SerializedObject(fusion);
                cso.FindProperty("resultIcon").objectReferenceValue = resultIcon;
                cso.FindProperty("nameText").objectReferenceValue = nameText;
                cso.FindProperty("explainText").objectReferenceValue = explain;
                cso.FindProperty("materialIconA").objectReferenceValue = matA;
                cso.FindProperty("materialIconB").objectReferenceValue = matB;
                cso.FindProperty("selectBackground").objectReferenceValue = selectBg.gameObject;
                cso.FindProperty("leftCursor").objectReferenceValue = leftCursor;
                cso.FindProperty("rightCursor").objectReferenceValue = rightCursor;
                RewardData[] fallback = DEFS.SelectMany(d => new[] { d.matA, d.matB }).Select(LoadReward).Where(r => null != r).ToArray();
                SerializedProperty fb = cso.FindProperty("fallbackRewards");
                fb.arraySize = fallback.Length;
                for (int i = 0; i < fallback.Length; i++)
                {
                    fb.GetArrayElementAtIndex(i).objectReferenceValue = fallback[i];
                }
                cso.ApplyModifiedPropertiesWithoutUndo();

                RectTransform left = null;
                RectTransform right = null;
                foreach (SelectPanelGroup g in root.GetComponentsInChildren<SelectPanelGroup>(true))
                {
                    if (PlayerSide.Left == g.Side) left = g.transform as RectTransform;
                    else right = g.transform as RectTransform;
                }

                var uso = new SerializedObject(ui);
                uso.FindProperty("fusionRegistry").objectReferenceValue = _registry;
                uso.FindProperty("fusionCard").objectReferenceValue = fusion;
                uso.FindProperty("fusionGroupOffset").floatValue = FUSION_GROUP_OFFSET;
                uso.FindProperty("leftGroupRect").objectReferenceValue = left;
                uso.FindProperty("rightGroupRect").objectReferenceValue = right;
                uso.ApplyModifiedPropertiesWithoutUndo();

                card.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, LEVELUP_PREFAB);
                _sb.AppendLine("LevelUpUI 프리팹: 합성 카드 + 필드 연결 (groups L=" + (null != left) + " R=" + (null != right) + ", fallback " + fallback.Length + ")");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            T c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }

        private static RectTransform GetOrCreateRect(Transform _parent, string _name)
        {
            Transform t = _parent.Find(_name);
            if (null == t)
            {
                var go = new GameObject(_name, typeof(RectTransform));
                go.layer = _parent.gameObject.layer;
                go.transform.SetParent(_parent, false);
                t = go.transform;
            }
            return (RectTransform)t;
        }

        private static void SetRect(RectTransform _rt, Vector2 _anchor, Vector2 _pos, Vector2 _size)
        {
            _rt.anchorMin = _anchor;
            _rt.anchorMax = _anchor;
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = _pos;
            _rt.sizeDelta = _size;
            _rt.localScale = Vector3.one;
        }

        private static Image MakeImage(Transform _parent, string _name, Vector2 _pos, Vector2 _size)
        {
            RectTransform rt = GetOrCreateRect(_parent, _name);
            SetRect(rt, new Vector2(0.5f, 1f), _pos, _size);
            Image img = GetOrAdd<Image>(rt.gameObject);
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private static TMP_Text MakeText(Transform _parent, string _name, TMP_FontAsset _font, float _size, Vector2 _pos, Vector2 _rect)
        {
            RectTransform rt = GetOrCreateRect(_parent, _name);
            SetRect(rt, new Vector2(0.5f, 1f), _pos, _rect);
            TextMeshProUGUI tmp = GetOrAdd<TextMeshProUGUI>(rt.gameObject);
            if (null != _font)
            {
                tmp.font = _font;
            }
            tmp.fontSize = _size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static GameObject MakeCursor(Transform _parent, string _name, bool _left)
        {
            RectTransform rt = GetOrCreateRect(_parent, _name);
            SetRect(rt, new Vector2(_left ? 0f : 1f, 0.5f), new Vector2(_left ? -14f : 14f, 0f), new Vector2(12f, 540f));
            Image img = GetOrAdd<Image>(rt.gameObject);
            img.color = _left ? new Color(0.3f, 0.7f, 1f, 1f) : new Color(1f, 0.4f, 0.4f, 1f);
            img.raycastTarget = false;
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }
        #endregion

        #region 5. 씬
        private static void BuildScene(StringBuilder _sb)
        {
            var scene = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Single);
            FinalBossDirector director = UnityEngine.Object.FindFirstObjectByType<FinalBossDirector>(FindObjectsInactive.Include);
            if (null == director)
            {
                _sb.AppendLine("경고: GameScene 에 FinalBossDirector 없음 — StageSpace 카메라 미연결");
            }

            StageSpace space = UnityEngine.Object.FindFirstObjectByType<StageSpace>(FindObjectsInactive.Include);
            if (null == space)
            {
                var go = new GameObject("StageSpace");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                space = go.AddComponent<StageSpace>();
            }

            var sso = new SerializedObject(space);
            if (null != director)
            {
                var dso = new SerializedObject(director);
                foreach (string f in new[] { "leftCamera", "rightCamera", "leftOverlayCamera", "rightOverlayCamera" })
                {
                    sso.FindProperty(f).objectReferenceValue = dso.FindProperty(f).objectReferenceValue;
                }
                sso.FindProperty("stageWorldOrigin").vector2Value = dso.FindProperty("bossWorldOrigin").vector2Value;
            }
            sso.ApplyModifiedPropertiesWithoutUndo();

            _sb.AppendLine("GameScene StageSpace: L=" + Name(sso, "leftCamera") + " R=" + Name(sso, "rightCamera")
                + " LO=" + Name(sso, "leftOverlayCamera") + " RO=" + Name(sso, "rightOverlayCamera")
                + " origin=" + sso.FindProperty("stageWorldOrigin").vector2Value);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static string Name(SerializedObject _so, string _prop)
        {
            UnityEngine.Object o = _so.FindProperty(_prop).objectReferenceValue;
            return null != o ? o.name : "null";
        }
        #endregion

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
