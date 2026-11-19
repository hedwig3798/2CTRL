using UnityEngine;

/// <summary>
/// 휘두르기(Sweep) 베기 궤적 연출 (판정과 무관. SweepMovement 와 같은 오브젝트에 붙인다)
/// 칼날 오브젝트는 길이 / 두께로 늘어나고 칼날 방향으로 돌기 때문에, 궤적 렌더러는 부모에서 떼어 월드에서 따로 배치한다
/// - 위치 = 휘두르는 중심, 회전 = 조준 방향, 크기 = 칼날 끝까지의 거리 / 그림의 궤적 반지름
/// - 그림은 아래쪽(-y)이 조준 방향이고 왼쪽 -> 오른쪽(반시계)으로 그어진다. 시계 방향으로 휘두르면 좌우를 뒤집는다
/// - 프레임은 휘두르기 진행도에 맞춰 한 번 재생한다
/// SweepMovement.LateUpdate 뒤에 돌도록 실행 순서를 늦춘다
/// </summary>
[DefaultExecutionOrder(100)]
public class SweepTrailVisual
    : MonoBehaviour
{
    [Header("refs")]
    [Tooltip("비워두면 같은 오브젝트의 SweepMovement")]
    public SweepMovement movement;
    [Tooltip("궤적 렌더러 (자식으로 두면 시작할 때 부모에서 뗀다)")]
    public SpriteRenderer trail;

    [Header("frames")]
    [Tooltip("휘두르기 진행도(0 -> 1)에 맞춰 재생할 프레임")]
    public Sprite[] frames;
    [Tooltip("크기 1 일 때 그림 속 궤적 바깥쪽 반지름 (유닛)")]
    [Min(0.01f)]
    public float unitRadius = 1.0625f;

    private Transform trailTransform;

    private void Awake()
    {
        if (null == movement)
        {
            movement = GetComponent<SweepMovement>();
        }
        if (null != trail)
        {
            trailTransform = trail.transform;
            trailTransform.SetParent(null, false);
            trail.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (null != trail)
        {
            // 배치는 발사 초기화(BlackBoard) 뒤 LateUpdate 에서 한다 (그리기 전에 같은 프레임 안에서 맞춰진다)
            trail.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (null != trail)
        {
            trail.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (null != trail)
        {
            Destroy(trail.gameObject);
        }
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (null == trail || null == movement)
        {
            return;
        }

        // 분할 카메라는 레이어로 그리므로 칼날(발사한 쪽)의 레이어를 따른다
        if (trail.gameObject.layer != gameObject.layer)
        {
            trail.gameObject.layer = gameObject.layer;
        }

        float s = movement.Reach / unitRadius;
        trailTransform.SetPositionAndRotation(movement.Origin, Quaternion.Euler(0f, 0f, movement.MidAngle + 90f));
        trailTransform.localScale = new Vector3(s, s, 1f);
        trail.flipX = movement.SweepAngle < 0f;

        if (null != frames && frames.Length > 0)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(movement.Progress * frames.Length), 0, frames.Length - 1);
            trail.sprite = frames[index];
        }
    }
}
