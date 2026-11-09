using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// GameScene 에 인게임 HUD 를 배치한다 (멱등: 기존 HUDCanvas 를 지우고 다시 만든다)
    /// - 루트 HUDCanvas (Screen Space Overlay, sortingOrder 10 → EXPUI/LevelUpUI(0) 위, PauseCanvas(90) 아래)
    ///   LetterboxCanvas 를 붙여 16:9 영역 안 1920x1080 좌표계로 배치
    /// - WaveTimer: 중앙 위 TMP + WaveTimerUI (spawners = 씬의 스포너 전부 직접 연결)
    /// - WeaponHUD_Left / WeaponHUD_Right: 좌상단 / 우상단, 슬롯 6개 (Slot0 = 첫 획득, 화면 가장자리부터)
    /// - 색 · 스프라이트는 플레이스홀더 (visual-designer 담당)
    /// </summary>
    public static class InGameHudBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string FONT_PATH = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        private const string CANVAS_NAME = "HUDCanvas";
        private const int SORTING_ORDER = 10;
        private const int UI_LAYER = 5;

        private const int SLOT_COUNT = 6;
        private const float SLOT_SIZE = 96f;
        private const float SLOT_SPACING = 12f;
        private const float EDGE_MARGIN = 24f;

        private static readonly Color SLOT_BG_COLOR = new Color(0f, 0f, 0f, 0.45f);

        [MenuItem("Tools/Builders/InGameHud")]
        private static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[InGameHudBuilder]");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            if (null == font)
            {
                sb.AppendLine("경고: 폰트 없음 " + FONT_PATH);
            }

            // 1. 기존 것 제거 후 Canvas
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (CANVAS_NAME == root.name)
                {
                    Object.DestroyImmediate(root);
                }
            }

            var canvasGo = new GameObject(CANVAS_NAME, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvasGo.layer = UI_LAYER;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<LetterboxCanvas>();

            // 2. WaveTimer (중앙 위)
            RectTransform timerRt = MakeRect("WaveTimer", canvasGo.transform);
            SetAnchor(timerRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -EDGE_MARGIN), new Vector2(320f, 84f));
            TextMeshProUGUI timerText = timerRt.gameObject.AddComponent<TextMeshProUGUI>();
            SetupText(timerText, font, "30:00", 60f, TextAlignmentOptions.Center);
            WaveTimerUI timer = timerRt.gameObject.AddComponent<WaveTimerUI>();

            var spawners = new List<Spawner>(Object.FindObjectsByType<Spawner>(FindObjectsInactive.Include));
            spawners.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var timerSo = new SerializedObject(timer);
            timerSo.FindProperty("timerText").objectReferenceValue = timerText;
            SerializedProperty spArr = timerSo.FindProperty("spawners");
            spArr.arraySize = spawners.Count;
            for (int i = 0; i < spawners.Count; ++i)
            {
                spArr.GetArrayElementAtIndex(i).objectReferenceValue = spawners[i];
            }
            timerSo.ApplyModifiedPropertiesWithoutUndo();
            sb.Append("WaveTimer 스포너 ").Append(spawners.Count).Append("개:");
            foreach (Spawner s in spawners)
            {
                sb.Append(' ').Append(s.name);
            }
            sb.AppendLine();
            if (2 != spawners.Count)
            {
                sb.AppendLine("경고: 스포너가 2개가 아님");
            }

            // 3. 무기 HUD
            BuildWeaponHud(sb, canvasGo.transform, font, PlayerSide.Left);
            BuildWeaponHud(sb, canvasGo.transform, font, PlayerSide.Right);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine("씬: " + SCENE_PATH);
            sb.AppendLine(CANVAS_NAME + "(order " + SORTING_ORDER + ")/{WaveTimer, WeaponHUD_Left/Slot0.." + (SLOT_COUNT - 1) + ", WeaponHUD_Right/Slot0.." + (SLOT_COUNT - 1) + "}");
            sb.AppendLine("Slot/{Background, Content/{Icon, NameText, LevelText}}");
            return sb.ToString();
        }

        private static void BuildWeaponHud(StringBuilder _sb, Transform _parent, TMP_FontAsset _font, PlayerSide _side)
        {
            bool left = PlayerSide.Left == _side;
            float width = SLOT_COUNT * SLOT_SIZE + (SLOT_COUNT - 1) * SLOT_SPACING;

            RectTransform hudRt = MakeRect(left ? "WeaponHUD_Left" : "WeaponHUD_Right", _parent);
            Vector2 corner = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            SetAnchor(hudRt, corner, corner, new Vector2(left ? EDGE_MARGIN : -EDGE_MARGIN, -EDGE_MARGIN), new Vector2(width, SLOT_SIZE));
            WeaponHUD hud = hudRt.gameObject.AddComponent<WeaponHUD>();

            // 슬롯: Slot0 이 화면 가장자리 (왼쪽은 좌→우, 오른쪽은 우→좌)
            var slots = new WeaponSlotUI[SLOT_COUNT];
            for (int i = 0; i < SLOT_COUNT; ++i)
            {
                float x = i * (SLOT_SIZE + SLOT_SPACING);
                slots[i] = BuildSlot(hudRt, _font, i, corner, new Vector2(left ? x : -x, 0f));
            }

            PlayerStats player = null;
            foreach (PlayerStats p in Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include))
            {
                if (p.Side == _side)
                {
                    player = p;
                    break;
                }
            }

            var appliers = new List<DimensionApplier>();
            foreach (DimensionApplier d in Object.FindObjectsByType<DimensionApplier>(FindObjectsInactive.Include))
            {
                if (d.Side == _side)
                {
                    appliers.Add(d);
                }
            }

            var so = new SerializedObject(hud);
            so.FindProperty("side").enumValueIndex = (int)_side;
            so.FindProperty("player").objectReferenceValue = player;
            SerializedProperty slotArr = so.FindProperty("slots");
            slotArr.arraySize = SLOT_COUNT;
            for (int i = 0; i < SLOT_COUNT; ++i)
            {
                slotArr.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }
            SerializedProperty dimArr = so.FindProperty("dimensionAppliers");
            dimArr.arraySize = appliers.Count;
            for (int i = 0; i < appliers.Count; ++i)
            {
                dimArr.GetArrayElementAtIndex(i).objectReferenceValue = appliers[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            _sb.Append(hudRt.name).Append(": player=").Append(null != player ? player.name : "없음(런타임 Find)")
               .Append(", dimensionAppliers=").Append(appliers.Count).AppendLine();
        }

        private static WeaponSlotUI BuildSlot(Transform _parent, TMP_FontAsset _font, int _index, Vector2 _corner, Vector2 _pos)
        {
            RectTransform slotRt = MakeRect("Slot" + _index, _parent);
            SetAnchor(slotRt, _corner, _corner, _pos, new Vector2(SLOT_SIZE, SLOT_SIZE));

            Image bg = MakeRect("Background", slotRt).gameObject.AddComponent<Image>();
            Stretch(bg.rectTransform, 0f);
            bg.color = SLOT_BG_COLOR;
            bg.raycastTarget = false;

            RectTransform content = MakeRect("Content", slotRt);
            Stretch(content, 0f);

            Image icon = MakeRect("Icon", content).gameObject.AddComponent<Image>();
            Stretch(icon.rectTransform, 8f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI nameText = MakeRect("NameText", content).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch(nameText.rectTransform, 4f);
            SetupText(nameText, _font, string.Empty, 18f, TextAlignmentOptions.Center);
            nameText.textWrappingMode = TextWrappingModes.Normal;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.gameObject.SetActive(false);

            TextMeshProUGUI levelText = MakeRect("LevelText", content).gameObject.AddComponent<TextMeshProUGUI>();
            SetAnchor(levelText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(6f, 2f), new Vector2(48f, 32f));
            SetupText(levelText, _font, "0", 24f, TextAlignmentOptions.BottomLeft);

            WeaponSlotUI slot = slotRt.gameObject.AddComponent<WeaponSlotUI>();
            var so = new SerializedObject(slot);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("contentRoot").objectReferenceValue = content.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            content.gameObject.SetActive(false);
            return slot;
        }

        private static RectTransform MakeRect(string _name, Transform _parent)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.layer = UI_LAYER;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            return rt;
        }

        private static void SetAnchor(RectTransform _rt, Vector2 _anchor, Vector2 _pivot, Vector2 _pos, Vector2 _size)
        {
            _rt.anchorMin = _rt.anchorMax = _anchor;
            _rt.pivot = _pivot;
            _rt.sizeDelta = _size;
            _rt.anchoredPosition = _pos;
        }

        private static void Stretch(RectTransform _rt, float _inset)
        {
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = new Vector2(_inset, _inset);
            _rt.offsetMax = new Vector2(-_inset, -_inset);
        }

        private static void SetupText(TMP_Text _tmp, TMP_FontAsset _font, string _text, float _size, TextAlignmentOptions _align)
        {
            if (null != _font)
            {
                _tmp.font = _font;
                _tmp.fontSharedMaterial = _font.material;
            }
            _tmp.text = _text;
            _tmp.fontSize = _size;
            _tmp.alignment = _align;
            _tmp.color = Color.white;
            _tmp.raycastTarget = false;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
            _tmp.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
