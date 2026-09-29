using UnityEngine;

/// <summary>
/// 화면 영역(왼쪽 / 오른쪽 / 전체)별 UI Canvas 표식
/// Awake 때 Canvas 를 해당 화면에 맞게 설정한다.
/// - Left / Right : Screen Space - Camera, 해당 카메라 지정, 레이어 LUI / RUI
/// - Full         : Screen Space - Overlay, 레이어 UI
///
/// 씬의 Canvas 에 붙여 사용하며, 없으면 UICanvasProvider 가 자동으로 만든다.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class UIScreenCanvas
    : MonoBehaviour
{
    [Tooltip("이 Canvas 가 담당할 화면 (Auto 는 Full 로 처리)")]
    public UI_SCREEN screen = UI_SCREEN.Full;
    [Tooltip("Left / Right 에서 사용할 카메라 (비어있으면 자동으로 찾음)")]
    public Camera targetCamera;

    // 월드 스프라이트보다 위에 그려지도록 할 정렬 순서
    private const int SORTING_ORDER = 1000;

    private Canvas canvas;

    public Canvas Canvas => canvas;

    public UI_SCREEN Screen => (UI_SCREEN.Auto == screen) ? UI_SCREEN.Full : screen;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        Configure();
        UICanvasProvider.Register(this);
    }

    private void OnDestroy()
    {
        UICanvasProvider.Unregister(this);
    }

    /// <summary>
    /// 화면 설정에 맞게 Canvas 를 다시 설정
    /// </summary>
    public void Configure()
    {
        if (null == canvas)
        {
            canvas = GetComponent<Canvas>();
        }

        if (UI_SCREEN.Full == Screen)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        else
        {
            if (null == targetCamera)
            {
                targetCamera = UICanvasProvider.GetCamera(Screen);
            }
            if (null == targetCamera)
            {
                Debug.LogWarning($"[UIScreenCanvas] {Screen} 화면 카메라를 찾지 못해 Overlay 로 표시합니다.");
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = targetCamera;
                canvas.planeDistance = targetCamera.nearClipPlane + 1f;

                // 2D 렌더러에서는 Camera 모드 Canvas 도 스프라이트와 함께 정렬되므로 가장 위로
                SortingLayer[] layers = SortingLayer.layers;
                if (0 < layers.Length)
                {
                    canvas.sortingLayerID = layers[layers.Length - 1].id;
                }
                canvas.sortingOrder = SORTING_ORDER;
            }
        }

        LayerUtils.SetLayer(gameObject, UICanvasProvider.GetUILayer(Screen));
    }
}
