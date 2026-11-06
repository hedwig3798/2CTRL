using UnityEngine;

/// <summary>
/// 대상의 위치를 그대로 따라다닌다
/// overrideZ 가 켜져 있으면 z 를 고정한다 (카메라용)
/// offset 만큼 대상 위치에서 떨어져 따라간다 (분할 화면 카메라가 플레이어를 화면 가운데 쪽에 보이게 할 때 사용)
/// </summary>
public class Follow
    : MonoBehaviour
    , Initializable
{
    public Transform target;

    public Vector2 offset;

    [Header("z value")]
    public bool overrideZ = true;
    public float z = -10.0f;

    public void Initialize(BlackBoard _data)
    {
        target = _data.GetTransform(DATA_TYPE.moveTarget);
        UpdatePosition();
    }

    void LateUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (null == target)
        {
            return;
        }

        Vector3 vec = target.position;
        vec.x += offset.x;
        vec.y += offset.y;
        if (true == overrideZ)
        {
            vec.z = z;
        }
        transform.position = vec;
    }
}
