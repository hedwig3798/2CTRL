using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen Space Overlay 루트 캔버스를 16:9 게임 영역 안에 맞추는 컴포넌트
/// - 런타임 시작 시 캔버스의 자식들을 자식 "LetterboxRoot" 아래로 옮기고, 그 루트를 16:9 영역에 맞춘다.
///   (씬 / 프리팹의 계층은 바꾸지 않으므로 기존 빌더 · ArtResourceBuilder 의 경로 탐색에 영향 없음)
/// - CanvasScaler 가 ScaleWithScreenSize 면 화면 전체가 아니라 16:9 영역 크기를 기준으로 배율을 계산한다.
///   (기준 1920x1080 이면 16:9 영역 안에서는 항상 1920x1080 좌표계)
/// - 나중에 캔버스 바로 아래에 추가되는 자식(Instantiate 등)도 LetterboxRoot 아래로 옮긴다.
///   단, 자체 Canvas 가 있는 자식(드롭다운 블로커 등)은 화면 전체를 덮어야 하므로 옮기지 않는다.
/// - SettingsApplier 가 씬 로드 때 모든 Overlay 루트 캔버스에 자동으로 붙인다. 직접 붙여도 된다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class LetterboxCanvas
    : MonoBehaviour
{
    private const string ROOT_NAME = "LetterboxRoot";

    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform root;

    // ScaleWithScreenSize 를 대신 계산하므로 원래 설정을 보관 (끄면 복구)
    private bool controlsScaler;
    private CanvasScaler.ScaleMode originalScaleMode;
    private Vector2 referenceResolution;
    private float matchWidthOrHeight;

    private Rect appliedRect;
    private int appliedWidth;
    private int appliedHeight;
    private bool childrenDirty;

    private readonly List<Transform> moveBuffer = new List<Transform>();

    public RectTransform Root => root;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        scaler = GetComponent<CanvasScaler>();

        if (false == IsTargetCanvas())
        {
            enabled = false;
            return;
        }

        CreateRoot();
        MoveChildrenToRoot(true);
    }

    private void OnEnable()
    {
        if (null == root)
        {
            return;
        }

        if (null != scaler && CanvasScaler.ScaleMode.ScaleWithScreenSize == scaler.uiScaleMode)
        {
            controlsScaler = true;
            originalScaleMode = scaler.uiScaleMode;
            referenceResolution = scaler.referenceResolution;
            matchWidthOrHeight = scaler.matchWidthOrHeight;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }

        Apply(true);
    }

    private void OnDisable()
    {
        if (true == controlsScaler && null != scaler)
        {
            scaler.uiScaleMode = originalScaleMode;
            controlsScaler = false;
        }

        if (null != root)
        {
            SetAnchors(root, new Rect(0f, 0f, 1f, 1f));
        }
    }

    private void OnTransformChildrenChanged()
    {
        childrenDirty = true;
    }

    private void LateUpdate()
    {
        if (true == childrenDirty)
        {
            childrenDirty = false;
            MoveChildrenToRoot(false);
        }
        Apply(false);
    }

    private bool IsTargetCanvas()
    {
        return null != canvas && true == canvas.isRootCanvas && RenderMode.ScreenSpaceOverlay == canvas.renderMode;
    }

    private void CreateRoot()
    {
        Transform existing = transform.Find(ROOT_NAME);
        if (null != existing && existing is RectTransform existingRect)
        {
            root = existingRect;
            return;
        }

        GameObject go = new GameObject(ROOT_NAME, typeof(RectTransform));
        go.layer = gameObject.layer;
        root = go.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one;
        SetAnchors(root, new Rect(0f, 0f, 1f, 1f));
    }

    /// <summary>
    /// 캔버스 바로 아래 자식을 LetterboxRoot 아래로 옮긴다 (순서 유지)
    /// </summary>
    /// <param name="_includeCanvases">true 면 자체 Canvas 가 있는 자식도 옮긴다 (시작 시 원래 계층)</param>
    private void MoveChildrenToRoot(bool _includeCanvases)
    {
        if (null == root)
        {
            return;
        }

        moveBuffer.Clear();
        for (int i = 0; i < transform.childCount; ++i)
        {
            Transform child = transform.GetChild(i);
            if (child == root)
            {
                continue;
            }
            if (false == _includeCanvases && null != child.GetComponent<Canvas>())
            {
                continue;
            }
            moveBuffer.Add(child);
        }

        foreach (Transform child in moveBuffer)
        {
            child.SetParent(root, false);
        }
        moveBuffer.Clear();

        // 자체 Canvas 자식(블로커 등)이 LetterboxRoot 보다 위에 그려지도록 루트는 맨 앞에 둔다
        if (0 != root.GetSiblingIndex())
        {
            root.SetAsFirstSibling();
        }
    }

    private void Apply(bool _force)
    {
        if (null == root)
        {
            return;
        }

        Rect area = Letterbox.NormalizedRect;
        int width = Screen.width;
        int height = Screen.height;
        if (false == _force && area == appliedRect && width == appliedWidth && height == appliedHeight)
        {
            return;
        }

        appliedRect = area;
        appliedWidth = width;
        appliedHeight = height;

        SetAnchors(root, area);

        if (true == controlsScaler && null != scaler)
        {
            scaler.scaleFactor = CalculateScaleFactor(area.width * width, area.height * height);
        }
    }

    /// <summary>
    /// CanvasScaler 의 ScaleWithScreenSize(Match Width Or Height) 와 같은 식을 16:9 영역 크기로 계산
    /// </summary>
    private float CalculateScaleFactor(float _width, float _height)
    {
        if (referenceResolution.x <= 0f || referenceResolution.y <= 0f || _width <= 0f || _height <= 0f)
        {
            return 1f;
        }

        float logWidth = Mathf.Log(_width / referenceResolution.x, 2f);
        float logHeight = Mathf.Log(_height / referenceResolution.y, 2f);
        float log = Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight);
        return Mathf.Pow(2f, log);
    }

    private static void SetAnchors(RectTransform _rt, Rect _area)
    {
        _rt.anchorMin = new Vector2(_area.xMin, _area.yMin);
        _rt.anchorMax = new Vector2(_area.xMax, _area.yMax);
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
