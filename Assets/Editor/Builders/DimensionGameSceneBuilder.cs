using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Builders
{
    /// <summary>
    /// GameScene 에 진영별 DimensionApplier 를 배치한다 (멱등)
    /// - 씬에 직접 놓인 WFCChunkLoader 는 맵 중복을 막기 위해 제거한다. 제거 전에 씬 인스턴스의 설정(타일 목록 등)을
    ///   원본 프리팹에 옮긴다 (프리팹 원본에는 타일이 비어 있거나 덜 들어 있었음). targetCamera 는 옮기지 않는다
    /// - 적용기: side, defaultDimension(좌 Forest / 우 Factory), player, sideCamera(화면 왼쪽/오른쪽 카메라),
    ///   mapLayerName(LBG / RBG), opponentSpawner(반대 진영 플레이어를 target 으로 하는 Spawner), rewardManager
    /// - 플레이어(씬/프리팹)에 미리 붙은 Weapon 이 있으면 제거한다 (시작 무기는 차원이 준다)
    /// </summary>
    public static class DimensionGameSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string PLAYER_PREFAB = "Assets/prefab/Player/Player.prefab";

        [MenuItem("Tools/Builders/DimensionGameScene")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[DimensionGameSceneBuilder]");

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            // 1. 카메라 / 플레이어 / 스포너
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            Camera leftCam = cams.Where(c => c.rect.width < 1f).OrderBy(c => c.rect.x).FirstOrDefault();
            Camera rightCam = cams.Where(c => c.rect.width < 1f).OrderByDescending(c => c.rect.x).FirstOrDefault();
            PlayerStats leftPlayer = FindPlayer(PlayerSide.Left);
            PlayerStats rightPlayer = FindPlayer(PlayerSide.Right);
            Spawner[] spawners = Object.FindObjectsByType<Spawner>(FindObjectsInactive.Include);
            // 반대 진영 스포너 = 상대 플레이어를 노리는 스포너
            Spawner spawnerToRight = FindSpawnerTargeting(spawners, rightPlayer);
            Spawner spawnerToLeft = FindSpawnerTargeting(spawners, leftPlayer);
            RewardManager reward = Object.FindFirstObjectByType<RewardManager>(FindObjectsInactive.Include);
            sb.AppendLine("cam L=" + Path(leftCam) + " R=" + Path(rightCam)
                + " / player L=" + Path(leftPlayer) + " R=" + Path(rightPlayer)
                + " / spawner->R=" + Path(spawnerToRight) + " ->L=" + Path(spawnerToLeft)
                + " / reward=" + Path(reward));

            // 2. 씬의 맵 로더 정리 (설정은 프리팹 원본으로)
            string leftLayer = "LBG";
            string rightLayer = "RBG";
            foreach (WFCChunkLoader loader in Object.FindObjectsByType<WFCChunkLoader>(FindObjectsInactive.Include))
            {
                if (loader.targetCamera == leftCam)
                {
                    leftLayer = LayerMask.LayerToName(loader.gameObject.layer);
                }
                else if (loader.targetCamera == rightCam)
                {
                    rightLayer = LayerMask.LayerToName(loader.gameObject.layer);
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(loader);
                if (null != source)
                {
                    int count = CopySettings(loader, source);
                    sb.AppendLine("로더 설정 → 프리팹 " + AssetDatabase.GetAssetPath(source) + " tileDatas=" + count);
                }
                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(loader.gameObject);
                GameObject target = null != root ? root : loader.gameObject;
                sb.AppendLine("씬 로더 제거: " + target.name + " (" + LayerMask.LayerToName(target.layer) + ")");
                Object.DestroyImmediate(target);
            }

            // 3. 적용기
            var forest = AssetDatabase.LoadAssetAtPath<DimensionData>(DimensionDataBuilder.DATA_DIR + "/Forest.asset");
            var factory = AssetDatabase.LoadAssetAtPath<DimensionData>(DimensionDataBuilder.DATA_DIR + "/Factory.asset");
            sb.AppendLine(SetupApplier(PlayerSide.Left, forest, leftPlayer, leftCam, leftLayer, spawnerToRight, reward));
            sb.AppendLine(SetupApplier(PlayerSide.Right, factory, rightPlayer, rightCam, rightLayer, spawnerToLeft, reward));

            // 4. 미리 붙은 무기 정리
            foreach (PlayerStats p in new[] { leftPlayer, rightPlayer })
            {
                if (null == p)
                {
                    continue;
                }
                foreach (Weapon w in p.GetComponentsInChildren<Weapon>(true))
                {
                    sb.AppendLine("씬 무기 제거: " + p.name + "/" + w.name);
                    Object.DestroyImmediate(w.gameObject);
                }
            }
            sb.AppendLine(CleanPlayerPrefab());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            sb.AppendLine("저장: " + SCENE_PATH);

            string result = sb.ToString();
            Debug.Log(result);
            return result;
        }

        private static string SetupApplier(PlayerSide _side, DimensionData _default, PlayerStats _player, Camera _cam, string _layer, Spawner _opponent, RewardManager _reward)
        {
            DimensionApplier applier = Object.FindObjectsByType<DimensionApplier>(FindObjectsInactive.Include).FirstOrDefault(a => a.Side == _side);
            if (null == applier)
            {
                applier = new GameObject("DimensionApplier_" + _side).AddComponent<DimensionApplier>();
            }
            applier.gameObject.name = "DimensionApplier_" + _side;

            var so = new SerializedObject(applier);
            so.FindProperty("side").intValue = (int)_side;
            so.FindProperty("defaultDimension").objectReferenceValue = _default;
            so.FindProperty("player").objectReferenceValue = _player;
            so.FindProperty("sideCamera").objectReferenceValue = _cam;
            so.FindProperty("mapLayerName").stringValue = _layer;
            so.FindProperty("opponentSpawner").objectReferenceValue = _opponent;
            so.FindProperty("rewardManager").objectReferenceValue = _reward;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(applier);

            return applier.name + ": default=" + (null != _default ? _default.name : "null") + " player=" + Path(_player)
                + " cam=" + Path(_cam) + "(x=" + (null != _cam ? _cam.rect.x : -1f) + ") layer=" + _layer
                + " opponentSpawner=" + Path(_opponent) + "(target=" + (null != _opponent && null != _opponent.target ? _opponent.target.name : "null") + ")";
        }

        private static PlayerStats FindPlayer(PlayerSide _side)
        {
            return Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include)
                .FirstOrDefault(p => new SerializedObject(p).FindProperty("side").intValue == (int)_side);
        }

        private static Spawner FindSpawnerTargeting(Spawner[] _spawners, PlayerStats _player)
        {
            if (null == _player)
            {
                return null;
            }
            return _spawners.FirstOrDefault(s => null != s.target
                && (s.target == _player.transform || s.target.IsChildOf(_player.transform) || _player.transform.IsChildOf(s.target)));
        }

        /// <summary>
        /// 씬 로더의 설정을 프리팹 원본에 복사 (씬 오브젝트 참조인 targetCamera 는 제외)
        /// </summary>
        private static int CopySettings(WFCChunkLoader _from, WFCChunkLoader _to)
        {
            var src = new SerializedObject(_from);
            var dst = new SerializedObject(_to);
            SerializedProperty it = src.GetIterator();
            it.NextVisible(true);
            while (it.NextVisible(false))
            {
                if (it.name == "m_Script" || it.name == "targetCamera")
                {
                    continue;
                }
                dst.CopyFromSerializedProperty(it);
            }
            dst.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_to);
            PrefabUtility.SavePrefabAsset(_to.transform.root.gameObject);
            return dst.FindProperty("tileDatas").arraySize;
        }

        private static string CleanPlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB);
            try
            {
                Weapon[] weapons = root.GetComponentsInChildren<Weapon>(true);
                if (0 == weapons.Length)
                {
                    return "플레이어 프리팹 무기: 없음";
                }
                foreach (Weapon w in weapons)
                {
                    Object.DestroyImmediate(w.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB);
                return "플레이어 프리팹 무기 제거: " + weapons.Length;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string Path(Component _c)
        {
            if (null == _c)
            {
                return "null";
            }
            Transform t = _c.transform;
            return null != t.parent ? t.parent.name + "/" + t.name : t.name;
        }
    }
}
