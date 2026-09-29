using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 텍스트 하나
/// 지정된 화면(왼쪽 / 오른쪽 / 전체)의 Canvas 에 월드 위치를 따라 출력하고, 떠오르며 사라진 뒤 매니져 풀로 돌아간다.
/// (DamageTextManager 가 생성 / 관리하므로 직접 붙일 필요 없음)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DamageTextUI
    : MonoBehaviour
{
    // 숫자가 잘리지 않을 정도의 넉넉한 크기 (Overflow 로 표시)
    private static readonly Vector2 TEXT_SIZE = new Vector2(200f, 60f);

    private DamageTextManager manager;
    private RectTransform rectTransform;
    private TextMeshProUGUI text;

    private Canvas rootCanvas;
    private Camera worldCamera;
    private Vector3 startWorldPos;
    private Color baseColor;
    private float elapsed;
    private bool isPlaying = false;

    /// <summary>
    /// 코드로 텍스트 오브젝트 생성
    /// </summary>
    public static DamageTextUI Create(DamageTextManager _manager)
    {
        GameObject go = new GameObject(nameof(DamageTextUI), typeof(RectTransform), typeof(TextMeshProUGUI), typeof(DamageTextUI));
        go.SetActive(false);

        DamageTextUI ui = go.GetComponent<DamageTextUI>();
        ui.manager = _manager;
        ui.rectTransform = go.GetComponent<RectTransform>();
        ui.rectTransform.sizeDelta = TEXT_SIZE;

        ui.text = go.GetComponent<TextMeshProUGUI>();
        ui.text.alignment = TextAlignmentOptions.Center;
        ui.text.textWrappingMode = TextWrappingModes.NoWrap;
        ui.text.overflowMode = TextOverflowModes.Overflow;
        // 데미지 텍스트가 클릭을 막지 않도록
        ui.text.raycastTarget = false;

        go.transform.SetParent(_manager.transform, false);
        return ui;
    }

    /// <summary>
    /// 출력 시작
    /// </summary>
    /// <param name="_screen">그려질 화면 (Auto 는 Full 로 처리)</param>
    /// <param name="_worldCamera">월드 위치를 비추는 카메라 (null 이면 Camera.main)</param>
    public void Play(UI_SCREEN _screen, Camera _worldCamera, Vector3 _worldPos, string _text, Color _color)
    {
        Canvas canvas = UICanvasProvider.GetCanvas(_screen);
        rectTransform.SetParent(canvas.transform, false);
        // 최신 텍스트가 위에 그려지도록
        rectTransform.SetAsLastSibling();
        LayerUtils.SetLayer(gameObject, UICanvasProvider.GetUILayer(_screen));

        rootCanvas = canvas.rootCanvas;
        worldCamera = _worldCamera;
        startWorldPos = _worldPos;
        baseColor = _color;
        elapsed = 0f;
        isPlaying = true;

        if (null != manager.font)
        {
            text.font = manager.font;
        }
        text.fontSize = manager.fontSize;
        text.fontStyle = manager.fontStyle;
        text.text = _text;

        gameObject.SetActive(true);
        UpdateVisual(0f);
    }

    /// <summary>
    /// 출력 중지 (매니져가 풀로 회수할 때 호출)
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        gameObject.SetActive(false);
    }

    // 카메라가 Update 에서 움직인 뒤 위치를 맞추기 위해 LateUpdate 사용
    private void LateUpdate()
    {
        if (false == isPlaying)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / manager.duration);
        UpdateVisual(t);

        if (1f <= t)
        {
            manager.Release(this);
        }
    }

    private void UpdateVisual(float _t)
    {
        Color color = baseColor;
        color.a *= Mathf.Clamp01(manager.fadeCurve.Evaluate(_t));
        text.color = color;

        Vector3 worldPos = startWorldPos + Vector3.up * (manager.worldRise * manager.riseCurve.Evaluate(_t));
        FollowWorldPosition(worldPos);
    }

    private void FollowWorldPosition(Vector3 _worldPos)
    {
        Camera cam = (null != worldCamera) ? worldCamera : Camera.main;
        if (null == cam || null == rootCanvas)
        {
            return;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(_worldPos);

        // 카메라 뒤에 있으면 숨김
        bool isBehind = screenPos.z < 0f;
        text.enabled = !isBehind;
        if (true == isBehind)
        {
            return;
        }

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
}
