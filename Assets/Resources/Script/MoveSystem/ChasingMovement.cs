using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;


public sealed class ChasingMovement
    : MonoBehaviour
    , IMovementSystem
{
    public Transform target;
    public float speed;
    Vector3 dir;

    public void Init(MovementInitData _initData)
    {
        target = _initData.target;
        speed = _initData.speed;
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
