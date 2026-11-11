using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 몬스터가 남기는 장판 (Projectile/Weapon 에 의존하지 않는 경량 컴포넌트)
/// tickInterval 마다 반경 안의 대상에게 한 번씩 데미지를 주고, 수명이 끝나면 풀로 돌아간다
/// HazardZone.Spawn 으로 꺼낸다
/// </summary>
public class HazardZone
    : MonoBehaviour
{
    [Tooltip("크기 1 일 때의 반지름. 크기 = radius / unitRadius")]
    [Min(0.01f)]
    [SerializeField]
    private float unitRadius = 0.5f;

    [Tooltip("둔화 장판일 때 둔화 지속 = tickInterval + 이 값 (틱 사이에 풀리지 않도록)")]
    [Min(0f)]
    [SerializeField]
    private float slowGrace = 0.15f;

    private IObjectPool<HazardZone> pool;
    private bool returningToPool;
    private Vector3 baseScale = Vector3.one;
    private bool hasBaseScale;

    private float radius;
    private float remainTime;
    private float tickInterval;
    private float tickTimer;
    private float damage;
    private LayerMask targetLayer;
    private DamagePipeline attacker;
    private float slowRate;
    private bool active;

    /// <summary>
    /// 남은 수명 비율 (1 → 0). 연출용
    /// </summary>
    public float RemainRate { get; private set; }

    private float duration;

    public void SetPool(IObjectPool<HazardZone> _pool)
    {
        pool = _pool;
    }

    /// <summary>
    /// 이동 둔화 비율 (0 이면 둔화 없음)
    /// </summary>
    public float SlowRate => slowRate;

    /// <summary>
    /// _position 에 장판을 펼친다. 첫 틱은 바로 들어간다
    /// _slowRate 가 0 보다 크면 틱마다 장판 안 플레이어에게 임시 둔화(tickInterval + slowGrace 초)를 갱신해 건다
    /// </summary>
    public void Activate(
        Vector2 _position
        , float _radius
        , float _duration
        , float _tickInterval
        , float _damage
        , LayerMask _targetLayer
        , DamagePipeline _attacker = null
        , float _slowRate = 0f)
    {
        // 비활성으로 생성돼 Awake 전에 불릴 수 있으므로 원본 크기는 처음 한 번 여기서 기록한다
        if (false == hasBaseScale)
        {
            baseScale = transform.localScale;
            hasBaseScale = true;
        }

        transform.position = new Vector3(_position.x, _position.y, transform.position.z);
        transform.localScale = _radius > 0f ? baseScale * (_radius / unitRadius) : baseScale;

        radius = _radius;
        duration = Mathf.Max(0.01f, _duration);
        remainTime = duration;
        tickInterval = Mathf.Max(0.01f, _tickInterval);
        tickTimer = 0f;
        damage = _damage;
        targetLayer = _targetLayer;
        attacker = _attacker;
        slowRate = Mathf.Clamp01(_slowRate);
        RemainRate = 1f;
        active = true;

        if (false == gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    // 활성 장판 전체 (최종 보스 진입 시 일괄 회수)
    private static readonly HashSet<HazardZone> ActiveZones = new HashSet<HazardZone>();
    private static readonly List<HazardZone> deactivateBuffer = new List<HazardZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveZones()
    {
        ActiveZones.Clear();
        deactivateBuffer.Clear();
    }

    /// <summary>
    /// 활성 장판을 모두 끈다 (OnDisable 에서 풀로 돌아간다). 끈 개수
    /// </summary>
    public static int DeactivateAll()
    {
        deactivateBuffer.Clear();
        deactivateBuffer.AddRange(ActiveZones);
        int count = 0;
        foreach (HazardZone zone in deactivateBuffer)
        {
            if (null != zone && true == zone.gameObject.activeSelf)
            {
                zone.gameObject.SetActive(false);
                ++count;
            }
        }
        deactivateBuffer.Clear();
        return count;
    }

    private void OnEnable()
    {
        returningToPool = false;
        ActiveZones.Add(this);
    }

    private void Update()
    {
        if (false == active)
        {
            return;
        }

        float dt = Time.deltaTime;

        tickTimer -= dt;
        if (tickTimer <= 0f)
        {
            tickTimer += tickInterval;
            MonsterAreaDamage.Apply(transform.position, radius, targetLayer, damage, 0f, attacker);
            if (slowRate > 0f)
            {
                MonsterAreaDamage.ApplyPlayerSlow(transform.position, radius, targetLayer, slowRate, tickInterval + slowGrace);
            }
        }

        remainTime -= dt;
        RemainRate = Mathf.Clamp01(remainTime / duration);
        if (remainTime <= 0f)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        ActiveZones.Remove(this);
        active = false;
        attacker = null;

        if (returningToPool || null == pool)
        {
            return;
        }

        returningToPool = true;
        pool.Release(this);
    }

    #region Pool
    private static Dictionary<HazardZone, IObjectPool<HazardZone>> pools = new Dictionary<HazardZone, IObjectPool<HazardZone>>();

    /// <summary>
    /// _prefab 장판을 풀에서 꺼내 펼친다. _prefab 이 없으면 null
    /// _layer 가 0 이상이면 장판 전체 레이어를 맞춘다 (분할 카메라 culling 때문에 낸 몬스터의 레이어를 넘긴다)
    /// </summary>
    public static HazardZone Spawn(
        HazardZone _prefab
        , Vector2 _position
        , float _radius
        , float _duration
        , float _tickInterval
        , float _damage
        , LayerMask _targetLayer
        , DamagePipeline _attacker = null
        , float _slowRate = 0f
        , int _layer = -1)
    {
        if (null == _prefab)
        {
            return null;
        }

        if (false == pools.TryGetValue(_prefab, out IObjectPool<HazardZone> p))
        {
            p = CreatePool(_prefab);
            pools.Add(_prefab, p);
        }

        // 씬이 바뀌어 파괴된 오브젝트가 풀에 남아 있으면 건너뛴다
        HazardZone zone = p.Get();
        while (null == zone)
        {
            zone = p.Get();
        }

        if (0 <= _layer && zone.gameObject.layer != _layer)
        {
            LayerUtils.SetLayer(zone.gameObject, _layer);
        }

        zone.Activate(_position, _radius, _duration, _tickInterval, _damage, _targetLayer, _attacker, _slowRate);
        return zone;
    }

    private static IObjectPool<HazardZone> CreatePool(HazardZone _prefab)
    {
        IObjectPool<HazardZone> p = null;
        p = new ObjectPool<HazardZone>
            (
                createFunc: () =>
                {
                    HazardZone created = Object.Instantiate(_prefab);
                    created.gameObject.SetActive(false);
                    created.SetPool(p);
                    return created;
                }
                , actionOnGet: null
                , actionOnRelease: null
                , actionOnDestroy: (z) =>
                {
                    if (null != z)
                    {
                        Object.Destroy(z.gameObject);
                    }
                }
                , collectionCheck: false
                , defaultCapacity: 16
                , maxSize: 100
            );
        return p;
    }
    #endregion
}
