using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Builders
{
    /// <summary>
    /// 설정 창 / 일시정지 메뉴 빌더가 같이 쓰는 uGUI 생성 헬퍼 (에디터 전용)
    /// - 모든 텍스트는 갈무리 픽셀 폰트(PixelFontBuilder.GetOrCreateRegular), 크기는 12 의 배수
    /// - 색 · 스프라이트는 플레이스홀더 (다듬기는 visual-designer / ArtResourceBuilder)
    /// </summary>
    internal static class SettingsUIKit
    {
        public const int UI_LAYER = 5;
        public static readonly Color DIM_COLOR = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color WINDOW_COLOR = new Color(0.08f, 0.08f, 0.11f, 0.96f);
        public static readonly Color BUTTON_COLOR = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        public static readonly Color CONTROL_BG_COLOR = new Color(0.2f, 0.2f, 0.26f, 1f);
        public static readonly Color ACCENT_COLOR = new Color(0.95f, 0.75f, 0.3f, 1f);

        private static TMP_FontAsset font;

        public static TMP_FontAsset Font
        {
            get
            {
                if (null == font)
                {
                    font = PixelFontBuilder.GetOrCreateRegular();
                }
                return font;
            }
        }

        /// <summary>
        /// 다음 빌드에서 폰트를 다시 불러오도록 (도메인 리로드 없이 여러 번 실행할 때)
        /// </summary>
        public static void ResetCache()
        {
            font = null;
        }

        public static Sprite BuiltinSprite(string _name)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + _name);
        }

        private const string UI_ART = "Assets/Resources/Textuer/Art/UI/";
        // ArtResourceBuilder.UI_PIXEL_MULT 와 같은 값 (UI 픽셀 2배)
        private const float PIXEL_MULT = 100f / 32f / 2f;

        /// <summary>
        /// 게임 UI 픽셀 스프라이트 (없으면 null)
        /// </summary>
        public static Sprite ArtSprite(string _name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(UI_ART + "Common_UI_" + _name + ".png");
        }

        public static void Slice(Image _image, string _sprite, Color _color)
        {
            Sprite sprite = ArtSprite(_sprite);
            if (null == sprite)
            {
                return;
            }
            _image.sprite = sprite;
            _image.type = Image.Type.Sliced;
            _image.pixelsPerUnitMultiplier = PIXEL_MULT;
            _image.color = _color;
        }

        public static void StyleButton(Button _button)
        {
            Image image = _button.targetGraphic as Image;
            if (null == image || null == ArtSprite("Button_Normal"))
            {
                return;
            }
            Slice(image, "Button_Normal", Color.white);
            _button.transition = Selectable.Transition.SpriteSwap;
            _button.spriteState = new SpriteState
            {
                highlightedSprite = ArtSprite("Button_Hover"),
                selectedSprite = ArtSprite("Button_Hover"),
                pressedSprite = ArtSprite("Button_Pressed"),
            };
        }

        public static RectTransform MakeRect(string _name, Transform _parent)
        {
            var go = new GameObject(_name, typeof(RectTransform));
            go.layer = UI_LAYER;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_parent, false);
            return rt;
        }

        public static void Stretch(RectTransform _rt)
        {
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
            _rt.localScale = Vector3.one;
        }

        public static void Center(RectTransform _rt, Vector2 _size, Vector2 _position)
        {
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = _size;
            _rt.anchoredPosition = _position;
        }

        public static Image MakeImage(string _name, Transform _parent, Color _color, bool _raycast)
        {
            RectTransform rt = MakeRect(_name, _parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = _color;
            image.raycastTarget = _raycast;
            return image;
        }

        public static TextMeshProUGUI MakeLabel(string _name, Transform _parent, string _text, float _size, TextAlignmentOptions _align)
        {
            RectTransform rt = MakeRect(_name, _parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            SetupText(tmp, _text, _size, _align);
            return tmp;
        }

        public static void SetupText(TMP_Text _tmp, string _text, float _size, TextAlignmentOptions _align)
        {
            if (null != Font)
            {
                _tmp.font = Font;
                _tmp.fontSharedMaterial = Font.material;
            }
            _tmp.text = _text;
            _tmp.fontSize = _size;
            _tmp.fontStyle = FontStyles.Normal;
            _tmp.alignment = _align;
            _tmp.color = Color.white;
            _tmp.raycastTarget = false;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
            _tmp.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// 모든 하위 TMP 텍스트에 갈무리 폰트 적용 (DefaultControls 로 만든 컨트롤용)
        /// </summary>
        public static void ApplyFont(GameObject _root, float _size, Color _color)
        {
            foreach (TMP_Text t in _root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (null != Font)
                {
                    t.font = Font;
                    t.fontSharedMaterial = Font.material;
                }
                t.fontSize = _size;
                t.fontStyle = FontStyles.Normal;
                t.color = _color;
            }
        }

        public static LayoutElement SetLayoutSize(GameObject _go, float _width, float _height)
        {
            LayoutElement le = _go.GetComponent<LayoutElement>();
            if (null == le)
            {
                le = _go.AddComponent<LayoutElement>();
            }
            le.preferredWidth = _width;
            le.preferredHeight = _height;
            le.minWidth = _width;
            le.minHeight = _height;
            return le;
        }

        public static Button MakeButton(string _name, Transform _parent, string _label, float _width, float _height, float _fontSize)
        {
            Image image = MakeImage(_name, _parent, BUTTON_COLOR, true);
            image.sprite = BuiltinSprite("UISprite.psd");
            image.type = Image.Type.Sliced;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            image.rectTransform.sizeDelta = new Vector2(_width, _height);
            SetLayoutSize(image.gameObject, _width, _height);

            TextMeshProUGUI label = MakeLabel("Label", image.transform, _label, _fontSize, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        public static VerticalLayoutGroup AddVertical(GameObject _go, float _spacing, TextAnchor _align)
        {
            var vlg = _go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = _spacing;
            vlg.childAlignment = _align;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        public static HorizontalLayoutGroup AddHorizontal(GameObject _go, float _spacing, TextAnchor _align)
        {
            var hlg = _go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = _spacing;
            hlg.childAlignment = _align;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            return hlg;
        }

        public static void SetLayerRecursive(GameObject _root, int _layer)
        {
            foreach (Transform t in _root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = _layer;
            }
        }
    }
}
