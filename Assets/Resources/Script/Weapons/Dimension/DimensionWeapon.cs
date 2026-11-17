using UnityEngine;

/// <summary>
/// 차원 무기 베이스 (두 차원의 최대 단계 무기 2종을 합성해 만드는 화면 공용 무기)
/// - Weapon 을 상속하지 않는다 (소유자가 둘이고 물리 탐색을 쓰지 않음)
/// - 화면 공간(StageSpace) 중앙에 1개 생성되고, 레이어는 Stage (자식 포함)
/// - 수치는 양쪽 PlayerStats 합산: Final = (무기 값 + ΣAdd_L + ΣAdd_R) × max(minMultiplier, 1 + ΣMul_L + ΣMul_R)
///   statTags 에 없는 수치는 무기 값 그대로. 양쪽 플레이어가 모두 없으면 무기 값 그대로
/// - 쿨타임 루프는 Weapon(Repeat) 과 같이 Time.deltaTime 으로 흐른다 (timeScale 0 이면 멈춤)
/// - 업그레이드 없음 (완성형)
///
/// 새 차원 무기 추가 방법
///   1. DimensionWeapon 을 상속해 Fire 를 구현한다 (필요하면 OnActivated / Update 를 재정의)
///   2. 결과 프리팹을 만들고 DimensionWeaponData.resultPrefab 에 연결한다
/// </summary>
public abstract class DimensionWeapon
    : MonoBehaviour
{
    [Header("weapon stat (양쪽 플레이어 스텟이 합산되어 더해진다)")]
    [Tooltip("기본 데미지. 실제 데미지 = (damage + ΣAdd_L + ΣAdd_R) × 합산 배율")]
    [SerializeField]
    private float damage = 10f;

    [Tooltip("초당 공격 횟수 (0 이하면 쿨타임 루프로 Fire 하지 않음)")]
    [SerializeField]
    private float attackSpeed = 1f;

    [Tooltip("탐색 범위 (화면 공간 단위)")]
    [Min(0f)]
    [SerializeField]
    private float range = 5f;

    [Tooltip("한 번에 발사하는 수 / 유지하는 수 (0 이하면 0)")]
    [SerializeField]
    private int fireCount = 1;

    [Tooltip("지속 시간 (초). 0 이면 지속 시간을 쓰지 않음")]
    [Min(0f)]
    [SerializeField]
    private float lifetime;

    [Tooltip("범위 공격 반지름 (플레이어 스텟 미적용, Weapon.areaRadius 와 같음)")]
    [Min(0f)]
    [SerializeField]
    private float areaRadius;

    [Header("weapon tag")]
    [Tooltip("플레이어 버프 / 디버프를 받을 수치. 태그가 없는 수치는 무기 값 그대로")]
    [SerializeField]
    private WeaponStatTag statTags = WeaponStatTag.All;

    [Tooltip("플레이어 스텟이 줄어도 공격이 완전히 멈추지 않도록 하는 최소 공격 속도")]
    [Min(0.01f)]
    [SerializeField]
    private float minAttackSpeed = 0.1f;

    private DimensionWeaponData data;
    private bool isActivated;
    private bool isPlaced;
    private bool hasWarnedLayer;
    private float timer;

    // 양쪽 플레이어 참조 (없을 때만 PlayerStats.Find)
    private PlayerStats leftStats;
    private PlayerStats rightStats;

    // ApplyCombinedStat 용 스텟 캐시 (Time.frameCount 기준 프레임당 1회, ValueType 별로 처음 쓸 때 채운다)
    private static readonly int ValueTypeCount = GetValueTypeCount();
    private readonly float[] cachedAddLeft = new float[ValueTypeCount];
    private readonly float[] cachedAddRight = new float[ValueTypeCount];
    private readonly float[] cachedMulLeft = new float[ValueTypeCount];
    private readonly float[] cachedMulRight = new float[ValueTypeCount];
    private readonly bool[] cachedValid = new bool[ValueTypeCount];
    private int cachedFrame = -1;
    private bool cachedHasPlayer;
    private float cachedMinMultiplier;

    /// <summary>
    /// 합성법 (Initialize 전이면 null)
    /// </summary>
    public DimensionWeaponData Data => data;

    /// <summary>
    /// Initialize 로 켜졌는지
    /// </summary>
    public bool IsActivated => isActivated;

    /// <summary>
    /// 화면 공간 중앙에 배치했는지 (StageSpace 준비 전이면 준비될 때까지 미룬다)
    /// </summary>
    public bool IsPlaced => isPlaced;

    public float Damage => damage;
    public float AttackSpeed => attackSpeed;
    public float Range => range;
    public int FireCount => fireCount;
    public float Lifetime => lifetime;
    public float AreaRadius => areaRadius;
    public WeaponStatTag StatTags => statTags;

    #region Final Stat
    /// <summary>
    /// 합산 데미지 (0 미만은 0)
    /// </summary>
    public float FinalDamage => Mathf.Max(0f, ApplyCombinedStat(damage, ValueType.Damage));

    /// <summary>
    /// 합산 공격 속도. attackSpeed 가 0 이하면 0, AttackSpeed 태그가 없으면 무기 값 그대로, 아니면 최소 minAttackSpeed
    /// </summary>
    public float FinalAttackSpeed
    {
        get
        {
            if (attackSpeed <= 0f)
            {
                return 0f;
            }
            if (false == statTags.Has(ValueType.AttackSpeed))
            {
                return attackSpeed;
            }
            return Mathf.Max(minAttackSpeed, ApplyCombinedStat(attackSpeed, ValueType.AttackSpeed));
        }
    }

    /// <summary>
    /// 공격 간격 (초). 공격하지 않으면 0
    /// </summary>
    public float FinalCoolTime
    {
        get
        {
            float speed = FinalAttackSpeed;
            return speed > 0f ? 1f / speed : 0f;
        }
    }

    /// <summary>
    /// 다음 Fire 까지 남은 시간 (초)
    /// </summary>
    public float RemainingCoolTime => Mathf.Max(0f, FinalCoolTime - timer);

    /// <summary>
    /// 합산 범위 (0 미만은 0)
    /// </summary>
    public float FinalRange => Mathf.Max(0f, ApplyCombinedStat(range, ValueType.Range));

    /// <summary>
    /// 합산 발사 수. fireCount 가 0 이하면 0, 아니면 최소 1
    /// </summary>
    public int FinalFireCount
    {
        get
        {
            if (fireCount <= 0)
            {
                return 0;
            }
            return DimensionStatMath.RoundCount(ApplyCombinedStat(fireCount, ValueType.ProjectileCount));
        }
    }

    /// <summary>
    /// 합산 지속 시간. lifetime 이 0 이하면 0, 하한은 lifetime × Weapon.MinDurationRate
    /// </summary>
    public float FinalLifetime => lifetime <= 0f ? 0f : ApplyDuration(lifetime, lifetime);

    /// <summary>
    /// 무기 필드가 아닌 지속 시간에 양쪽 Duration 을 적용한다 (태그 없으면 그대로)
    /// _initialSeconds 가 0 보다 크면 그 × Weapon.MinDurationRate 미만으로 내려가지 않는다
    /// </summary>
    public float ApplyDuration(float _seconds, float _initialSeconds = 0f)
    {
        if (false == statTags.Has(ValueType.Duration))
        {
            return _seconds;
        }
        float value = Mathf.Max(0f, ApplyCombinedStat(_seconds, ValueType.Duration));
        return _initialSeconds > 0f ? Mathf.Max(_initialSeconds * Weapon.MinDurationRate, value) : value;
    }

    /// <summary>
    /// _value 에 양쪽 플레이어 스텟을 합산 적용한다
    /// - statTags 에 _type 이 없거나 양쪽 플레이어가 모두 없으면 _value 그대로
    /// - 배율 하한은 있는 플레이어의 minMultiplier 중 큰 값
    /// </summary>
    public float ApplyCombinedStat(float _value, ValueType _type)
    {
        if (false == statTags.Has(_type))
        {
            return _value;
        }

        RefreshStatCache();
        if (false == cachedHasPlayer)
        {
            return _value;
        }

        int i = (int)_type;
        if (i < 0 || i >= ValueTypeCount)
        {
            return _value;
        }

        if (false == cachedValid[i])
        {
            cachedAddLeft[i] = null != leftStats ? leftStats.GetAdd(_type) : 0f;
            cachedAddRight[i] = null != rightStats ? rightStats.GetAdd(_type) : 0f;
            cachedMulLeft[i] = null != leftStats ? leftStats.GetMultiply(_type) : 0f;
            cachedMulRight[i] = null != rightStats ? rightStats.GetMultiply(_type) : 0f;
            cachedValid[i] = true;
        }

        return DimensionStatMath.Apply(
            _value,
            cachedAddLeft[i],
            cachedAddRight[i],
            cachedMulLeft[i],
            cachedMulRight[i],
            cachedMinMultiplier);
    }

    /// <summary>
    /// 프레임이 바뀌었으면 플레이어 참조(없을 때만 Find) 와 배율 하한을 다시 잡고 ValueType 별 캐시를 비운다
    /// </summary>
    private void RefreshStatCache()
    {
        int frame = Time.frameCount;
        if (frame == cachedFrame)
        {
            return;
        }
        cachedFrame = frame;

        if (null == leftStats)
        {
            leftStats = PlayerStats.Find(PlayerSide.Left);
        }
        if (null == rightStats)
        {
            rightStats = PlayerStats.Find(PlayerSide.Right);
        }

        cachedHasPlayer = null != leftStats || null != rightStats;
        cachedMinMultiplier = Mathf.Max(
            null != leftStats ? leftStats.MinMultiplier : 0f,
            null != rightStats ? rightStats.MinMultiplier : 0f);
        System.Array.Clear(cachedValid, 0, cachedValid.Length);
    }

    private static int GetValueTypeCount()
    {
        int max = 0;
        foreach (ValueType type in System.Enum.GetValues(typeof(ValueType)))
        {
            max = Mathf.Max(max, (int)type);
        }
        return max + 1;
    }
    #endregion

    /// <summary>
    /// 합성 결과로 켠다. 화면 공간 중앙(StageCenter) 에 두고 레이어를 Stage 로 맞춘 뒤 OnActivated 를 부른다
    /// - StageSpace 가 준비 전이면 준비될 때까지 배치를 미루고, 배치 전에는 Fire 하지 않는다
    /// </summary>
    public void Initialize(DimensionWeaponData _data)
    {
        data = _data;
        if (null != data)
        {
            name = data.name;
        }

        ApplyStageLayer();
        TryPlace();

        timer = 0f;
        isActivated = true;
        OnActivated();
    }

    /// <summary>
    /// 공격 1회. 쿨타임(FinalCoolTime) 마다 호출된다
    /// </summary>
    protected abstract void Fire();

    /// <summary>
    /// Initialize 직후 1회 (유지형 연출 생성 등)
    /// </summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>
    /// _side 플레이어의 화면 공간 좌표. 플레이어나 StageSpace 가 없으면 false
    /// </summary>
    public bool TryGetPlayerStagePos(PlayerSide _side, out Vector2 _stagePos)
    {
        StageSpace space = StageSpace.Instance;
        PlayerStats player = PlayerStats.Find(_side);
        if (null == space || false == space.IsReady || null == player)
        {
            _stagePos = transform.position;
            return false;
        }

        _stagePos = space.WorldToStage(_side, player.transform.position);
        return true;
    }

    /// <summary>
    /// _side 플레이어의 화면 공간 좌표 (찾지 못하면 이 무기의 위치)
    /// </summary>
    public Vector2 GetPlayerStagePos(PlayerSide _side)
    {
        TryGetPlayerStagePos(_side, out Vector2 pos);
        return pos;
    }

    protected virtual void Update()
    {
        if (false == isActivated)
        {
            return;
        }

        if (false == isPlaced && false == TryPlace())
        {
            return;
        }

        float coolTime = FinalCoolTime;
        if (coolTime <= 0f)
        {
            return;
        }

        timer += Time.deltaTime;
        if (timer >= coolTime)
        {
            timer -= coolTime;
            Fire();
        }
    }

    /// <summary>
    /// StageSpace 가 준비됐으면 화면 중앙에 둔다
    /// </summary>
    private bool TryPlace()
    {
        StageSpace space = StageSpace.Instance;
        if (null == space || false == space.IsReady)
        {
            return false;
        }

        Vector2 center = space.StageCenter;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        isPlaced = true;
        return true;
    }

    private void ApplyStageLayer()
    {
        int layer = StageSpace.StageLayer;
        if (layer < 0)
        {
            if (false == hasWarnedLayer)
            {
                hasWarnedLayer = true;
                Debug.LogWarning($"[DimensionWeapon] Stage 레이어가 없어 레이어를 바꾸지 않습니다. ({name})", this);
            }
            return;
        }
        LayerUtils.SetLayer(gameObject, layer);
    }
}
