using System;
using UnityEngine;

/// <summary>
/// 판정의 고리 (폭풍 20분 보스): 느리게 선회하다가 cooldown 마다 고리 패턴 (패턴 중 정지)
/// 1. 측정(예고) measureTime: 보스 중심, 반지름 = 플레이어 거리 (점선, 실시간)
/// 2. 고정 lockTime: 중심·반지름 고정, 실선
/// 3. 경고 warnTime: 안쪽 또는 바깥쪽을 붉게 채운다 (50:50, 같은 쪽 maxSameSide 번 연속까지)
/// 4. 피해: 위험 영역에 플레이어 중심점이 있으면 damage
/// HP 가 enrageHPRatio 이하면 고리 2개 (반지름 ± bandHalfWidth 띠, 띠 안쪽이 안전 또는 위험)
/// 판정은 BossMath.IsRingDanger / PickRingSide (순수 함수)
/// </summary>
[CreateAssetMenu(fileName = "RingJudgeKeyword", menuName = "2CTRL/Monster/Keyword/RingJudge")]
public class RingJudgeKeyword
    : MonsterKeyword
{
    [Header("선회")]
    [Min(0f)]
    public float orbitRadius = 6f;

    [Tooltip("선회 속도 (도/초)")]
    public float orbitAngularSpeed = 15f;

    [Header("고리 패턴")]
    [Min(0f)]
    public float cooldown = 6f;

    [Min(0f)]
    public float measureTime = 2f;

    [Min(0f)]
    public float lockTime = 1f;

    [Min(0f)]
    public float warnTime = 1.2f;

    [Tooltip("데미지. damageRate 를 곱한다 (보스는 1)")]
    [Min(0f)]
    public float damage = 35f;

    [Tooltip("같은 쪽(안/밖) 최대 연속 횟수 (2 = 3연속 금지)")]
    [Min(1)]
    public int maxSameSide = 2;

    [Header("광폭화")]
    [Range(0f, 1f)]
    public float enrageHPRatio = 0.5f;

    [Tooltip("고리 2개 띠의 반폭 (반지름 ± 이 값)")]
    [Min(0f)]
    public float bandHalfWidth = 2f;

    [Header("예고 표시")]
    public BossTelegraphStyle telegraphStyle;

    [Tooltip("바깥쪽 위험 영역을 채울 폭")]
    [Min(0f)]
    public float outsideFillWidth = 30f;

    public Color ringColor = new Color(1f, 1f, 1f, 0.9f);
    public Color dangerColor = new Color(1f, 0.1f, 0.1f, 0.35f);

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new RingJudgeRuntime(this);
    }
}

public enum RingJudgePhase
{
    None,
    Measure,
    Lock,
    Warn,
}

public class RingJudgeRuntime
    : KeywordRuntime<RingJudgeKeyword>
{
    private float readyTime;
    private float timer;
    private float orbitSign = 1f;
    private RingJudgePhase phase;
    private Vector2 center;
    private float radius;
    private bool isBand;
    private bool insideDanger;
    private bool hasLastSide;
    private bool lastInside;
    private int sideStreak;
    private DamagePipeline targetPipeline;
    private BossTelegraph telegraph;

    public RingJudgePhase Phase => phase;
    public Vector2 Center => center;
    public float Radius => radius;
    public bool IsBand => isBand;

    /// <summary>
    /// 안쪽(띠면 띠 안)이 위험인지 (Lock 이후 의미 있음)
    /// </summary>
    public bool InsideDanger => insideDanger;

    public event Action<RingJudgeRuntime, RingJudgePhase> OnPhaseChanged;

    /// <summary>
    /// 판정 (이 런타임, 플레이어가 맞았는지)
    /// </summary>
    public event Action<RingJudgeRuntime, bool> OnJudged;

    public RingJudgeRuntime(RingJudgeKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        readyTime = Time.time + data.cooldown;
        timer = 0f;
        orbitSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        phase = RingJudgePhase.None;
        hasLastSide = false;
        sideStreak = 0;
        targetPipeline = BossKeywordUtil.FindTargetPipeline(_ctx.target);
        ReleaseTelegraph();
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        Vector2 targetPos = _ctx.TargetPosition;
        Vector2 offset = _ctx.Position - targetPos;
        float dist = offset.magnitude;

        float angle = (dist > Mathf.Epsilon ? Mathf.Atan2(offset.y, offset.x) : 0f)
            + orbitSign * data.orbitAngularSpeed * Mathf.Deg2Rad * _dt;
        Vector2 desired = targetPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * data.orbitRadius;

        float maxSpeed = _ctx.MoveSpeed + Mathf.Abs(data.orbitAngularSpeed) * Mathf.Deg2Rad * data.orbitRadius;
        _ctx.MoveTowards(desired, maxSpeed, _dt);
        return true;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        return Time.time >= readyTime && true == _ctx.HasTarget;
    }

    public override float TelegraphTime => data.measureTime;

    public override void OnTelegraphStart(MonsterContext _ctx)
    {
        isBand = BossKeywordUtil.IsEnraged(_ctx, data.enrageHPRatio) && data.bandHalfWidth > 0f;
        center = _ctx.Position;
        radius = _ctx.DistanceToTarget();

        ReleaseTelegraph();
        telegraph = BossTelegraph.Spawn(data.telegraphStyle, BossKeywordUtil.Layer(_ctx), center);
        SetPhase(RingJudgePhase.Measure);
        DrawRings(true);
    }

    public override void OnTelegraphTick(MonsterContext _ctx, float _dt)
    {
        center = _ctx.Position;
        radius = _ctx.DistanceToTarget();
        if (null != telegraph)
        {
            telegraph.SetCenter(center);
        }
        DrawRings(true);
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        if (data.measureTime <= 0f)
        {
            OnTelegraphStart(_ctx);
        }

        timer = 0f;
        SetPhase(RingJudgePhase.Lock);
        DrawRings(false);

        insideDanger = BossMath.PickRingSide(lastInside, true == hasLastSide ? sideStreak : 0, data.maxSameSide, UnityEngine.Random.value);
        if (true == hasLastSide && insideDanger == lastInside)
        {
            ++sideStreak;
        }
        else
        {
            sideStreak = 1;
        }
        lastInside = insideDanger;
        hasLastSide = true;
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        timer += _dt;

        if (RingJudgePhase.Lock == phase)
        {
            if (timer >= data.lockTime)
            {
                SetPhase(RingJudgePhase.Warn);
                DrawDanger();
            }
            return false;
        }

        if (timer < data.lockTime + data.warnTime)
        {
            return false;
        }

        Judge(_ctx);
        return true;
    }

    private void Judge(MonsterContext _ctx)
    {
        bool hit = false;
        if (true == _ctx.HasTarget)
        {
            float dist = Vector2.Distance(center, _ctx.TargetPosition);
            hit = BossMath.IsRingDanger(dist, radius, true == isBand ? data.bandHalfWidth : 0f, insideDanger);
            if (true == hit)
            {
                if (null == targetPipeline)
                {
                    targetPipeline = BossKeywordUtil.FindTargetPipeline(_ctx.target);
                }
                BossKeywordUtil.DamageTarget(targetPipeline, data.damage * _ctx.damageRate, _ctx.pipeline, center);
            }
        }
        OnJudged?.Invoke(this, hit);
    }

    private void DrawRings(bool _dashed)
    {
        if (null == telegraph)
        {
            return;
        }

        if (true == isBand)
        {
            telegraph.ShowRing(0, Mathf.Max(0f, radius - data.bandHalfWidth), data.ringColor, _dashed);
            telegraph.ShowRing(1, radius + data.bandHalfWidth, data.ringColor, _dashed);
        }
        else
        {
            telegraph.ShowRing(0, radius, data.ringColor, _dashed);
            telegraph.HideRing(1);
        }
    }

    private void DrawDanger()
    {
        if (null == telegraph)
        {
            return;
        }

        float inner = true == isBand ? Mathf.Max(0f, radius - data.bandHalfWidth) : radius;
        float outer = true == isBand ? radius + data.bandHalfWidth : radius;

        if (true == isBand && true == insideDanger)
        {
            // 띠 안이 위험
            telegraph.HideDisc();
            telegraph.ShowBand(inner, outer, data.dangerColor);
        }
        else if (true == insideDanger)
        {
            // 원 안이 위험
            telegraph.ShowDisc(radius, data.dangerColor);
            telegraph.HideBand();
        }
        else
        {
            // 바깥이 위험 (띠면 띠 안쪽 원도 위험)
            telegraph.ShowBand(outer, outer + data.outsideFillWidth, data.dangerColor);
            if (true == isBand && inner > 0f)
            {
                telegraph.ShowDisc(inner, data.dangerColor);
            }
            else
            {
                telegraph.HideDisc();
            }
        }
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        ReleaseTelegraph();
        readyTime = Time.time + data.cooldown;
        SetPhase(RingJudgePhase.None);
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        ReleaseTelegraph();
        phase = RingJudgePhase.None;
    }

    private void SetPhase(RingJudgePhase _phase)
    {
        if (phase == _phase)
        {
            return;
        }
        phase = _phase;
        OnPhaseChanged?.Invoke(this, _phase);
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
