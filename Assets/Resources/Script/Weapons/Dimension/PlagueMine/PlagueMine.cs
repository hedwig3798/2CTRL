using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 차원 무기: 역병 지뢰 (독 장판 + 지뢰)
/// - 쿨타임마다 화면(StageRect) 안 무작위 위치에 지뢰를 FinalFireCount 개 설치 (동시 최대 maxMines)
/// - 지뢰는 최근접 적(양 진영, 화면 거리)을 천천히 추적하다 닿으면 폭발: 반지름(AreaRadius) 안 적 데미지 + 폭발 중심으로 끌어당김 → 그 자리에 독 장판
/// - 끌어당김은 Knockback 이 있는(넉백 가능한) 적만, Knockback.resistance 만큼 줄여 Knockback.moveRoot 를 pullDuration 동안 옮긴다
///   (화면 공간과 진영 월드는 평행이동 관계라 화면 방향 그대로 진영 월드에 적용)
/// - 장판: 지속 시간 = FinalLifetime (lifetime 0 이면 defaultZoneDuration 에 Duration 적용), 틱 = zoneTickInterval × 기본/최종 공격 속도
/// - 수치 근거(목표 468 = 폭발 234 + 장판 234, 권장 프리팹 damage 234 · attackSpeed 0.5 · fireCount 2 → 초당 지뢰 1개):
///   폭발 234 × 1개/s = 234, 장판 틱 234 × 1/6 = 39 × 2틱/s × 3s × 1개/s = 234 (단일 대상 기준, WeaponTest DPS 미터로 실측 보정)
/// </summary>
public class PlagueMine
    : DimensionWeapon
{
    private struct Pull
    {
        public Transform root;
        public HealthSystem health;
        public Knockback knockback;
        // 시작 시점 Knockback 활성 세대. 바뀌면 풀 반환 후 재사용된 다른 몬스터로 본다
        public int generation;
        public Vector2 velocity;
        public float remaining;

        public bool IsValid => null != root && null != knockback && generation == knockback.Generation
            && null != health && false == health.isDead && true == health.isActiveAndEnabled
            && knockback.resistance < 1f;
    }

    [Header("prefab (비우면 렌더러 없는 빈 오브젝트로 동작)")]
    [SerializeField]
    private PlagueMineBomb minePrefab;
    [SerializeField]
    private PlagueZone zonePrefab;
    [Tooltip("폭발 연출 (EffectPool, 크기는 폭발 반지름). 프리팹의 렌더러 정렬 레이어는 attack")]
    [SerializeField]
    private PooledEffect explosionEffect;

    [Header("mine")]
    [Tooltip("지뢰 추적 속도 (화면 공간 단위/초)")]
    [Min(0f)]
    [SerializeField]
    private float chaseSpeed = 1.2f;
    [Tooltip("적 가장자리가 이 거리 안에 들어오면 폭발")]
    [Min(0f)]
    [SerializeField]
    private float triggerRadius = 0.35f;
    [Tooltip("설치 후 폭발 가능까지의 시간 (초)")]
    [Min(0f)]
    [SerializeField]
    private float armDelay = 0.3f;
    [Tooltip("터지지 않은 지뢰가 사라지는 시간 (초). 0 이면 사라지지 않음")]
    [Min(0f)]
    [SerializeField]
    private float mineLifetime = 15f;
    [Tooltip("동시에 있을 수 있는 지뢰 수 (넘으면 설치하지 않음)")]
    [Min(1)]
    [SerializeField]
    private int maxMines = 12;
    [Tooltip("설치 위치가 화면 가장자리에서 떨어지는 거리")]
    [Min(0f)]
    [SerializeField]
    private float spawnMargin = 0.5f;

    [Header("explosion")]
    [Tooltip("폭발 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float explosionDamageRate = 1f;
    [Tooltip("무기 areaRadius 가 0 일 때 쓰는 폭발 반지름")]
    [Min(0f)]
    [SerializeField]
    private float defaultExplosionRadius = 1.5f;
    [Tooltip("끌어당기는 최대 거리")]
    [Min(0f)]
    [SerializeField]
    private float pullDistance = 1.2f;
    [Tooltip("폭발 중심과 이 거리 이상 떨어진 곳에서 멈춘다")]
    [Min(0f)]
    [SerializeField]
    private float pullStopGap = 0.3f;
    [Tooltip("끌어당기는 데 걸리는 시간 (초). 0 이면 즉시")]
    [Min(0f)]
    [SerializeField]
    private float pullDuration = 0.2f;

    [Header("zone")]
    [Tooltip("독 장판 반지름")]
    [Min(0f)]
    [SerializeField]
    private float zoneRadius = 1.5f;
    [Tooltip("독 장판 기본 틱 간격 (초). 실제 = 이 값 × 기본 공격 속도 / 최종 공격 속도")]
    [Min(0.05f)]
    [SerializeField]
    private float zoneTickInterval = 0.5f;
    [Tooltip("독 장판 틱 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float zoneTickDamageRate = 1f / 6f;
    [Tooltip("무기 lifetime 이 0 일 때 쓰는 장판 지속 시간 (초)")]
    [Min(0f)]
    [SerializeField]
    private float defaultZoneDuration = 3f;

    private readonly List<StageTarget> hitBuffer = new List<StageTarget>();
    private readonly List<Pull> pulls = new List<Pull>();
    private ObjectPool<PlagueMineBomb> minePool;
    private ObjectPool<PlagueZone> zonePool;

    public float ChaseSpeed => chaseSpeed;
    public float TriggerRadius => triggerRadius;
    public int ActiveMineCount => null != minePool ? minePool.CountActive : 0;
    public int ActiveZoneCount => null != zonePool ? zonePool.CountActive : 0;
    public float ExplosionRadius => AreaRadius > 0f ? AreaRadius : defaultExplosionRadius;
    public float ExplosionDamage => FinalDamage * explosionDamageRate;
    public float ZoneRadius => zoneRadius;
    public float ZoneTickDamage => FinalDamage * zoneTickDamageRate;
    public float ZoneTickInterval => DimensionStatMath.ScaledTickInterval(zoneTickInterval, AttackSpeed, FinalAttackSpeed);
    public float ZoneDuration => Lifetime > 0f ? FinalLifetime : ApplyDuration(defaultZoneDuration, defaultZoneDuration);

    private void Awake()
    {
        minePool = new ObjectPool<PlagueMineBomb>(CreateMine, null, OnRelease, OnDestroyPooled, false, 8, 64);
        zonePool = new ObjectPool<PlagueZone>(CreateZone, null, OnRelease, OnDestroyPooled, false, 8, 64);
    }

    protected override void Fire()
    {
        StageSpace space = StageSpace.Instance;
        if (null == space || false == space.IsReady)
        {
            return;
        }

        Rect rect = space.StageRect;
        int count = FinalFireCount;
        for (int i = 0; i < count; ++i)
        {
            if (minePool.CountActive >= maxMines)
            {
                return;
            }
            Vector2 pos = PlagueMineMath.PointInRect(rect, spawnMargin, Random.value, Random.value);
            minePool.Get().Activate(this, pos, mineLifetime, armDelay);
        }
    }

    /// <summary>
    /// 지뢰 폭발: 데미지 → 끌어당김 → 연출 → 독 장판. 지뢰는 풀로 돌려보낸다
    /// </summary>
    public void Explode(PlagueMineBomb _mine, Vector2 _center)
    {
        float radius = ExplosionRadius;
        StageQuadTree tree = StageSpace.Targets;
        if (null != tree)
        {
            float damage = ExplosionDamage;
            tree.QueryCircle(_center, radius, hitBuffer);
            for (int i = 0; i < hitBuffer.Count; ++i)
            {
                StageTarget target = hitBuffer[i];
                if (false == target.IsAlive)
                {
                    continue;
                }
                StageDamage.Apply(target, damage, this);
                if (true == target.IsAlive)
                {
                    AddPull(target, _center);
                }
            }
            hitBuffer.Clear();
        }

        int layer = StageSpace.StageLayer >= 0 ? StageSpace.StageLayer : gameObject.layer;
        EffectPool.Spawn(explosionEffect, _center, radius, layer);

        zonePool.Get().Activate(this, _center, zoneRadius, ZoneDuration);
        ReleaseMine(_mine);
    }

    public void ReleaseMine(PlagueMineBomb _mine)
    {
        if (null != _mine && true == _mine.gameObject.activeSelf)
        {
            minePool.Release(_mine);
        }
    }

    public void ReleaseZone(PlagueZone _zone)
    {
        if (null != _zone && true == _zone.gameObject.activeSelf)
        {
            zonePool.Release(_zone);
        }
    }

    protected override void Update()
    {
        base.Update();
        UpdatePulls(Time.deltaTime);
    }

    #region 끌어당김
    private void AddPull(in StageTarget _target, Vector2 _center)
    {
        if (false == _target.health.TryGetComponent(out Knockback knockback) || knockback.resistance >= 1f)
        {
            return;
        }

        Transform root = knockback.ResolvedRoot;
        Vector2 delta = PlagueMineMath.PullDelta(_target.stagePos, _center, pullDistance * (1f - knockback.resistance), pullStopGap);
        if (delta.sqrMagnitude < 0.000001f)
        {
            return;
        }

        if (pullDuration <= 0f)
        {
            root.position += (Vector3)delta;
            return;
        }

        Pull pull = new Pull
        {
            root = root,
            health = _target.health,
            knockback = knockback,
            generation = knockback.Generation,
            velocity = delta / pullDuration,
            remaining = pullDuration,
        };
        for (int i = 0; i < pulls.Count; ++i)
        {
            if (pulls[i].root == root)
            {
                pulls[i] = pull;
                return;
            }
        }
        pulls.Add(pull);
    }

    private void UpdatePulls(float _dt)
    {
        if (_dt <= 0f)
        {
            return;
        }

        for (int i = pulls.Count - 1; i >= 0; --i)
        {
            Pull pull = pulls[i];
            // 죽었거나, 꺼졌거나, 풀 반환 후 다른 몬스터로 재사용됐으면 그만 끈다
            if (false == pull.IsValid)
            {
                pulls.RemoveAt(i);
                continue;
            }

            float step = Mathf.Min(_dt, pull.remaining);
            pull.root.position += (Vector3)(pull.velocity * step);
            pull.remaining -= step;
            if (pull.remaining <= 0f)
            {
                pulls.RemoveAt(i);
            }
            else
            {
                pulls[i] = pull;
            }
        }
    }
    #endregion

    #region 풀
    private PlagueMineBomb CreateMine()
    {
        PlagueMineBomb mine = null != minePrefab
            ? Instantiate(minePrefab, transform)
            : new GameObject(nameof(PlagueMineBomb)).AddComponent<PlagueMineBomb>();
        SetupPooled(mine.gameObject);
        return mine;
    }

    private PlagueZone CreateZone()
    {
        PlagueZone zone = null != zonePrefab
            ? Instantiate(zonePrefab, transform)
            : new GameObject(nameof(PlagueZone)).AddComponent<PlagueZone>();
        SetupPooled(zone.gameObject);
        return zone;
    }

    private void SetupPooled(GameObject _go)
    {
        if (_go.transform.parent != transform)
        {
            _go.transform.SetParent(transform, false);
        }
        _go.SetActive(false);
        if (StageSpace.StageLayer >= 0)
        {
            LayerUtils.SetLayer(_go, StageSpace.StageLayer);
        }
    }

    private static void OnRelease(Component _item)
    {
        if (null != _item)
        {
            _item.gameObject.SetActive(false);
        }
    }

    private static void OnDestroyPooled(Component _item)
    {
        if (null != _item)
        {
            Destroy(_item.gameObject);
        }
    }
    #endregion

    private void OnDestroy()
    {
        pulls.Clear();
        minePool?.Dispose();
        zonePool?.Dispose();
    }
}
