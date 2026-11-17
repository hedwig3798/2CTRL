using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 차원 무기: 화염 부메랑 (부메랑 + 화염 방사)
/// - 쿨타임마다 양쪽 플레이어 화면 위치에서 각각 FinalFireCount 개 발사 (여러 개면 spreadAngle 간격 부채꼴)
///   방향 = 그 위치에서 최근접 적(양 진영, FinalRange 안). 없으면 그 진영의 마지막 발사 방향, 그것도 없으면 임의 방향
/// - 이동은 원본 부메랑(ReturnMovement)과 같고 귀환 목표는 발사한 플레이어의 현재 화면 위치. 1회 타격 없이 비행 중 원 범위 틱 화염 데미지
/// - 수치 근거(목표 468 지속 화염, 권장 프리팹 damage 46.8 · attackSpeed 0.5 · fireCount 1 · lifetime 2 → 진영마다 부메랑 1개 상시 비행):
///   틱 46.8 / 0.2s = 234 DPS × 2개(좌우) = 468 (닿아 있는 동안 기준, WeaponTest DPS 미터로 실측 보정)
/// </summary>
public class BlazeBoomerang
    : DimensionWeapon
{
    [Header("prefab (비우면 렌더러 없는 빈 오브젝트로 동작)")]
    [SerializeField]
    private BlazeBoomerangProjectile projectilePrefab;

    [Header("flight (ReturnMovement 와 같은 의미)")]
    [Tooltip("출발 속도이자 돌아올 때의 기본 최고 속도 (화면 공간 단위/초)")]
    [Min(0.01f)]
    [SerializeField]
    private float flightSpeed = 8f;
    [Tooltip("목표 지점을 지나 더 날아가는 거리")]
    [Min(0f)]
    [SerializeField]
    private float overshoot = 1f;
    [Tooltip("목표가 가까워도 최소 이 거리만큼은 날아간다")]
    [Min(0.1f)]
    [SerializeField]
    private float minDistance = 2f;
    [Tooltip("돌아올 때의 최고 속도 배율")]
    [Min(0.1f)]
    [SerializeField]
    private float returnSpeedRate = 1.5f;
    [Tooltip("적이 없을 때 날아가는 목표 거리")]
    [Min(0.1f)]
    [SerializeField]
    private float noTargetDistance = 4f;
    [Tooltip("여러 개 발사할 때 부메랑 사이 각도 (도)")]
    [Range(0f, 180f)]
    [SerializeField]
    private float spreadAngle = 20f;
    [Tooltip("무기 lifetime 이 0 일 때 쓰는 부메랑 수명 (초)")]
    [Min(0.1f)]
    [SerializeField]
    private float defaultFlightLifetime = 2f;

    [Header("flame")]
    [Tooltip("화염 틱 기본 간격 (초). 실제 = 이 값 × 기본 공격 속도 / 최종 공격 속도")]
    [Min(0.05f)]
    [SerializeField]
    private float flameTickInterval = 0.2f;
    [Tooltip("화염 틱 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float flameTickDamageRate = 1f;
    [Tooltip("무기 areaRadius 가 0 일 때 쓰는 화염 반지름")]
    [Min(0f)]
    [SerializeField]
    private float defaultFlameRadius = 1.2f;

    private readonly Vector2[] lastDirections = new Vector2[2];
    private ObjectPool<BlazeBoomerangProjectile> pool;

    public float FlightSpeed => flightSpeed;
    public float Overshoot => overshoot;
    public float MinDistance => minDistance;
    public float ReturnSpeedRate => returnSpeedRate;
    public int ActiveCount => null != pool ? pool.CountActive : 0;
    public float FlightLifetime => Lifetime > 0f ? FinalLifetime : ApplyDuration(defaultFlightLifetime, defaultFlightLifetime);
    public float FlameRadius => AreaRadius > 0f ? AreaRadius : defaultFlameRadius;
    public float FlameTickDamage => FinalDamage * flameTickDamageRate;
    public float FlameTickInterval => DimensionStatMath.ScaledTickInterval(flameTickInterval, AttackSpeed, FinalAttackSpeed);

    /// <summary>
    /// 발사 방향: 대상이 있으면 대상 쪽, 없으면 마지막 방향, 그것도 없으면 _fallback (모두 정규화, 길이 0 이면 오른쪽)
    /// </summary>
    public static Vector2 ResolveAim(Vector2 _from, bool _hasTarget, Vector2 _targetPos, Vector2 _last, Vector2 _fallback)
    {
        if (true == _hasTarget)
        {
            Vector2 toTarget = _targetPos - _from;
            if (toTarget.sqrMagnitude > 0.000001f)
            {
                return toTarget.normalized;
            }
        }
        if (_last.sqrMagnitude > 0.000001f)
        {
            return _last.normalized;
        }
        return _fallback.sqrMagnitude > 0.000001f ? _fallback.normalized : Vector2.right;
    }

    /// <summary>
    /// _count 개 부채꼴 중 _index 번째 방향 (가운데 기준 좌우 대칭, 간격 _spreadDeg)
    /// </summary>
    public static Vector2 SpreadDirection(Vector2 _dir, int _index, int _count, float _spreadDeg)
    {
        float angle = (_index - (_count - 1) * 0.5f) * _spreadDeg;
        return (Vector2)(Quaternion.Euler(0f, 0f, angle) * _dir);
    }

    private void Awake()
    {
        pool = new ObjectPool<BlazeBoomerangProjectile>(Create, null, OnRelease, OnDestroyPooled, false, 4, 32);
    }

    protected override void Fire()
    {
        FireFrom(PlayerSide.Left);
        FireFrom(PlayerSide.Right);
    }

    private void FireFrom(PlayerSide _side)
    {
        int count = FinalFireCount;
        if (count <= 0 || false == TryGetPlayerStagePos(_side, out Vector2 start))
        {
            return;
        }

        bool hasTarget = false;
        Vector2 targetPos = Vector2.zero;
        StageQuadTree tree = StageSpace.Targets;
        if (null != tree && true == tree.QueryNearest(start, FinalRange, (ICollection<HealthSystem>)null, out StageTarget target))
        {
            hasTarget = true;
            targetPos = target.stagePos;
        }

        int index = (int)_side;
        Vector2 aim = ResolveAim(start, hasTarget, targetPos, lastDirections[index], Random.insideUnitCircle);
        lastDirections[index] = aim;

        float distance = true == hasTarget ? Vector2.Distance(start, targetPos) : noTargetDistance;
        float lifetime = FlightLifetime;
        for (int i = 0; i < count; ++i)
        {
            Vector2 dir = SpreadDirection(aim, i, count, spreadAngle);
            pool.Get().Launch(this, _side, start, start + dir * distance, lifetime);
        }
    }

    public void Release(BlazeBoomerangProjectile _projectile)
    {
        if (null != _projectile && true == _projectile.gameObject.activeSelf)
        {
            pool.Release(_projectile);
        }
    }

    #region 풀
    private BlazeBoomerangProjectile Create()
    {
        BlazeBoomerangProjectile created = null != projectilePrefab
            ? Instantiate(projectilePrefab, transform)
            : new GameObject(nameof(BlazeBoomerangProjectile)).AddComponent<BlazeBoomerangProjectile>();
        if (created.transform.parent != transform)
        {
            created.transform.SetParent(transform, false);
        }
        created.gameObject.SetActive(false);
        if (StageSpace.StageLayer >= 0)
        {
            LayerUtils.SetLayer(created.gameObject, StageSpace.StageLayer);
        }
        return created;
    }

    private static void OnRelease(BlazeBoomerangProjectile _item)
    {
        if (null != _item)
        {
            _item.gameObject.SetActive(false);
        }
    }

    private static void OnDestroyPooled(BlazeBoomerangProjectile _item)
    {
        if (null != _item)
        {
            Destroy(_item.gameObject);
        }
    }
    #endregion

    private void OnDestroy()
    {
        pool?.Dispose();
    }
}
