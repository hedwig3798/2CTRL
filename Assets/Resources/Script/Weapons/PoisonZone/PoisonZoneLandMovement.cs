using UnityEngine;

/// <summary>
/// 독 장판 착지 이동
/// 시작 지점(startPoint / startPosition)에서 목표 지점(moveTargetPoint, 없으면 moveTarget 의 위치)까지
/// flightTime 동안 포물선을 그리며 날아간 뒤 그 자리에 멈춘다
/// 멈춘 뒤에는 목표가 움직여도 따라가지 않는다. 착지했는지는 IsLanded 로 알 수 있다
/// 타격은 하지 않는다 (PoisonZoneProjectile 이 착지 후에 담당)
/// </summary>
public class PoisonZoneLandMovement
    : MonoBehaviour
    , Initializable
{
    [Header("movement value")]
    [Tooltip("시작 지점에서 목표 지점까지 날아가는 시간 (초). moveSpeedRate 로 나눈다. 0 이면 바로 착지")]
    [Min(0f)]
    public float flightTime = 0.3f;
    [Tooltip("날아가는 동안 위로 솟는 높이 (던지는 연출). 0 이면 직선")]
    public float arcHeight = 0.8f;

    private float baseFlightTime;
    private float currFlightTime;
    private float elapsed;

    private Vector3 startPoint;
    private Vector3 endPoint;
    private bool landed = true;

    /// <summary>
    /// 목표 지점에 도착해 멈췄는지
    /// </summary>
    public bool IsLanded => landed;

    private void Awake()
    {
        baseFlightTime = flightTime;
    }

    public void Initialize(BlackBoard _data)
    {
        if (false == _data.TryGetStartPoint(out startPoint))
        {
            startPoint = transform.position;
        }

        // 무기가 랜덤 보정한 지점이 있으면 타겟 대신 그 지점에 내려앉는다
        if (false == _data.TryGetVector(DATA_TYPE.moveTargetPoint, out endPoint))
        {
            Transform target = _data.GetTransform(DATA_TYPE.moveTarget);
            endPoint = null != target ? target.position : startPoint;
        }

        startPoint.z = transform.position.z;
        endPoint.z = transform.position.z;

        float rate = _data.GetFloat(DATA_TYPE.moveSpeedRate);
        if (rate <= 0f)
        {
            rate = 1f;
        }
        currFlightTime = baseFlightTime / rate;

        elapsed = 0f;
        landed = currFlightTime <= 0f || (endPoint - startPoint).sqrMagnitude < 0.0001f;
        transform.position = true == landed ? endPoint : startPoint;
    }

    private void Update()
    {
        if (true == landed)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / currFlightTime);
        if (t >= 1f)
        {
            transform.position = endPoint;
            landed = true;
            return;
        }

        // 빠르게 출발해 감속하며 내려앉는다 (ease out) + 포물선 높이
        float eased = 1f - (1f - t) * (1f - t);
        Vector3 position = Vector3.Lerp(startPoint, endPoint, eased);
        position.y += arcHeight * 4f * t * (1f - t);
        transform.position = position;
    }

    private void OnDisable()
    {
        landed = true;
        elapsed = 0f;
    }
}
