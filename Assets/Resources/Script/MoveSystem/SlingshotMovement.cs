using UnityEngine;

public class SlingshotMovement
    : MonoBehaviour
    , Initializable
{
    [Header("movement value")]
    public float maxSpeed = 5.0f;
    public Transform target;
    public float slingSpeed = -5.0f;
    public float acceleration = 1.0f;
    public bool isStop = true;

    [SerializeField]
    private float currSpeed;

    // 자석(Pull) 으로 끌려올 때의 최대 속도 / 가속도 (0 이면 maxSpeed / acceleration 사용). Initialize 때 초기화
    private float pullMaxSpeed;
    private float pullAcceleration;

    public void Initialize(BlackBoard _data)
    {
        isStop = true;
        currSpeed = slingSpeed;
        pullMaxSpeed = 0f;
        pullAcceleration = 0f;
    }

    /// <summary>
    /// _target 으로 끌려가기 시작한다 (특수 아이템 "자석")
    /// 최대 속도 / 가속도는 원래 값과 _maxSpeed / _acceleration 중 큰 값을 쓰고, 목표를 지나치지 않게 한 프레임 이동량을 거리로 제한한다
    /// </summary>
    public void Pull(Transform _target, float _maxSpeed, float _acceleration)
    {
        if (null == _target)
        {
            return;
        }

        target = _target;
        isStop = false;
        pullMaxSpeed = Mathf.Max(pullMaxSpeed, _maxSpeed);
        pullAcceleration = Mathf.Max(pullAcceleration, _acceleration);
    }

    private void Update()
    {
        if (null == target
            || true == isStop)
        {
            return;
        }

        bool pulled = pullMaxSpeed > 0f || pullAcceleration > 0f;
        float limit = true == pulled ? Mathf.Max(maxSpeed, pullMaxSpeed) : maxSpeed;
        float accel = true == pulled ? Mathf.Max(acceleration, pullAcceleration) : acceleration;

        Vector2 dir = target.position - transform.position;
        float step = currSpeed * Time.deltaTime;

        // 자석으로 빠르게 끌려올 때는 목표를 지나쳐 진동하지 않도록 이동량을 남은 거리로 제한
        if (true == pulled && step > dir.magnitude)
        {
            step = dir.magnitude;
        }

        transform.Translate(dir.normalized * step);
        currSpeed += accel * Time.deltaTime;

        if (currSpeed > limit)
        {
            currSpeed = limit;
        }
    }
}
