using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적을 추적하다가 일정 거리 안으로 다가가면 주변의 다음 적으로 전환해 이동한다
/// 이미 방문한 적으로는 돌아가지 않으며, 전이 횟수를 다 쓰거나 다음 적이 없으면 비활성화된다
/// 타격은 하지 않는다 (OneHitProjectile 등 타격 컴포넌트가 담당)
/// </summary>
public class ChainMovement
    : MonoBehaviour
    , Initializable
{
    public Transform target;

    [Header("chain value")]
    [Tooltip("기본 이동 속도 (moveSpeedRate 가 곱해진다)")]
    public float speed;
    [Tooltip("타겟과의 거리가 이 값 이하가 되면 다음 타겟으로 전환")]
    public float switchDistance = 0.2f;

    private float baseSpeed;
    private LayerMask targetLayer;
    /// <summary>
    /// 전이 횟수 (총 방문 수 = 1 + chainCount). BlackBoard 로 받는다
    /// </summary>
    private int chainCount;
    /// <summary>
    /// 이전 타겟 기준 다음 타겟 탐색 범위. BlackBoard 로 받는다
    /// </summary>
    private float chainRange;
    private int remainChain;
    private HashSet<Transform> visited = new HashSet<Transform>();

    private bool arrived;
    private float arriveFixedTime;

    private void Awake()
    {
        baseSpeed = speed;
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

        targetLayer = _data.GetInt(DATA_TYPE.targetLayer);
        chainCount = Mathf.Max(0, _data.GetInt(DATA_TYPE.chainCount));
        chainRange = _data.GetFloat(DATA_TYPE.chainRange);
        remainChain = chainCount;
        visited.Clear();
        arrived = false;
    }

    private void Update()
    {
        if (false == TargetFinder.IsAlive(target))
        {
            // 도착한 타겟이 타격으로 죽은 경우는 정상 도착으로 처리한다
            if (true == arrived)
            {
                SwitchTarget();
                return;
            }

            // 이동 중 타겟이 사라지면 현재 위치에서 다시 찾는다 (전이 횟수는 소모하지 않음)
            target = TargetFinder.FindNearest(transform.position, chainRange, targetLayer, visited);
            if (null == target)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        transform.position = Vector3.MoveTowards(
            transform.position
            , target.position
            , speed * Time.deltaTime
        );

        if (false == arrived)
        {
            if ((target.position - transform.position).sqrMagnitude > switchDistance * switchDistance)
            {
                return;
            }

            arrived = true;
            arriveFixedTime = Time.fixedTime;
        }

        // 도착 후 물리 스텝이 한 번 이상 돌아 트리거 판정이 끝난 뒤 전환한다
        if (Time.fixedTime <= arriveFixedTime)
        {
            return;
        }

        SwitchTarget();
    }

    /// <summary>
    /// 현재 타겟을 방문 처리하고 그 주변의 다음 타겟으로 전환한다
    /// 전이 횟수를 다 썼거나 다음 타겟이 없으면 비활성화된다
    /// </summary>
    private void SwitchTarget()
    {
        arrived = false;
        Vector3 from = null != target ? target.position : transform.position;
        visited.Add(target);
        if (remainChain <= 0)
        {
            gameObject.SetActive(false);
            return;
        }

        --remainChain;
        target = TargetFinder.FindNearest(from, chainRange, targetLayer, visited);
        if (null == target)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        target = null;
        visited.Clear();
        arrived = false;
    }
}
