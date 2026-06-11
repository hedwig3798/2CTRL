using Unity.VisualScripting;
using UnityEngine;

public class SlingshotMovement 
    : MonoBehaviour
    , Initializable
{
    [Header("movement value")]
    public float maxSpeed = 5.0f;
    public Transform target;
    public float slingSpeed = -5.0f;
    public float acceleration = 1.0f;
    public bool isStop = true;

    [SerializeField]
    private float currSpeed;

    public void Initialize(BlackBoard _data)
    {
        isStop = true;
        currSpeed = slingSpeed;
    }

    private void Update()
    {
        if (null == target
            || true == isStop)
        {
            return;
        }

        Vector2 dir = target.position - transform.position;

        transform.Translate(dir.normalized * currSpeed * Time.deltaTime);
        currSpeed += acceleration * Time.deltaTime;

        if (currSpeed > maxSpeed)
        {
            currSpeed = maxSpeed;
        }
    }
}
