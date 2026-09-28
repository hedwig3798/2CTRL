using UnityEngine;

public class ArrowMovemnt
    : MonoBehaviour
{
    [Header("movement value")]
    public float speed = 5.0f;

    [Header("key code bind")]
    public KeyCode right;
    public KeyCode left;
    public KeyCode up;
    public KeyCode down;

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

    private bool isLeft = true;

    private void FlipSprite()
    {
        if (null == spriteRenderer)
        {
            return;
        }

        spriteRenderer.flipX = isDefaultLeft ^ isLeft;
    }

    void Update()
    {
        if (Input.GetKey(right))
        {
            transform.Translate(Vector2.right * speed * Time.deltaTime);
            isLeft = false;
        }

        if (Input.GetKey(left))
        {
            transform.Translate(Vector2.left * speed * Time.deltaTime);
            isLeft = true;
        }

        if (Input.GetKey(up))
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        if (Input.GetKey(down))
        {
            transform.Translate(Vector2.down * speed * Time.deltaTime);
        }

        if (SPRITE_ROTATE_MODE.FLIP == rotateMode)
        {
            FlipSprite();
        }
        else if (SPRITE_ROTATE_MODE.ROTATE == rotateMode)
        {
            Vector2 move = Vector2.zero;
            if (Input.GetKey(right)) move.x += 1f;
            if (Input.GetKey(left)) move.x -= 1f;
            if (Input.GetKey(up)) move.y += 1f;
            if (Input.GetKey(down)) move.y -= 1f;
            if (move.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg + rotateOffset;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
        else
        {
            FlipSprite();
        }
    }
}
