using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// 최종 보스 연출: 운석·그림자·보스 외형 프리팹, 레이저/화살표 색, HP 바(이름·프레임·눈금), 인트로 배너, 처치 연출, BGM
    /// FinalBossBuilder 재실행 후 다시 실행할 것
    /// </summary>
    public static class FinalBossVisual
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string DIR = "Assets/prefab/Monster/Boss/FinalBoss/";
        private const string DATA_PATH = "Assets/Resources/Data/Monster/Boss/FinalBossData.asset";
        private const string FONT_PATH = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        private const string OUTLINE_MAT = "Assets/Resources/Textuer/Hud/HudTimerOutline.mat";
        private const string URP_UNLIT_MAT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string SORTING = "attack";
        private const int BOSS_LAYER = 18;
        private const string NAME_KEY = "boss.FinalBoss.name";

        [MenuItem("Tools/Builders/Visual/FinalBoss")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder("[FinalBossVisual]\n");
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(URP_UNLIT_MAT);

            TuneData(sb);
            BuildMeteor(circle, mat, sb);
            TuneBossPrefab(circle, mat, sb);
            AddLocalization(sb);
            BuildScene(circle, mat, sb);
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        // ---------- 데이터 ----------
        private static void TuneData(StringBuilder _sb)
        {
            var d = AssetDatabase.LoadAssetAtPath<FinalBossData>(DATA_PATH);
            if (null == d) { _sb.AppendLine("FinalBossData 없음"); return; }
            d.warnColor = new Color(1f, 0.35f, 0.2f, 1f);
            d.fillColor = new Color(1f, 0.25f, 0.1f, 0.5f);
            d.arrowColor = new Color(1f, 0.85f, 0.15f, 0.95f);
            d.dashArrowWidth = 0.9f;
            d.dashArrowLength = 6f;
            d.laserWarnWidth = 0.07f;
            d.laserWidth = 1.0f;
            d.laserColor = new Color(1f, 0.92f, 0.95f, 1f);
            d.bossBgm = SoundId.BGM_RUSH;
            const string src = "Assets/Resources/Data/Monster/Boss/BossTelegraphStyle.asset";
            const string dst = "Assets/Resources/Data/Monster/Boss/FinalBossTelegraphStyle.asset";
            if (null == AssetDatabase.LoadAssetAtPath<BossTelegraphStyle>(dst)) { AssetDatabase.CopyAsset(src, dst); }
            var st = AssetDatabase.LoadAssetAtPath<BossTelegraphStyle>(dst);
            if (null != st) { st.sortingOrder = 12; EditorUtility.SetDirty(st); d.telegraphStyle = st; }
            EditorUtility.SetDirty(d);
            _sb.AppendLine("데이터: 색/굵기 조정, bossBgm=BGM_RUSH");
        }

        // ---------- 운석 / 그림자 ----------
        private static GameObject Child(GameObject _parent, string _name, Sprite _s, Material _m, Color _c, float _diameter, Vector2 _scaleXY, int _order)
        {
            Transform t = _parent.transform.Find(_name);
            GameObject go = null != t ? t.gameObject : new GameObject(_name);
            go.layer = BOSS_LAYER;
            go.transform.SetParent(_parent.transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            if (null == sr) { sr = go.AddComponent<SpriteRenderer>(); }
            sr.sprite = _s;
            sr.sharedMaterial = _m;
            sr.color = _c;
            sr.sortingLayerName = SORTING;
            sr.sortingOrder = _order;
            float s = _diameter / Mathf.Max(0.01f, _s.bounds.size.x);
            go.transform.localScale = new Vector3(s * _scaleXY.x, s * _scaleXY.y, 1f);
            return go;
        }

        private static void ClearChildren(GameObject _go)
        {
            for (int i = _go.transform.childCount - 1; i >= 0; --i)
            {
                Object.DestroyImmediate(_go.transform.GetChild(i).gameObject);
            }
        }

        private static void BuildMeteor(Sprite _c, Material _m, StringBuilder _sb)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DIR + "FinalBossMeteor.prefab");
            try
            {
                var rsr = root.GetComponent<SpriteRenderer>();
                if (null != rsr) { Object.DestroyImmediate(rsr); }
                root.transform.localScale = Vector3.one;
                ClearChildren(root);
                root.layer = BOSS_LAYER;
                Child(root, "Glow", _c, _m, new Color(1f, 0.35f, 0.1f, 0.3f), 3.4f, Vector2.one, 18);
                Child(root, "Mid", _c, _m, new Color(1f, 0.5f, 0.12f, 0.85f), 2.1f, Vector2.one, 19);
                Child(root, "Core", _c, _m, new Color(1f, 0.97f, 0.75f, 1f), 1.1f, Vector2.one, 20);
                var p = root.GetComponent<FinalBossPulse>();
                if (null == p) { p = root.AddComponent<FinalBossPulse>(); }
                PrefabUtility.SaveAsPrefabAsset(root, DIR + "FinalBossMeteor.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            GameObject sh = PrefabUtility.LoadPrefabContents(DIR + "FinalBossMeteorShadow.prefab");
            try
            {
                var sr = sh.GetComponent<SpriteRenderer>();
                sr.color = new Color(0f, 0f, 0f, 0.4f);
                float s = 3.2f / Mathf.Max(0.01f, sr.sprite.bounds.size.x);
                sh.transform.localScale = new Vector3(s, s * 0.55f, 1f);
                PrefabUtility.SaveAsPrefabAsset(sh, DIR + "FinalBossMeteorShadow.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(sh); }
            _sb.AppendLine("운석: 코어+중간+글로우, 맥동, 그림자 타원");
        }

        // ---------- 보스 외형 ----------
        private static void TuneBossPrefab(Sprite _c, Material _m, StringBuilder _sb)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DIR + "FinalBoss.prefab");
            try
            {
                Transform v = root.transform.Find("Visual");
                if (null != v)
                {
                    var vsr = v.GetComponent<SpriteRenderer>();
                    vsr.color = new Color(0.85f, 0.2f, 0.5f, 1f);
                    float size = Mathf.Max(vsr.sprite.bounds.size.x, vsr.sprite.bounds.size.y);
                    float scale = (2.0f * 2f) / Mathf.Max(0.01f, size);
                    v.localScale = new Vector3(scale, scale, 1f);
                }
                Transform a = root.transform.Find("Aura");
                if (null != a)
                {
                    var asr = a.GetComponent<SpriteRenderer>();
                    asr.color = new Color(0.8f, 0.1f, 0.4f, 0.22f);
                    float s = (2.6f * 2f) / Mathf.Max(0.01f, asr.sprite.bounds.size.x);
                    a.localScale = new Vector3(s, s, 1f);
                    asr.sortingOrder = 7;
                    if (null == a.GetComponent<FinalBossPulse>()) { var p = a.gameObject.AddComponent<FinalBossPulse>(); }
                }
                GameObject inner = FindOrMake(root, "AuraInner");
                inner.layer = BOSS_LAYER;
                var isr = inner.GetComponent<SpriteRenderer>();
                if (null == isr) { isr = inner.AddComponent<SpriteRenderer>(); }
                isr.sprite = _c;
                isr.sharedMaterial = _m;
                isr.color = new Color(1f, 0.15f, 0.3f, 0.12f);
                isr.sortingLayerName = SORTING;
                isr.sortingOrder = 8;
                float si = (2.3f * 2f) / Mathf.Max(0.01f, _c.bounds.size.x);
                inner.transform.localScale = new Vector3(si, si, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, DIR + "FinalBoss.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            _sb.AppendLine("보스: 몸체 시각 반경 2.0, 옅은 오라(2.6/2.3)");
        }

        private static GameObject FindOrMake(GameObject _parent, string _name)
        {
            Transform t = _parent.transform.Find(_name);
            if (null != t) { return t.gameObject; }
            var go = new GameObject(_name);
            go.transform.SetParent(_parent.transform, false);
            return go;
        }

        // ---------- 문자열 ----------
        private static void AddLocalization(StringBuilder _sb)
        {
            string path = "Assets/Resources/Localization/StringTable.csv";
            string text = System.IO.File.ReadAllText(path);
            if (text.Contains(NAME_KEY + ",")) { return; }
            if (false == text.EndsWith("\n")) { text += "\n"; }
            text += NAME_KEY + ",차원의 군주,Lord of Dimensions\n";
            System.IO.File.WriteAllText(path, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            _sb.AppendLine("StringTable: " + NAME_KEY);
        }

        // ---------- 씬 ----------
        private static GameObject UiChild(Transform _p, string _n)
        {
            Transform t = _p.Find(_n);
            if (null != t) { return t.gameObject; }
            var go = new GameObject(_n, typeof(RectTransform));
            go.transform.SetParent(_p, false);
            go.layer = _p.gameObject.layer;
            return go;
        }

        private static Image Img(GameObject _go, Color _c, Sprite _s = null)
        {
            var i = _go.GetComponent<Image>();
            if (null == i) { i = _go.AddComponent<Image>(); }
            i.sprite = _s;
            i.color = _c;
            i.raycastTarget = false;
            if (null != _s) { i.type = Image.Type.Sliced; }
            return i;
        }

        private static void Rect(GameObject _go, Vector2 _amin, Vector2 _amax, Vector2 _pivot, Vector2 _pos, Vector2 _size)
        {
            var rt = _go.GetComponent<RectTransform>();
            rt.anchorMin = _amin;
            rt.anchorMax = _amax;
            rt.pivot = _pivot;
            rt.anchoredPosition = _pos;
            rt.sizeDelta = _size;
        }

        private static TMP_Text Text(GameObject _go, string _s, float _size, Color _col, TMP_FontAsset _f, Material _o)
        {
            var t = _go.GetComponent<TextMeshProUGUI>();
            if (null == t) { t = _go.AddComponent<TextMeshProUGUI>(); }
            if (null != _f) { t.font = _f; }
            if (null != _o) { t.fontSharedMaterial = _o; }
            t.text = _s;
            t.fontSize = _size;
            t.color = _col;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static void SetLocKey(GameObject _go, string _key)
        {
            var lt = _go.GetComponent<LocalizedText>();
            if (null == lt) { lt = _go.AddComponent<LocalizedText>(); }
            var so = new SerializedObject(lt);
            so.FindProperty("key").stringValue = _key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildScene(Sprite _circle, Material _mat, StringBuilder _sb)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH) { scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single); }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var outline = AssetDatabase.LoadAssetAtPath<Material>(OUTLINE_MAT);
            Sprite ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            GameObject canvas = GameObject.Find("HUDCanvas");
            if (null == canvas) { _sb.AppendLine("HUDCanvas 없음"); return; }
            Transform cv = canvas.transform;

            // HP 바: 프레임 + 안쪽 배경 + 채움 + 눈금 + 이름
            Transform bar = cv.Find("FinalBossHPBar");
            if (null != bar)
            {
                Img(bar.gameObject, new Color(0.95f, 0.75f, 0.3f, 1f), ui);
                var rt = bar.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(-160f, 30f);
                GameObject inner = UiChild(bar, "Inner");
                Rect(inner, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var irt = inner.GetComponent<RectTransform>();
                irt.offsetMin = new Vector2(3f, 3f);
                irt.offsetMax = new Vector2(-3f, -3f);
                Img(inner, new Color(0.1f, 0.02f, 0.12f, 0.95f), ui);
                inner.transform.SetSiblingIndex(0);
                Transform fill = bar.Find("Fill");
                if (null != fill)
                {
                    var frt = fill.GetComponent<RectTransform>();
                    frt.offsetMin = new Vector2(5f, 5f);
                    frt.offsetMax = new Vector2(-5f, -5f);
                    fill.GetComponent<Image>().color = new Color(0.85f, 0.2f, 0.55f, 1f);
                }
                float[] marks = { 0.33f, 0.66f };
                for (int i = 0; i < marks.Length; ++i)
                {
                    GameObject tick = UiChild(bar, "Tick" + i);
                    Rect(tick, new Vector2(marks[i], 0f), new Vector2(marks[i], 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
                    Img(tick, new Color(1f, 1f, 1f, 0.85f));
                    tick.transform.SetAsLastSibling();
                }
                GameObject nm = UiChild(bar, "Name");
                Rect(nm, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(700f, 40f));
                Text(nm, "차원의 군주", 30f, new Color(1f, 0.85f, 0.4f, 1f), font, outline);
                SetLocKey(nm, NAME_KEY);
                _sb.AppendLine("HP 바: 프레임/눈금 33,66/이름");
            }

            // 인트로
            GameObject intro = UiChild(cv, "FinalBossIntro");
            Rect(intro, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var group = intro.GetComponent<CanvasGroup>();
            if (null == group) { group = intro.AddComponent<CanvasGroup>(); }
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            GameObject dim = UiChild(intro.transform, "Dim");
            Rect(dim, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(dim, new Color(0.08f, 0f, 0.02f, 0.4f));
            GameObject glow = UiChild(intro.transform, "CrackGlow");
            Rect(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 0f));
            Img(glow, new Color(1f, 0.15f, 0.2f, 0.35f));
            GameObject crack = UiChild(intro.transform, "Crack");
            Rect(crack, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 0f));
            Image crackImg = Img(crack, Color.white);
            GameObject b0 = UiChild(intro.transform, "BandTop");
            Rect(b0, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 115f), new Vector2(0f, 8f));
            Img(b0, new Color(1f, 0.2f, 0.2f, 0.9f));
            GameObject b1 = UiChild(intro.transform, "BandBottom");
            Rect(b1, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), new Vector2(0f, 8f));
            Img(b1, new Color(1f, 0.2f, 0.2f, 0.9f));
            GameObject strip = UiChild(intro.transform, "Strip");
            Rect(strip, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 222f));
            Img(strip, new Color(0.05f, 0f, 0.05f, 0.7f));
            strip.transform.SetSiblingIndex(2);
            GameObject warn = UiChild(intro.transform, "Warning");
            Rect(warn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 55f), new Vector2(1000f, 100f));
            TMP_Text wt = Text(warn, "WARNING", 96f, new Color(1f, 0.25f, 0.2f, 1f), font, outline);
            GameObject nameGo = UiChild(intro.transform, "BossName");
            Rect(nameGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(1000f, 70f));
            TMP_Text nt = Text(nameGo, "차원의 군주", 56f, new Color(1f, 0.88f, 0.5f, 0f), font, outline);
            SetLocKey(nameGo, NAME_KEY);
            intro.transform.SetAsLastSibling();

            var introUi = intro.GetComponent<FinalBossIntroUI>();
            if (null == introUi) { introUi = intro.AddComponent<FinalBossIntroUI>(); }
            var iso = new SerializedObject(introUi);
            iso.FindProperty("group").objectReferenceValue = group;
            iso.FindProperty("crack").objectReferenceValue = crack.GetComponent<RectTransform>();
            iso.FindProperty("crackGlow").objectReferenceValue = glow.GetComponent<RectTransform>();
            iso.FindProperty("crackImage").objectReferenceValue = crackImg;
            iso.FindProperty("warningText").objectReferenceValue = wt;
            iso.FindProperty("nameText").objectReferenceValue = nt;
            SerializedProperty bands = iso.FindProperty("bands");
            bands.arraySize = 2;
            bands.GetArrayElementAtIndex(0).objectReferenceValue = b0.GetComponent<RectTransform>();
            bands.GetArrayElementAtIndex(1).objectReferenceValue = b1.GetComponent<RectTransform>();
            iso.ApplyModifiedPropertiesWithoutUndo();

            // 처치 플래시
            GameObject fx = UiChild(cv, "FinalBossDefeatFX");
            Rect(fx, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image flash = Img(fx, new Color(1f, 1f, 1f, 0f));
            var dfx = fx.GetComponent<FinalBossDefeatFX>();
            if (null == dfx) { dfx = fx.AddComponent<FinalBossDefeatFX>(); }
            var dso = new SerializedObject(dfx);
            dso.FindProperty("flash").objectReferenceValue = flash;
            dso.FindProperty("circleSprite").objectReferenceValue = _circle;
            dso.FindProperty("circleMaterial").objectReferenceValue = _mat;
            dso.ApplyModifiedPropertiesWithoutUndo();
            fx.transform.SetAsLastSibling();

            // 라인 글로우
            GameObject director = GameObject.Find("FinalBossDirector");
            if (null != director && null == director.GetComponent<FinalBossLineGlow>())
            {
                director.AddComponent<FinalBossLineGlow>();
            }
            _sb.AppendLine("씬: 인트로/처치FX/라인글로우 배치");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
