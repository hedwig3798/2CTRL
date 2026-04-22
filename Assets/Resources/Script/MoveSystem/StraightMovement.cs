using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Straight
    : MonoBehaviour
    , Initializable
{
    public Vector3 dir = Vector3.zero;
    public Transform target;
    public float speed;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    public void Initialize(BlackBoard _data)
    {
        target = _data.GetTransform(DATA_TYPE.moveTarget);

        dir = target.position - transform.position;
        dir.z = 0;
        dir = dir.normalized;

        speed *= _data.GetFloat(DATA_TYPE.moveSpeedRate);
    }

    private void Awake()
    {
        if (dir == Vector3.zero)
        {
            dir = Random.insideUnitCircle.normalized;
        }
    }

    private void Update()
    {
        transform.Translate(speed * Time.deltaTime * dir);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !(dir.x < 0);
        }
    }
}