using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최대치 / 현재치에 따라 게이지가 늘어나고 줄어드는 범용 슬라이더 UI
/// - background : 배경 UI
/// - fill       : 현재치 UI (background의 자식으로 두는 것을 권장)
///
/// fill Image의 Type이
///   Filled  -> fillAmount 로 표시 (스프라이트가 잘리는 방식)
///   그 외   -> 앵커를 조절해 크기를 늘리고 줄임 (스프라이트가 늘어나는 방식)
///
/// 타 스크립트 사용 예)
///   slider.SetValue(currHP, maxHP);
///   slider.CurrValue = 50f;
///   slider.MaxValue  = 200f;
/// </summary>
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
    [Tooltip("배경 UI")]
    public Image background;
    [Tooltip("현재치 UI")]
    public Image fill;

    [Header("값")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float currValue = 100f;

    [Header("표시 옵션")]
    public FILL_DIRECTION direction = FILL_DIRECTION.LeftToRight;
    [Tooltip("0 이하 : 즉시 반영\n0 초과 : 초당 이 비율(0~1)만큼 부드럽게 변화")]
    public float smoothSpeed = 0f;

    /// <summary>
    /// 값이 바뀔 때 호출 (현재치, 최대치)
    /// </summary>
    public Action<float, float> OnValueChanged;

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
    /// <param name="_immediate">true면 smoothSpeed를 무시하고 즉시 반영</param>
    public void SetValue(float _curr, float _max, bool _immediate = false)
    {
        float prevCurr = currValue;
        float prevMax = maxValue;

        maxValue = Mathf.Max(0f, _max);
        currValue = Mathf.Clamp(_curr, 0f, maxValue);

        if (true == _immediate || smoothSpeed <= 0f)
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

    #endregion

    private void Awake()
    {
        maxValue = Mathf.Max(0f, maxValue);
        currValue = Mathf.Clamp(currValue, 0f, maxValue);
        Refresh();
    }

    private void Update()
    {
        float target = Ratio;
        if (Mathf.Approximately(displayRatio, target))
        {
            return;
        }

        if (smoothSpeed <= 0f)
        {
            displayRatio = target;
        }
        else
        {
            displayRatio = Mathf.MoveTowards(displayRatio, target, smoothSpeed * Time.unscaledDeltaTime);
        }

        ApplyVisual(displayRatio);
    }

    private void ApplyVisual(float _ratio)
    {
        if (null == fill)
        {
            return;
        }

        bool isHorizontal = (FILL_DIRECTION.LeftToRight == direction || FILL_DIRECTION.RightToLeft == direction);

        // Filled 타입 : fillAmount 사용
        if (Image.Type.Filled == fill.type)
        {
            if (true == isHorizontal)
            {
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (FILL_DIRECTION.LeftToRight == direction)
                    ? (int)Image.OriginHorizontal.Left
                    : (int)Image.OriginHorizontal.Right;
            }
            else
            {
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (FILL_DIRECTION.BottomToTop == direction)
                    ? (int)Image.OriginVertical.Bottom
                    : (int)Image.OriginVertical.Top;
            }

            fill.fillAmount = _ratio;
            return;
        }

        // 그 외 타입 : 부모 기준 앵커로 크기 조절
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
        Refresh();
    }
#endif
}
