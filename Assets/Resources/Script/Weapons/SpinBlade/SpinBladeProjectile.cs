using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전 칼날 발사체 (무기 Repeat, 쿨타임 60초. 발사체 1 개가 한 번의 발동을 맡는다)
/// 발동하면 칼날이 플레이어 주위를 spinDuration 초에 한 바퀴 돌고 사라진다. 발동 전 예고 연출은 SpinBladeTelegraph 가 맡는다
/// - 대상 : 발사 순간 무기 targetLayer 진영의 살아 있는 몬스터 전부 (거리 제한 없음. ★ 면 부활 대기 중인 몬스터 포함)
/// - 대상 수집은 HealthSystem 기준이라 MonsterBrain 이 없는 적(bat, Dummy) 도 맞는다
/// - 타격 : 대상을 칼날이 처음 지나가는 각도순으로 정렬하고, 회전 진행도만큼 앞에서부터 나눠 처리한다 (한 프레임 대량 처치 방지)
///   타격 시점에 이미 죽었거나 비활성화된 대상은 건너뛴다
/// - 일반 타격은 DamagePipeline 으로 FinalDamage, ★ FlagSpinBladeExecute 면 HealthSystem.Execute 로 즉사
/// - 대상이 없어도 예고 연출에 이어 회전은 보여준다 (발사 실패로 처리하지 않는다)
/// - 소멸은 직접 한다 (Lifetime 을 붙이지 않는다). 도중에 무기가 꺼지면 바로 풀로 돌아간다
/// </summary>
public class SpinBladeProjectile
    : Projectile
{
    /// <summary>
    /// 정렬된 타격 대상. sweepAngle 은 칼날이 이 대상에 닿을 때까지 돌아야 하는 각도 (도)
    /// </summary>
    private struct SweepTarget
    {
        public HealthSystem health;
        public float sweepAngle;
    }

    private static readonly System.Comparison<SweepTarget> SweepComparison = (_a, _b) => _a.sweepAngle.CompareTo(_b.sweepAngle);

    [Header("spin")]
    [Tooltip("한 바퀴 도는 시간 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float spinDuration = 0.4f;
    [Tooltip("플레이어 중심에서 칼날까지의 거리. 예고 연출이 끝나는 거리도 이 값을 쓴다")]
    [Min(0f)]
    [SerializeField]
    private float bladeRadius = 1.5f;
    [Tooltip("첫 번째 칼날의 시작 각도 (도, 0 = 오른쪽). 나머지 칼날은 균등하게 벌어진다. 예고 연출도 이 값을 쓴다")]
    [SerializeField]
    private float startAngle = 0f;

    [Header("visual")]
    [Tooltip("칼날 Transform (자식, 스프라이트의 +X 가 바깥쪽). 칼날 수가 타격 각도 분할 수가 된다")]
    [SerializeField]
    private Transform[] blades;
    [Tooltip("정렬 레이어를 맞출 렌더러. 비워두면 자식의 모든 렌더러")]
    [SerializeField]
    private Renderer[] renderers;
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다). 비워두면 바꾸지 않는다")]
    [SerializeField]
    private string sortingLayerName = "attack";

    private readonly List<HealthSystem> collectBuffer = new List<HealthSystem>();
    private readonly List<SweepTarget> targets = new List<SweepTarget>();

    private int nextTargetIndex;
    private float startTime;
    private bool isExecute;

    /// <summary>
    /// 이번 발동의 대상 수
    /// </summary>
    public int TargetCount => targets.Count;

    /// <summary>
    /// 플레이어 중심에서 칼날까지의 거리
    /// </summary>
    public float BladeRadius => bladeRadius;

    /// <summary>
    /// 첫 번째 칼날의 시작 각도 (도)
    /// </summary>
    public float StartAngle => startAngle;

    // 칼날 수는 프리팹의 blades 로 정해지고, 무기는 발사체를 1 개만 꺼낸다
    public override bool HandlesFireCount => true;

    private Transform Origin => null != weapon.owner ? weapon.owner : weapon.transform;

    private void Awake()
    {
        ApplySortingLayer();
    }

    private void ApplySortingLayer()
    {
        if (true == string.IsNullOrEmpty(sortingLayerName))
        {
            return;
        }

        if (null == renderers || 0 == renderers.Length)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer r in renderers)
        {
            if (null != r)
            {
                r.sortingLayerName = sortingLayerName;
            }
        }
    }

    /// <summary>
    /// _blades 를 _origin 중심 반지름 _radius 원 위에 균등하게 놓는다 (첫 칼날 각도 _angle, 각 칼날의 +X 가 바깥쪽)
    /// 예고 연출과 회전이 같은 배치를 쓴다
    /// </summary>
    public static void PlaceBlades(Transform[] _blades, Vector3 _origin, float _angle, float _radius)
    {
        if (null == _blades || 0 == _blades.Length)
        {
            return;
        }

        for (int i = 0; i < _blades.Length; ++i)
        {
            Transform blade = _blades[i];
            if (null == blade)
            {
                continue;
            }

            float angle = _angle + 360f * i / _blades.Length;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                _origin.x + Mathf.Cos(rad) * _radius
                , _origin.y + Mathf.Sin(rad) * _radius
                , blade.position.z
            );
            blade.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle));
        }
    }

    /// <summary>
    /// 대상을 확정하고 각도순으로 정렬한 뒤 회전을 시작한다 (대상이 없어도 회전은 보여준다)
    /// </summary>
    protected override bool OnLaunch()
    {
        ResetState();

        isExecute = weapon.HasFlag(WeaponParam.FlagSpinBladeExecute);
        CollectTargets();

        Vector3 origin = Origin.position;
        transform.position = new Vector3(origin.x, origin.y, weapon.transform.position.z);
        startTime = Time.time;

        PlaceBlades(blades, origin, startAngle, bladeRadius);
        return true;
    }

    private void CollectTargets()
    {
        HealthSystem.CollectAlive(weapon.targetLayer, collectBuffer, isExecute);

        Vector2 origin = Origin.position;
        float sector = 360f / Mathf.Max(1, null != blades ? blades.Length : 0);
        for (int i = 0; i < collectBuffer.Count; ++i)
        {
            HealthSystem health = collectBuffer[i];
            Vector2 delta = (Vector2)health.transform.position - origin;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            SweepTarget target = new SweepTarget();
            target.health = health;
            target.sweepAngle = Mathf.Repeat(angle - startAngle, sector);
            targets.Add(target);
        }
        collectBuffer.Clear();

        targets.Sort(SweepComparison);
    }

    private void LateUpdate()
    {
        // 무기가 꺼져도 유지형이 아니라 Weapon 이 회수해 주지 않으므로 직접 끝낸다
        if (null == weapon || false == weapon.isActiveAndEnabled)
        {
            gameObject.SetActive(false);
            return;
        }

        float progress = Mathf.Clamp01((Time.time - startTime) / spinDuration);
        float swept = progress * 360f;

        ProcessTargets(progress);

        if (progress >= 1f)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 origin = Origin.position;
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
        PlaceBlades(blades, origin, startAngle + swept, bladeRadius);
    }

    /// <summary>
    /// 각도순 대상을 회전 진행도(_progress, 0 ~ 1) 만큼 앞에서부터 처리한다. 진행도 1 이면 전부 처리된다
    /// </summary>
    private void ProcessTargets(float _progress)
    {
        int processCount = Mathf.CeilToInt(targets.Count * _progress);
        while (nextTargetIndex < processCount && nextTargetIndex < targets.Count)
        {
            HealthSystem health = targets[nextTargetIndex].health;
            ++nextTargetIndex;
            HitTarget(health);
        }
    }

    /// <summary>
    /// 대상을 타격한다. 이미 죽었거나 비활성화됐으면 건너뛴다 (★ 는 부활 대기 중인 대상도 처리한다)
    /// </summary>
    private void HitTarget(HealthSystem _health)
    {
        if (null == _health || false == _health.gameObject.activeInHierarchy)
        {
            return;
        }

        bool isHittable = false == _health.isDead || (true == isExecute && true == _health.IsDowned);
        if (false == isHittable)
        {
            return;
        }

        Vector2 hitPoint = Origin.position;
        if (true == isExecute)
        {
            DamageMassage message = damageMassage;
            message.hitPoint = hitPoint;
            _health.Execute(ref message);
            return;
        }

        if (_health.TryGetComponent(out DamagePipeline dp))
        {
            SendDamage(dp, hitPoint);
        }
    }

    private void ResetState()
    {
        targets.Clear();
        collectBuffer.Clear();
        nextTargetIndex = 0;
        startTime = 0f;
        isExecute = false;
    }

    protected override void OnDisable()
    {
        ResetState();

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, bladeRadius);
    }
}
