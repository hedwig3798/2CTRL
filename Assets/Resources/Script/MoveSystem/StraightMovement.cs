using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Straight
    : MonoBehaviour
    , Initializable
{
    [Header("movement value")]
    private Vector3 direction;
    public Transform target;
    public float speed;

    [Header("sprite value")]
    [SerializeField]
    private SPRITE_ROTATE_MODE rotateMode;
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private bool isDefaultLeft = true;
    [SerializeField]
    [Range(0f, 360f)]
    private float rotateOffset;

    private void FlipSprite()
    {
        if (null == spriteRenderer)
        {
            return;
        }

        bool flip = direction.x < 0;
        spriteRenderer.flipX = isDefaultLeft ^ flip;
    }

    public void Initialize(BlackBoard _data)
    {
        Transform pos = _data.GetTransform(DATA_TYPE.startPosition);

        if (null != pos)
        {
            transform.position = pos.position;
        }

        target = _data.GetTransform(DATA_TYPE.moveTarget);

        direction = target.position - transform.position;
        direction = direction.normalized;

        speed *= _data.GetFloat(DATA_TYPE.moveSpeedRate);

        if (SPRITE_ROTATE_MODE.ROTATE == rotateMode)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void Awake()
    {
        if (direction == Vector3.zero)
        {
            direction = Random.insideUnitCircle.normalized;
        }
    }

    private void Update()
    {
        if (SPRITE_ROTATE_MODE.ROTATE == rotateMode)
        {
            transform.Translate(speed * Time.deltaTime * Vector3.right);
        }
        else
        {
            transform.Translate(speed * Time.deltaTime * direction);
            FlipSprite();
        }
    }
}