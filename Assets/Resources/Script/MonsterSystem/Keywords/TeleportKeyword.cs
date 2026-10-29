using UnityEngine;

/// <summary>
/// 순간이동: 멀면 예고(사라짐)한 뒤 타깃 주변 링으로 이동하고 postDelay 동안 정지한다
/// </summary>
[CreateAssetMenu(fileName = "TeleportKeyword", menuName = "2CTRL/Monster/Keyword/Teleport")]
public class TeleportKeyword
    : MonsterKeyword
{
    [Tooltip("타깃과 이 거리 이상 떨어지면 순간이동한다")]
    [Min(0f)]
    public float minDistance = 7f;

    [Min(0f)]
    public float telegraphTime = 0.5f;

    [Tooltip("나타날 링 반경 (x = 최소, y = 최대)")]
    public Vector2 appearRadius = new Vector2(2f, 3f);

    [Tooltip("나타난 뒤 정지하는 시간")]
    [Min(0f)]
    public float postDelay = 0.5f;

    [Min(0f)]
    public float cooldown = 4f;

    [Tooltip("예고 동안 스프라이트 알파를 0 까지 줄인다")]
    public bool fadeOnTelegraph = true;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new TeleportRuntime(this);
    }
}

public class TeleportRuntime
    : KeywordRuntime<TeleportKeyword>
{
    private float timer;
    private float readyTime;
    private float baseAlpha = 1f;
    private bool faded;

    public TeleportRuntime(TeleportKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        RestoreAlpha(_ctx);
        timer = 0f;
        readyTime = 0f;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        if (Time.time < readyTime)
        {
            return false;
        }
        return _ctx.DistanceToTarget() >= data.minDistance;
    }

    public override float TelegraphTime => data.telegraphTime;

    public override void OnTelegraphStart(MonsterContext _ctx)
    {
        timer = 0f;
        if (true == data.fadeOnTelegraph && null != _ctx.spriteRenderer && false == faded)
        {
            baseAlpha = _ctx.spriteRenderer.color.a;
            faded = true;
        }
    }

    public override void OnTelegraphTick(MonsterContext _ctx, float _dt)
    {
        timer += _dt;
        if (false == faded)
        {
            return;
        }

        float t = data.telegraphTime > 0f ? Mathf.Clamp01(timer / data.telegraphTime) : 1f;
        SetAlpha(_ctx, Mathf.Lerp(baseAlpha, 0f, t));
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        timer = 0f;

        float min = Mathf.Max(0f, Mathf.Min(data.appearRadius.x, data.appearRadius.y));
        float max = Mathf.Max(data.appearRadius.x, data.appearRadius.y);
        Vector2 dir = Random.insideUnitCircle;
        dir = dir.sqrMagnitude > Mathf.Epsilon ? dir.normalized : Vector2.right;

        Vector2 targetPos = _ctx.TargetPosition;
        _ctx.SetPosition(targetPos + dir * Random.Range(min, max));
        _ctx.Face(targetPos.x - _ctx.Position.x);

        RestoreAlpha(_ctx);
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        timer += _dt;
        return timer >= data.postDelay;
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        RestoreAlpha(_ctx);
        readyTime = Time.time + data.cooldown;
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        RestoreAlpha(_ctx);
    }

    private void RestoreAlpha(MonsterContext _ctx)
    {
        if (false == faded)
        {
            return;
        }

        faded = false;
        SetAlpha(_ctx, baseAlpha);
    }

    private static void SetAlpha(MonsterContext _ctx, float _alpha)
    {
        if (null == _ctx.spriteRenderer)
        {
            return;
        }

        Color c = _ctx.spriteRenderer.color;
        c.a = _alpha;
        _ctx.spriteRenderer.color = c;
    }
}
