using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// 중간 보스 시각·연출 (멱등). 보스 배너·화살표(HUDCanvas/BossHud), 보스 보상 제목(각 SelectPanelGroup),
    /// 보스 프리팹 6종에 BossAppearFX, BossTelegraphStyle 두께.
    /// InGameHudBuilder 를 다시 돌렸다면 이 빌더도 다시 실행 (HUDCanvas 가 새로 만들어지므로 BossHud 가 사라진다).
    /// </summary>
    public static class BossVisual
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string FONT_PATH = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        private const string TIMER_MAT = "Assets/Resources/Textuer/Hud/HudTimerOutline.mat";
        private const string ARROW_PATH = "Assets/Resources/Textuer/Hud/BossArrow.png";
        private const string STYLE_PATH = "Assets/Resources/Data/Monster/Boss/BossTelegraphStyle.asset";
        private const int UI_LAYER = 5;
        private static readonly string[] BOSSES = { "ForestBoss10", "FactoryBoss10", "StormBoss10", "ForestBoss20", "FactoryBoss20", "StormBoss20" };

        [MenuItem("Tools/Builders/Visual/Boss")]
        private static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder("[BossVisual]\n");

            var style = AssetDatabase.LoadAssetAtPath<BossTelegraphStyle>(STYLE_PATH);
            if (null != style)
            {
                style.ringWidth = 0.16f;
                EditorUtility.SetDirty(style);
            }

            foreach (string n in BOSSES)
            {
                string path = "Assets/prefab/Monster/Boss/" + n + "/" + n + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var fx = root.GetComponent<BossAppearFX>();
                    if (null == fx) { fx = root.AddComponent<BossAppearFX>(); }
                    var so = new SerializedObject(fx);
                    so.FindProperty("body").objectReferenceValue = root.transform.Find("visual");
                    so.FindProperty("style").objectReferenceValue = style;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
                    {
                        if ("HP" == rt.name && "UI" == rt.parent.name)
                        {
                            rt.anchoredPosition = new Vector2(0f, -2.1f);
                            rt.sizeDelta = new Vector2(2.4f, 0.3f);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            sb.AppendLine("BossAppearFX x" + BOSSES.Length);

            Sprite arrow = EnsureArrowSprite();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var outline = AssetDatabase.LoadAssetAtPath<Material>(TIMER_MAT);

            GameObject canvas = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if ("HUDCanvas" == r.name) { canvas = r; }
            }
            if (null == canvas) { return sb + "HUDCanvas 없음"; }

            Transform old = canvas.transform.Find("BossHud");
            if (null != old) { Object.DestroyImmediate(old.gameObject); }

            RectTransform hud = Rect("BossHud", canvas.transform);
            hud.anchorMin = Vector2.zero; hud.anchorMax = Vector2.one; hud.offsetMin = hud.offsetMax = Vector2.zero;
            var ui = hud.gameObject.AddComponent<BossHudUI>();

            RectTransform arrowRt = Rect("ArrowTemplate", hud);
            arrowRt.sizeDelta = new Vector2(64f, 64f);
            var arrowImg = arrowRt.gameObject.AddComponent<Image>();
            arrowImg.sprite = arrow;
            arrowImg.color = new Color(1f, 0.25f, 0.2f, 0.95f);
            arrowImg.raycastTarget = false;
            arrowRt.gameObject.SetActive(false);

            var viewsSo = new SerializedObject(ui);
            SerializedProperty views = viewsSo.FindProperty("views");
            views.arraySize = 2;
            viewsSo.FindProperty("arrowTemplate").objectReferenceValue = arrowRt;

            PlayerSide[] sides = { PlayerSide.Left, PlayerSide.Right };
            foreach (PlayerSide side in sides)
            {
                bool left = PlayerSide.Left == side;
                SerializedProperty v = views.GetArrayElementAtIndex(left ? 0 : 1);
                v.FindPropertyRelative("side").enumValueIndex = (int)side;

                RectTransform banner = Rect("Banner_" + side, hud);
                banner.anchorMin = banner.anchorMax = new Vector2(left ? 0.25f : 0.75f, 1f);
                banner.pivot = new Vector2(0.5f, 1f);
                banner.anchoredPosition = new Vector2(0f, -150f);
                banner.sizeDelta = new Vector2(640f, 76f);
                var bg = banner.gameObject.AddComponent<Image>();
                bg.color = new Color(0.45f, 0.04f, 0.05f, 0.82f);
                bg.raycastTarget = false;
                var cg = banner.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

                RectTransform edge = Rect("Edge", banner);
                edge.anchorMin = new Vector2(0f, 0f); edge.anchorMax = new Vector2(1f, 0f);
                edge.pivot = new Vector2(0.5f, 0f); edge.sizeDelta = new Vector2(0f, 4f);
                var edgeImg = edge.gameObject.AddComponent<Image>();
                edgeImg.color = new Color(1f, 0.35f, 0.25f, 1f); edgeImg.raycastTarget = false;

                RectTransform text = Rect("Text", banner);
                text.anchorMin = Vector2.zero; text.anchorMax = Vector2.one; text.offsetMin = text.offsetMax = Vector2.zero;
                var tmp = text.gameObject.AddComponent<TextMeshProUGUI>();
                Style(tmp, font, outline, "경고! 보스 출현", 40f);
                tmp.color = new Color(1f, 0.9f, 0.85f, 1f);

                v.FindPropertyRelative("banner").objectReferenceValue = cg;
                v.FindPropertyRelative("bannerText").objectReferenceValue = tmp;
            }

            foreach (SelectPanelGroup g in Object.FindObjectsByType<SelectPanelGroup>(FindObjectsInactive.Include))
            {
                Transform t = g.transform.Find("BossRewardTitle");
                if (null != t) { Object.DestroyImmediate(t.gameObject); }
                RectTransform title = Rect("BossRewardTitle", g.transform);
                title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.855f);
                title.sizeDelta = new Vector2(560f, 64f);
                var tmp = title.gameObject.AddComponent<TextMeshProUGUI>();
                Style(tmp, font, outline, "보스 보상", 48f);
                tmp.color = new Color(1f, 0.82f, 0.25f, 1f);
                var lt = title.gameObject.AddComponent<LocalizedText>();
                var lso = new SerializedObject(lt);
                SerializedProperty key = lso.FindProperty("key");
                if (null != key) { key.stringValue = "ui.boss.reward_title"; lso.ApplyModifiedPropertiesWithoutUndo(); }
                title.gameObject.SetActive(false);

                for (int i = 0; i < views.arraySize; ++i)
                {
                    SerializedProperty vv = views.GetArrayElementAtIndex(i);
                    if (g.Side == (PlayerSide)vv.FindPropertyRelative("side").enumValueIndex)
                    {
                        vv.FindPropertyRelative("rewardTitle").objectReferenceValue = title.gameObject;
                    }
                }
                sb.AppendLine("보상 제목 -> " + g.Side);
            }
            viewsSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            sb.AppendLine("HUDCanvas/BossHud 생성");
            return sb.ToString();
        }

        private static void Style(TMP_Text _t, TMP_FontAsset _font, Material _mat, string _text, float _size)
        {
            if (null != _font) { _t.font = _font; }
            _t.fontSharedMaterial = null != _mat ? _mat : (null != _font ? _font.material : _t.fontSharedMaterial);
            _t.text = _text;
            _t.fontSize = _size;
            _t.alignment = TextAlignmentOptions.Center;
            _t.raycastTarget = false;
            _t.textWrappingMode = TextWrappingModes.NoWrap;
            _t.overflowMode = TextOverflowModes.Overflow;
        }

        private static RectTransform Rect(string _name, Transform _parent)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.layer = UI_LAYER;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            return rt;
        }

        private static Sprite EnsureArrowSprite()
        {
            if (null == AssetDatabase.LoadAssetAtPath<Texture2D>(ARROW_PATH))
            {
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; ++y)
                {
                    float half = (y / (float)(N - 1)) * 0.46f;
                    for (int x = 0; x < N; ++x)
                    {
                        float dx = Mathf.Abs((x + 0.5f) / N - 0.5f);
                        bool inside = y >= 4 && y < N - 4 && dx <= half;
                        tex.SetPixel(x, N - 1 - y, inside ? Color.white : Color.clear);
                    }
                }
                File.WriteAllBytes(ARROW_PATH, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(ARROW_PATH);
                var imp = (TextureImporter)AssetImporter.GetAtPath(ARROW_PATH);
                imp.textureType = TextureImporterType.Sprite;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ARROW_PATH);
        }
    }
}
