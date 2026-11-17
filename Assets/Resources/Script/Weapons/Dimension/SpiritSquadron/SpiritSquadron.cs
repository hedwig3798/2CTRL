using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 차원 무기 정령 편대 (정령 + 드론)
/// - 정령 : 최대 FinalFireCount 마리 (Capped). Fire 마다 모자라면 spawnPerFire 마리를 화면 중앙에 만든다.
///   쿼드트리 최근접 적(정령 위치 기준, FinalRange 이내)을 추격하고, 닿아 있으면 spiritTickInterval 마다 타격. spiritHitCount 번 타격하면 소멸
/// - 드론 : 정령 수 × dronesPerSpirit 기. 정령 주위를 공전하고(정령이 없으면 화면 중앙), Fire 마다
///   정령들의 현재 대상이 아닌 최근접 적(드론 위치 기준, FinalRange 이내)에게 탄 1발. 탄은 직선으로 날아가 처음 닿은 적 1명 타격
/// - 대상이 없으면 정령·드론 모두 화면 중앙(StageCenter) 근처에서 대기
/// - 모든 생성 오브젝트는 이 무기의 자식(레이어 = 무기 레이어 = Stage), 렌더러 정렬 레이어 sortingLayerName. Time.deltaTime 기준 (timeScale 0 이면 멈춤)
/// 수치 근거 (프리팹 권장 damage 39, attackSpeed 2, fireCount 3): 정령 3 × 39 / 틱 0.5 = 234 + 드론 3 × 39 × 1.0 × 2/초 × 1타 = 234 → 468
/// </summary>
public class SpiritSquadron
    : DimensionWeapon
{
    #region 내부 상태
    private class Spirit
    {
        public GameObject go;
        public Transform tr;
        public SpriteRenderer sr;
        public HealthSystem target;
        public float tickTimer;
        public int hitsLeft;
        public float phase;
    }

    private class Drone
    {
        public GameObject go;
        public Transform tr;
        public float angle;
    }

    private class Bullet
    {
        public GameObject go;
        public Transform tr;
        public Vector2 dir;
        public float travelLeft;
    }
    #endregion

    [Header("spirit")]
    [Tooltip("Fire 1회에 새로 만드는 정령 수 (살아 있는 정령이 FinalFireCount 미만일 때만)")]
    [Min(1)]
    [SerializeField]
    private int spawnPerFire = 1;

    [Tooltip("정령 1마리가 소멸 전까지 타격하는 횟수")]
    [Min(1)]
    [SerializeField]
    private int spiritHitCount = 20;

    [Tooltip("정령 틱 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float spiritDamageRate = 1f;

    [Tooltip("정령이 닿아 있는 동안 타격하는 간격 (초, 공격 속도와 무관)")]
    [Min(0.01f)]
    [SerializeField]
    private float spiritTickInterval = 0.5f;

    [Tooltip("정령 추격 속도 (화면 단위/초)")]
    [Min(0f)]
    [SerializeField]
    private float spiritChaseSpeed = 7f;

    [Tooltip("대상 표면까지 거리가 이 값 이하면 닿은 것으로 본다")]
    [Min(0f)]
    [SerializeField]
    private float spiritContactRadius = 0.35f;

    [Tooltip("대기 중 화면 중앙에서 떨어져 떠다니는 거리")]
    [Min(0f)]
    [SerializeField]
    private float spiritIdleRadius = 1.2f;

    [Tooltip("대기 중 중앙을 도는 속도 (도/초)")]
    [SerializeField]
    private float spiritIdleOrbitSpeed = 60f;

    [Tooltip("대기 위치를 따라가는 민첩도 (클수록 바로 붙는다)")]
    [Min(0f)]
    [SerializeField]
    private float spiritIdleSharpness = 5f;

    [Header("drone")]
    [Tooltip("정령 1마리당 드론 수 (드론 수 = FinalFireCount × 이 값)")]
    [Min(0)]
    [SerializeField]
    private int dronesPerSpirit = 1;

    [Tooltip("드론 탄 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float droneDamageRate = 1f;

    [Tooltip("드론 공전 반지름")]
    [Min(0f)]
    [SerializeField]
    private float droneOrbitRadius = 0.8f;

    [Tooltip("드론 공전 속도 (도/초)")]
    [SerializeField]
    private float droneOrbitSpeed = 180f;

    [Tooltip("드론이 공전 위치를 따라가는 민첩도")]
    [Min(0f)]
    [SerializeField]
    private float droneFollowSharpness = 10f;

    [Tooltip("정령의 대상밖에 없을 때 드론도 그 대상을 쏠지 (끄면 쏘지 않는다)")]
    [SerializeField]
    private bool droneFallbackToSpiritTarget;

    [Tooltip("드론 탄 속도 (화면 단위/초)")]
    [Min(0.01f)]
    [SerializeField]
    private float bulletSpeed = 14f;

    [Tooltip("드론 탄 판정 반지름")]
    [Min(0f)]
    [SerializeField]
    private float bulletRadius = 0.15f;

    [Tooltip("드론 탄 최대 비행 거리 = FinalRange + 이 값")]
    [Min(0f)]
    [SerializeField]
    private float bulletExtraTravel = 2f;

    [Header("visual (비워두면 렌더러 없이 동작)")]
    [Tooltip("정령 외형 프리팹 (있으면 sprite 대신 사용)")]
    [SerializeField]
    private GameObject spiritPrefab;
    [SerializeField]
    private Sprite spiritSprite;
    [SerializeField]
    private Color spiritColor = Color.white;
    [Min(0f)]
    [SerializeField]
    private float spiritScale = 1f;

    [Tooltip("드론 외형 프리팹 (있으면 sprite 대신 사용)")]
    [SerializeField]
    private GameObject dronePrefab;
    [SerializeField]
    private Sprite droneSprite;
    [SerializeField]
    private Color droneColor = Color.white;
    [Min(0f)]
    [SerializeField]
    private float droneScale = 1f;

    [Tooltip("드론 탄 외형 프리팹 (있으면 sprite 대신 사용)")]
    [SerializeField]
    private GameObject bulletPrefab;
    [SerializeField]
    private Sprite bulletSprite;
    [SerializeField]
    private Color bulletColor = Color.white;
    [Min(0f)]
    [SerializeField]
    private float bulletScale = 1f;

    [Tooltip("생성 렌더러 정렬 레이어 (Default 면 바닥에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";
    [SerializeField]
    private int sortingOrder = 10;

    [Tooltip("왼쪽으로 움직일 때 정령 flipX 를 켠다 (스프라이트가 오른쪽을 보는 기준)")]
    [SerializeField]
    private bool flipSpiritByDirection = true;

    private readonly List<Spirit> spirits = new List<Spirit>();
    private readonly List<Drone> drones = new List<Drone>();
    private readonly List<Bullet> bullets = new List<Bullet>();
    private readonly List<HealthSystem> spiritTargets = new List<HealthSystem>();
    private readonly List<StageTarget> hitBuffer = new List<StageTarget>();

    private ObjectPool<Spirit> spiritPool;
    private ObjectPool<Drone> dronePool;
    private ObjectPool<Bullet> bulletPool;
    private Predicate<StageTarget> aliveFilter;
    private Predicate<StageTarget> droneFilter;
    private float idleAngle;
    private int spiritSerial;

    public int SpiritCount => spirits.Count;
    public int DroneCount => drones.Count;
    public int BulletCount => bullets.Count;

    #region 순수 로직 (테스트용)
    /// <summary>
    /// Capped: 살아 있는 수가 최대보다 적을 때 이번 Fire 에 만들 수 (0 ~ _perFire)
    /// </summary>
    public static int GetSpawnCount(int _alive, int _max, int _perFire)
    {
        return Mathf.Clamp(_max - _alive, 0, Mathf.Max(0, _perFire));
    }

    /// <summary>
    /// 드론 수 = 정령 최대 수 × 정령당 드론 수
    /// </summary>
    public static int GetDroneCount(int _spiritMax, int _perSpirit)
    {
        return Mathf.Max(0, _spiritMax) * Mathf.Max(0, _perSpirit);
    }

    /// <summary>
    /// _droneIndex 드론이 공전할 정령 인덱스. 정령이 없으면 -1 (화면 중앙 공전)
    /// </summary>
    public static int GetDroneAnchorIndex(int _droneIndex, int _spiritCount)
    {
        if (_spiritCount <= 0 || _droneIndex < 0)
        {
            return -1;
        }
        return _droneIndex % _spiritCount;
    }

    /// <summary>
    /// 드론 대상 규칙: 정령들의 현재 대상이 아닌 적만 (null 은 제외)
    /// </summary>
    public static bool IsDroneCandidate(HealthSystem _health, IList<HealthSystem> _spiritTargets)
    {
        if (null == _health)
        {
            return false;
        }
        return null == _spiritTargets || false == _spiritTargets.Contains(_health);
    }

    /// <summary>
    /// 정령이 대상에 닿았는지 (중심 거리 - 대상 반지름 ≤ _contactRadius)
    /// </summary>
    public static bool IsInContact(Vector2 _pos, Vector2 _targetPos, float _targetRadius, float _contactRadius)
    {
        return Vector2.Distance(_pos, _targetPos) - _targetRadius <= _contactRadius;
    }
    #endregion

    private void Awake()
    {
        aliveFilter = IsAliveTarget;
        droneFilter = IsDroneTarget;
        spiritPool = new ObjectPool<Spirit>(CreateSpirit, OnGetObject, OnReleaseObject, OnDestroyObject, false, 4, 32);
        dronePool = new ObjectPool<Drone>(CreateDrone, OnGetObject, OnReleaseObject, OnDestroyObject, false, 4, 32);
        bulletPool = new ObjectPool<Bullet>(CreateBullet, OnGetObject, OnReleaseObject, OnDestroyObject, false, 16, 128);
    }

    protected override void Fire()
    {
        Vector2 center = GetCenter();

        int spawn = GetSpawnCount(spirits.Count, FinalFireCount, spawnPerFire);
        for (int i = 0; i < spawn; ++i)
        {
            Spirit s = spiritPool.Get();
            s.target = null;
            s.tickTimer = spiritTickInterval;
            s.hitsLeft = spiritHitCount;
            s.phase = (spiritSerial++ % 8) * 45f;
            s.tr.position = ToWorld(center, s.tr.position.z);
            spirits.Add(s);
        }

        StageQuadTree tree = StageSpace.Targets;
        if (null == tree)
        {
            return;
        }

        CollectSpiritTargets();
        float range = FinalRange;
        for (int i = 0; i < drones.Count; ++i)
        {
            Vector2 from = drones[i].tr.position;
            bool found = tree.QueryNearest(from, range, droneFilter, out StageTarget target);
            if (false == found && true == droneFallbackToSpiritTarget)
            {
                found = tree.QueryNearest(from, range, aliveFilter, out target);
            }
            if (false == found)
            {
                continue;
            }
            LaunchBullet(from, target.stagePos - from, range + bulletExtraTravel);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (false == IsActivated || false == IsPlaced)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        StageQuadTree tree = StageSpace.Targets;
        Vector2 center = GetCenter();
        idleAngle += spiritIdleOrbitSpeed * dt;

        UpdateSpirits(tree, center, dt);
        SyncDroneCount();
        UpdateDrones(center, dt);
        UpdateBullets(tree, dt);
    }

    #region 정령
    private void UpdateSpirits(StageQuadTree _tree, Vector2 _center, float _dt)
    {
        float range = FinalRange;
        float damage = FinalDamage * spiritDamageRate;

        for (int i = spirits.Count - 1; i >= 0; --i)
        {
            Spirit s = spirits[i];
            Vector2 pos = s.tr.position;
            s.tickTimer = Mathf.Min(spiritTickInterval, s.tickTimer + _dt);

            if (null != _tree && true == _tree.QueryNearest(pos, range, aliveFilter, out StageTarget target))
            {
                s.target = target.health;
                Vector2 to = target.stagePos - pos;
                float stopDist = target.radius + spiritContactRadius * 0.5f;
                Vector2 next = pos;
                if (to.magnitude > stopDist)
                {
                    next = Vector2.MoveTowards(pos, target.stagePos, spiritChaseSpeed * _dt);
                }
                MoveSpirit(s, pos, next);

                if (true == IsInContact(next, target.stagePos, target.radius, spiritContactRadius)
                    && s.tickTimer >= spiritTickInterval)
                {
                    s.tickTimer = 0f;
                    StageDamage.Apply(target, damage, this);
                    if (--s.hitsLeft <= 0)
                    {
                        ReleaseSpiritAt(i);
                    }
                }
                continue;
            }

            s.target = null;
            float angle = (idleAngle + s.phase) * Mathf.Deg2Rad;
            Vector2 idlePos = _center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spiritIdleRadius;
            MoveSpirit(s, pos, Vector2.Lerp(pos, idlePos, 1f - Mathf.Exp(-spiritIdleSharpness * _dt)));
        }
    }

    private void MoveSpirit(Spirit _s, Vector2 _from, Vector2 _to)
    {
        _s.tr.position = ToWorld(_to, _s.tr.position.z);
        if (true == flipSpiritByDirection && null != _s.sr)
        {
            float dx = _to.x - _from.x;
            if (Mathf.Abs(dx) > 0.0001f)
            {
                _s.sr.flipX = dx < 0f;
            }
        }
    }

    private void ReleaseSpiritAt(int _index)
    {
        Spirit s = spirits[_index];
        int last = spirits.Count - 1;
        spirits[_index] = spirits[last];
        spirits.RemoveAt(last);
        s.target = null;
        spiritPool.Release(s);
    }

    private void CollectSpiritTargets()
    {
        spiritTargets.Clear();
        for (int i = 0; i < spirits.Count; ++i)
        {
            HealthSystem t = spirits[i].target;
            if (null != t && false == spiritTargets.Contains(t))
            {
                spiritTargets.Add(t);
            }
        }
    }
    #endregion

    #region 드론
    private void SyncDroneCount()
    {
        int want = GetDroneCount(FinalFireCount, dronesPerSpirit);
        while (drones.Count < want)
        {
            Drone d = dronePool.Get();
            d.angle = drones.Count * 360f / Mathf.Max(1, want);
            d.tr.position = ToWorld(GetCenter(), d.tr.position.z);
            drones.Add(d);
        }
        while (drones.Count > want)
        {
            int last = drones.Count - 1;
            Drone d = drones[last];
            drones.RemoveAt(last);
            dronePool.Release(d);
        }
    }

    private void UpdateDrones(Vector2 _center, float _dt)
    {
        float k = 1f - Mathf.Exp(-droneFollowSharpness * _dt);
        for (int i = 0; i < drones.Count; ++i)
        {
            Drone d = drones[i];
            d.angle += droneOrbitSpeed * _dt;
            int anchorIndex = GetDroneAnchorIndex(i, spirits.Count);
            Vector2 anchor = anchorIndex >= 0 ? (Vector2)spirits[anchorIndex].tr.position : _center;
            float rad = d.angle * Mathf.Deg2Rad;
            Vector2 goal = anchor + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * droneOrbitRadius;
            Vector2 pos = d.tr.position;
            d.tr.position = ToWorld(Vector2.Lerp(pos, goal, k), d.tr.position.z);
        }
    }
    #endregion

    #region 탄
    private void LaunchBullet(Vector2 _from, Vector2 _dir, float _travel)
    {
        if (_dir.sqrMagnitude < 0.000001f)
        {
            _dir = Vector2.right;
        }
        Bullet b = bulletPool.Get();
        b.dir = _dir.normalized;
        b.travelLeft = _travel;
        b.tr.position = ToWorld(_from, b.tr.position.z);
        b.tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.dir.y, b.dir.x) * Mathf.Rad2Deg);
        bullets.Add(b);
    }

    private void UpdateBullets(StageQuadTree _tree, float _dt)
    {
        float damage = FinalDamage * droneDamageRate;
        float step = bulletSpeed * _dt;
        for (int i = bullets.Count - 1; i >= 0; --i)
        {
            Bullet b = bullets[i];
            Vector2 from = b.tr.position;
            Vector2 to = from + b.dir * step;
            b.tr.position = ToWorld(to, b.tr.position.z);
            b.travelLeft -= step;

            bool done = b.travelLeft <= 0f;
            if (null != _tree && _tree.QuerySegment(from, to, bulletRadius, hitBuffer) > 0)
            {
                if (true == TryHitNearest(from, damage))
                {
                    done = true;
                }
            }

            if (true == done)
            {
                int last = bullets.Count - 1;
                bullets[i] = bullets[last];
                bullets.RemoveAt(last);
                bulletPool.Release(b);
            }
        }
    }

    /// <summary>
    /// hitBuffer 중 _from 에 가장 가까운 살아 있는 적 1명 타격
    /// </summary>
    private bool TryHitNearest(Vector2 _from, float _damage)
    {
        int best = -1;
        float bestSq = float.MaxValue;
        for (int j = 0; j < hitBuffer.Count; ++j)
        {
            if (false == hitBuffer[j].IsAlive)
            {
                continue;
            }
            float sq = (hitBuffer[j].stagePos - _from).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = j;
            }
        }
        if (best < 0)
        {
            return false;
        }
        StageDamage.Apply(hitBuffer[best], _damage, this);
        return true;
    }
    #endregion

    #region 필터
    private bool IsAliveTarget(StageTarget _t)
    {
        return true == _t.IsAlive;
    }

    private bool IsDroneTarget(StageTarget _t)
    {
        return true == _t.IsAlive && true == IsDroneCandidate(_t.health, spiritTargets);
    }
    #endregion

    #region 풀
    private Spirit CreateSpirit()
    {
        GameObject go = CreateVisual("Spirit", spiritPrefab, spiritSprite, spiritColor, spiritScale, out SpriteRenderer sr);
        return new Spirit { go = go, tr = go.transform, sr = sr };
    }

    private Drone CreateDrone()
    {
        GameObject go = CreateVisual("Drone", dronePrefab, droneSprite, droneColor, droneScale, out _);
        return new Drone { go = go, tr = go.transform };
    }

    private Bullet CreateBullet()
    {
        GameObject go = CreateVisual("DroneBullet", bulletPrefab, bulletSprite, bulletColor, bulletScale, out _);
        return new Bullet { go = go, tr = go.transform };
    }

    private static void OnGetObject(Spirit _s) => _s.go.SetActive(true);
    private static void OnReleaseObject(Spirit _s) => _s.go.SetActive(false);
    private static void OnDestroyObject(Spirit _s) => DestroyGo(_s.go);
    private static void OnGetObject(Drone _d) => _d.go.SetActive(true);
    private static void OnReleaseObject(Drone _d) => _d.go.SetActive(false);
    private static void OnDestroyObject(Drone _d) => DestroyGo(_d.go);
    private static void OnGetObject(Bullet _b) => _b.go.SetActive(true);
    private static void OnReleaseObject(Bullet _b) => _b.go.SetActive(false);
    private static void OnDestroyObject(Bullet _b) => DestroyGo(_b.go);

    private static void DestroyGo(GameObject _go)
    {
        if (null != _go)
        {
            Destroy(_go);
        }
    }

    /// <summary>
    /// 이 무기의 자식으로 외형을 만든다 (레이어 = 무기 레이어, 렌더러 정렬 레이어 = sortingLayerName)
    /// _prefab 이 있으면 복제, 없으면 빈 오브젝트 + (_sprite 가 있으면) SpriteRenderer
    /// </summary>
    private GameObject CreateVisual(string _name, GameObject _prefab, Sprite _sprite, Color _color, float _scale, out SpriteRenderer _renderer)
    {
        GameObject go;
        _renderer = null;
        if (null != _prefab)
        {
            go = Instantiate(_prefab, transform);
            go.name = _name;
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].sortingLayerName = sortingLayerName;
            }
            _renderer = renderers.Length > 0 ? renderers[0] : null;
        }
        else
        {
            go = new GameObject(_name);
            go.transform.SetParent(transform, false);
            if (null != _sprite)
            {
                _renderer = go.AddComponent<SpriteRenderer>();
                _renderer.sprite = _sprite;
                _renderer.color = _color;
                _renderer.sortingLayerName = sortingLayerName;
                _renderer.sortingOrder = sortingOrder;
            }
        }
        go.transform.localScale *= _scale;
        LayerUtils.SetLayer(go, gameObject.layer);
        go.SetActive(false);
        return go;
    }
    #endregion

    private Vector2 GetCenter()
    {
        StageSpace space = StageSpace.Instance;
        if (null != space && true == space.IsReady)
        {
            return space.StageCenter;
        }
        return transform.position;
    }

    private static Vector3 ToWorld(Vector2 _pos, float _z)
    {
        return new Vector3(_pos.x, _pos.y, _z);
    }
}
