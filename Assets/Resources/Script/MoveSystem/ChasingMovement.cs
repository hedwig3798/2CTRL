using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;


public sealed class ChasingMovement
    : MonoBehaviour
{
    public Transform target;
    public float speed;

    private void Update()
    {
        if (target != null)
        {
            Vector3 dir = MathUtils.GetDirection(transform, target);
            dir.z = 0;

            transform.Translate(dir * speed);
        }
        else
        {
            Debug.Log("chasingmovement has no target");
        }
    }
}
