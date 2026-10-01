using UnityEngine;

/// <summary>
/// 대상의 위치를 그대로 따라다닌다
/// overrideZ 가 켜져 있으면 z 를 고정한다 (카메라용)
/// </summary>
public class Follow
    : MonoBehaviour
    , Initializable
{
    public Transform target;

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
        if (true == overrideZ)
        {
            vec.z = z;
        }
        transform.position = vec;
    }
}
