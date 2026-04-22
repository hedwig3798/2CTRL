using UnityEngine;

public class Follow 
    : MonoBehaviour
    , IMovementSystem
{
    public Transform target;

    public void Init(MovementInitData _initData)
    {
        target = _initData.target;
    }

    void Update()
    {
        Vector3 vec = target.position;
        vec.z = -10.0f;
        transform.position = vec;
    }
}
