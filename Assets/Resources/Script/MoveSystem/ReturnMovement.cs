using UnityEngine;

/// <summary>
/// 부메랑처럼 목표 지점을 향해 감속하며 날아가다가, 멈춘 지점에서 출발한 곳(startPosition)으로 가속하며 돌아온다
/// 돌아오는 동안은 출발한 곳을 계속 따라가며, 붙잡히면(catchDistance) 비활성화된다
/// 되돌아서는 순간 OneHitProjectile 의 타격 기록을 지워 돌아오는 길에 같은 적을 다시 타격할 수 있다
/// 타격은 하지 않는다 (OneHitProjectile 등 타격 컴포넌트가 담당)
/// </summary>
public class ReturnMovement
    : MonoBehaviour
    , Initializable
{
    [Header("movement value")]
    [Tooltip("출발 속도이자 돌아올 때의 기본 최고 속도 (moveSpeedRate 가 곱해진다)")]
    public float speed = 8f;
    [Tooltip("목표 지점을 지나 더 날아가는 거리")]
    [Min(0f)]
    public float overshoot = 1f;
    [Tooltip("목표가 가까워도 최소 이 거리만큼은 날아간다")]
    [Min(0.1f)]
    public float minDistance = 2f;
    [Tooltip("돌아올 때의 최고 속도 배율. 주인이 움직여도 따라잡을 수 있도록 1 보다 크게 둔다")]
    [Min(0.1f)]
    public float returnSpeedRate = 1.5f;
    [Tooltip("돌아오는 중 출발한 곳과의 거리가 이 값 이하가 되면 비활성화")]
    [Min(0f)]
    public float catchDistance = 0.3f;

    [Header("hit value")]
    [Tooltip("되돌아설 때 타격 기록을 지워 돌아오는 길에 같은 적을 다시 타격한다")]
    public bool resetHitsOnReturn = true;

    [Header("sprite value")]
    [Tooltip("회전시킬 대상 (비우면 회전하지 않는다). 콜라이더가 같이 돌지 않도록 visual 자식을 넣는다")]
    [SerializeField]
    private Transform spinTransform;
    [Tooltip("초당 회전 각도")]
    [SerializeField]
    private float spinSpeed = 720f;

    private float baseSpeed;
    private OneHitProjectile oneHitProjectile;

    private Transform returnTarget;
    private Vector3 direction;
    private float deceleration;
    private float currSpeed;
    private bool returning;

    private void Awake()
    {
        baseSpeed = speed;
        oneHitProjectile = GetComponent<OneHitProjectile>();
    }

    public void Initialize(BlackBoard _data)
    {
        returnTarget = _data.GetTransform(DATA_TYPE.startPosition);
        if (null != returnTarget)
        {
            transform.position = returnTarget.position;
        }

        float rate = _data.GetFloat(DATA_TYPE.moveSpeedRate);
        if (rate <= 0f)
        {
            rate = 1f;
        }
        speed = baseSpeed * rate;

        // 무기가 랜덤 보정한 지점이 있으면 타겟 대신 그 지점을 향한다
        Vector3 point;
        if (false == _data.TryGetVector(DATA_TYPE.moveTargetPoint, out point))
        {
            Transform target = _data.GetTransform(DATA_TYPE.moveTarget);
            point = null != target ? target.position : transform.position + Vector3.right;
        }

        direction = point - transform.position;
        direction.z = 0f;
        float distance = direction.magnitude;
        direction = distance < 0.0001f ? Vector3.right : direction / distance;

        // 목표 지점 + overshoot 에서 정확히 멈추도록 감속도를 정한다 (v^2 = 2ad)
        float travel = Mathf.Max(minDistance, distance + overshoot);
        deceleration = speed * speed / (2f * travel);

        currSpeed = speed;
        returning = false;
    }

    private void Update()
    {
        Spin();

        if (false == returning)
        {
            UpdateOut();
        }
        else
        {
            UpdateReturn();
        }
    }

    private void UpdateOut()
    {
        transform.position += currSpeed * Time.deltaTime * direction;
        currSpeed -= deceleration * Time.deltaTime;
        if (currSpeed > 0f)
        {
            return;
        }

        currSpeed = 0f;
        returning = true;
        if (true == resetHitsOnReturn && null != oneHitProjectile)
        {
            oneHitProjectile.ResetHitTargets();
        }
    }

    private void UpdateReturn()
    {
        // 돌아갈 곳이 사라지면 그 자리에서 사라진다
        if (null == returnTarget || false == returnTarget.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        currSpeed = Mathf.Min(currSpeed + deceleration * Time.deltaTime, speed * returnSpeedRate);

        Vector3 goal = returnTarget.position;
        goal.z = transform.position.z;
        transform.position = Vector3.MoveTowards(transform.position, goal, currSpeed * Time.deltaTime);

        if ((goal - transform.position).sqrMagnitude <= catchDistance * catchDistance)
        {
            gameObject.SetActive(false);
        }
    }

    private void Spin()
    {
        if (null == spinTransform || 0f == spinSpeed)
        {
            return;
        }

        spinTransform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        returnTarget = null;
        returning = false;
        currSpeed = 0f;
    }
}
