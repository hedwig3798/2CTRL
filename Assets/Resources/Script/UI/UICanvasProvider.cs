using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 영역(왼쪽 / 오른쪽 / 전체)별 UI Canvas 와 카메라를 찾아서 제공
/// - 씬 전환 등으로 캐시된 Canvas가 파괴되면 다시 찾는다
/// - 해당 화면의 Canvas가 없으면 새로 만든다
///
/// 왼쪽 / 오른쪽 카메라는 cullingMask 에 LUI / RUI 레이어가 포함된 카메라로 판단한다.
/// </summary>
public static class UICanvasProvider
{
    private const string LEFT_UI_LAYER = "LUI";
    private const string RIGHT_UI_LAYER = "RUI";
    private const string FULL_UI_LAYER = "UI";

    private static readonly Dictionary<UI_SCREEN, UIScreenCanvas> canvases = new Dictionary<UI_SCREEN, UIScreenCanvas>();

    #region Canvas

    /// <summary>
    /// 해당 화면의 Canvas 반환 (Auto 는 Full 로 처리)
    /// </summary>
    public static Canvas GetCanvas(UI_SCREEN _screen)
    {
        if (UI_SCREEN.Auto == _screen)
        {
            _screen = UI_SCREEN.Full;
        }

        canvases.TryGetValue(_screen, out UIScreenCanvas screenCanvas);
        if (null == screenCanvas)
        {
            screenCanvas = FindScreenCanvas(_screen);
        }
        if (null == screenCanvas)
        {
            screenCanvas = CreateScreenCanvas(_screen);
        }

        canvases[_screen] = screenCanvas;
        return screenCanvas.Canvas;
    }

    /// <summary>
    /// 해당 화면의 Canvas 를 찾기만 함 (없으면 null, 새로 만들지 않음)
    /// 에디터 디버그 표시 등 Canvas 를 생성하면 안 되는 곳에서 사용
    /// </summary>
    public static Canvas FindCanvas(UI_SCREEN _screen)
    {
        if (UI_SCREEN.Auto == _screen)
        {
            _screen = UI_SCREEN.Full;
        }

        canvases.TryGetValue(_screen, out UIScreenCanvas screenCanvas);
        if (null == screenCanvas)
        {
            screenCanvas = FindScreenCanvas(_screen);
            if (null == screenCanvas)
            {
                return null;
            }
            canvases[_screen] = screenCanvas;
        }

        return screenCanvas.GetComponent<Canvas>();
    }

    public static void Register(UIScreenCanvas _canvas)
    {
        if (null == _canvas)
        {
            return;
        }

        canvases.TryGetValue(_canvas.Screen, out UIScreenCanvas prev);
        if (null != prev && prev != _canvas)
        {
            Debug.LogWarning($"[UICanvasProvider] {_canvas.Screen} 화면 Canvas 가 여러 개입니다. ({prev.name}, {_canvas.name}) 먼저 등록된 것을 사용합니다.");
            return;
        }
        canvases[_canvas.Screen] = _canvas;
    }

    public static void Unregister(UIScreenCanvas _canvas)
    {
        if (null == _canvas)
        {
            return;
        }

        if (canvases.TryGetValue(_canvas.Screen, out UIScreenCanvas curr) && curr == _canvas)
        {
            canvases.Remove(_canvas.Screen);
        }
    }

    private static UIScreenCanvas FindScreenCanvas(UI_SCREEN _screen)
    {
        UIScreenCanvas[] found = Object.FindObjectsByType<UIScreenCanvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (UIScreenCanvas screenCanvas in found)
        {
            if (_screen == screenCanvas.Screen)
            {
                return screenCanvas;
            }
        }
        return null;
    }

    private static UIScreenCanvas CreateScreenCanvas(UI_SCREEN _screen)
    {
        Debug.LogWarning($"[UICanvasProvider] 씬에 {_screen} 화면 Canvas가 없어 새로 생성합니다.");

        // Awake 전에 screen 을 지정하기 위해 비활성 상태로 생성
        GameObject go = new GameObject($"UICanvas_{_screen}", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.SetActive(false);

        UIScreenCanvas screenCanvas = go.AddComponent<UIScreenCanvas>();
        screenCanvas.screen = _screen;
        go.SetActive(true);

        // UI 입력 처리를 위해 EventSystem 도 하나 보장
        EventSystemGuard.Ensure();

        return screenCanvas;
    }

    #endregion

    #region Camera / Layer

    /// <summary>
    /// 해당 화면을 그리는 카메라 (Full / Auto 는 null)
    /// </summary>
    public static Camera GetCamera(UI_SCREEN _screen)
    {
        if (UI_SCREEN.Left != _screen && UI_SCREEN.Right != _screen)
        {
            return null;
        }

        Camera[] cameras = Camera.allCameras;

        // 1순위 : UI 레이어를 그리는 카메라
        int layer = GetUILayer(_screen);
        foreach (Camera cam in cameras)
        {
            if (true == IsLayerRendered(cam, layer))
            {
                return cam;
            }
        }

        // 2순위 : viewport 위치
        foreach (Camera cam in cameras)
        {
            Rect rect = cam.rect;
            if (1f <= rect.width)
            {
                continue;
            }

            bool isLeft = rect.x < 0.5f;
            if ((UI_SCREEN.Left == _screen) == isLeft)
            {
                return cam;
            }
        }

        return null;
    }

    /// <summary>
    /// 해당 레이어를 그리는 카메라 (없으면 null)
    /// </summary>
    public static Camera GetCameraForLayer(int _layer)
    {
        foreach (Camera cam in Camera.allCameras)
        {
            if (true == IsLayerRendered(cam, _layer))
            {
                return cam;
            }
        }
        return null;
    }

    /// <summary>
    /// 해당 레이어의 오브젝트가 보이는 화면
    /// 왼쪽 카메라에만 보이면 Left, 오른쪽에만 보이면 Right, 그 외에는 Full
    /// </summary>
    public static UI_SCREEN ResolveScreen(int _layer)
    {
        bool isLeft = IsLayerRendered(GetCamera(UI_SCREEN.Left), _layer);
        bool isRight = IsLayerRendered(GetCamera(UI_SCREEN.Right), _layer);

        if (true == isLeft && false == isRight)
        {
            return UI_SCREEN.Left;
        }
        if (true == isRight && false == isLeft)
        {
            return UI_SCREEN.Right;
        }
        return UI_SCREEN.Full;
    }

    /// <summary>
    /// 해당 화면의 UI 가 사용할 레이어 (Left = LUI, Right = RUI, 그 외 = UI)
    /// </summary>
    public static int GetUILayer(UI_SCREEN _screen)
    {
        string layerName = FULL_UI_LAYER;
        switch (_screen)
        {
            case UI_SCREEN.Left:
                layerName = LEFT_UI_LAYER;
                break;
            case UI_SCREEN.Right:
                layerName = RIGHT_UI_LAYER;
                break;
        }

        int layer = LayerMask.NameToLayer(layerName);
        return (0 <= layer) ? layer : LayerMask.NameToLayer(FULL_UI_LAYER);
    }

    private static bool IsLayerRendered(Camera _cam, int _layer)
    {
        if (null == _cam || _layer < 0)
        {
            return false;
        }
        return 0 != (_cam.cullingMask & (1 << _layer));
    }

    #endregion
}
