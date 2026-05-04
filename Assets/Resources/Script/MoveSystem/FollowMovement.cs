using UnityEngine;

public class Follow 
    : MonoBehaviour
{
    public Transform target;

    void Update()
    {
        Vector3 vec = target.position;
        vec.z = -10.0f;
        transform.position = vec;
    }
}
