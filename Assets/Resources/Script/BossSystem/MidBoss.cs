using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중간 보스 공통 (보스 프리팹 루트에 MonsterBrain 과 함께 붙는다)
/// - 고정 HP·데미지·속도: Spawner 가 이 컴포넌트가 있는 프리팹은 배율 1 로 스폰한다
/// - 즉사·넉백 면역, 둔화·속박은 slowEffectRate 만큼만 받는다
/// - 사라지지 않음: 진영 카메라에서 relocateDistance 이상 멀어지면 HP 를 유지한 채 플레이어 앞쪽 스폰 링으로 옮긴다
/// - 처치 시 보석을 흩뿌리고 EventManager.OnBossDefeated 로 보스 보상을 알린다
/// </summary>
[DisallowMultipleComponent]
public class MidBoss
    : MonoBehaviour
    , Initializable
{
    [Tooltip("경고 배너 등에 표시할 이름 StringTable 키")]
    [SerializeField]
    private string displayNameKey;

    [Header("면역")]
    [Tooltip("받는 둔화·속박 효과 배율 (0.3 = 30% 만 받음)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float slowEffectRate = 0.3f;

    [Header("재배치")]
    [Tooltip("진영 카메라 중심에서 이 거리를 넘으면 재배치")]
    [Min(1f)]
    [SerializeField]
    private float relocateDistance = 20f;

    [Tooltip("재배치 위치: 플레이어 이동 방향 앞쪽 링 (최소, 최대 거리)")]
    [SerializeField]
    private Vector2 relocateRing = new Vector2(11f, 13f);

    [Tooltip("재배치 위치 부채꼴 각도 (이동 방향 기준 ±절반)")]
    [Range(0f, 360f)]
    [SerializeField]
    private float relocateSpread = 60f;

    [Min(0.05f)]
    [SerializeField]
    private float relocateCheckInterval = 0.25f;

    [Header("처치 보상 (MidBossEntry 가 없을 때)")]
    [Min(0f)]
    [SerializeField]
    private float defaultExpTotal = 60f;

    [Min(0)]
    [SerializeField]
    private int defaultGemCount = 20;

    /// <summary>
    /// 재배치됐을 때 (보스, 새 위치). 등장 연출용
    /// </summary>
    public event Action<MidBoss, Vector2> OnRelocated;

    /// <summary>
    /// 최종 사망했을 때 (드롭과 OnBossDefeated 발행 뒤)
    /// </summary>
    public event Action<MidBoss> OnDefeated;

    /// <summary>
    /// 비활성화(풀 반환) 시
    /// </summary>
    public event Action<MidBoss> OnDespawned;

    /// <summary>
    /// 공중(피격 불가) 상태가 바뀔 때
    /// </summary>
    public event Action<MidBoss, bool> OnAirborneChanged;

    private MonsterBrain brain;
    private HealthSystem health;
    private Knockback knockback;
    private Collider2D[] colliders;
    private SpriteRenderer[] bodyRenderers;
    private bool[] bodyRendererStates;
    // 프리팹 원래 표시 상태 (재사용 시 강제 복원용)
    private bool[] bodyRendererDefaults;

    // 받는 데미지 배율 출처별 값 (공중 0, 과열 1.5 등). 모두 곱해 HealthSystem.IncomingDamageMultiplier 에 넣는다
    private readonly Dictionary<object, float> damageTakenFactors = new Dictionary<object, float>();
    private static readonly object AirborneKey = new object();

    private Spawner spawner;
    private Transform target;
    private ArrowMovemnt targetMovement;
    private DropManager dropManager;
    private MidBossEntry entry;
    private PlayerSide side = PlayerSide.Left;
    private bool hasSide;
    private bool initialized;
    private bool isAirborne;
    private bool defeated;

    private Camera cachedCamera;
    private int cachedLayer = -1;
    private float relocateTimer;

    public string DisplayNameKey => displayNameKey;
    public MonsterBrain Brain => brain;
    public HealthSystem Health => health;
    public MidBossEntry Entry => entry;
    public Spawner Spawner => spawner;
    public Transform Target => target;

    /// <summary>
    /// 보스가 나온 진영 (= 스포너 target 플레이어 진영 = 처치 보상을 받을 진영)
    /// </summary>
    public PlayerSide Side => side;

    public bool HasSide => hasSide;
    public bool IsAirborne => isAirborne;
    public float SlowEffectRate => slowEffectRate;

    /// <summary>
    /// 현재 HP 비율 (0 ~ 1)
    /// </summary>
    public float HPRatio => null != health && health.MaxHP > 0f ? health.CurrHP / health.MaxHP : 0f;

    /// <summary>
    /// 이 보스의 레이어를 렌더링하는 진영 카메라 (레이어가 바뀌었을 때만 다시 찾는다)
    /// </summary>
    public Camera SideCamera
    {
        get
        {
            if (null == cachedCamera || cachedLayer != gameObject.layer)
            {
                cachedLayer = gameObject.layer;
                cachedCamera = DespawnOutOfCamera.FindCameraForLayer(cachedLayer);
            }
            return cachedCamera;
        }
    }

    private void Awake()
    {
        brain = GetComponentInChildren<MonsterBrain>(true);
        health = GetComponentInChildren<HealthSystem>(true);
        knockback = GetComponentInChildren<Knockback>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        bodyRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bodyRendererStates = new bool[bodyRenderers.Length];
        bodyRendererDefaults = new bool[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; ++i)
        {
            bodyRendererDefaults[i] = null != bodyRenderers[i] && bodyRenderers[i].enabled;
        }

        if (null == health)
        {
            Debug.LogError($"[MidBoss] {name} 에 HealthSystem 이 없습니다.", this);
        }
        else
        {
            health.IsExecuteImmune = true;
            health.OnDeath += HandleDeath;
        }

        // 죽기 전에는 사라지지 않으므로 거리 디스폰을 끈다
        foreach (DespawnOutOfCamera despawn in GetComponentsInChildren<DespawnOutOfCamera>(true))
        {
            Debug.LogWarning($"[MidBoss] {name} : DespawnOutOfCamera 는 보스에 쓰지 않으므로 끕니다.", this);
            despawn.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (null != health)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    public void Initialize(BlackBoard _data)
    {
        entry = null;
        defeated = false;
        relocateTimer = 0f;
        ResetBody();

        spawner = null != _data ? _data.spawner : null;
        target = null != _data ? _data.GetTransform(DATA_TYPE.moveTarget) : null;
        dropManager = null != spawner ? spawner.DropManager : (null != _data ? _data.dropManager : null);
        targetMovement = null;

        hasSide = false;
        if (null != target)
        {
            targetMovement = target.GetComponentInParent<ArrowMovemnt>();
            if (null == targetMovement)
            {
                targetMovement = target.GetComponentInChildren<ArrowMovemnt>();
            }

            PlayerStats stats = target.GetComponentInParent<PlayerStats>();
            if (null != stats)
            {
                side = stats.Side;
                hasSide = true;
            }
        }
        if (false == hasSide)
        {
            Debug.LogWarning($"[MidBoss] {name} : 대상 플레이어의 PlayerStats 를 찾지 못해 진영을 {side} 로 둡니다.", this);
        }

        if (null != knockback)
        {
            knockback.resistance = 1f;
        }

        if (null != health)
        {
            health.IsExecuteImmune = true;
        }

        if (null != brain && null != brain.Context)
        {
            brain.Context.slowEffectRate = slowEffectRate;
        }

        initialized = true;
    }

    /// <summary>
    /// 스케줄러가 스폰 직후 출현 설정을 넣는다 (처치 보상 값)
    /// </summary>
    public void Setup(MidBossEntry _entry)
    {
        entry = _entry;
    }

    private void OnDisable()
    {
        if (true == isAirborne)
        {
            SetAirborne(false);
        }

        initialized = false;
        OnDespawned?.Invoke(this);
    }

    private void Update()
    {
        if (false == initialized || null == health || true == health.isDead || true == isAirborne)
        {
            return;
        }

        relocateTimer += Time.deltaTime;
        if (relocateTimer < relocateCheckInterval)
        {
            return;
        }
        relocateTimer = 0f;

        Camera cam = SideCamera;
        if (null == cam)
        {
            return;
        }

        Vector2 offset = transform.position - cam.transform.position;
        if (offset.sqrMagnitude > relocateDistance * relocateDistance)
        {
            Relocate();
        }
    }

    /// <summary>
    /// 플레이어 이동 방향 앞쪽 스폰 링 위로 옮긴다 (HP 유지)
    /// </summary>
    public void Relocate()
    {
        if (null == target)
        {
            return;
        }

        Vector2 pos = FindSpawnPoint(target, targetMovement, relocateRing.x, relocateRing.y, relocateSpread);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        OnRelocated?.Invoke(this, pos);
    }

    /// <summary>
    /// _target 의 이동 방향(ArrowMovemnt.LastMoveDirection, 없으면 오른쪽) 앞쪽 링 위의 무작위 점
    /// </summary>
    public static Vector2 FindSpawnPoint(Transform _target, ArrowMovemnt _movement, float _minRadius, float _maxRadius, float _spreadDeg)
    {
        Vector2 center = null != _target ? (Vector2)_target.position : Vector2.zero;
        Vector2 forward = null != _movement ? _movement.LastMoveDirection : Vector2.right;
        return BossMath.ForwardRingPoint(center, forward, _minRadius, _maxRadius, _spreadDeg, UnityEngine.Random.value, UnityEngine.Random.value);
    }

    /// <summary>
    /// 재사용(새 생애) 시작: 공중·받는 데미지 배율을 지우고 콜라이더·본체 표시를 프리팹 상태로 강제 복원한다
    /// (공중에서 죽은 뒤 재사용돼도 콜라이더가 꺼진 채 남지 않도록 사망 여부와 상관없이 켠다)
    /// </summary>
    private void ResetBody()
    {
        bool wasAirborne = isAirborne;
        isAirborne = false;

        if (null != colliders)
        {
            foreach (Collider2D c in colliders)
            {
                if (null != c)
                {
                    c.enabled = true;
                }
            }
        }

        if (null != bodyRenderers)
        {
            for (int i = 0; i < bodyRenderers.Length; ++i)
            {
                if (null != bodyRenderers[i])
                {
                    bodyRenderers[i].enabled = bodyRendererDefaults[i];
                }
            }
        }

        damageTakenFactors.Clear();
        ApplyDamageTakenFactors();

        if (true == wasAirborne)
        {
            OnAirborneChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// _source 출처의 받는 데미지 배율을 넣는다 (출처별로 곱해서 적용. 공중 0 이 있으면 0)
    /// </summary>
    public void SetDamageTakenFactor(object _source, float _factor)
    {
        if (null == _source)
        {
            return;
        }
        damageTakenFactors[_source] = Mathf.Max(0f, _factor);
        ApplyDamageTakenFactors();
    }

    /// <summary>
    /// _source 출처의 받는 데미지 배율을 뺀다
    /// </summary>
    public void ClearDamageTakenFactor(object _source)
    {
        if (null != _source && true == damageTakenFactors.Remove(_source))
        {
            ApplyDamageTakenFactors();
        }
    }

    /// <summary>
    /// 출처별 배율의 곱 (없으면 1)
    /// </summary>
    public float DamageTakenMultiplier
    {
        get
        {
            float m = 1f;
            foreach (float f in damageTakenFactors.Values)
            {
                m *= f;
            }
            return m;
        }
    }

    private void ApplyDamageTakenFactors()
    {
        if (null != health)
        {
            health.IncomingDamageMultiplier = DamageTakenMultiplier;
        }
    }

    /// <summary>
    /// 공중 상태 (도약): 콜라이더·본체 스프라이트를 끄고 데미지를 받지 않는다. 끄면 원래대로 (죽었으면 콜라이더는 꺼 둔다)
    /// </summary>
    public void SetAirborne(bool _airborne)
    {
        if (isAirborne == _airborne)
        {
            return;
        }
        isAirborne = _airborne;

        bool dead = null != health && true == health.isDead;
        if (null != colliders)
        {
            foreach (Collider2D c in colliders)
            {
                if (null != c)
                {
                    c.enabled = false == _airborne && false == dead;
                }
            }
        }

        if (null != bodyRenderers)
        {
            for (int i = 0; i < bodyRenderers.Length; ++i)
            {
                SpriteRenderer sr = bodyRenderers[i];
                if (null == sr)
                {
                    continue;
                }

                if (true == _airborne)
                {
                    bodyRendererStates[i] = sr.enabled;
                    sr.enabled = false;
                }
                else
                {
                    sr.enabled = bodyRendererStates[i];
                }
            }
        }

        if (true == _airborne)
        {
            SetDamageTakenFactor(AirborneKey, 0f);
        }
        else
        {
            ClearDamageTakenFactor(AirborneKey);
        }

        OnAirborneChanged?.Invoke(this, _airborne);
    }

    private void HandleDeath(GameObject _object)
    {
        if (false == initialized || true == defeated)
        {
            return;
        }
        defeated = true;

        float expTotal = null != entry ? entry.expTotal : defaultExpTotal;
        int gemCount = null != entry ? entry.gemCount : defaultGemCount;
        if (null != dropManager)
        {
            dropManager.DropExp(transform.position, expTotal, gemCount);
        }
        else
        {
            Debug.LogWarning($"[MidBoss] {name} : DropManager 가 없어 보석을 떨어뜨리지 못했습니다.", this);
        }

        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnBossDefeated?.Invoke(side, this);
        }

        OnDefeated?.Invoke(this);
    }
}
