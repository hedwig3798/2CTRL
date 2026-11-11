using System;
using UnityEngine;

/// <summary>
/// 밀기·당기기 (폭풍 10분 보스): 플레이어와 keepDistance 를 유지하며, interval 마다 예고 후 반경 안 플레이어를 밀거나 당긴다
/// 예고 색: 당김 = pullColor (링이 안쪽으로 수축), 밀기 = pushColor (링이 바깥으로 확장)
/// </summary>
[CreateAssetMenu(fileName = "PushPullKeyword", menuName = "2CTRL/Monster/Keyword/PushPull")]
public class PushPullKeyword
    : MonsterKeyword
{
    [Header("이동")]
    [Tooltip("플레이어와 유지할 거리")]
    [Min(0f)]
    public float keepDistance = 5f;

    [Tooltip("이 오차 안이면 멈춘다")]
    [Min(0f)]
    public float keepDistanceTolerance = 0.5f;

    [Header("밀기·당기기")]
    [Tooltip("발동 간격 (예고 시작 기준, 초)")]
    [Min(0.1f)]
    public float interval = 5f;

    [Min(0f)]
    public float telegraphTime = 1f;

    [Tooltip("이 반경 안의 플레이어만")]
    [Min(0f)]
    public float radius = 7f;

    [Tooltip("강제 이동 거리")]
    [Min(0f)]
    public float moveDistance = 6f;

    [Tooltip("강제 이동 시간")]
    [Min(0.01f)]
    public float moveDuration = 0.4f;

    [Tooltip("당길 때 보스 중심에서 이 거리 앞에서 멈춘다")]
    [Min(0f)]
    public float pullStopGap = 1.5f;

    [Range(0f, 1f)]
    public float pullChance = 0.5f;

    [Header("예고 표시")]
    public BossTelegraphStyle telegraphStyle;
    public Color pullColor = new Color(0.3f, 0.6f, 1f, 0.9f);
    public Color pushColor = new Color(1f, 0.55f, 0.15f, 0.9f);

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new PushPullRuntime(this);
    }
}

public class PushPullRuntime
    : KeywordRuntime<PushPullKeyword>
{
    private float readyTime;
    private float telegraphTimer;
    private float actionTimer;
    private bool isPull;
    private BossTelegraph telegraph;

    /// <summary>
    /// 이번 발동이 당기기인지 (예고 시작 때 정해진다)
    /// </summary>
    public bool IsPull => isPull;

    /// <summary>
    /// 예고 진행률 (0 → 1)
    /// </summary>
    public float TelegraphProgress => data.telegraphTime > 0f ? Mathf.Clamp01(telegraphTimer / data.telegraphTime) : 1f;

    /// <summary>
    /// 예고 시작 (이 런타임, 당기기인지). 시각 연출용
    /// </summary>
    public event Action<PushPullRuntime, bool> OnTelegraphStarted;

    /// <summary>
    /// 발동 (이 런타임, 당기기인지, 플레이어가 반경 안이었는지)
    /// </summary>
    public event Action<PushPullRuntime, bool, bool> OnFired;

    public PushPullRuntime(PushPullKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        readyTime = Time.time + data.interval;
        telegraphTimer = 0f;
        actionTimer = 0f;
        isPull = false;
        ReleaseTelegraph();
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        Vector2 targetPos = _ctx.TargetPosition;
        Vector2 offset = _ctx.Position - targetPos;
        float dist = offset.magnitude;

        if (Mathf.Abs(dist - data.keepDistance) <= data.keepDistanceTolerance)
        {
            _ctx.Face(-offset.x);
            return true;
        }

        Vector2 dir = dist > Mathf.Epsilon ? offset / dist : Vector2.right;
        _ctx.MoveTowards(targetPos + dir * data.keepDistance, _ctx.MoveSpeed, _dt);
        return true;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        return Time.time >= readyTime && true == _ctx.HasTarget;
    }

    public override float TelegraphTime => data.telegraphTime;

    public override void OnTelegraphStart(MonsterContext _ctx)
    {
        readyTime = Time.time + data.interval;
        telegraphTimer = 0f;
        isPull = UnityEngine.Random.value < data.pullChance;

        ReleaseTelegraph();
        telegraph = BossTelegraph.Spawn(data.telegraphStyle, BossKeywordUtil.Layer(_ctx), _ctx.Position);
        UpdateTelegraph(_ctx);
        OnTelegraphStarted?.Invoke(this, isPull);
    }

    public override void OnTelegraphTick(MonsterContext _ctx, float _dt)
    {
        telegraphTimer += _dt;
        UpdateTelegraph(_ctx);
    }

    private void UpdateTelegraph(MonsterContext _ctx)
    {
        if (null == telegraph)
        {
            return;
        }

        telegraph.SetCenter(_ctx.Position);
        float t = TelegraphProgress;
        Color color = true == isPull ? data.pullColor : data.pushColor;

        // 범위 테두리는 고정, 움직이는 링으로 방향을 보인다
        telegraph.ShowRing(0, data.radius, color, true);
        float moving = true == isPull
            ? Mathf.Lerp(data.radius, data.pullStopGap, t)
            : Mathf.Lerp(data.pullStopGap, data.radius, t);
        telegraph.ShowRing(1, moving, color, false);
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        actionTimer = 0f;
        if (data.telegraphTime <= 0f)
        {
            OnTelegraphStart(_ctx);
        }
        ReleaseTelegraph();

        bool inRange = false;
        if (true == _ctx.HasTarget)
        {
            Vector2 toPlayer = _ctx.TargetPosition - _ctx.Position;
            float dist = toPlayer.magnitude;
            inRange = dist <= data.radius;
            if (true == inRange)
            {
                Vector2 dir = dist > Mathf.Epsilon ? toPlayer / dist : Vector2.right;
                float distance = true == isPull
                    ? BossMath.ClampPullDistance(data.moveDistance, dist, data.pullStopGap)
                    : data.moveDistance;
                Vector2 delta = (true == isPull ? -dir : dir) * distance;

                PlayerForcedMove forced = PlayerForcedMove.For(_ctx.target);
                if (null != forced)
                {
                    forced.Push(delta, data.moveDuration);
                }
            }
        }

        OnFired?.Invoke(this, isPull, inRange);
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        // 강제 이동이 끝날 때까지 제자리
        actionTimer += _dt;
        return actionTimer >= data.moveDuration;
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        ReleaseTelegraph();
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        ReleaseTelegraph();
    }

    private void ReleaseTelegraph()
    {
        if (null != telegraph)
        {
            telegraph.Release();
            telegraph = null;
        }
    }
}
