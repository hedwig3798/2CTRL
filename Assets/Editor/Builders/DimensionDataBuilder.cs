using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 차원 데이터 3개(Forest / Factory / Storm)와 플레이어 AnimatorOverrideController 3개를 생성·갱신한다 (멱등)
    /// - 먼저 Wave30Builder 로 차원별 웨이브를 만들고 DimensionData.waves 에 연결한다
    /// - 무기 보상은 기존 WeaponRewardEffect RewardData(prefab/Upgrade/Weapon) 를 쓰고, 첫 번째 무기가 시작 무기
    /// - 맵: 숲 = chunkLoader 1(ForestTile), 공장 = chunkLoader(FactoryTile), 폭풍 = 임시로 공장 맵 (아트 단계에서 교체)
    /// - 플레이어 외형: TempAnimationContorller 기반 오버라이드 (임시로 같은 클립), 스프라이트 Farmer 0 의 Stand 0
    /// - previewSprite 는 임시로 타일 스프라이트, monsterSkins 는 비워 둔다 (아트 단계)
    /// </summary>
    public static class DimensionDataBuilder
    {
        public const string DATA_DIR = "Assets/Resources/Data/Dimension";
        private const string ANIM_DIR = "Assets/prefab/Player/Dimension";
        private const string BASE_CONTROLLER = "Assets/prefab/Player/TempAnimationContorller.controller";
        private const string PLAYER_TEXTURE = "Assets/Resources/Textuer/Player/Farmer 0.png";
        private const string PLAYER_SPRITE = "Stand 0";
        private const string WEAPON_DIR = "Assets/prefab/weapon";
        private const string REWARD_DIR = "Assets/prefab/Upgrade/Weapon";
        public const string FOREST_LOADER = "Assets/prefab/Managers/chunkLoader 1.prefab";
        public const string FACTORY_LOADER = "Assets/prefab/Managers/chunkLoader.prefab";
        private const string FOREST_PREVIEW = "Assets/prefab/TileMap/ForestTile/ForstTile_1.prefab";
        private const string FACTORY_PREVIEW = "Assets/prefab/TileMap/FactoryTile/Factory_1.prefab";

        private struct Def
        {
            public string id;
            public string displayName;
            public Color theme;
            public string[] weapons;
            public string loader;
            public string preview;
        }

        private static readonly Def[] DEFS =
        {
            new Def { id = "Forest", displayName = "숲", theme = new Color(0.35f, 0.75f, 0.35f, 1f),
                weapons = new[] { "Boomerang", "PoisonZone", "Sweep", "SplitShot" }, loader = FOREST_LOADER, preview = FOREST_PREVIEW },
            new Def { id = "Factory", displayName = "공장", theme = new Color(0.85f, 0.55f, 0.2f, 1f),
                weapons = new[] { "Laser", "Drone", "Mine" }, loader = FACTORY_LOADER, preview = FACTORY_PREVIEW },
            // 폭풍 맵은 임시로 공장 맵 (전용 타일 셋은 아트 단계)
            new Def { id = "Storm", displayName = "폭풍", theme = new Color(0.45f, 0.55f, 1f, 1f),
                weapons = new[] { "Chain", "Shockwave", "Explosive" }, loader = FACTORY_LOADER, preview = FACTORY_PREVIEW },
        };

        [MenuItem("Tools/Builders/DimensionData")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[DimensionDataBuilder]");

            // 1. 차원별 웨이브
            sb.Append(Wave30Builder.Build());

            EnsureFolder(DATA_DIR);
            EnsureFolder(ANIM_DIR);

            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BASE_CONTROLLER);
            if (null == baseController)
            {
                return sb.AppendLine("오류: 기본 애니메이터 없음 " + BASE_CONTROLLER).ToString();
            }
            Sprite playerSprite = AssetDatabase.LoadAllAssetsAtPath(PLAYER_TEXTURE).OfType<Sprite>().FirstOrDefault(s => s.name == PLAYER_SPRITE);
            if (null == playerSprite)
            {
                sb.AppendLine("경고: 플레이어 스프라이트 없음 " + PLAYER_TEXTURE + " / " + PLAYER_SPRITE);
            }

            foreach (Def def in DEFS)
            {
                // 2. 플레이어 애니메이터 (임시로 같은 클립)
                string aocPath = ANIM_DIR + "/Player_" + def.id + ".overrideController";
                var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(aocPath);
                if (null == aoc)
                {
                    aoc = new AnimatorOverrideController(baseController);
                    AssetDatabase.CreateAsset(aoc, aocPath);
                }
                else if (aoc.runtimeAnimatorController != baseController)
                {
                    aoc.runtimeAnimatorController = baseController;
                }
                aoc.name = "Player_" + def.id;
                EditorUtility.SetDirty(aoc);

                // 3. 무기 / 보상
                Weapon start = null;
                var rewards = new RewardData[def.weapons.Length];
                for (int i = 0; i < def.weapons.Length; i++)
                {
                    string w = def.weapons[i];
                    var weaponGo = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPON_DIR + "/" + w + "/" + w + ".prefab");
                    Weapon weapon = null != weaponGo ? weaponGo.GetComponent<Weapon>() : null;
                    RewardData reward = AssetDatabase.LoadAssetAtPath<RewardData>(REWARD_DIR + "/" + w + "Reward.asset");
                    if (null == weapon || null == reward)
                    {
                        sb.AppendLine("경고: " + def.id + " 무기/보상 없음 " + w + " weapon=" + (null != weapon) + " reward=" + (null != reward));
                    }
                    else if (false == (reward.GetEffect() is WeaponRewardEffect effect && effect.WeaponPrefab == weapon))
                    {
                        sb.AppendLine("경고: " + def.id + " 보상 " + reward.name + " 이 " + w + " 무기를 주지 않음");
                    }
                    if (0 == i)
                    {
                        start = weapon;
                    }
                    rewards[i] = reward;
                }

                // 4. DimensionData
                string dataPath = DATA_DIR + "/" + def.id + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<DimensionData>(dataPath);
                if (null == data)
                {
                    data = ScriptableObject.CreateInstance<DimensionData>();
                    AssetDatabase.CreateAsset(data, dataPath);
                }

                var loaderGo = AssetDatabase.LoadAssetAtPath<GameObject>(def.loader);
                var previewGo = AssetDatabase.LoadAssetAtPath<GameObject>(def.preview);
                SpriteRenderer previewRenderer = null != previewGo ? previewGo.GetComponentInChildren<SpriteRenderer>(true) : null;
                WaveData[] waves = Wave30Builder.LoadWaves(def.id);

                var so = new SerializedObject(data);
                so.FindProperty("displayName").stringValue = def.displayName;
                so.FindProperty("themeColor").colorValue = def.theme;
                so.FindProperty("previewSprite").objectReferenceValue = null != previewRenderer ? previewRenderer.sprite : null;
                so.FindProperty("startWeapon").objectReferenceValue = start;
                SetArray(so.FindProperty("weaponRewards"), rewards);
                SetArray(so.FindProperty("waves"), waves);
                so.FindProperty("chunkLoaderPrefab").objectReferenceValue = null != loaderGo ? loaderGo.GetComponent<WFCChunkLoader>() : null;
                so.FindProperty("playerAnimator").objectReferenceValue = aoc;
                so.FindProperty("playerSprite").objectReferenceValue = playerSprite;
                // monsterSkins 는 아트 단계에서 채운다 (기존 값 유지)
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);

                RewardData startReward = data.FindStartWeaponReward();
                sb.AppendLine(def.id + "(" + def.displayName + "): start=" + (null != start ? start.name : "null")
                    + " startReward=" + (null != startReward ? startReward.name : "없음")
                    + " rewards=[" + string.Join(",", rewards.Select(r => null != r ? r.name : "null")) + "]"
                    + " waves=" + waves.Length
                    + " loader=" + (null != data.ChunkLoaderPrefab ? data.ChunkLoaderPrefab.name : "null")
                    + " anim=" + aoc.name
                    + " preview=" + (null != data.PreviewSprite ? data.PreviewSprite.name : "null"));
            }

            AssetDatabase.SaveAssets();
            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        private static void SetArray(SerializedProperty _prop, Object[] _values)
        {
            _prop.arraySize = _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                _prop.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
            }
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
