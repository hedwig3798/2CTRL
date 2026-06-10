using Unity.VisualScripting;
using UnityEngine;

public class SlingshotMovement 
    : MonoBehaviour
{
    [Header("movement value")]
    public float maxSpeed = 5.0f;
    public Transform target;
    public float slingSpeed = -5.0f;
    public float acceleration = 1.0f;

    private void Update()
    {
        if (null == target)
        {
            return;
        }

        Vector2 dir = target.position - transform.position;

        transform.Translate(dir.normalized * slingSpeed * Time.deltaTime);
        slingSpeed += acceleration * Time.deltaTime;

        if (slingSpeed > maxSpeed)
        {
            slingSpeed = maxSpeed;
        }
    }
}
