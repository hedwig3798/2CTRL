using UnityEngine;

public class Follow : MonoBehaviour
{
    [SerializeField] private Transform target;

    // Update is called once per frame
    void Update()
    {
        Vector3 vec = target.position;
        vec.z = -10.0f;
        transform.position = vec;
    }
}
