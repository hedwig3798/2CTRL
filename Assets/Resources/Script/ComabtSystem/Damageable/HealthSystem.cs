using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// 실제 체력의 증감 및 사망 처리
/// </summary>
public class HealthSystem
    : MonoBehaviour
    , IDamageable
    , Initializable
{
    public float maxHP;
    public float currHP;

    public bool isDead;

    public Action<GameObject> OnDeath;

    /// <summary>
    /// HP 가 바뀔 때 호출 (현재치, 최대치)
    /// </summary>
    public Action<float, float> OnHPChanged;

    /// <summary>
    /// 데미지를 받았을 때 호출 (표시할 데미지 양)
    /// 무적이면 받은 데미지 그대로, 아니면 실제로 깎인 양
    /// </summary>
    public Action<float> OnDamaged;

    /// <summary>
    /// HP 가 0 이 됐을 때 OnDeath 전에 호출 (단일 소유자)
    /// true 를 돌려주면 OnDeath 를 부르지 않고 다운 상태(isDead = true, IsDowned = true)가 된다. Revive 로 되살린다
    /// </summary>
    public Func<bool> DeathInterceptor;

    // 활성화된 적 전체 (전 맵 공격용. TargetFinder 는 버퍼 제한이 있고, 박쥐처럼 MonsterBrain 이 없는 적도 있다)
    private static readonly HashSet<HealthSystem> ActiveEnemies = new HashSet<HealthSystem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveEnemies()
    {
        ActiveEnemies.Clear();
    }

    /// <summary>
    /// _mask 레이어의 살아 있는 적을 _result 에 담는다 (_result 는 먼저 비운다, 플레이어 제외)
    /// _includeDowned 면 부활 대기 중인 적도 포함한다
    /// </summary>
    public static void CollectAlive(LayerMask _mask, List<HealthSystem> _result, bool _includeDowned = false)
    {
        _result.Clear();
        foreach (HealthSystem health in ActiveEnemies)
        {
            if (null == health || 0 == (_mask.value & (1 << health.gameObject.layer)))
            {
                continue;
            }

            if (false == health.isDead || (true == _includeDowned && true == health.IsDowned))
            {
                _result.Add(health);
            }
        }
    }

    private bool isDowned;

    public bool IsDowned => isDowned;

    // Execute 로 죽는 중이면 부활 가로채기를 건너뛴다 (Initialize 에서 초기화)
    private bool isExecuted;

    /// <summary>
    /// Execute 로 죽었는지. 몬스터의 부활·분열 등 사망 키워드를 건너뛰는 데 쓴다
    /// </summary>
    public bool IsExecuted => isExecuted;

    [SerializeField]
    private bool isInvincibility = false;

    /// <summary>
    /// 무적 여부. 켜면 데미지 이벤트는 그대로 발생하지만 HP 가 다시 최대치로 채워져 죽지 않는다
    /// </summary>
    public bool IsInvincible
    {
        get => isInvincibility;
        set => isInvincibility = value;
    }

    private DropManager boundDropManager;

    // 죽을 때 드롭할 EXP 배율 (Initialize 에서 BlackBoard 의 expRate 를 저장, 0 이하면 DropManager 기본값)
    private float expRate;

    /// <summary>
    /// 죽을 때 드롭할 EXP 배율 (설정되지 않았으면 0)
    /// </summary>
    public float ExpRate => expRate;
    private float baseMaxHP;

    // 사망 시 꺼서 추가 피격/접촉/타깃 탐색을 막는다
    private Collider2D[] colliders;

    // 플레이어면 최대 HP 를 플레이어 HP 스텟으로 맞춘다 (적은 null)
    private PlayerStats playerStats;

    public float CurrHP
    {
        get => currHP;
        set => SetCurrHP(value);
    }

    public float MaxHP
    {
        get => maxHP;
        set => SetMaxHP(value);
    }

    public void SetCurrHP(float _val)
    {
        if (true == Mathf.Approximately(currHP, _val))
        {
            return;
        }
        currHP = _val;
        OnHPChanged?.Invoke(currHP, maxHP);
    }

    /// <summary>
    /// 초당 _rate 만큼 회복한다 (0 이하면 회복 없음, 죽었거나 다운이면 무시, 최대 HP 를 넘지 않음)
    /// </summary>
    public void ApplyRegen(float _rate, float _deltaTime)
    {
        if (_rate <= 0f || _deltaTime <= 0f || true == isDead || CurrHP >= MaxHP)
        {
            return;
        }
        CurrHP = Mathf.Min(MaxHP, CurrHP + _rate * _deltaTime);
    }

    // 플레이어만 체력 재생 (PlayerStats HPRegen, 기본 0. PlayerStats.Get 이 0 미만을 0 으로 자른다)
    private void Update()
    {
        if (null == playerStats)
        {
            return;
        }
        ApplyRegen(playerStats.Get(ValueType.HPRegen), Time.deltaTime);
    }

    public void SetMaxHP(float _val)
    {
        if (true == Mathf.Approximately(maxHP, _val))
        {
            return;
        }
        maxHP = _val;
        OnHPChanged?.Invoke(currHP, maxHP);
    }

    private void SetCollidersEnabled(bool _enabled)
    {
        if (null == colliders)
        {
            return;
        }

        foreach (Collider2D c in colliders)
        {
            if (null != c)
            {
                c.enabled = _enabled;
            }
        }
    }

    public void ProcessDamage(ref DamageMassage _msg)
    {
        if (true == isDead)
        {
            return;
        }

        float prevHP = CurrHP;
        CurrHP -= _msg.damage;
        if (CurrHP <= 0)
        {
            CurrHP = 0;
        }

        // 현재 HP 보다 큰 데미지는 실제로 깎인 양만 출력
        float appliedDamage = prevHP - CurrHP;

        // 무적이면 HP 가 다시 채워지므로 받은 데미지를 그대로 출력
        float displayDamage = (true == isInvincibility) ? _msg.damage : appliedDamage;
        if (0f < displayDamage)
        {
            OnDamaged?.Invoke(displayDamage);
        }

        // 통계는 무적 여부와 상관없이 실제로 깎인 양 기준
        if (0f < appliedDamage)
        {
            PublishDamage(ref _msg, appliedDamage);
        }

        if (true == isInvincibility)
        {
            CurrHP = MaxHP;
            return;
        }

        if (CurrHP <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 데미지와 상관없이 즉시 사망시킨다 (자폭 등)
    /// </summary>
    public void Kill()
    {
        if (true == isDead)
        {
            return;
        }

        CurrHP = 0;
        Die();
    }

    /// <summary>
    /// 즉사. 남은 HP 전부를 _msg 의 무기 데미지로 처리하고 부활 가로채기 없이 최종 사망한다
    /// 부활 대기(다운) 중이면 바로 최종 사망으로 넘긴다. 무적이면 ProcessDamage 처럼 죽지 않는다
    /// </summary>
    public void Execute(ref DamageMassage _msg)
    {
        if (true == isDowned)
        {
            isExecuted = true;
            isDowned = false;
            OnDeath?.Invoke(gameObject);
            return;
        }

        if (true == isDead)
        {
            return;
        }

        isExecuted = true;
        _msg.damage = Mathf.Max(CurrHP, 1f);
        ProcessDamage(ref _msg);

        if (false == isDead)
        {
            isExecuted = false;
        }
    }

    /// <summary>
    /// 다운 상태에서 되살린다. 최대 HP 의 _hpRatio 만큼 채운다
    /// </summary>
    public void Revive(float _hpRatio)
    {
        if (false == isDowned)
        {
            return;
        }

        isDowned = false;
        isDead = false;
        SetCollidersEnabled(true);
        CurrHP = Mathf.Max(1f, MaxHP * Mathf.Clamp01(_hpRatio));
    }

    private void Die()
    {
        isDead = true;
        SetCollidersEnabled(false);

        // 가로챘으면 OnDeath(드롭/디졸브) 없이 다운 상태로 남는다
        // 인터셉터 안에서 상태가 바뀔 때 IsDowned 가 맞게 보이도록 먼저 세운다
        isDowned = true;
        if (false == isExecuted && null != DeathInterceptor && true == DeathInterceptor())
        {
            return;
        }

        isDowned = false;

        // 인터셉터 안에서 되살아났으면 사망 처리하지 않는다
        if (false == isDead)
        {
            return;
        }

        if (null != playerStats && true == EventManager.HasInstance)
        {
            EventManager.Instance.OnPlayerDeath?.Invoke(playerStats.Side);
        }
        OnDeath?.Invoke(gameObject);
    }

    /// <summary>
    /// 통계용 데미지 이벤트 발행
    /// - 플레이어가 맞았으면 OnPlayerDamaged, 아니면 무기 주인 기준 OnWeaponDamage
    /// </summary>
    private void PublishDamage(ref DamageMassage _msg, float _applied)
    {
        // 매니져가 없으면 구독자도 없다 (종료 중 새로 만들지 않도록 Instance 를 건드리지 않는다)
        if (false == EventManager.HasInstance)
        {
            return;
        }

        if (null != playerStats)
        {
            EventManager.Instance.OnPlayerDamaged?.Invoke(playerStats.Side, _applied);
            return;
        }

        if (null == _msg.weapon)
        {
            return;
        }

        PlayerStats owner = _msg.weapon.OwnerStats;
        if (null == owner)
        {
            return;
        }

        EventManager.Instance.OnWeaponDamage?.Invoke(owner.Side, _msg.weapon, _applied);
    }

    public void Initialize(BlackBoard _data)
    {
        isDead = false;
        isDowned = false;
        isExecuted = false;
        SetCollidersEnabled(true);

        float rate = 1f;
        if (null != _data)
        {
            rate = _data.GetFloat(DATA_TYPE.HPRate);
            if (rate <= 0f)
            {
                rate = 1f;
            }
        }

        MaxHP = baseMaxHP * rate;
        CurrHP = MaxHP;

        // 죽을 때 드롭할 EXP 배율 (Spawner 가 스폰할 때 MonsterData.expValue 로 넣는다, 없으면 0)
        expRate = null != _data ? _data.GetFloat(DATA_TYPE.expRate) : 0f;

        if (null != boundDropManager)
        {
            OnDeath -= boundDropManager.DropItem;
            boundDropManager = null;
        }

        if (null != _data && null != _data.dropManager)
        {
            boundDropManager = _data.dropManager;
            OnDeath -= boundDropManager.DropItem;
            OnDeath += boundDropManager.DropItem;
        }
    }

    private void Awake()
    {
        isDead = false;
        colliders = GetComponentsInChildren<Collider2D>(true);
        if (baseMaxHP <= 0f)
        {
            baseMaxHP = MaxHP;
        }

        playerStats = GetComponentInParent<PlayerStats>();
    }

    private void OnEnable()
    {
        if (null != playerStats)
        {
            playerStats.OnStatChanged -= HandlePlayerStatChanged;
            playerStats.OnStatChanged += HandlePlayerStatChanged;
        }
        else
        {
            ActiveEnemies.Add(this);
        }
    }

    /// <summary>
    /// 최대 HP 를 플레이어 HP 스텟으로 맞춘다
    /// 최대치가 늘면 늘어난 만큼 현재 HP 도 채우고, 줄면 현재 HP 를 최대치로 자른다 (살아 있으면 최소 1)
    /// </summary>
    private void HandlePlayerStatChanged(PlayerStats _stats)
    {
        float newMax = Mathf.Max(1f, _stats.Get(ValueType.HP));
        float delta = newMax - MaxHP;
        MaxHP = newMax;

        if (true == isDead)
        {
            return;
        }

        float newCurr = (delta > 0f) ? CurrHP + delta : Mathf.Min(CurrHP, MaxHP);
        CurrHP = Mathf.Max(1f, newCurr);
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);

        if (null != playerStats)
        {
            playerStats.OnStatChanged -= HandlePlayerStatChanged;
        }

        if (null != boundDropManager)
        {
            OnDeath -= boundDropManager.DropItem;
            boundDropManager = null;
        }
    }
}
