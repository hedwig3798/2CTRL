using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회오리 발사체 (무기 WEAPON_TYPE.Capped 로 쓴다)
/// 주인 근처에 생겨 랜덤한 방향으로 천천히 떠돌며, 끌어당김 반경 안의 몬스터를 중심으로 끌어당기고
/// 타격 반경 안의 적을 tickInterval 마다 타격한다
/// - 이동 / 수명은 이 컴포넌트가 직접 처리한다 (Lifetime / 이동 컴포넌트를 붙이지 않는다)
/// - 끌어당기기는 몬스터 루트(MonsterContext.root) 위치를 직접 옮긴다
///   몬스터 이동(MonsterMoveState) / Knockback 과 같은 방식(transform 이동)이라 서로 덮어쓰지 않고 더해진다
///   Knockback.resistance 만큼 덜 끌려온다. MonsterBrain 이 없는 대상(플레이어 등)은 끌어당기지 않는다
/// - 크기 : 반경과 표시 크기 = 기본값 * weapon.GetParam(ProjectileScale, 1)
/// - 수명 : weapon.lifetime (0 이하면 defaultLifetime). 매 프레임 읽으므로 업그레이드가 살아 있는 회오리에도 적용된다
/// - 끌어당기는 속도 : weapon.GetParam(PullForce, pullForce)
/// - ★ FlagTornadoPermanent : 수명이 줄지 않는다. 주인과 leashDistance 보다 멀어지면 주인 쪽으로 돌아온다
/// - 무기 knockbackForce 는 0 으로 둔다 (넉백은 바깥으로 밀어내 끌어당김과 반대)
/// </summary>
public class TornadoProjectile
    : Projectile
{
    [Header("spawn")]
    [Tooltip("주인 위치에서 이 반지름 안의 무작위 지점에 생긴다")]
    [Min(0f)]
    [SerializeField]
    private float spawnRadius = 1.5f;

    [Header("wander")]
    [Tooltip("떠도는 이동 속도")]
    [Min(0f)]
    [SerializeField]
    private float wanderSpeed = 1.5f;
    [Tooltip("떠도는 방향을 새로 뽑는 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float wanderInterval = 1.5f;

    [Header("hit")]
    [Tooltip("크기 1 일 때 타격 반경")]
    [Min(0.01f)]
    [SerializeField]
    private float hitRadius = 0.8f;
    [Tooltip("타격 반경 안의 적을 타격하는 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float tickInterval = 0.3f;

    [Header("pull")]
    [Tooltip("크기 1 일 때 끌어당김 반경")]
    [Min(0.01f)]
    [SerializeField]
    private float pullRadius = 2f;
    [Tooltip("끌어당기는 속도 (초당 거리) 기본값. 업그레이드 PullForce 가 곱/가산된다. 몬스터 이동 속도보다 커야 중심으로 모인다")]
    [Min(0f)]
    [SerializeField]
    private float pullForce = 3f;
    [Tooltip("중심에서 이 거리 안이면 더 끌어당기지 않는다 (중심에서 떨리는 것 방지)")]
    [Min(0f)]
    [SerializeField]
    private float pullStopDistance = 0.1f;

    [Header("lifetime")]
    [Tooltip("무기 lifetime 이 0 이하일 때 쓰는 수명 (초)")]
    [Min(0.1f)]
    [SerializeField]
    private float defaultLifetime = 5f;

    [Header("permanent (★)")]
    [Tooltip("FlagTornadoPermanent 일 때 주인과 이 거리보다 멀어지면 주인 쪽으로 돌아온다")]
    [Min(0f)]
    [SerializeField]
    private float leashDistance = 6f;
    [Tooltip("돌아오는 이동 속도")]
    [Min(0f)]
    [SerializeField]
    private float leashSpeed = 3f;
    [Tooltip("돌아오다가 주인과 거리가 leashDistance * 이 비율 이하가 되면 다시 떠돈다")]
    [Range(0f, 1f)]
    [SerializeField]
    private float leashResumeRate = 0.5f;

    [Header("cache")]
    [Tooltip("몬스터 조회 캐시 최대 개수. 넘으면 비운다 (영구 회오리에서 캐시가 계속 커지는 것 방지)")]
    [Min(1)]
    [SerializeField]
    private int brainCacheLimit = 256;

    [Header("visual")]
    [Tooltip("크기를 바꿀 표시 오브젝트. 비워두면 크기를 바꾸지 않는다 (정렬 레이어는 attack)")]
    [SerializeField]
    private Transform scaleTarget;

    private readonly List<Transform> pullBuffer = new List<Transform>();
    // 대상 콜라이더 -> MonsterBrain (몬스터가 아니면 null 로 기록해 다시 찾지 않는다)
    private readonly Dictionary<Transform, MonsterBrain> brainCache = new Dictionary<Transform, MonsterBrain>();

    private Vector3 baseScale = Vector3.one;
    private Vector2 wanderDirection = Vector2.right;
    private float nextWanderTime;
    private float nextTickTime;
    private float elapsed;
    private bool isLeashing;
    private float currentScale = 1f;
    private int appliedUpgradeVersion = -1;

    /// <summary>
    /// 현재 크기 배율 (ProjectileScale)
    /// </summary>
    public float CurrentScale => currentScale;

    /// <summary>
    /// 현재 크기가 반영된 타격 반경
    /// </summary>
    public float CurrentHitRadius => hitRadius * currentScale;

    /// <summary>
    /// 현재 크기가 반영된 끌어당김 반경
    /// </summary>
    public float CurrentPullRadius => pullRadius * currentScale;

    /// <summary>
    /// 남은 수명 비율 (1 -> 0). 영구 상태면 1
    /// </summary>
    public float RemainRate
    {
        get
        {
            if (null == weapon || true == IsPermanent)
            {
                return 1f;
            }
            float life = FinalLifetime;
            return Mathf.Clamp01(1f - elapsed / life);
        }
    }

    private bool IsPermanent => null != weapon && true == weapon.HasFlag(WeaponParam.FlagTornadoPermanent);

    private float FinalLifetime => null != weapon && weapon.lifetime > 0f ? weapon.lifetime : defaultLifetime;

    private Transform Owner
    {
        get
        {
            if (null == weapon)
            {
                return null;
            }
            return null != weapon.owner ? weapon.owner : weapon.transform;
        }
    }

    private void Awake()
    {
        if (null != scaleTarget)
        {
            baseScale = scaleTarget.localScale;
        }
    }

    protected override bool OnLaunch()
    {
        ResetState();

        Transform owner = Owner;
        Vector3 center = null != owner ? owner.position : transform.position;
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        transform.position = new Vector3(center.x + offset.x, center.y + offset.y, transform.position.z);

        PickWanderDirection();
        // 생긴 순간 바로 첫 타격
        nextTickTime = Time.time;
        RefreshUpgrade();
        return true;
    }

    private void Update()
    {
        if (null == weapon)
        {
            return;
        }

        float dt = Time.deltaTime;

        if (appliedUpgradeVersion != weapon.UpgradeVersion)
        {
            RefreshUpgrade();
        }

        // ★ 영구 상태면 수명이 줄지 않는다 (업그레이드 시점에 살아 있던 회오리도 이 프레임부터 적용)
        if (false == IsPermanent)
        {
            elapsed += dt;
            if (elapsed >= FinalLifetime)
            {
                gameObject.SetActive(false);
                return;
            }
        }
        else
        {
            isLeashing = UpdateLeash();
        }

        Move(dt);
        Pull(dt);

        if (Time.time >= nextTickTime)
        {
            // 데미지 업그레이드 / 플레이어 패시브를 살아 있는 회오리에도 반영한다
            RefreshDamage();
            ApplyAreaDamage(transform.position, CurrentHitRadius);
            nextTickTime = Time.time + tickInterval;
        }
    }

    /// <summary>
    /// 업그레이드로 바뀐 크기를 반영한다
    /// </summary>
    private void RefreshUpgrade()
    {
        appliedUpgradeVersion = weapon.UpgradeVersion;
        currentScale = Mathf.Max(0.01f, weapon.GetParam(WeaponParam.ProjectileScale, 1f));
        if (null != scaleTarget)
        {
            scaleTarget.localScale = baseScale * currentScale;
        }
    }

    /// <summary>
    /// 주인과 너무 멀어졌는지 확인해 돌아오는 중인지 반환한다 (★ 영구 상태에서만)
    /// </summary>
    private bool UpdateLeash()
    {
        Transform owner = Owner;
        if (null == owner)
        {
            return false;
        }

        float distance = Vector2.Distance(transform.position, owner.position);
        if (true == isLeashing)
        {
            return distance > leashDistance * leashResumeRate;
        }
        return distance > leashDistance;
    }

    private void Move(float _dt)
    {
        Vector2 dir;
        float speed;

        Transform owner = Owner;
        if (true == isLeashing && null != owner)
        {
            dir = ((Vector2)(owner.position - transform.position)).normalized;
            speed = leashSpeed;
            // 다시 떠돌 때 바로 새 방향을 뽑는다
            nextWanderTime = 0f;
        }
        else
        {
            if (Time.time >= nextWanderTime)
            {
                PickWanderDirection();
            }
            dir = wanderDirection;
            speed = wanderSpeed;
        }

        transform.position += (Vector3)(dir * speed * _dt);
    }

    private void PickWanderDirection()
    {
        Vector2 dir = Random.insideUnitCircle;
        wanderDirection = dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;
        nextWanderTime = Time.time + wanderInterval;
    }

    /// <summary>
    /// 끌어당김 반경 안의 몬스터를 중심 쪽으로 옮긴다 (넘어가지 않는다)
    /// </summary>
    private void Pull(float _dt)
    {
        float force = weapon.GetParam(WeaponParam.PullForce, pullForce);
        if (force <= 0f)
        {
            return;
        }

        Vector2 center = transform.position;
        int count = FindAll(center, CurrentPullRadius, pullBuffer);
        for (int i = 0; i < count; ++i)
        {
            MonsterBrain brain = GetBrain(pullBuffer[i]);
            if (null == brain || null == brain.Context || null == brain.Context.root)
            {
                continue;
            }

            MonsterContext ctx = brain.Context;
            float resistance = null != ctx.knockback ? ctx.knockback.resistance : 0f;
            if (resistance >= 1f)
            {
                continue;
            }

            Vector2 toCenter = center - (Vector2)pullBuffer[i].position;
            float distance = toCenter.magnitude;
            float step = Mathf.Min(force * (1f - resistance) * _dt, distance - pullStopDistance);
            if (step <= 0f)
            {
                continue;
            }

            ctx.root.position += (Vector3)(toCenter / distance * step);
        }
        pullBuffer.Clear();
    }

    private MonsterBrain GetBrain(Transform _target)
    {
        if (false == brainCache.TryGetValue(_target, out MonsterBrain brain))
        {
            brain = _target.GetComponentInParent<MonsterBrain>();
            if (brainCache.Count >= brainCacheLimit)
            {
                brainCache.Clear();
            }
            brainCache[_target] = brain;
        }
        return brain;
    }

    private void ResetState()
    {
        elapsed = 0f;
        isLeashing = false;
        nextWanderTime = 0f;
        nextTickTime = 0f;
        appliedUpgradeVersion = -1;
        pullBuffer.Clear();
        brainCache.Clear();
    }

    protected override void OnDisable()
    {
        ResetState();

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, CurrentHitRadius);
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, CurrentPullRadius);
    }
}
