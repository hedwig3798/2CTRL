using UnityEngine;

/// <summary>
/// 모든 UI 컴포넌트의 부모 클래스
/// 월드 오브젝트(프리팹 등)의 자식으로 두면 Start 때 화면(screen)에 맞는 씬의 Canvas 아래로 이동한다.
/// - screen = Auto 면 원래 부모(owner)의 레이어를 보고 왼쪽 / 오른쪽 / 전체 화면을 결정
/// - 원래 부모(owner)가 비활성화되면 UI도 비활성화, 활성화되면 다시 활성화
/// - owner가 파괴되면 UI도 파괴
///
/// 상속 시 Awake / Start / OnDestroy 를 재정의한다면 반드시 base 를 먼저 호출할 것
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class CanvasUI
    : MonoBehaviour
{
    [Header("Canvas 배치")]
    [Tooltip("true면 Start 때 씬의 Canvas 아래로 이동\n(이미 Canvas 아래에 있다면 이동하지 않음)")]
    public bool attachToSceneCanvas = true;
    [Tooltip("그려질 화면\nAuto : 원래 부모의 레이어로 결정 (왼쪽 카메라에만 보이면 Left, 오른쪽에만 보이면 Right, 그 외 Full)")]
    public UI_SCREEN screen = UI_SCREEN.Auto;

    /// <summary>
    /// Canvas로 옮기기 전 원래 부모 (옮기지 않았다면 null)
    /// </summary>
    protected Transform owner;
    private UIOwnerLink ownerLink;
    private UI_SCREEN resolvedScreen = UI_SCREEN.Full;

    public Transform Owner => owner;

    /// <summary>
    /// 실제로 배치된 화면
    /// </summary>
    public UI_SCREEN ResolvedScreen => resolvedScreen;

    protected virtual void Awake()
    {
    }

    // Spawner 등이 Instantiate 직후 레이어를 바꾸므로 Awake 가 아닌 Start 에서 배치
    protected virtual void Start()
    {
        AttachToSceneCanvas();
    }

    protected virtual void OnDestroy()
    {
        if (null != ownerLink)
        {
            ownerLink.Unregister(this);
        }
    }

    /// <summary>
    /// owner가 파괴될 때 호출
    /// </summary>
    public virtual void OnOwnerDestroyed()
    {
        Destroy(gameObject);
    }

    private void AttachToSceneCanvas()
    {
        // 이미 Canvas 아래에 배치된 UI
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (null != parentCanvas)
        {
            UIScreenCanvas screenCanvas = parentCanvas.rootCanvas.GetComponent<UIScreenCanvas>();
            resolvedScreen = (null != screenCanvas) ? screenCanvas.Screen : UI_SCREEN.Full;
            return;
        }

        if (false == attachToSceneCanvas)
        {
            return;
        }

        // 다른 UI의 자식이라면 부모 UI와 함께 이동
        CanvasUI[] parentUIs = GetComponentsInParent<CanvasUI>(true);
        foreach (CanvasUI ui in parentUIs)
        {
            if (this != ui)
            {
                return;
            }
        }

        owner = transform.parent;
        resolvedScreen = ResolveScreen();

        transform.SetParent(UICanvasProvider.GetCanvas(resolvedScreen).transform, false);
        LayerUtils.SetLayer(gameObject, UICanvasProvider.GetUILayer(resolvedScreen));

        if (null != owner)
        {
            ownerLink = owner.GetComponent<UIOwnerLink>();
            if (null == ownerLink)
            {
                ownerLink = owner.gameObject.AddComponent<UIOwnerLink>();
            }
            ownerLink.Register(this);
        }
    }

    private UI_SCREEN ResolveScreen()
    {
        if (UI_SCREEN.Auto != screen)
        {
            return screen;
        }
        if (null == owner)
        {
            return UI_SCREEN.Full;
        }
        return UICanvasProvider.ResolveScreen(owner.gameObject.layer);
    }
}
