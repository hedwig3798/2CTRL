using UnityEngine;
using UnityEngine.InputSystem;

public class ArrowMovemnt
    : MonoBehaviour
{
    [Header("movement value")]
    public float speed = 5.0f;

    [Header("key code bind")]
    public Key right;
    public Key left;
    public Key up;
    public Key down;

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

    private Vector2 lastMoveDirection = Vector2.right;

    /// <summary>
    /// 마지막으로 이동한 방향 (정규화). 멈춰 있으면 마지막 값을 유지한다. 처음에는 오른쪽
    /// </summary>
    public Vector2 LastMoveDirection => lastMoveDirection;

    // 있으면 speed 를 플레이어 MoveSpeed 스텟으로 맞춘다
    private PlayerStats stats;

    private void Awake()
    {
        stats = GetComponentInParent<PlayerStats>();
    }

    private void OnEnable()
    {
        if (null != stats)
        {
            stats.OnStatChanged -= HandleStatChanged;
            stats.OnStatChanged += HandleStatChanged;
            HandleStatChanged(stats);
        }
    }

    private void OnDisable()
    {
        if (null != stats)
        {
            stats.OnStatChanged -= HandleStatChanged;
        }
    }

    private void HandleStatChanged(PlayerStats _stats)
    {
        speed = _stats.Get(ValueType.MoveSpeed);
    }

    private void FlipSprite()
    {
        if (null == spriteRenderer)
        {
            return;
        }

        spriteRenderer.flipX = isDefaultLeft ^ isLeft;
    }

    private static bool IsPressed(Keyboard _keyboard, Key _key)
    {
        return Key.None != _key && _keyboard[_key].isPressed;
    }

    void Update()
    {
        // 일시정지(timeScale 0) 중에는 입력을 무시한다 (Update 는 계속 돌아서 방향·스프라이트가 바뀌면 Flame 등이 따라 돈다)
        if (true == GamePause.IsPaused || Time.timeScale <= 0f)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (null == keyboard)
        {
            return;
        }

        bool isRightPressed = IsPressed(keyboard, right);
        bool isLeftPressed = IsPressed(keyboard, left);
        bool isUpPressed = IsPressed(keyboard, up);
        bool isDownPressed = IsPressed(keyboard, down);

        if (isRightPressed)
        {
            transform.Translate(Vector2.right * speed * Time.deltaTime);
            isLeft = false;
        }

        if (isLeftPressed)
        {
            transform.Translate(Vector2.left * speed * Time.deltaTime);
            isLeft = true;
        }

        if (isUpPressed)
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        if (isDownPressed)
        {
            transform.Translate(Vector2.down * speed * Time.deltaTime);
        }

        Vector2 move = Vector2.zero;
        if (isRightPressed) move.x += 1f;
        if (isLeftPressed) move.x -= 1f;
        if (isUpPressed) move.y += 1f;
        if (isDownPressed) move.y -= 1f;
        if (move.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = move.normalized;
        }

        if (SPRITE_ROTATE_MODE.FLIP == rotateMode)
        {
            FlipSprite();
        }
        else if (SPRITE_ROTATE_MODE.ROTATE == rotateMode)
        {
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
