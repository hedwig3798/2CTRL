using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 최대치 / 현재치에 따라 게이지가 늘어나고 줄어드는 범용 슬라이더 UI
/// 항상 Canvas 아래에 두고 사용하며, 크기는 RectTransform 의 Width / Height 를 그대로 사용한다.
/// - background : 배경 UI
/// - fill       : 현재치 UI (background의 자식)
/// 둘 다 비어있으면 자식으로 자동 생성되며, 스프라이트 / 색은 "이미지" 항목에서 지정한다.
/// 스프라이트가 비어있으면 지정한 색의 단색으로, 지정하면 해당 스프라이트로 출력한다. (색은 틴트로 곱해짐)
///
/// fill Image의 Type이
///   Filled (스프라이트 있음) -> fillAmount 로 표시 (스프라이트가 잘리는 방식)
///   그 외                    -> 앵커를 조절해 크기를 늘리고 줄임 (스프라이트가 늘어나는 방식)
///
/// 타 스크립트 사용 예)
///   slider.SetValue(currHP, maxHP);
///   slider.CurrValue = 50f;
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SliderUI
    : MonoBehaviour
{
    public enum FILL_DIRECTION
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom,
    }

    [Header("UI 지정")]
    [Tooltip("배경 UI (비어있으면 자동 생성)")]
    public Image background;
    [Tooltip("현재치 UI (비어있으면 자동 생성)")]
    public Image fill;

    [Header("이미지")]
    [Tooltip("비어있으면 backgroundColor 단색으로 출력")]
    public Sprite backgroundSprite;
    public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [Tooltip("비어있으면 fillColor 단색으로 출력")]
    public Sprite fillSprite;
    public Color fillColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Header("값")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float currValue = 100f;

    [Header("표시 옵션")]
    [SerializeField] private FILL_DIRECTION direction = FILL_DIRECTION.LeftToRight;
    [Tooltip("0 이하 : 즉시 반영\n0 초과 : 초당 이 비율(0~1)만큼 천천히 변화")]
    [FormerlySerializedAs("smoothSpeed")]
    public float smoothStep = 0f;

    /// <summary>
    /// 값이 바뀔 때 호출 (현재치, 최대치)
    /// </summary>
    public Action<float, float> OnValueChanged;

    // 실제 화면에 표시 중인 비율 (smoothStep 에 따라 Ratio 를 따라감)
    private float displayRatio;

    #region Public API

    public float MaxValue
    {
        get => maxValue;
        set => SetValue(currValue, value);
    }

    public float CurrValue
    {
        get => currValue;
        set => SetValue(value, maxValue);
    }

    public FILL_DIRECTION Direction
    {
        get => direction;
        set => SetDirection(value);
    }

    /// <summary>
    /// 현재치 / 최대치 (0 ~ 1)
    /// </summary>
    public float Ratio
    {
        get
        {
            if (maxValue <= 0f)
            {
                return 0f;
            }
            return Mathf.Clamp01(currValue / maxValue);
        }
    }

    /// <summary>
    /// 현재치와 최대치를 동시에 설정
    /// </summary>
    /// <param name="_immediate">true면 smoothStep을 무시하고 즉시 반영</param>
    public void SetValue(float _curr, float _max, bool _immediate = false)
    {
        float prevCurr = currValue;
        float prevMax = maxValue;

        maxValue = Mathf.Max(0f, _max);
        currValue = Mathf.Clamp(_curr, 0f, maxValue);

        if (true == _immediate || smoothStep <= 0f)
        {
            Refresh();
        }

        if (false == Mathf.Approximately(prevCurr, currValue)
            || false == Mathf.Approximately(prevMax, maxValue))
        {
            OnValueChanged?.Invoke(currValue, maxValue);
        }
    }

    /// <summary>
    /// 최대치만 변경
    /// </summary>
    /// <param name="_keepRatio">true면 현재 비율을 유지하도록 현재치도 함께 조정</param>
    public void SetMax(float _max, bool _keepRatio = false, bool _immediate = false)
    {
        float curr = currValue;
        if (true == _keepRatio)
        {
            curr = Ratio * Mathf.Max(0f, _max);
        }
        SetValue(curr, _max, _immediate);
    }

    /// <summary>
    /// 현재치만 변경
    /// </summary>
    public void SetCurr(float _curr, bool _immediate = false)
    {
        SetValue(_curr, maxValue, _immediate);
    }

    /// <summary>
    /// 현재치에 더하기 (음수면 감소)
    /// </summary>
    public void AddCurr(float _amount, bool _immediate = false)
    {
        SetValue(currValue + _amount, maxValue, _immediate);
    }

    /// <summary>
    /// 애니메이션 없이 현재 값으로 즉시 갱신
    /// </summary>
    public void Refresh()
    {
        displayRatio = Ratio;
        ApplyVisual(displayRatio);
    }

    /// <summary>
    /// 게이지가 채워지는 방향 변경
    /// </summary>
    public void SetDirection(FILL_DIRECTION _direction)
    {
        direction = _direction;
        ApplyVisual(displayRatio);
    }

    /// <summary>
    /// 배경 / 현재치 스프라이트 변경 (null 이면 단색)
    /// </summary>
    public void SetSprites(Sprite _background, Sprite _fill)
    {
        backgroundSprite = _background;
        fillSprite = _fill;
        ApplyImageSettings();
        ApplyVisual(displayRatio);
    }

    /// <summary>
    /// 배경 / 현재치 색 변경
    /// </summary>
    public void SetColors(Color _background, Color _fill)
    {
        backgroundColor = _background;
        fillColor = _fill;
        ApplyImageSettings();
    }

    #endregion

    private void Awake()
    {
        EnsureImages();
        ApplyImageSettings();

        maxValue = Mathf.Max(0f, maxValue);
        currValue = Mathf.Clamp(currValue, 0f, maxValue);
        Refresh();
    }

    private void Update()
    {
        float targetRatio = Ratio;
        if (Mathf.Approximately(displayRatio, targetRatio))
        {
            return;
        }

        if (smoothStep <= 0f)
        {
            displayRatio = targetRatio;
        }
        else
        {
            displayRatio = Mathf.MoveTowards(displayRatio, targetRatio, smoothStep * Time.unscaledDeltaTime);
        }

        ApplyVisual(displayRatio);
    }

    /// <summary>
    /// background / fill Image 가 없으면 자식으로 생성
    /// (Reset 등으로 참조만 끊긴 경우 이미 있는 자식을 다시 연결)
    /// </summary>
    private void EnsureImages()
    {
        if (null == background)
        {
            background = FindChildImage(transform, "Background");
        }
        if (null == background)
        {
            background = CreateImage("Background", transform);
        }

        if (null == fill)
        {
            fill = FindChildImage(background.transform, "Fill");
        }
        if (null == fill)
        {
            fill = CreateImage("Fill", background.transform);
        }
    }

    private Image FindChildImage(Transform _parent, string _name)
    {
        Transform child = _parent.Find(_name);
        return (null != child) ? child.GetComponent<Image>() : null;
    }

    private Image CreateImage(string _name, Transform _parent)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;

#if UNITY_EDITOR
        if (false == Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + _name);
        }
#endif

        // 부모 영역 전체로 늘림
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(_parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 게이지가 클릭을 막지 않도록
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;

        return image;
    }

    /// <summary>
    /// 인스펙터의 스프라이트 / 색을 Image 에 적용
    /// 스프라이트가 null 이면 Image 기본(흰 사각형) * 색 = 단색으로 출력된다.
    /// </summary>
    private void ApplyImageSettings()
    {
        if (null != background)
        {
            background.sprite = backgroundSprite;
            background.color = backgroundColor;
        }
        if (null != fill)
        {
            fill.sprite = fillSprite;
            fill.color = fillColor;
        }
    }

    private void ApplyVisual(float _ratio)
    {
        if (null == fill)
        {
            return;
        }

        // Filled 타입 : fillAmount 사용
        // (스프라이트가 없으면 Image 가 fillAmount 를 무시하므로 앵커 방식 사용)
        if (Image.Type.Filled == fill.type && null != fill.sprite)
        {
            ApplyFillAmount(_ratio);
        }
        else
        {
            ApplyAnchor(_ratio);
        }
    }

    private void ApplyFillAmount(float _ratio)
    {
        // 앵커 방식으로 줄어든 상태였다면 원래 크기로 복구
        RectTransform rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        switch (direction)
        {
            case FILL_DIRECTION.LeftToRight:
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                break;
            case FILL_DIRECTION.RightToLeft:
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Right;
                break;
            case FILL_DIRECTION.BottomToTop:
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                break;
            case FILL_DIRECTION.TopToBottom:
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Top;
                break;
        }

        fill.fillAmount = _ratio;
    }

    private void ApplyAnchor(float _ratio)
    {
        // 부모(background) 기준 앵커로 크기 조절
        Vector2 anchorMin = Vector2.zero;
        Vector2 anchorMax = Vector2.one;

        switch (direction)
        {
            case FILL_DIRECTION.LeftToRight:
                anchorMax.x = _ratio;
                break;
            case FILL_DIRECTION.RightToLeft:
                anchorMin.x = 1f - _ratio;
                break;
            case FILL_DIRECTION.BottomToTop:
                anchorMax.y = _ratio;
                break;
            case FILL_DIRECTION.TopToBottom:
                anchorMin.y = 1f - _ratio;
                break;
        }

        RectTransform rt = fill.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

#if UNITY_EDITOR
    // 에디터에서 컴포넌트를 추가하면 이미지 자동 생성
    private void Reset()
    {
        EnsureImages();
        ApplyImageSettings();
        Refresh();
    }

    // 인스펙터에서 값을 바꾸면 바로 미리보기
    private void OnValidate()
    {
        maxValue = Mathf.Max(0f, maxValue);
        currValue = Mathf.Clamp(currValue, 0f, maxValue);
        UnityEditor.EditorApplication.delayCall += EditorRefresh;
    }

    private void EditorRefresh()
    {
        if (null == this)
        {
            return;
        }
        ApplyImageSettings();

        // 플레이 중에는 smoothStep 에 따라 Update 에서 반영
        if (false == Application.isPlaying)
        {
            Refresh();
        }
        else
        {
            ApplyVisual(displayRatio);
        }
    }
#endif
}
