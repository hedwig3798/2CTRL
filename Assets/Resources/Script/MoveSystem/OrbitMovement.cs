using UnityEngine;

/// <summary>
/// 대상을 중심으로 같은 반지름의 원을 돈다
/// 함께 발사된 발사체 수와 자신의 순서로 각도를 나눠 원 위에 균등하게 배치된다
/// </summary>
public class OrbitMovement
    : MonoBehaviour
    , Initializable
{
    public Transform target;

    [Header("orbit value")]
    [Tooltip("중심으로부터의 거리")]
    public float radius = 1.5f;
    [Tooltip("회전 속도 (도/초, 음수면 시계 방향). moveSpeedRate 가 곱해진다")]
    public float angularSpeed = 180f;

    private float speedRate = 1f;
    private int formationIndex;
    private int formationCount = 1;

    public void Initialize(BlackBoard _data)
    {
        target = _data.GetTransform(DATA_TYPE.moveTarget);

        speedRate = _data.GetFloat(DATA_TYPE.moveSpeedRate);
        if (speedRate <= 0f)
        {
            speedRate = 1f;
        }

        formationCount = Mathf.Max(1, Mathf.RoundToInt(_data.GetFloat(DATA_TYPE.formationCount)));
        formationIndex = Mathf.Clamp(Mathf.RoundToInt(_data.GetFloat(DATA_TYPE.formationIndex)), 0, formationCount - 1);

        if (null != target)
        {
            transform.position = GetOrbitPosition();
        }
    }

    private void LateUpdate()
    {
        if (null == target)
        {
            return;
        }

        transform.position = GetOrbitPosition();
    }

    private Vector3 GetOrbitPosition()
    {
        // 모든 발사체가 같은 시간 기준 위상을 공유하므로 수가 바뀌어도 간격이 균등하게 유지된다
        float angle = Time.time * angularSpeed * speedRate
            + 360f * formationIndex / formationCount;
        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        return target.position + offset;
    }

    private void OnDisable()
    {
        target = null;
    }
}
