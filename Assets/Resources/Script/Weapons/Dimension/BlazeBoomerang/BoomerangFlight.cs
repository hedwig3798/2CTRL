using UnityEngine;

/// <summary>
/// 부메랑 비행 계산 (ReturnMovement 와 같은 규칙, 화면 공간 2D, 테스트용 순수 로직)
/// - 목표 지점 + overshoot (최소 minDistance) 에서 정확히 멈추도록 감속하며 날아간다 (v^2 = 2ad)
/// - 멈추는 순간 귀환 목표 방향을 한 번만 정하고, 그 방향으로 가속하며 돌아온다 (최고 속도 speed × returnSpeedRate)
///   귀환 목표가 없거나 너무 가까우면 날아온 반대 방향
/// - 사용: StepOut 이 true 를 돌려주면(멈춤) 호출한 쪽이 귀환 목표를 구해 BeginReturn, 이후 StepReturn
/// </summary>
public struct BoomerangFlight
{
    public Vector2 direction;
    public float speed;
    public float currSpeed;
    public float deceleration;
    public float returnSpeedRate;
    public bool isReturning;

    /// <summary>
    /// _start 에서 _targetPoint 쪽으로 출발
    /// </summary>
    public static BoomerangFlight Launch(Vector2 _start, Vector2 _targetPoint, float _speed, float _overshoot, float _minDistance, float _returnSpeedRate)
    {
        BoomerangFlight flight = new BoomerangFlight();
        Vector2 dir = _targetPoint - _start;
        float distance = dir.magnitude;
        flight.direction = distance < 0.0001f ? Vector2.right : dir / distance;

        flight.speed = Mathf.Max(0.01f, _speed);
        float travel = Mathf.Max(Mathf.Max(0.1f, _minDistance), distance + Mathf.Max(0f, _overshoot));
        flight.deceleration = flight.speed * flight.speed / (2f * travel);
        flight.currSpeed = flight.speed;
        flight.returnSpeedRate = Mathf.Max(0.1f, _returnSpeedRate);
        flight.isReturning = false;
        return flight;
    }

    /// <summary>
    /// 나가는 중 한 프레임. 이번 프레임에 멈췄으면 true (호출한 쪽이 BeginReturn 을 불러야 한다)
    /// </summary>
    public bool StepOut(ref Vector2 _pos, float _dt)
    {
        if (true == isReturning)
        {
            return false;
        }

        _pos += currSpeed * _dt * direction;
        currSpeed -= deceleration * _dt;
        if (currSpeed > 0f)
        {
            return false;
        }

        currSpeed = 0f;
        return true;
    }

    /// <summary>
    /// 되돌아서기. 귀환 방향은 여기서 한 번만 정한다
    /// </summary>
    public void BeginReturn(Vector2 _pos, bool _hasReturnTarget, Vector2 _returnTarget)
    {
        isReturning = true;
        currSpeed = 0f;
        if (false == _hasReturnTarget)
        {
            direction = -direction;
            return;
        }

        Vector2 toTarget = _returnTarget - _pos;
        float distance = toTarget.magnitude;
        direction = distance < 0.0001f ? -direction : toTarget / distance;
    }

    /// <summary>
    /// 돌아오는 중 한 프레임 (가속, 최고 속도 제한)
    /// </summary>
    public void StepReturn(ref Vector2 _pos, float _dt)
    {
        currSpeed = Mathf.Min(currSpeed + deceleration * _dt, speed * returnSpeedRate);
        _pos += currSpeed * _dt * direction;
    }
}
