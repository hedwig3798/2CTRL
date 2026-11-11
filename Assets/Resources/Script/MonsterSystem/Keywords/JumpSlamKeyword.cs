using System;
using UnityEngine;

/// <summary>
/// 도약 (숲 20분 보스): 추적 chaseTime 초 → 웅크림 → 공중(본체·콜라이더 숨김, 피격 불가) → 착지
/// 공중에서 그림자가 trackTime 동안 플레이어를 따라간 뒤 고정되고, 착지하면 반경 데미지 + 밀어냄 + 둘레에 뿌리 장판
/// HP 가 enrageHPRatio 이하면 enragedJumps 연속 (각각 다시 노림, 공중 시간 enragedAirTime)
/// 공중 처리는 루트의 MidBoss.SetAirborne 을 쓴다 (없으면 숨기지 않는다)
/// </summary>
[CreateAssetMenu(fileName = "JumpSlamKeyword", menuName = "2CTRL/Monster/Keyword/JumpSlam")]
public class JumpSlamKeyword
    : MonsterKeyword
{
    [Header("순환")]
    [Tooltip("착지 후 다음 도약까지 추적 시간")]
    [Min(0f)]
    public float chaseTime = 4f;

    [Tooltip("웅크림 (예고)")]
    [Min(0f)]
    public float crouchTime = 0.3f;

    [Min(0.1f)]
    public float airTime = 1.5f;

    [Tooltip("그림자가 플레이어를 따라가는 시간 (이후 고정)")]
    [Min(0f)]
    public float trackTime = 0.8f;

    [Tooltip("그림자 추적 속도")]
    [Min(0f)]
    public float shadowFollowSpeed = 14f;

    [Tooltip("착지 후 다음 행동까지 경직")]
    [Min(0f)]
    public float landRecoverTime = 0.3f;

    [Header("광폭화")]
    [Range(0f, 1f)]
    public float enrageHPRatio = 0.5f;

    [Min(1)]
    public int enragedJumps = 3;

    [Min(0.1f)]
    public float enragedAirTime = 1.2f;

    [Header("착지")]
    [Min(0f)]
    public float landRadius = 2.5f;

    [Tooltip("착지 데미지. damageRate 를 곱한다 (보스는 1)")]
    [Min(0f)]
    public float landDamage = 30f;

    [Tooltip("맞은 플레이어를 밀어내는 거리")]
    [Min(0f)]
    public float landPushDistance = 2f;

    [Min(0.01f)]
    public float landPushDuration = 0.2f;

    [Header("뿌리 장판")]
    public HazardZone rootZonePrefab;

    [Min(0)]
    public int rootZoneCount = 6;

    [Tooltip("착지 지점에서 장판까지 거리")]
    [Min(0f)]
    public float rootRingRadius = 2.5f;

    [Min(0f)]
    public float rootZoneRadius = 1.2f;

    [Min(0.01f)]
    public float rootZoneDuration = 5f;

    [Min(0.01f)]
    public float rootTickInterval = 0.5f;

    [Min(0f)]
    public float rootDamagePerTick = 4f;

    [Range(0f, 1f)]
    public float rootSlowRate = 0.4f;

    [Header("예고 표시")]
    public BossTelegraphStyle telegraphStyle;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.6f);
    public Color ringColor = new Color(1f, 0.3f, 0.2f, 0.9f);

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new JumpSlamRuntime(this);
    }
}

public enum JumpSlamPhase
{
    None,
    Crouch,
    Air,
    Recover,
}

public class JumpSlamRuntime
    : KeywordRuntime<JumpSlamKeyword>
{
    private float readyTime;
    private float timer;
    private float currentAirTime;
    private int jumpsLeft;
    private Vector2 shadowPos;
    private JumpSlamPhase phase;
    private MidBoss boss;
    private BossTelegraph telegraph;

    public JumpSlamPhase Phase => phase;
    public Vector2 ShadowPosition => shadowPos;

    /// <summary>
    /// 남은 도약 수 (지금 것 포함)
    /// </summary>
    public int JumpsLeft => jumpsLeft;

    /// <summary>
    /// 단계가 바뀔 때 (이 런타임, 새 단계)
    /// </summary>
    public event Action<JumpSlamRuntime, JumpSlamPhase> OnPhaseChanged;

    /// <summary>
    /// 착지 (이 런타임, 착지 위치, 맞은 대상 수)
    /// </summary>
    public event Action<JumpSlamRuntime, Vector2, int> OnLanded;

    public JumpSlamRuntime(JumpSlamKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        boss = _ctx.root.GetComponent<MidBoss>();
        readyTime = Time.time + data.chaseTime;
        timer = 0f;
        jumpsLeft = 0;
        phase = JumpSlamPhase.None;
        ReleaseTelegraph();
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        return Time.time >= readyTime && true == _ctx.HasTarget;
    }

    public override float TelegraphTime => data.crouchTime;

    public override void OnTelegraphStart(MonsterContext _ctx)
    {
        SetPhase(JumpSlamPhase.Crouch);
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        bool enraged = BossKeywordUtil.IsEnraged(_ctx, data.enrageHPRatio);
        jumpsLeft = true == enraged ? Mathf.Max(1, data.enragedJumps) : 1;
        currentAirTime = true == enraged ? data.enragedAirTime : data.airTime;
        StartAir(_ctx);
    }

    private void StartAir(MonsterContext _ctx)
    {
        timer = 0f;
        shadowPos = _ctx.TargetPosition;
        SetAirborne(true);

        ReleaseTelegraph();
        telegraph = BossTelegraph.Spawn(data.telegraphStyle, BossKeywordUtil.Layer(_ctx), shadowPos);
        UpdateShadow();
        SetPhase(JumpSlamPhase.Air);
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        timer += _dt;
        switch (phase)
        {
            case JumpSlamPhase.Air:
                if (timer <= data.trackTime && true == _ctx.HasTarget)
                {
                    shadowPos = Vector2.MoveTowards(shadowPos, _ctx.TargetPosition, data.shadowFollowSpeed * _dt);
                }
                UpdateShadow();

                if (timer >= currentAirTime)
                {
                    Land(_ctx);
                }
                return false;

            case JumpSlamPhase.Crouch:
                if (timer >= data.crouchTime)
                {
                    StartAir(_ctx);
                }
                return false;

            case JumpSlamPhase.Recover:
                if (timer < data.landRecoverTime)
                {
                    return false;
                }

                if (jumpsLeft > 0)
                {
                    timer = 0f;
                    SetPhase(JumpSlamPhase.Crouch);
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    private void UpdateShadow()
    {
        if (null == telegraph)
        {
            return;
        }

        telegraph.SetCenter(shadowPos);
        float t = currentAirTime > 0f ? Mathf.Clamp01(timer / currentAirTime) : 1f;
        Color c = data.shadowColor;
        c.a *= Mathf.Lerp(0.25f, 1f, t);
        telegraph.ShowDisc(data.landRadius, c);
        telegraph.ShowRing(0, data.landRadius, data.ringColor, timer <= data.trackTime);
    }

    private void Land(MonsterContext _ctx)
    {
        ReleaseTelegraph();
        _ctx.SetPosition(shadowPos);
        SetAirborne(false);

        LayerMask mask = _ctx.TargetLayer;
        int hit = MonsterAreaDamage.Apply(shadowPos, data.landRadius, mask, data.landDamage * _ctx.damageRate, 0f, _ctx.pipeline);

        // 맞았으면 착지 지점 바깥으로 밀어낸다
        if (hit > 0 && true == _ctx.HasTarget && data.landPushDistance > 0f)
        {
            Vector2 dir = _ctx.TargetPosition - shadowPos;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : UnityEngine.Random.insideUnitCircle.normalized;
            PlayerForcedMove forced = PlayerForcedMove.For(_ctx.target);
            if (null != forced)
            {
                forced.Push(dir * data.landPushDistance, data.landPushDuration);
            }
        }

        SpawnRootZones(_ctx, shadowPos);

        --jumpsLeft;
        timer = 0f;
        SetPhase(JumpSlamPhase.Recover);
        OnLanded?.Invoke(this, shadowPos, hit);
    }

    private void SpawnRootZones(MonsterContext _ctx, Vector2 _center)
    {
        if (null == data.rootZonePrefab || data.rootZoneCount <= 0)
        {
            return;
        }

        float offset = UnityEngine.Random.value * 360f;
        int layer = BossKeywordUtil.Layer(_ctx);
        for (int i = 0; i < data.rootZoneCount; ++i)
        {
            float a = (offset + 360f * i / data.rootZoneCount) * Mathf.Deg2Rad;
            Vector2 pos = _center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * data.rootRingRadius;
            HazardZone.Spawn(
                data.rootZonePrefab
                , pos
                , data.rootZoneRadius
                , data.rootZoneDuration
                , data.rootTickInterval
                , data.rootDamagePerTick * _ctx.damageRate
                , _ctx.TargetLayer
                , _ctx.pipeline
                , data.rootSlowRate
                , layer);
        }
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        // 공중에서 끊겨도(사망 등) 본체를 되돌린다
        SetAirborne(false);
        ReleaseTelegraph();
        readyTime = Time.time + data.chaseTime;
        jumpsLeft = 0;
        SetPhase(JumpSlamPhase.None);
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        SetAirborne(false);
        ReleaseTelegraph();
        phase = JumpSlamPhase.None;
    }

    private void SetAirborne(bool _airborne)
    {
        if (null != boss)
        {
            boss.SetAirborne(_airborne);
        }
    }

    private void SetPhase(JumpSlamPhase _phase)
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
