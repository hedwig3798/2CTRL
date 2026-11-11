using System;
using UnityEngine;

/// <summary>
/// 화면 반사 (공장 20분 보스): 진영 카메라 뷰 사각형(매 프레임) 경계에서 튕기며 대각 등속 이동
/// overheatInterval 마다 과열: overheatDuration 동안 정지하고 받는 데미지 × overheatDamageMultiplier
/// HP 가 enrageHPRatio 이하면 속도 enragedSpeed, 튕길 때마다 그 자리에 지연 폭발 지뢰
/// </summary>
[CreateAssetMenu(fileName = "ScreenBounceKeyword", menuName = "2CTRL/Monster/Keyword/ScreenBounce")]
public class ScreenBounceKeyword
    : MonsterKeyword
{
    [Header("이동")]
    [Min(0f)]
    public float speed = 7f;

    [Tooltip("카메라 뷰에서 안쪽으로 줄일 여백 (몸집 반쯤)")]
    [Min(0f)]
    public float edgeInset = 1f;

    [Header("과열")]
    [Tooltip("이동 시간 (과열이 끝난 뒤 다음 과열까지)")]
    [Min(0.1f)]
    public float overheatInterval = 8f;

    [Min(0f)]
    public float overheatDuration = 2.5f;

    [Min(0f)]
    public float overheatDamageMultiplier = 1.5f;

    [Header("광폭화")]
    [Range(0f, 1f)]
    public float enrageHPRatio = 0.5f;

    [Min(0f)]
    public float enragedSpeed = 9f;

    [Tooltip("광폭화 중 튕길 때 지뢰를 남길지")]
    public bool dropMinesWhenEnraged = true;

    [Min(0f)]
    public float mineDelay = 2f;

    [Min(0f)]
    public float mineRadius = 1.5f;

    [Tooltip("지뢰 데미지. damageRate 를 곱한다 (보스는 1)")]
    [Min(0f)]
    public float mineDamage = 20f;

    [Header("예고 표시")]
    public BossTelegraphStyle telegraphStyle;
    public Color mineColor = new Color(1f, 0.25f, 0.1f, 0.9f);

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Move;

    public override KeywordRuntime CreateRuntime()
    {
        return new ScreenBounceRuntime(this);
    }
}

public class ScreenBounceRuntime
    : KeywordRuntime<ScreenBounceKeyword>
{
    private Vector2 dir;
    private bool hasDir;
    private float nextOverheatTime;
    private float overheatTimer;
    private bool isOverheated;
    private MidBoss boss;

    public Vector2 Direction => dir;
    public bool IsOverheated => isOverheated;

    /// <summary>
    /// 경계에서 튕겼을 때 (이 런타임, 위치, 지뢰를 남겼는지)
    /// </summary>
    public event Action<ScreenBounceRuntime, Vector2, bool> OnBounced;

    /// <summary>
    /// 과열 시작(true) / 끝(false). 증기 연출용
    /// </summary>
    public event Action<ScreenBounceRuntime, bool> OnOverheatChanged;

    public ScreenBounceRuntime(ScreenBounceKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        boss = _ctx.root.GetComponent<MidBoss>();
        hasDir = false;
        dir = Vector2.zero;
        nextOverheatTime = Time.time + data.overheatInterval;
        // 재사용 시 MidBoss 가 배율을 이미 지웠으므로 상태만 되돌린다 (MidBoss 가 없으면 배율도 되돌림)
        SetOverheated(_ctx, false);
    }

    private Camera GetCamera(MonsterContext _ctx)
    {
        return null != boss ? boss.SideCamera : DespawnOutOfCamera.FindCameraForLayer(BossKeywordUtil.Layer(_ctx));
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        if (false == hasDir)
        {
            // 처음에는 플레이어 쪽 사분면 대각으로 (화면 밖 스폰 링에서 안으로 들어온다)
            dir = BossMath.DiagonalToward(_ctx.Position, _ctx.TargetPosition);
            hasDir = true;
        }

        bool enraged = BossKeywordUtil.IsEnraged(_ctx, data.enrageHPRatio);
        float moveSpeed = (true == enraged ? data.enragedSpeed : data.speed) * _ctx.moveSpeedRate * _ctx.SlowMultiplier;

        Camera cam = GetCamera(_ctx);
        if (null != cam)
        {
            Rect rect = BossMath.ViewRect(cam.transform.position, cam.orthographicSize, cam.aspect, data.edgeInset);
            if (true == BossMath.Bounce(_ctx.Position, dir, rect, out Vector2 newDir))
            {
                dir = newDir;
                bool mine = true == enraged && true == data.dropMinesWhenEnraged;
                if (true == mine)
                {
                    BossTelegraph t = BossTelegraph.Spawn(data.telegraphStyle, BossKeywordUtil.Layer(_ctx), _ctx.Position);
                    t.ArmBlast(data.mineDelay, data.mineRadius, data.mineDamage * _ctx.damageRate, _ctx.TargetLayer, _ctx.pipeline, data.mineColor);
                }
                OnBounced?.Invoke(this, _ctx.Position, mine);
            }
        }

        _ctx.Move(dir * moveSpeed * _dt);
        return true;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        return Time.time >= nextOverheatTime;
    }

    public override void OnActionStart(MonsterContext _ctx)
    {
        overheatTimer = 0f;
        SetOverheated(_ctx, true);
    }

    public override bool TickAction(MonsterContext _ctx, float _dt)
    {
        overheatTimer += _dt;
        return overheatTimer >= data.overheatDuration;
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        SetOverheated(_ctx, false);
        nextOverheatTime = Time.time + data.overheatInterval;
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        SetOverheated(_ctx, false);
    }

    private void SetOverheated(MonsterContext _ctx, bool _overheated)
    {
        if (isOverheated == _overheated)
        {
            return;
        }
        isOverheated = _overheated;

        // 다른 출처(공중 등)를 덮어쓰지 않도록 MidBoss 에 출처별로 넣는다. MidBoss 가 없으면 곱/나누기로 합성한다
        if (null != boss)
        {
            if (true == _overheated)
            {
                boss.SetDamageTakenFactor(this, data.overheatDamageMultiplier);
            }
            else
            {
                boss.ClearDamageTakenFactor(this);
            }
        }
        else if (null != _ctx.health && data.overheatDamageMultiplier > 0f)
        {
            float m = _ctx.health.IncomingDamageMultiplier;
            _ctx.health.IncomingDamageMultiplier = true == _overheated
                ? m * data.overheatDamageMultiplier
                : m / data.overheatDamageMultiplier;
        }

        OnOverheatChanged?.Invoke(this, _overheated);
    }
}
