using UnityEngine;

/// <summary>
/// 플레이어를 진영 카메라 뷰 안(여백 margin)에 가둔다 (최종 보스전)
/// - 이동(ArrowMovemnt Update)·강제 이동(PlayerForcedMove) 뒤에 돌도록 실행 순서를 늦춘다
/// </summary>
[DefaultExecutionOrder(1000)]
public class PlayerScreenClamp
    : MonoBehaviour
{
    [SerializeField]
    private Camera viewCamera;

    [SerializeField]
    private float margin = 0.5f;

    /// <summary>
    /// _target 에 붙이고(이미 있으면 재사용) 켠다
    /// </summary>
    public static PlayerScreenClamp Attach(Transform _target, Camera _camera, float _margin)
    {
        if (null == _target)
        {
            return null;
        }

        if (false == _target.TryGetComponent(out PlayerScreenClamp clamp))
        {
            clamp = _target.gameObject.AddComponent<PlayerScreenClamp>();
        }
        clamp.Setup(_camera, _margin);
        clamp.enabled = true;
        return clamp;
    }

    public void Setup(Camera _camera, float _margin)
    {
        viewCamera = _camera;
        margin = _margin;
    }

    /// <summary>
    /// 점을 사각형 안으로 자른다
    /// </summary>
    public static Vector2 Clamp(Vector2 _point, Rect _rect)
    {
        return new Vector2(
            Mathf.Clamp(_point.x, _rect.xMin, _rect.xMax),
            Mathf.Clamp(_point.y, _rect.yMin, _rect.yMax));
    }

    private void LateUpdate()
    {
        if (null == viewCamera)
        {
            return;
        }

        Rect view = BossMath.ViewRect(viewCamera.transform.position, viewCamera.orthographicSize, viewCamera.aspect, margin);
        Vector3 pos = transform.position;
        Vector2 clamped = Clamp(pos, view);
        if (clamped.x != pos.x || clamped.y != pos.y)
        {
            transform.position = new Vector3(clamped.x, clamped.y, pos.z);
        }
    }
}
