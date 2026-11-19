using UnityEngine;

/// <summary>
/// 정령 걷기 애니메이션 (판정과 무관. SpiritProjectile 과 같은 오브젝트에 붙인다)
/// 실제 이동 방향(프레임 사이 위치 변화)으로 4 방향 중 하나를 고르고 그 방향의 프레임을 반복한다
/// - frames 순서 = 방향 * framesPerDirection + 프레임, 방향은 아래 / 위 / 왼쪽 / 오른쪽
/// - 멈춰 있으면 마지막 방향을 유지한 채 계속 반복한다 (둥실 떠 있는 느낌)
/// - 방향 그림이 따로 있으므로 SpiritProjectile.flipByDirection 은 끈다
/// </summary>
[DefaultExecutionOrder(100)]
public class SpiritVisual
    : MonoBehaviour
{
    private const int DOWN = 0;
    private const int UP = 1;
    private const int LEFT = 2;
    private const int RIGHT = 3;

    [Tooltip("비워두면 같은 오브젝트의 SpriteRenderer")]
    public SpriteRenderer target;
    public Sprite[] frames;
    [Min(1)]
    public int framesPerDirection = 4;
    [Min(0.01f)]
    public float fps = 8f;
    [Tooltip("이 거리(초당) 이상 움직여야 방향을 바꾼다 (제자리 떨림 방지)")]
    [Min(0f)]
    public float minTurnSpeed = 0.3f;

    private Vector3 lastPosition;
    private int direction = DOWN;

    private void Awake()
    {
        if (null == target)
        {
            target = GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        direction = DOWN;
    }

    private void LateUpdate()
    {
        if (null == target || null == frames || frames.Length < framesPerDirection * 4)
        {
            return;
        }

        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        float dt = Time.deltaTime;
        if (dt > 0f && delta.magnitude / dt >= minTurnSpeed)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                direction = delta.x < 0f ? LEFT : RIGHT;
            }
            else
            {
                direction = delta.y > 0f ? UP : DOWN;
            }
        }

        int frame = Mathf.FloorToInt(Time.time * fps) % framesPerDirection;
        target.sprite = frames[direction * framesPerDirection + frame];
        target.flipX = false;
    }
}
