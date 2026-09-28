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

    private float baseSpeed;

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

        float rate = _data.GetFloat(DATA_TYPE.moveSpeedRate);
        if (rate <= 0f)
        {
            rate = 1f;
        }
        speed = baseSpeed * rate;

        if (null == target)
        {
            if (direction == Vector3.zero)
            {
                direction = Vector3.right;
            }
            return;
        }

        direction = target.position - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.right;
        }
        else
        {
            direction = direction.normalized;
        }

        if (SPRITE_ROTATE_MODE.ROTATE == rotateMode)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotateOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void Awake()
    {
        baseSpeed = speed;
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
