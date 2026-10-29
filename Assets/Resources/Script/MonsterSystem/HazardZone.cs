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
    /// _position 에 장판을 펼친다. 첫 틱은 바로 들어간다
    /// </summary>
    public void Activate(
        Vector2 _position
        , float _radius
        , float _duration
        , float _tickInterval
        , float _damage
        , LayerMask _targetLayer
        , DamagePipeline _attacker = null)
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
        RemainRate = 1f;
        active = true;

        if (false == gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void OnEnable()
    {
        returningToPool = false;
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
    /// </summary>
    public static HazardZone Spawn(
        HazardZone _prefab
        , Vector2 _position
        , float _radius
        , float _duration
        , float _tickInterval
        , float _damage
        , LayerMask _targetLayer
        , DamagePipeline _attacker = null)
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

        zone.Activate(_position, _radius, _duration, _tickInterval, _damage, _targetLayer, _attacker);
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
