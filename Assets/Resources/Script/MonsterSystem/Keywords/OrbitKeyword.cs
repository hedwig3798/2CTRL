using UnityEngine;

/// <summary>
/// 선회: startRadius 밖이면 접근하고, 안이면 타깃 주위를 돌며 반경을 minRadius 까지 줄인다
/// 회전 방향은 개체마다 랜덤
/// </summary>
[CreateAssetMenu(fileName = "OrbitKeyword", menuName = "2CTRL/Monster/Keyword/Orbit")]
public class OrbitKeyword
    : MonsterKeyword
{
    [Tooltip("이 반경 안에 들어오면 선회를 시작한다")]
    [Min(0f)]
    public float startRadius = 5f;

    [Tooltip("줄어드는 반경의 하한")]
    [Min(0f)]
    public float minRadius = 2f;

    [Tooltip("초당 반경 감소량")]
    [Min(0f)]
    public float shrinkSpeed = 0.3f;

    [Tooltip("초당 회전 각도 (도)")]
    public float angularSpeed = 60f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new OrbitRuntime(this);
    }
}

public class OrbitRuntime
    : KeywordRuntime<OrbitKeyword>
{
    private bool orbiting;
    private float currRadius;
    private float sign;

    public OrbitRuntime(OrbitKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        orbiting = false;
        currRadius = data.startRadius;
        sign = Random.value < 0.5f ? -1f : 1f;
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        Vector2 targetPos = _ctx.TargetPosition;
        Vector2 offset = _ctx.Position - targetPos;
        float dist = offset.magnitude;

        if (false == orbiting)
        {
            if (dist > data.startRadius)
            {
                // 접근
                _ctx.MoveTowards(targetPos, _ctx.MoveSpeed, _dt);
                return true;
            }

            orbiting = true;
            currRadius = Mathf.Max(data.minRadius, dist);
        }

        currRadius = Mathf.Max(data.minRadius, currRadius - data.shrinkSpeed * _dt);

        float angle = (dist > Mathf.Epsilon ? Mathf.Atan2(offset.y, offset.x) : 0f)
            + sign * data.angularSpeed * Mathf.Deg2Rad * _dt;
        Vector2 desired = targetPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currRadius;

        // 회전 속도 + 이동 속도까지 허용해 반경 보정과 선회를 함께 한다
        float maxSpeed = _ctx.MoveSpeed + Mathf.Abs(data.angularSpeed) * Mathf.Deg2Rad * currRadius;
        _ctx.MoveTowards(desired, maxSpeed, _dt);
        return true;
    }
}
