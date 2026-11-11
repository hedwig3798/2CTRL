using UnityEngine;

/// <summary>
/// 플레이어 강제 이동 (보스 밀기·당기기·착지 밀어냄)
/// - ArrowMovemnt 와 같은 오브젝트에서 transform 을 직접 옮긴다. 입력 이동과 같은 프레임에 더해져 충돌 없이 합산된다
/// - 여러 번 밀면 각각 따로 진행되어 합쳐진다. 일시정지(timeScale 0) 중에는 움직이지 않는다
/// - 프리팹에 없으면 For 가 런타임에 붙인다
/// </summary>
[DisallowMultipleComponent]
public class PlayerForcedMove
    : MonoBehaviour
{
    private const int MaxPushes = 8;

    private readonly Vector2[] velocities = new Vector2[MaxPushes];
    private readonly float[] remainTimes = new float[MaxPushes];
    private int activeCount;

    /// <summary>
    /// 진행 중인 강제 이동이 있는지
    /// </summary>
    public bool IsMoving => activeCount > 0;

    /// <summary>
    /// _player(또는 그 자식/부모) 의 이동 오브젝트(ArrowMovemnt 가 붙은 곳, 없으면 _player)의 PlayerForcedMove. 없으면 붙인다
    /// </summary>
    public static PlayerForcedMove For(Transform _player)
    {
        if (null == _player)
        {
            return null;
        }

        ArrowMovemnt movement = _player.GetComponentInParent<ArrowMovemnt>();
        if (null == movement)
        {
            movement = _player.GetComponentInChildren<ArrowMovemnt>();
        }

        GameObject host = null != movement ? movement.gameObject : _player.gameObject;
        PlayerForcedMove forced = host.GetComponent<PlayerForcedMove>();
        if (null == forced)
        {
            forced = host.AddComponent<PlayerForcedMove>();
        }
        return forced;
    }

    /// <summary>
    /// _duration 초 동안 _delta 만큼 등속으로 옮긴다 (0 이하면 즉시)
    /// </summary>
    public void Push(Vector2 _delta, float _duration)
    {
        if (_delta.sqrMagnitude <= 0f)
        {
            return;
        }

        if (_duration <= 0f)
        {
            transform.position += new Vector3(_delta.x, _delta.y, 0f);
            return;
        }

        if (activeCount >= MaxPushes)
        {
            // 가장 오래된 것을 덮어쓰지 않고 남은 이동을 즉시 처리한다
            transform.position += new Vector3(_delta.x, _delta.y, 0f);
            return;
        }

        velocities[activeCount] = _delta / _duration;
        remainTimes[activeCount] = _duration;
        ++activeCount;
    }

    /// <summary>
    /// 진행 중인 강제 이동을 모두 멈춘다
    /// </summary>
    public void Stop()
    {
        activeCount = 0;
    }

    private void OnDisable()
    {
        Stop();
    }

    private void Update()
    {
        if (0 == activeCount)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        Vector2 move = Vector2.zero;
        for (int i = activeCount - 1; i >= 0; --i)
        {
            float step = Mathf.Min(dt, remainTimes[i]);
            move += velocities[i] * step;
            remainTimes[i] -= step;

            if (remainTimes[i] <= 0f)
            {
                // 마지막 항목을 빈자리로 옮긴다
                --activeCount;
                velocities[i] = velocities[activeCount];
                remainTimes[i] = remainTimes[activeCount];
            }
        }

        transform.position += new Vector3(move.x, move.y, 0f);
    }
}
