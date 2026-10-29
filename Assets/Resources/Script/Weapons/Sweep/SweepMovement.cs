using UnityEngine;

/// <summary>
/// 휘두르기(Sweep) 칼날의 이동
/// 시작 위치(startPosition = 무기, 플레이어에 붙어 있음) 를 따라다니며, 조준 방향을 중심으로 arcAngle 만큼 칼날을 휘두른다
/// - 칼날은 중심에서 innerRadius 부터 reach(BlackBoard areaRadius) 까지 뻗은 막대이며, 오브젝트 스케일로 길이 / 두께를 맞춘다
///   (콜라이더 / 스프라이트는 1 x 1 기준, 오브젝트의 오른쪽(x+) 이 칼날 방향)
/// - 휘두르는 시간은 BlackBoard lifetime (0 이하면 swingDuration). Lifetime 컴포넌트가 같은 시간에 비활성화한다
/// - 함께 발사된 칼날이 여러 개면 조준 방향을 기준으로 360 / formationCount 간격으로 나눠 휘두르고, 홀수 번째는 반대 방향으로 휘두른다
/// 타격은 하지 않는다 (SweepProjectile 이 담당)
/// </summary>
public class SweepMovement
    : MonoBehaviour
    , Initializable
{
    [Header("swing value")]
    [Tooltip("휘두르는 각도 (도). 조준 방향을 중심으로 -arcAngle / 2 에서 +arcAngle / 2 까지")]
    [Range(0f, 360f)]
    public float arcAngle = 120f;
    [Tooltip("휘두르는 시간 (초). BlackBoard lifetime 이 0 보다 크면 그 값을 쓴다")]
    [Min(0.01f)]
    public float swingDuration = 0.25f;
    [Tooltip("켜면 반시계 방향(각도 증가) 으로, 끄면 시계 방향으로 휘두른다")]
    public bool counterClockwise = true;
    [Tooltip("켜면 처음에 빠르고 끝에서 느려진다 (ease out)")]
    public bool easeOut = true;

    [Header("blade value")]
    [Tooltip("중심에서 칼날이 시작되는 거리")]
    [Min(0f)]
    public float innerRadius = 0.3f;
    [Tooltip("칼날 끝이 닿는 거리. BlackBoard areaRadius 가 0 보다 크면 그 값을 쓴다")]
    [Min(0.1f)]
    public float reach = 3f;
    [Tooltip("칼날 두께")]
    [Min(0.01f)]
    public float bladeWidth = 0.5f;

    private Transform center;
    // 중심 transform 이 없을 때(LaunchAt) 쓰는 고정 중심
    private Vector3 fixedOrigin;
    private float startAngle;
    private float sweepAngle;
    private float duration;
    private float elapsed;
    private float bladeLength;

    public void Initialize(BlackBoard _data)
    {
        center = _data.GetTransform(DATA_TYPE.startPosition);

        float time = _data.GetFloat(DATA_TYPE.lifetime);
        duration = time > 0f ? time : swingDuration;
        elapsed = 0f;

        float currReach = _data.GetFloat(DATA_TYPE.areaRadius);
        if (currReach <= 0f)
        {
            currReach = reach;
        }
        bladeLength = Mathf.Max(0.1f, currReach - innerRadius);

        int formationCount = Mathf.Max(1, Mathf.RoundToInt(_data.GetFloat(DATA_TYPE.formationCount)));
        int formationIndex = Mathf.Clamp(Mathf.RoundToInt(_data.GetFloat(DATA_TYPE.formationIndex)), 0, formationCount - 1);

        Vector3 origin;
        if (false == _data.TryGetStartPoint(out origin))
        {
            origin = transform.position;
        }
        fixedOrigin = origin;

        // 조준 방향 (무기가 랜덤 보정한 지점이 있으면 그 지점, 없으면 이동 대상)
        Vector3 point;
        if (false == _data.TryGetVector(DATA_TYPE.moveTargetPoint, out point))
        {
            Transform target = _data.GetTransform(DATA_TYPE.moveTarget);
            point = null != target ? target.position : origin + Vector3.right;
        }

        Vector2 dir = point - origin;
        float aimAngle = dir.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        aimAngle += 360f * formationIndex / formationCount;

        // 홀수 번째 칼날은 반대 방향으로 휘둘러 여러 개일 때 엇갈리게 한다
        bool ccw = counterClockwise;
        if (1 == formationIndex % 2)
        {
            ccw = false == ccw;
        }

        sweepAngle = ccw ? arcAngle : -arcAngle;
        startAngle = aimAngle - sweepAngle * 0.5f;

        transform.localScale = new Vector3(bladeLength, bladeWidth, 1f);
        Apply(origin, 0f);
    }

    private void LateUpdate()
    {
        // 중심(플레이어) 이 사라지면 그 자리에서 사라진다. LaunchAt 으로 발사되면 시작 지점에 고정된다
        Vector3 origin;
        if (null != center)
        {
            if (false == center.gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }
            origin = center.position;
        }
        else
        {
            origin = fixedOrigin;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        if (true == easeOut)
        {
            t = 1f - (1f - t) * (1f - t);
        }

        Apply(origin, t);
    }

    /// <summary>
    /// 휘두르기 진행도 _t (0 ~ 1) 에 맞춰 칼날을 _origin 기준으로 배치한다
    /// </summary>
    private void Apply(Vector3 _origin, float _t)
    {
        float angle = startAngle + sweepAngle * _t;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);

        Vector3 position = _origin + dir * (innerRadius + bladeLength * 0.5f);
        position.z = _origin.z;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
    }

    private void OnDisable()
    {
        center = null;
        elapsed = 0f;
    }
}
