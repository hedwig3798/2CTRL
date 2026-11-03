using UnityEngine;

/// <summary>
/// 정령 발사체 (WEAPON_TYPE.Capped 무기가 쓴다. 살아 있는 정령 수 = 최대 fireCount)
/// 발사 시 주인 위치에 생겨 대기 상태로 주인 주변을 떠다닌다
/// - 대기 : 무기 사거리(FinalRange, 무기 위치 기준) 안에 적이 오면 가장 가까운 적을 추적한다
/// - 추적 : chaseSpeed 로 대상에게 다가가고, 닿아 있으면 (대상 콜라이더 표면까지 contactRadius 이하) 틱 간격마다 데미지
///   첫 접촉은 바로 타격한다. 총 hitCount(무기 hitCount. 살아 있는 동안 업그레이드로 늘어나면 증가분을 더한다) 번 타격하면 소멸한다
/// - 대상이 죽거나 사거리 + leashMargin 밖으로 벗어나면 사거리 안의 다음 적을 추적하고, 없으면 대기로 돌아간다 (남은 타격 수 유지)
/// - 틱 간격은 공격 속도와 무관하다 (Capped 무기의 공격 속도는 정령 생성 간격)
/// - 업그레이드 : 추적 속도 = weapon.GetParam(ChaseSpeed, chaseSpeed), 틱 간격 = weapon.GetParam(TickInterval, tickInterval)
/// - 소멸은 직접 한다 (Lifetime 컴포넌트를 붙이지 않는다)
/// </summary>
public class SpiritProjectile
    : Projectile
{
    private enum SPIRIT_STATE
    {
        Idle,
        Chase,
    }

    [Header("idle")]
    [Tooltip("대기 중 주인에게서 떨어져 떠다니는 거리")]
    [Min(0f)]
    [SerializeField]
    private float idleRadius = 1.2f;
    [Tooltip("대기 중 주인 주변을 도는 속도 (도/초)")]
    [SerializeField]
    private float idleOrbitSpeed = 60f;
    [Tooltip("대기 중 위아래로 흔들리는 높이")]
    [Min(0f)]
    [SerializeField]
    private float idleBobHeight = 0.15f;
    [Tooltip("대기 중 위아래로 흔들리는 속도 (라디안/초)")]
    [Min(0f)]
    [SerializeField]
    private float idleBobSpeed = 3f;
    [Tooltip("대기 위치를 따라가는 민첩도 (클수록 바로 붙는다)")]
    [Min(0.1f)]
    [SerializeField]
    private float idleFollowSharpness = 5f;
    [Tooltip("대기 중 적을 찾는 간격 (초)")]
    [Min(0.02f)]
    [SerializeField]
    private float scanInterval = 0.1f;

    [Header("chase")]
    [Tooltip("추적 이동 속도 기본값")]
    [Min(0f)]
    [SerializeField]
    private float chaseSpeed = 6f;
    [Tooltip("대상 콜라이더 표면까지의 거리가 이 값 이하면 닿은 것으로 본다 (콜라이더가 없으면 중심 거리)")]
    [Min(0f)]
    [SerializeField]
    private float contactRadius = 0.35f;
    [Tooltip("추적 대상이 사거리 + 이 값보다 멀어지면 놓친다")]
    [Min(0f)]
    [SerializeField]
    private float leashMargin = 2f;

    [Header("tick")]
    [Tooltip("닿아 있는 동안 타격하는 기본 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float tickInterval = 0.4f;

    [Header("visual")]
    [Tooltip("정령 렌더러. 비워두면 정렬 레이어 / 좌우 반전을 하지 않는다")]
    [SerializeField]
    private SpriteRenderer spiritRenderer;
    [Tooltip("정령 렌더러 정렬 레이어 (Default 면 바닥에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";
    [Tooltip("왼쪽으로 움직일 때 flipX 를 켠다 (스프라이트가 오른쪽을 보는 기준)")]
    [SerializeField]
    private bool flipByDirection = true;

    private SPIRIT_STATE state;
    private Transform target;
    private Collider2D targetCollider;
    private int remainingHits;
    private float nextTickTime;
    private float nextScanTime;
    private float idleAngle;
    // 마지막으로 반영한 무기 업그레이드 버전 / hitCount (살아 있는 정령에 공격 횟수 증가분을 더할 때 쓴다)
    private int appliedUpgradeVersion = -1;
    private int appliedHitCount;

    /// <summary>
    /// 남은 타격 수
    /// </summary>
    public int RemainingHits => remainingHits;

    /// <summary>
    /// 추적 중인지
    /// </summary>
    public bool IsChasing => SPIRIT_STATE.Chase == state;

    /// <summary>
    /// 업그레이드가 반영된 틱 간격 (초)
    /// </summary>
    public float FinalTickInterval
    {
        get
        {
            if (null == weapon)
            {
                return tickInterval;
            }
            return Mathf.Max(0.05f, weapon.GetParam(WeaponParam.TickInterval, tickInterval));
        }
    }

    private Transform Origin => null != weapon.owner ? weapon.owner : weapon.transform;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (null != spiritRenderer && false == string.IsNullOrEmpty(sortingLayerName))
        {
            spiritRenderer.sortingLayerName = sortingLayerName;
        }
    }

    /// <summary>
    /// 주인 위치에서 대기 상태로 시작한다 (적이 없어도 발사한다)
    /// </summary>
    protected override bool OnLaunch()
    {
        ResetState();

        Vector3 position = Origin.position;
        position.z = transform.position.z;
        transform.position = position;

        remainingHits = Mathf.Max(1, weapon.hitCount);
        appliedHitCount = weapon.hitCount;
        appliedUpgradeVersion = weapon.UpgradeVersion;
        idleAngle = Random.Range(0f, 360f);
        return true;
    }

    private void Update()
    {
        if (null == weapon)
        {
            return;
        }

        if (appliedUpgradeVersion != weapon.UpgradeVersion)
        {
            RefreshHitCount();
        }

        Vector3 before = transform.position;

        if (SPIRIT_STATE.Chase == state)
        {
            UpdateChase();
        }
        else
        {
            UpdateIdle();
        }

        UpdateFlip(transform.position.x - before.x);
    }

    /// <summary>
    /// 업그레이드로 늘어난 무기 hitCount 만큼 남은 타격 수를 더한다 (줄어든 경우는 반영하지 않는다)
    /// </summary>
    private void RefreshHitCount()
    {
        appliedUpgradeVersion = weapon.UpgradeVersion;

        int delta = weapon.hitCount - appliedHitCount;
        appliedHitCount = weapon.hitCount;
        if (delta > 0)
        {
            remainingHits += delta;
        }
    }

    #region State
    /// <summary>
    /// 주인 주변을 떠다니며 사거리 안의 적을 찾는다
    /// </summary>
    private void UpdateIdle()
    {
        idleAngle += idleOrbitSpeed * Time.deltaTime;

        float rad = idleAngle * Mathf.Deg2Rad;
        Vector3 hover = Origin.position
            + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * idleRadius
            + Vector3.up * (Mathf.Sin(Time.time * idleBobSpeed + rad) * idleBobHeight);
        hover.z = transform.position.z;

        float t = 1f - Mathf.Exp(-idleFollowSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, hover, t);

        if (Time.time < nextScanTime)
        {
            return;
        }
        nextScanTime = Time.time + scanInterval;

        Transform found = FindNearest(weapon.transform.position, weapon.FinalRange);
        if (null != found)
        {
            StartChase(found);
        }
    }

    /// <summary>
    /// 대상에게 다가가고, 닿아 있으면 틱 간격마다 타격한다
    /// </summary>
    private void UpdateChase()
    {
        if (false == IsTargetValid())
        {
            Transform next = FindNearest(weapon.transform.position, weapon.FinalRange);
            if (null == next)
            {
                EnterIdle();
                return;
            }
            StartChase(next);
        }

        Vector3 destination = target.position;
        destination.z = transform.position.z;
        float speed = Mathf.Max(0f, weapon.GetParam(WeaponParam.ChaseSpeed, chaseSpeed));
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        if (Time.time < nextTickTime || DistanceToTarget() > contactRadius)
        {
            return;
        }

        Hit();
    }

    private void StartChase(Transform _target)
    {
        state = SPIRIT_STATE.Chase;
        target = _target;
        targetCollider = _target.GetComponent<Collider2D>();
    }

    private void EnterIdle()
    {
        state = SPIRIT_STATE.Idle;
        target = null;
        targetCollider = null;
        nextScanTime = Time.time + scanInterval;
    }

    /// <summary>
    /// 대상이 살아 있고 사거리 + leashMargin 안에 있는지
    /// </summary>
    private bool IsTargetValid()
    {
        if (null == target || false == IsAlive(target))
        {
            return false;
        }

        float leash = weapon.FinalRange + leashMargin;
        return ((Vector2)(target.position - weapon.transform.position)).sqrMagnitude <= leash * leash;
    }

    /// <summary>
    /// 대상 콜라이더 표면까지의 거리 (콜라이더가 없거나 꺼져 있으면 중심 거리)
    /// </summary>
    private float DistanceToTarget()
    {
        Vector2 position = transform.position;
        if (null != targetCollider && true == targetCollider.enabled)
        {
            return Vector2.Distance(position, targetCollider.ClosestPoint(position));
        }
        return Vector2.Distance(position, target.position);
    }

    /// <summary>
    /// 대상을 한 번 타격한다. 타격 수를 다 쓰면 소멸한다
    /// </summary>
    private void Hit()
    {
        if (target.TryGetComponent(out DamagePipeline dp))
        {
            // 데미지 업그레이드 / 플레이어 패시브를 살아 있는 정령에도 반영한다
            RefreshDamage();
            SendDamage(dp, transform.position);
        }

        nextTickTime = Time.time + FinalTickInterval;
        --remainingHits;
        if (remainingHits <= 0)
        {
            gameObject.SetActive(false);
        }
    }
    #endregion

    private void UpdateFlip(float _dx)
    {
        if (false == flipByDirection || null == spiritRenderer || Mathf.Abs(_dx) < 0.0001f)
        {
            return;
        }
        spiritRenderer.flipX = _dx < 0f;
    }

    private void ResetState()
    {
        state = SPIRIT_STATE.Idle;
        target = null;
        targetCollider = null;
        remainingHits = 0;
        nextTickTime = 0f;
        nextScanTime = 0f;
        appliedUpgradeVersion = -1;
        appliedHitCount = 0;
    }

    protected override void OnDisable()
    {
        ResetState();

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, contactRadius);
    }
}
