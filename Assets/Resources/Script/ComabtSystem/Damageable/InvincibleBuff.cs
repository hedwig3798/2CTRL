using UnityEngine;

/// <summary>
/// 일정 시간 받는 데미지를 0 으로 만드는 무적 버프 (특수 아이템 "무적")
/// - DamagePipeline 의 INVINCIBLE 단계(첫 단계) 에 연결된다. DamagePipeline · HealthSystem 과 같은 오브젝트에 있어야 한다 (없으면 Awake 에서 경고)
/// - 무적 중에는 데미지 0 · 넉백 0 으로 바꿔 HealthSystem 이 피격 표시(OnDamaged) 를 내지 않고 Knockback 도 밀지 않는다
///   파이프라인을 거치지 않는 강제 이동(PlayerForcedMove.Push) 은 PlayerForcedMove 쪽에서 IsActive 를 보고 무시한다
/// - 무적 중에는 blinkRenderers 의 color alpha 를 깜빡이고, 끝나면 원래 alpha 로 돌려놓는다 (enabled 는 건드리지 않는다)
/// - 시간은 Time.deltaTime 기준. 일시정지(timeScale 0) 중에는 시간이 멈추고 렌더러는 보이는 상태로 고정한다
/// - 다시 켜면 남은 시간을 새 값으로 갱신한다 (누적하지 않음). 사망(OnDeath) · 다운(isDead) 이 되면 즉시 끝난다
/// - HealthSystem.IsInvincible(HP 를 최대치로 채우는 방식) 과는 별개다
/// </summary>
public class InvincibleBuff
    : MonoBehaviour
    , IDamageable
{
    [Tooltip("무적 중 깜빡일 렌더러 (플레이어 몸 스프라이트). 비어 있으면 깜빡이지 않는다 (무기 연출 렌더러는 넣지 말 것)")]
    [SerializeField]
    private SpriteRenderer[] blinkRenderers;

    [Tooltip("깜빡임 주기 (초). 이 간격마다 보임 / 흐림을 바꾼다")]
    [Min(0.01f)]
    [SerializeField]
    private float blinkInterval = 0.1f;

    [Tooltip("흐린 상태의 alpha 배율 (원래 alpha 에 곱한다)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float hiddenAlphaRate = 0.25f;

    private float remainTime;
    private float blinkTimer;
    private bool hidden;

    // 깜빡임을 시작할 때의 렌더러별 원래 alpha (blinkRenderers 와 같은 순서)
    private float[] originalAlphas;

    // 같은 오브젝트의 HealthSystem (사망 · 다운 감지)
    private HealthSystem health;

    /// <summary>
    /// 지금 무적인지
    /// </summary>
    public bool IsActive => remainTime > 0f;

    /// <summary>
    /// 남은 무적 시간 (초)
    /// </summary>
    public float RemainTime => remainTime;

    /// <summary>
    /// 지금 흐린(깜빡임 숨김) 상태인지
    /// </summary>
    public bool IsHidden => hidden;

    /// <summary>
    /// _duration 초 동안 무적이 된다. 이미 무적이면 남은 시간을 _duration 으로 갱신한다 (누적하지 않음)
    /// 죽었거나 다운 상태면 무시한다
    /// </summary>
    public void Activate(float _duration)
    {
        if (_duration <= 0f)
        {
            return;
        }

        if (null != health && true == health.isDead)
        {
            return;
        }

        bool wasActive = IsActive;
        remainTime = _duration;

        if (false == wasActive)
        {
            BeginBlink();
            StopForcedMove();
        }
    }

    /// <summary>
    /// 무적 직전에 시작된 강제 이동(밀기·당기기)도 멈춘다. 습득 순간에만 불리므로 탐색 비용은 무시한다
    /// </summary>
    private void StopForcedMove()
    {
        PlayerStats stats = GetComponentInParent<PlayerStats>();
        Transform root = null != stats ? stats.transform : transform.root;
        PlayerForcedMove forced = root.GetComponentInChildren<PlayerForcedMove>();
        if (null != forced)
        {
            forced.Stop();
        }
    }

    /// <summary>
    /// 무적을 즉시 끝내고 렌더러를 원래대로 돌려놓는다
    /// </summary>
    public void Deactivate()
    {
        bool wasActive = IsActive;
        remainTime = 0f;
        if (true == wasActive || true == hidden)
        {
            EndBlink();
        }
    }

    public void ProcessDamage(ref DamageMassage _msg)
    {
        if (false == IsActive)
        {
            return;
        }

        _msg.damage = 0f;
        _msg.knockbackForce = 0f;
    }

    /// <summary>
    /// 남은 시간에서 _deltaTime 을 뺀 값 (0 미만은 0)
    /// </summary>
    public static float TickRemain(float _remain, float _deltaTime)
    {
        return Mathf.Max(0f, _remain - Mathf.Max(0f, _deltaTime));
    }

    private void Awake()
    {
        health = GetComponent<HealthSystem>();
        if (null == health)
        {
            Debug.LogWarning($"[InvincibleBuff] {name} 에 HealthSystem 이 없어 사망·다운 시 무적이 자동으로 끝나지 않습니다.");
        }

        if (null == GetComponent<DamagePipeline>())
        {
            Debug.LogWarning($"[InvincibleBuff] {name} 에 DamagePipeline 이 없어 무적 단계가 파이프라인에 등록되지 않습니다. DamagePipeline 과 같은 오브젝트에 붙이세요.");
        }

        if (null == blinkRenderers || 0 == blinkRenderers.Length)
        {
            Debug.LogWarning($"[InvincibleBuff] {name} 의 blinkRenderers 가 비어 있어 무적 중 깜빡이지 않습니다.");
        }
    }

    private void OnEnable()
    {
        if (null != health)
        {
            health.OnDeath -= HandleDeath;
            health.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (null != health)
        {
            health.OnDeath -= HandleDeath;
        }
        Deactivate();
    }

    private void HandleDeath(GameObject _dead)
    {
        Deactivate();
    }

    private void Update()
    {
        if (false == IsActive)
        {
            return;
        }

        // 다운(isDead + IsDowned) 은 이벤트가 없어 매 프레임 확인한다
        if (null != health && true == health.isDead)
        {
            Deactivate();
            return;
        }

        float dt = Time.deltaTime;

        // 일시정지 중에는 시간을 멈추고 보이는 상태로 고정
        if (dt <= 0f)
        {
            if (true == hidden)
            {
                SetHidden(false);
            }
            return;
        }

        remainTime = TickRemain(remainTime, dt);
        if (false == IsActive)
        {
            EndBlink();
            return;
        }

        blinkTimer += dt;
        if (blinkTimer >= blinkInterval)
        {
            blinkTimer -= blinkInterval;
            SetHidden(false == hidden);
        }
    }

    private void BeginBlink()
    {
        blinkTimer = 0f;
        hidden = false;

        if (null == blinkRenderers)
        {
            return;
        }

        if (null == originalAlphas || originalAlphas.Length != blinkRenderers.Length)
        {
            originalAlphas = new float[blinkRenderers.Length];
        }

        for (int i = 0; i < blinkRenderers.Length; ++i)
        {
            SpriteRenderer sr = blinkRenderers[i];
            originalAlphas[i] = null != sr ? sr.color.a : 1f;
        }
    }

    private void EndBlink()
    {
        SetHidden(false);
        blinkTimer = 0f;
    }

    /// <summary>
    /// alpha 만 바꾼다 (깜빡이는 동안 다른 시스템이 바꾼 rgb 는 유지)
    /// </summary>
    private void SetHidden(bool _hidden)
    {
        hidden = _hidden;

        if (null == blinkRenderers || null == originalAlphas)
        {
            return;
        }

        int count = Mathf.Min(blinkRenderers.Length, originalAlphas.Length);
        for (int i = 0; i < count; ++i)
        {
            SpriteRenderer sr = blinkRenderers[i];
            if (null == sr)
            {
                continue;
            }

            Color c = sr.color;
            c.a = true == _hidden ? originalAlphas[i] * hiddenAlphaRate : originalAlphas[i];
            sr.color = c;
        }
    }
}
