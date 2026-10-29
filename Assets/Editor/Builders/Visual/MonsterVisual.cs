using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders.Visual
{
    /// <summary>
    /// 새 몬스터 6종 / HazardZone / 자폭 폭발 연출 적용 (멱등)
    /// MonsterKeywordBuilder 를 다시 돌려 프리팹이 재생성되면 이 빌더도 다시 실행해야 한다
    /// </summary>
    public static class MonsterVisual
    {
        private const string DIR = "Assets/prefab/Monster";
        private const string EXPLOSION_SRC = "Assets/prefab/weapon/Explosive/ExplosiveExplosionEffect.prefab";
        private const string EXPLOSION_DST = DIR + "/Bomber/BomberExplosion.prefab";
        private const string POISON_SRC = "Assets/prefab/weapon/PoisonZone/PoisonZoneProjectile.prefab";
        private const string ZONE_PATH = DIR + "/HazardZone/HazardZone.prefab";
        private const string SELFDESTRUCT = "Assets/Resources/Data/Monster/Keywords/SelfDestruct.asset";

        private static readonly string[] MONSTERS = { "Bomber", "Slime", "MiniSlime", "Wraith", "Shaman", "Golem" };

        public static string Build()
        {
            var sb = new StringBuilder();
            foreach (string m in MONSTERS)
            {
                sb.AppendLine(m + ": " + ApplyMonster(m));
            }
            sb.AppendLine("explosion: " + BuildExplosion());
            sb.AppendLine("zone: " + BuildZone());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }

        private static string ApplyMonster(string _name)
        {
            string path = DIR + "/" + _name + "/" + _name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var fx = root.GetComponent<MonsterVisualFX>();
                if (null == fx)
                {
                    fx = root.AddComponent<MonsterVisualFX>();
                }
                var so = new SerializedObject(fx);
                so.FindProperty("target").objectReferenceValue = root.transform.Find("visual").GetComponent<SpriteRenderer>();
                so.FindProperty("hpBar").objectReferenceValue = root.transform.Find("UI");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return "ok";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string BuildExplosion()
        {
            if (null == AssetDatabase.LoadAssetAtPath<GameObject>(EXPLOSION_DST))
            {
                AssetDatabase.CopyAsset(EXPLOSION_SRC, EXPLOSION_DST);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(EXPLOSION_DST);
            try
            {
                // 이미 코어를 만들었으면 지우고 다시 만든다 (멱등)
                Transform old = root.transform.Find("core");
                if (null != old)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                root.GetComponent<Lifetime>().lifetime = 0.45f;

                Transform visual = root.transform.Find("visual");
                var sr = visual.GetComponent<SpriteRenderer>();
                sr.color = new Color(1f, 0.35f, 0.1f, 0.8f);
                sr.sortingOrder = 5;

                GameObject core = Object.Instantiate(visual.gameObject, root.transform);
                core.name = "core";
                core.transform.localScale = visual.localScale * 0.55f;
                var csr = core.GetComponent<SpriteRenderer>();
                csr.color = new Color(1f, 0.9f, 0.4f, 0.95f);
                csr.sortingOrder = 6;

                // 복제된 Scale/Fade 가 core 자신의 렌더러를 쓰는지 확인
                var fade = core.GetComponent<FadeOverLifetime>();
                if (null != fade)
                {
                    fade.target = csr;
                }

                PrefabUtility.SaveAsPrefabAsset(root, EXPLOSION_DST);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(EXPLOSION_DST).GetComponent<PooledEffect>();
            var kw = AssetDatabase.LoadAssetAtPath<SelfDestructKeyword>(SELFDESTRUCT);
            if (null == kw)
            {
                return "SelfDestruct.asset not found: " + SELFDESTRUCT;
            }
            kw.explosionEffect = effect;
            EditorUtility.SetDirty(kw);
            return "linked";
        }

        private static string BuildZone()
        {
            GameObject poison = AssetDatabase.LoadAssetAtPath<GameObject>(POISON_SRC);
            Sprite sprite = null;
            if (null != poison)
            {
                var psr = poison.GetComponentInChildren<SpriteRenderer>(true);
                sprite = null != psr ? psr.sprite : null;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ZONE_PATH);
            try
            {
                var outer = root.GetComponent<SpriteRenderer>();
                if (null != sprite)
                {
                    outer.sprite = sprite;
                }
                outer.color = new Color(0.4f, 0.85f, 0.25f, 0.5f);
                outer.sortingOrder = -2;

                Transform coreT = root.transform.Find("core");
                if (null == coreT)
                {
                    var go = new GameObject("core");
                    go.transform.SetParent(root.transform, false);
                    coreT = go.transform;
                    go.AddComponent<SpriteRenderer>();
                }
                coreT.localScale = Vector3.one * 0.6f;
                var core = coreT.GetComponent<SpriteRenderer>();
                core.sprite = outer.sprite;
                core.color = new Color(0.6f, 0.2f, 0.85f, 0.45f);
                core.sortingOrder = -1;

                var vis = root.GetComponent<HazardZoneVisual>();
                if (null == vis)
                {
                    vis = root.AddComponent<HazardZoneVisual>();
                }
                var so = new SerializedObject(vis);
                so.FindProperty("outer").objectReferenceValue = outer;
                so.FindProperty("core").objectReferenceValue = core;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ZONE_PATH);
                return "ok sprite=" + (null != sprite ? sprite.name : "kept");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
