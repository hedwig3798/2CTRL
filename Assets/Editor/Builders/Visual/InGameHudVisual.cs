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
    /// HUDCanvas 시각 다듬기 (InGameHudBuilder 다음에 실행, 멱등).
    /// 슬롯 프레임, 레벨 배지 + 외곽선, WaveTimer 뒷판 + 외곽선.
    /// </summary>
    public static class InGameHudVisual
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string FONT_PATH = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        private const string MAT_DIR = "Assets/Resources/Textuer/Hud";
        private const string LEVEL_MAT = MAT_DIR + "/HudLevelOutline.mat";
        private const string TIMER_MAT = MAT_DIR + "/HudTimerOutline.mat";

        private static readonly Color FRAME_COLOR = new Color(0.78f, 0.81f, 0.90f, 0.8f);
        private static readonly Color FRAME_EDGE = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color FILL_COLOR = new Color(0.07f, 0.08f, 0.13f, 0.8f);
        private static readonly Color BADGE_COLOR = new Color(0.05f, 0.05f, 0.08f, 0.85f);

        [MenuItem("Tools/Builders/Visual/InGameHud")]
        private static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder("[InGameHudVisual]\n");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            GameObject canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if ("HUDCanvas" == root.name) { canvas = root; }
            }
            if (null == canvas) { return "HUDCanvas 없음"; }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            Material levelMat = MakeMat(font, LEVEL_MAT, 0.35f, 0f);
            Material timerMat = MakeMat(font, TIMER_MAT, 0.3f, 0.15f);

            int slots = 0;
            foreach (Transform hud in canvas.transform)
            {
                if (!hud.name.StartsWith("WeaponHUD")) { continue; }
                foreach (Transform slot in hud)
                {
                    StyleSlot(slot, levelMat);
                    ++slots;
                }
            }

            Transform timer = canvas.transform.Find("WaveTimer");
            if (null != timer)
            {
                var tmp = timer.GetComponent<TextMeshProUGUI>();
                tmp.fontSharedMaterial = timerMat;
                EnsurePlate(canvas.transform, timer);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            sb.Append("슬롯 ").Append(slots).Append("개, WaveTimer ").Append(null != timer ? "OK" : "없음");
            return sb.ToString();
        }

        private static Material MakeMat(TMP_FontAsset _font, string _path, float _outline, float _dilate)
        {
            if (!AssetDatabase.IsValidFolder(MAT_DIR))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Textuer", "Hud");
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (null == mat)
            {
                mat = new Material(_font.material);
                AssetDatabase.CreateAsset(mat, _path);
            }
            mat.shader = _font.material.shader;
            mat.CopyPropertiesFromMaterial(_font.material);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", _outline);
            mat.SetColor("_OutlineColor", Color.black);
            mat.SetFloat("_FaceDilate", _dilate);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void StyleSlot(Transform _slot, Material _levelMat)
        {
            Transform bg = _slot.Find("Background");
            if (null != bg)
            {
                var img = bg.GetComponent<Image>();
                img.color = FRAME_EDGE;
                var old = bg.GetComponent<Outline>();
                if (null != old) { Object.DestroyImmediate(old); }
                Transform frame = EnsureChild(bg, "Fill", 2f, FRAME_COLOR);
                EnsureChild(frame, "Inner", 3f, FILL_COLOR);
            }

            Transform content = _slot.Find("Content");
            if (null == content) { return; }
            Transform level = content.Find("LevelText");
            if (null == level) { return; }

            Transform badge = content.Find("LevelBadge");
            if (null == badge)
            {
                var go = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
                go.layer = _slot.gameObject.layer;
                badge = go.transform;
                badge.SetParent(content, false);
            }
            Transform iconTr = content.Find("Icon");
            if (null != iconTr) { iconTr.SetAsFirstSibling(); }
            badge.SetSiblingIndex(null != iconTr ? 1 : 0);
            var brt = (RectTransform)badge;
            brt.anchorMin = brt.anchorMax = Vector2.zero;
            brt.pivot = Vector2.zero;
            brt.anchoredPosition = new Vector2(4f, 4f);
            brt.sizeDelta = new Vector2(36f, 30f);
            var bi = badge.GetComponent<Image>();
            bi.color = BADGE_COLOR;
            bi.raycastTarget = false;
            var bo = GetOrAdd<Outline>(badge.gameObject);
            bo.effectColor = new Color(1f, 1f, 1f, 0.6f);
            bo.effectDistance = new Vector2(1f, -1f);

            var lrt = (RectTransform)level;
            lrt.anchorMin = lrt.anchorMax = Vector2.zero;
            lrt.pivot = Vector2.zero;
            lrt.anchoredPosition = new Vector2(4f, 4f);
            lrt.sizeDelta = new Vector2(36f, 30f);
            var lt = level.GetComponent<TextMeshProUGUI>();
            lt.alignment = TextAlignmentOptions.Center;
            lt.fontSharedMaterial = _levelMat;
        }

        private static Transform EnsureChild(Transform _parent, string _name, float _inset, Color _color)
        {
            Transform t = _parent.Find(_name);
            if (null == t)
            {
                var go = new GameObject(_name, typeof(RectTransform), typeof(Image));
                go.layer = _parent.gameObject.layer;
                t = go.transform;
                t.SetParent(_parent, false);
            }
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(_inset, _inset); rt.offsetMax = new Vector2(-_inset, -_inset);
            var im = t.GetComponent<Image>();
            im.color = _color;
            im.raycastTarget = false;
            return t;
        }

        private static void EnsurePlate(Transform _canvas, Transform _timer)
        {
            Transform plate = _canvas.Find("WaveTimerPlate");
            if (null == plate)
            {
                var go = new GameObject("WaveTimerPlate", typeof(RectTransform), typeof(Image));
                go.layer = _timer.gameObject.layer;
                plate = go.transform;
                plate.SetParent(_canvas, false);
            }
            plate.SetAsFirstSibling();
            var src = (RectTransform)_timer;
            var rt = (RectTransform)plate;
            rt.anchorMin = rt.anchorMax = src.anchorMin;
            rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -4f);
            rt.sizeDelta = new Vector2(240f, 72f);
            var img = plate.GetComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.09f, 0.82f);
            img.raycastTarget = false;
            var ol = GetOrAdd<Outline>(plate.gameObject);
            ol.effectColor = new Color(1f, 1f, 1f, 0.55f);
            ol.effectDistance = new Vector2(2f, -2f);
        }

        private static T GetOrAdd<T>(GameObject _go) where T : Component
        {
            var c = _go.GetComponent<T>();
            return null != c ? c : _go.AddComponent<T>();
        }
    }
}
