using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레이저 무기의 발사체
/// 발사 순간 시작 지점에서 가장 가까운 적 방향으로 길이 FinalRange 인 직선 광선을 쏜다
/// 광선(두께 width)에 닿은 살아있는 적을 가까운 순서로 hitCount 명까지 한 번씩 타격한다 (데미지는 발사 순간 한 번만)
/// 발사 후에는 아무것도 따라가지 않으며, 광선 모양은 Lifetime 동안 남아 가늘어지고 흐려진 뒤 사라진다
/// - 여러 발(fireCount > 1)을 쏘면 formation 순서에 따라 fanAngle 간격의 부채꼴로 퍼진다 (spreadRange 랜덤 보정도 함께 적용)
/// - 목표 지점은 Launch 가 OnLaunch 이후에 기록하므로, 실제 발사는 Initialize(BlackBoard) 에서 한다
/// </summary>
public class LaserBeamProjectile
    : Projectile
    , Initializable
{
    [Header("beam")]
    [Tooltip("광선 두께. 타격 판정(CircleCast 지름)과 광선 모양의 두께로 쓴다")]
    [Min(0.01f)]
    public float width = 0.3f;
    [Tooltip("여러 발을 한 번에 쏠 때 광선 사이 각도 (도). 0 이면 모두 같은 방향 (spreadRange 랜덤 보정만 적용)")]
    [Min(0f)]
    public float fanAngle = 15f;
    [Tooltip("넉백 기준점을 광선 위 대상 위치에서 시작 지점 쪽으로 당기는 거리. 클수록 광선 진행 방향으로, 작을수록 옆으로 밀려난다")]
    [Min(0f)]
    public float knockbackBackOffset = 0.5f;

    [Header("visual")]
    [Tooltip("광선 모양. 중심 피벗의 1 x 1 스프라이트를 길이 방향(x)으로 늘린다. 이 오브젝트의 자식이어야 한다")]
    public Transform visual;
    [Tooltip("흐려지게 할 스프라이트. 비워두면 visual 에서 찾는다")]
    public SpriteRenderer visualRenderer;
    [Tooltip("남은 수명 비율. 비워두면 같은 오브젝트의 Lifetime")]
    public Lifetime lifetime;

    private const int MaxCastCount = 64;
    private static RaycastHit2D[] castBuffer = new RaycastHit2D[MaxCastCount];
    private static List<RaycastHit2D> sortedHits = new List<RaycastHit2D>(MaxCastCount);
    private static ContactFilter2D castFilter = new ContactFilter2D();

    private HashSet<DamagePipeline> hitTargets = new HashSet<DamagePipeline>();

    /// <summary>
    /// 이번 활성화에서 이미 광선을 쐈는지 (유지형 무기가 다시 초기화해도 재발사하지 않도록)
    /// </summary>
    private bool fired;
    private float beamLength;
    private Color baseColor = Color.white;

    /// <summary>
    /// 발사 지점과 목표 지점이 BlackBoard 에 모두 기록된 뒤 호출된다. 이때 광선을 쏜다
    /// </summary>
    public void Initialize(BlackBoard _data)
    {
        if (true == fired || null == weapon)
        {
            return;
        }

        if (false == _data.TryGetStartPoint(out Vector3 start))
        {
            start = weapon.transform.position;
        }

        if (false == TryGetAimPoint(_data, out Vector3 point))
        {
            gameObject.SetActive(false);
            return;
        }

        Vector2 dir = (Vector2)(point - start);
        dir = dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;
        dir = ApplyFormationAngle(dir);

        fired = true;
        beamLength = weapon.FinalRange;

        PlaceBeam(start, dir);
        FireBeam(start, dir, Mathf.Max(1, _data.GetInt(DATA_TYPE.hitCount)));
        UpdateVisual();
    }

    protected override bool OnLaunch()
    {
        hitTargets.Clear();
        return AimAtNearest();
    }

    protected override bool OnLaunchAt()
    {
        hitTargets.Clear();
        return true;
    }

    /// <summary>
    /// 목표 지점. moveTargetPoint(랜덤 보정 포함) 가 있으면 그 값, 없으면 moveTarget 의 위치
    /// </summary>
    private bool TryGetAimPoint(BlackBoard _data, out Vector3 _point)
    {
        if (true == _data.TryGetVector(DATA_TYPE.moveTargetPoint, out _point))
        {
            return true;
        }

        Transform target = _data.GetTransform(DATA_TYPE.moveTarget);
        if (null == target)
        {
            return false;
        }

        _point = target.position;
        return true;
    }

    /// <summary>
    /// 함께 발사된 광선들이 목표 방향을 중심으로 fanAngle 간격의 부채꼴이 되도록 방향을 돌린다
    /// </summary>
    private Vector2 ApplyFormationAngle(Vector2 _dir)
    {
        if (formationCount <= 1 || fanAngle <= 0f)
        {
            return _dir;
        }

        float offset = (formationIndex - (formationCount - 1) * 0.5f) * fanAngle;
        return (Vector2)(Quaternion.Euler(0f, 0f, offset) * _dir);
    }

    /// <summary>
    /// 발사체를 시작 지점에 두고 광선 방향으로 회전시킨다 (이후 움직이지 않는다)
    /// </summary>
    private void PlaceBeam(Vector3 _start, Vector2 _dir)
    {
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(_start, Quaternion.Euler(0f, 0f, angle));
    }

    /// <summary>
    /// 광선에 닿은 살아있는 적을 가까운 순서로 _maxHit 명까지 한 번씩 타격한다
    /// 콜라이더가 여러 개인 대상은 한 번만 타격한다
    /// </summary>
    private void FireBeam(Vector2 _start, Vector2 _dir, int _maxHit)
    {
        if (beamLength <= 0f)
        {
            return;
        }

        castFilter.useLayerMask = true;
        castFilter.useTriggers = true;
        castFilter.SetLayerMask(weapon.targetLayer);

        int count = Physics2D.CircleCast(
            _start
            , width * 0.5f
            , _dir
            , castFilter
            , castBuffer
            , beamLength
        );

        sortedHits.Clear();
        for (int i = 0; i < count; ++i)
        {
            sortedHits.Add(castBuffer[i]);
        }
        sortedHits.Sort((_a, _b) => _a.distance.CompareTo(_b.distance));

        int hit = 0;
        for (int i = 0; i < sortedHits.Count && hit < _maxHit; ++i)
        {
            Transform target = sortedHits[i].collider.transform;
            if (target == weapon.transform || target == weapon.owner)
            {
                continue;
            }

            if (false == IsAlive(target))
            {
                continue;
            }

            if (false == target.TryGetComponent(out DamagePipeline dp) || true == hitTargets.Contains(dp))
            {
                continue;
            }

            hitTargets.Add(dp);
            SendDamage(dp, GetKnockbackPoint(_start, _dir, target.position));
            ++hit;
        }

        sortedHits.Clear();
    }

    /// <summary>
    /// 넉백 기준점. 대상 위치를 광선 위로 내린 지점에서 시작 지점 쪽으로 knockbackBackOffset 만큼 당긴 곳
    /// 광선 중심선에서 떨어진 쪽으로, 그리고 광선 진행 방향으로 밀려난다
    /// </summary>
    private Vector2 GetKnockbackPoint(Vector2 _start, Vector2 _dir, Vector2 _targetPosition)
    {
        float along = Vector2.Dot(_targetPosition - _start, _dir);
        return _start + _dir * Mathf.Max(0f, along - knockbackBackOffset);
    }

    /// <summary>
    /// 광선 모양을 길이 beamLength 로 늘리고, 남은 수명에 따라 가늘고 흐리게 만든다
    /// 스프라이트 피벗이 중심이므로 길이의 절반만큼 앞으로 옮겨 시작 지점에서 뻗어 나가게 한다
    /// </summary>
    private void UpdateVisual()
    {
        if (null == visual)
        {
            return;
        }

        float remain = null != lifetime ? lifetime.RemainRate : 1f;

        visual.localPosition = new Vector3(beamLength * 0.5f, 0f, 0f);
        visual.localScale = new Vector3(beamLength, width * remain, 1f);

        if (null != visualRenderer)
        {
            Color c = baseColor;
            c.a = baseColor.a * remain;
            visualRenderer.color = c;
        }
    }

    private void Awake()
    {
        if (null == lifetime)
        {
            lifetime = GetComponent<Lifetime>();
        }
        if (null == visualRenderer && null != visual)
        {
            visualRenderer = visual.GetComponent<SpriteRenderer>();
        }
        if (null != visualRenderer)
        {
            baseColor = visualRenderer.color;
        }
    }

    protected override void OnEnable()
    {
        fired = false;
        beamLength = 0f;
        hitTargets.Clear();

        // 발사 전에는 보이지 않게 한다
        if (null != visual)
        {
            visual.localScale = Vector3.zero;
        }

        base.OnEnable();
    }

    private void Update()
    {
        if (false == fired)
        {
            return;
        }

        UpdateVisual();
    }

    protected override void OnDisable()
    {
        fired = false;
        hitTargets.Clear();

        base.OnDisable();
    }
}
