using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최대치 / 현재치에 따라 게이지가 늘어나고 줄어드는 범용 슬라이더 UI
/// 월드 오브젝트의 자식으로 두면 화면(왼쪽 / 오른쪽 / 전체)에 맞는 씬의 Canvas 아래로 자동 이동하고 (CanvasUI 참고)
/// target 을 지정하면 카메라에서 보이는 위치를 따라간다. (비어있으면 원래 부모를 따라감)
/// - background : 배경 UI
/// - fill       : 현재치 UI (background의 자식으로 두는 것을 권장)
/// 둘 다 비어있으면 자식으로 자동 생성되며, 스프라이트 / 색은 "이미지" 항목에서 지정한다.
///
/// fill Image의 Type이
///   Filled  -> fillAmount 로 표시 (스프라이트가 잘리는 방식)
///   그 외   -> 앵커를 조절해 크기를 늘리고 줄임 (스프라이트가 늘어나는 방식)
///
/// 타 스크립트 사용 예)
///   slider.SetValue(currHP, maxHP);
///   slider.CurrValue = 50f;
///   slider.SetTarget(monster.transform, new Vector3(0f, 1.5f, 0f));
/// </summary>
public class SliderUI
    : CanvasUI
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
    public Sprite backgroundSprite;
    public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Sprite fillSprite;
    public Color fillColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Header("크기")]
    [Tooltip("UI 크기 (Canvas 기준 단위)\nRectTransform 의 Width / Height 대신 이 값을 사용")]
    [SerializeField] private Vector2 size = new Vector2(100f, 15f);

    [Header("값")]
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float currValue = 100f;

    [Header("표시 옵션")]
    public FILL_DIRECTION direction = FILL_DIRECTION.LeftToRight;
    [Tooltip("0 이하 : 즉시 반영\n0 초과 : 초당 이 비율(0~1)만큼 부드럽게 변화")]
    public float smoothSpeed = 0f;

    [Header("추적")]
    [Tooltip("따라갈 대상 (비어있으면 원래 부모를 따라감, 원래 부모도 없으면 추적하지 않음)")]
    public Transform target;
    [Tooltip("대상 위치에 더할 월드 오프셋 (예 : 머리 위)")]
    public Vector3 worldOffset = Vector3.zero;
    [Tooltip("화면 좌표에 더할 오프셋 (픽셀)")]
    public Vector2 screenOffset = Vector2.zero;
    [Tooltip("대상을 비추는 카메라 (비어있으면 대상 레이어를 그리는 카메라를 자동으로 찾음)")]
    public Camera targetCamera;
    [Tooltip("대상이 카메라 뒤에 있으면 숨김")]
    public bool hideWhenBehindCamera = true;

    [Header("디버그")]
    [Tooltip("Canvas 밖에 있을 때 Scene 뷰에 실제 표시 크기 / 위치를 사각형으로 표시")]
    public bool showDebugRect = true;
    public Color debugRectColor = Color.green;

    /// <summary>
    /// 값이 바뀔 때 호출 (현재치, 최대치)
    /// </summary>
    public Action<float, float> OnValueChanged;

    private float displayRatio;

    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private bool isHiddenByCamera = false;

    private Camera cachedCamera;
    private Transform cachedCameraTarget;
    private int cachedCameraLayer = -1;

    #region Public API

    /// <summary>
    /// UI 크기 (Canvas 기준 단위)
    /// </summary>
    public Vector2 Size
    {
        get => size;
        set => SetSize(value);
    }

    public void SetSize(Vector2 _size)
    {
        size = new Vector2(Mathf.Max(0f, _size.x), Mathf.Max(0f, _size.y));
        ApplySize();
    }

    public void SetSize(float _width, float _height)
    {
        SetSize(new Vector2(_width, _height));
    }

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

    /// <summary>
    /// 배경 / 현재치 스프라이트 변경 (null 이면 기존 유지)
    /// </summary>
    public void SetSprites(Sprite _background, Sprite _fill)
    {
        if (null != _background)
        {
            backgroundSprite = _background;
        }
        if (null != _fill)
        {
            fillSprite = _fill;
        }
        ApplyImageSettings();
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

    /// <summary>
    /// 추적 대상 변경 (null 이면 원래 부모를 따라감, 원래 부모도 없으면 추적 중지)
    /// </summary>
    /// <param name="_worldOffset">null 이면 기존 오프셋 유지</param>
    public void SetTarget(Transform _target, Vector3? _worldOffset = null)
    {
        target = _target;
        if (null != _worldOffset)
        {
            worldOffset = _worldOffset.Value;
        }

        if (null == FollowingTarget)
        {
            SetHidden(false);
        }
        else
        {
            FollowTarget();
        }
    }

    #endregion

    /// <summary>
    /// 실제로 따라가는 대상 (target 이 없으면 원래 부모)
    /// </summary>
    public Transform FollowingTarget => (null != target) ? target : owner;

    protected override void Awake()
    {
        base.Awake();

        // Start 에서 Canvas 아래로 옮겨지면 OnTransformParentChanged 에서 다시 캐싱

        rectTransform = GetComponent<RectTransform>();
        CacheCanvas();

        ApplySize();
        EnsureImages();
        ApplyImageSettings();

        maxValue = Mathf.Max(0f, maxValue);
        currValue = Mathf.Clamp(currValue, 0f, maxValue);
        Refresh();
    }

    private void OnTransformParentChanged()
    {
        CacheCanvas();
    }

    private void Update()
    {
        float targetRatio = Ratio;
        if (Mathf.Approximately(displayRatio, targetRatio))
        {
            return;
        }

        if (smoothSpeed <= 0f)
        {
            displayRatio = targetRatio;
        }
        else
        {
            displayRatio = Mathf.MoveTowards(displayRatio, targetRatio, smoothSpeed * Time.unscaledDeltaTime);
        }

        ApplyVisual(displayRatio);
    }

    // 대상이 Update 에서 움직인 뒤 위치를 맞추기 위해 LateUpdate 사용
    private void LateUpdate()
    {
        FollowTarget();
    }

    private void CacheCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = (null != canvas) ? canvas.rootCanvas : null;
    }

    private void FollowTarget()
    {
        Transform followTarget = FollowingTarget;
        if (null == followTarget || null == rootCanvas)
        {
            return;
        }

        Camera cam = ResolveCamera(followTarget);
        if (null == cam)
        {
            return;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(followTarget.position + worldOffset);

        // 카메라 뒤에 있으면 숨김
        bool isBehind = screenPos.z < 0f;
        SetHidden(true == hideWhenBehindCamera && true == isBehind);
        if (true == isBehind)
        {
            return;
        }

        screenPos.x += screenOffset.x;
        screenPos.y += screenOffset.y;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (null == parentRect)
        {
            return;
        }

        // Canvas 모드에 따라 스크린 좌표 -> UI 좌표 변환에 쓸 카메라가 다름
        Camera uiCamera = null;
        switch (rootCanvas.renderMode)
        {
            case RenderMode.ScreenSpaceOverlay:
                uiCamera = null;
                break;
            case RenderMode.ScreenSpaceCamera:
                uiCamera = rootCanvas.worldCamera;
                break;
            case RenderMode.WorldSpace:
                uiCamera = (null != rootCanvas.worldCamera) ? rootCanvas.worldCamera : cam;
                break;
        }

        if (true == RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCamera, out Vector2 localPos))
        {
            rectTransform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        }
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

    private void ApplySize()
    {
        RectTransform rt = (null != rectTransform) ? rectTransform : GetComponent<RectTransform>();
        rt.sizeDelta = size;
    }

    /// <summary>
    /// 인스펙터의 스프라이트 / 색을 Image 에 적용
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

    /// <summary>
    /// 추적 대상을 비추는 카메라
    /// targetCamera -> 대상 레이어를 그리는 카메라 -> Canvas 카메라 -> Camera.main
    /// </summary>
    private Camera ResolveCamera(Transform _followTarget)
    {
        if (null != targetCamera)
        {
            return targetCamera;
        }

        // 대상이나 레이어가 바뀔 때만 다시 찾음
        int layer = _followTarget.gameObject.layer;
        if (null == cachedCamera
            || cachedCameraTarget != _followTarget
            || cachedCameraLayer != layer)
        {
            cachedCameraTarget = _followTarget;
            cachedCameraLayer = layer;
            cachedCamera = UICanvasProvider.GetCameraForLayer(layer);
        }

        if (null != cachedCamera)
        {
            return cachedCamera;
        }
        if (null != rootCanvas && null != rootCanvas.worldCamera)
        {
            return rootCanvas.worldCamera;
        }
        return Camera.main;
    }

    private void SetHidden(bool _hidden)
    {
        if (isHiddenByCamera == _hidden)
        {
            return;
        }
        isHiddenByCamera = _hidden;

        if (null != background)
        {
            background.enabled = !_hidden;
        }
        if (null != fill)
        {
            fill.enabled = !_hidden;
        }
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
    // 에디터에서 컴포넌트를 추가하면 이미지 자동 생성
    private void Reset()
    {
        ApplySize();
        EnsureImages();
        ApplyImageSettings();
        Refresh();
    }

    // 인스펙터에서 값을 바꾸면 바로 미리보기
    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
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
        ApplySize();
        ApplyImageSettings();
        Refresh();
    }

    // 카메라가 없을 때 가정할 1 월드 단위당 픽셀 수
    private const float FALLBACK_PIXELS_PER_UNIT = 100f;

    // Canvas 밖(프리팹 / 월드 자식)에 있을 때 실제 표시될 크기와 위치를 Scene 뷰에 표시
    private void OnDrawGizmos()
    {
        if (false == showDebugRect || null != GetComponentInParent<Canvas>(true))
        {
            return;
        }

        // 기준 위치 : target -> owner -> 부모 -> 자기 자신
        Transform anchor = FollowingTarget;
        if (null == anchor)
        {
            anchor = (null != transform.parent) ? transform.parent : transform;
        }
        Vector3 anchorPos = anchor.position + worldOffset;

        Camera cam = ResolveCamera(anchor);
        bool isEstimated = (null == cam);

        // 픽셀 -> 월드 변환 비율과 화면 축
        float worldPerPixel;
        Quaternion rotation;
        if (false == isEstimated)
        {
            float depth = cam.WorldToScreenPoint(anchorPos).z;
            Vector3 p0 = cam.ScreenToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 p1 = cam.ScreenToWorldPoint(new Vector3(1f, 0f, depth));
            worldPerPixel = Vector3.Distance(p0, p1);
            rotation = cam.transform.rotation;
        }
        else
        {
            worldPerPixel = 1f / FALLBACK_PIXELS_PER_UNIT;
            rotation = Quaternion.identity;
        }

        // CanvasScaler 배율 반영 (UI 단위 -> 픽셀)
        float scaleFactor = 1f;
        UI_SCREEN resolved = (UI_SCREEN.Auto == screen) ? UICanvasProvider.ResolveScreen(anchor.gameObject.layer) : screen;
        Canvas sceneCanvas = UICanvasProvider.FindCanvas(resolved);
        if (null != sceneCanvas && 0f < sceneCanvas.scaleFactor)
        {
            scaleFactor = sceneCanvas.scaleFactor;
        }

        Vector2 pixelSize = size * scaleFactor;
        Vector2 pivot = GetComponent<RectTransform>().pivot;

        // 피벗이 기준 위치에 오도록 사각형 중심 계산 (화면 기준 좌표)
        Vector2 centerPixel = screenOffset + Vector2.Scale(new Vector2(0.5f, 0.5f) - pivot, pixelSize);
        Vector3 center = anchorPos + rotation * new Vector3(centerPixel.x, centerPixel.y, 0f) * worldPerPixel;
        Vector3 worldSize = new Vector3(pixelSize.x, pixelSize.y, 0f) * worldPerPixel;

        Color color = debugRectColor;
        if (true == isEstimated)
        {
            color.a *= 0.5f;
        }

        Matrix4x4 prevMatrix = Gizmos.matrix;
        Color prevColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);

        // 테두리
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, worldSize);

        // 현재 비율만큼 채움
        float ratio = Ratio;
        Vector3 fillSize = worldSize;
        Vector3 fillCenter = Vector3.zero;
        switch (direction)
        {
            case FILL_DIRECTION.LeftToRight:
                fillSize.x *= ratio;
                fillCenter.x = (-worldSize.x + fillSize.x) * 0.5f;
                break;
            case FILL_DIRECTION.RightToLeft:
                fillSize.x *= ratio;
                fillCenter.x = (worldSize.x - fillSize.x) * 0.5f;
                break;
            case FILL_DIRECTION.BottomToTop:
                fillSize.y *= ratio;
                fillCenter.y = (-worldSize.y + fillSize.y) * 0.5f;
                break;
            case FILL_DIRECTION.TopToBottom:
                fillSize.y *= ratio;
                fillCenter.y = (worldSize.y - fillSize.y) * 0.5f;
                break;
        }
        Gizmos.color = new Color(color.r, color.g, color.b, color.a * 0.35f);
        Gizmos.DrawCube(fillCenter, fillSize);

        Gizmos.matrix = prevMatrix;
        Gizmos.color = prevColor;

        // 크기 표시
        string label = $"{size.x:0.#}x{size.y:0.#}";
        if (true == isEstimated)
        {
            label += " (카메라 없음 : 추정)";
        }
        UnityEditor.Handles.Label(center + rotation * new Vector3(-worldSize.x * 0.5f, worldSize.y * 0.5f, 0f), label);
    }
#endif
}
