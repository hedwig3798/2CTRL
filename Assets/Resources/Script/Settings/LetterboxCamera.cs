using UnityEngine;

/// <summary>
/// 카메라 viewport rect 를 16:9 게임 영역 안으로 맞추는 컴포넌트
/// - 처음 켜질 때의 rect 를 "전체 화면 기준 원래 rect" 로 기억하고, 그 비율을 16:9 영역 안에서 유지한다.
///   (좌우 분할 카메라 (0,0,0.5,1) / (0.5,0,0.5,1) 도 그대로 동작)
/// - SettingsApplier 가 씬 로드 때 모든 카메라에 자동으로 붙인다. 직접 붙여도 된다.
/// - 영역 밖은 SettingsApplier 의 배경 카메라가 검은색으로 지운다.
/// - 런타임에 원래 rect 를 바꾸려면 camera.rect 대신 SetBaseRect 를 쓴다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class LetterboxCamera
    : MonoBehaviour
{
    private Camera cam;
    private Rect baseRect = new Rect(0f, 0f, 1f, 1f);
    private bool hasBaseRect;
    private Rect appliedRect;

    public Rect BaseRect => baseRect;

    /// <summary>
    /// 전체 화면 기준 원래 rect 변경
    /// </summary>
    public void SetBaseRect(Rect _rect)
    {
        baseRect = _rect;
        hasBaseRect = true;
        Apply(true);
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        CaptureBaseRect();
    }

    private void OnEnable()
    {
        Apply(true);
    }

    private void OnDisable()
    {
        // 꺼지면 원래 rect 로 복구 (다른 용도로 재사용될 때 영향 없도록)
        if (null != cam && true == hasBaseRect)
        {
            cam.rect = baseRect;
        }
    }

    private void LateUpdate()
    {
        Apply(false);
    }

    private void CaptureBaseRect()
    {
        if (true == hasBaseRect || null == cam)
        {
            return;
        }
        baseRect = cam.rect;
        hasBaseRect = true;
    }

    private void Apply(bool _force)
    {
        if (null == cam)
        {
            return;
        }
        CaptureBaseRect();

        Rect target = Letterbox.Map(baseRect);
        if (false == _force && target == appliedRect && cam.rect == target)
        {
            return;
        }

        appliedRect = target;
        cam.rect = target;
    }
}
