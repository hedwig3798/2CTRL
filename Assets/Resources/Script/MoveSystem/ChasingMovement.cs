using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;


public sealed class ChasingMovement
    : MonoBehaviour
    , Initializable
{
    public Transform target;
    public float speed;
    Vector3 dir;

    public void Initialize(BlackBoard _data)
    {
        target = _data.GetTransform(DATA_TYPE.moveTarget);
        speed = _data.GetFloat(DATA_TYPE.moveSpeedRate);
    }

    private void Update()
    {
        if (target != null)
        {
            dir = MathUtils.GetDirection(transform, target);
            dir.z = 0;

        }
        transform.Translate(dir * speed);
    }
}
