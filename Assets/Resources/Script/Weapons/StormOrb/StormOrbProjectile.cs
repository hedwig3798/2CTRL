using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전격 구체 발사체 (무기 Persistent. 구체 수 = FinalFireCount, 무기가 유지한다)
/// 이동은 OrbitMovement 가 맡고, 이 컴포넌트가 반경 / 속도를 매 프레임 넣어 준다
/// - 반경 = weapon.GetParam(OrbitRadius, FinalRange). 무기 range 를 기본 반경으로 쓰므로 Range 태그 패시브가 공전 반경에 적용된다 (range 가 0 이하면 defaultOrbitRadius, Range 미적용)
/// - 속도 = 1 바퀴 orbitPeriod 초 * weapon.GetParam(OrbitSpeedRate, 1)
/// - 타격 : 트리거에 닿은 적을 때린다. 같은 구체는 같은 적을 hitInterval 초에 한 번만 때린다
/// - ★ FlagStormOrbLink : 이웃한 구체를 선으로 잇고, 선 위의 적에게 linkTickInterval 마다 데미지 * linkDamageRate
///   구체 순서(formationIndex) 로 고리를 만든다. 2 개면 선 1 개, 1 개면 선 없음. 틱은 무기 단위로 하나이며 여러 선에 걸친 적도 한 번만 맞는다
/// - LateUpdate 는 OrbitMovement 뒤에 돌아야 선 끝이 구체에 붙는다 (DefaultExecutionOrder)
/// </summary>
[DefaultExecutionOrder(100)]
public class StormOrbProjectile
    : Projectile
{
    /// <summary>
    /// 같은 무기의 구체 묶음. 전기선 틱 시각과 틱 처리 버퍼를 공유한다
    /// </summary>
    private sealed class LinkGroup
    {
        public readonly List<StormOrbProjectile> orbs = new List<StormOrbProjectile>();
        public readonly HashSet<DamagePipeline> hitSet = new HashSet<DamagePipeline>();
        public readonly List<Collider2D> overlaps = new List<Collider2D>();
        public float nextTickTime;
    }

    private static readonly Dictionary<Weapon, LinkGroup> Groups = new Dictionary<Weapon, LinkGroup>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGroups()
    {
        Groups.Clear();
    }

    [Header("orbit")]
    [Tooltip("공전 이동. 비어 있으면 같은 오브젝트에서 찾는다. radius / angularSpeed 는 이 컴포넌트가 덮어쓴다")]
    [SerializeField]
    private OrbitMovement orbitMovement;
    [Tooltip("무기 range 가 0 이하일 때 쓰는 기본 반경 (보통은 무기 range 가 기본 반경)")]
    [Min(0f)]
    [SerializeField]
    private float defaultOrbitRadius = 2f;
    [Tooltip("OrbitSpeedRate 1 일 때 1 바퀴 도는 시간 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float orbitPeriod = 1.8f;

    [Header("hit")]
    [Tooltip("같은 구체가 같은 적을 다시 때리기까지의 간격 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float hitInterval = 0.5f;
    [Tooltip("지난 타격 기록을 지우는 간격 (초)")]
    [Min(0.1f)]
    [SerializeField]
    private float pruneInterval = 5f;

    [Header("link (★)")]
    [Tooltip("전기선 데미지 비율 (0.6 = 60%)")]
    [Min(0f)]
    [SerializeField]
    private float linkDamageRate = 0.6f;
    [Tooltip("전기선 데미지 간격 (초)")]
    [Min(0.02f)]
    [SerializeField]
    private float linkTickInterval = 0.3f;
    [Tooltip("전기선 판정 두께")]
    [Min(0.01f)]
    [SerializeField]
    private float linkWidth = 0.4f;
    [Tooltip("전기선 (자식 LineRenderer). 비워두면 선을 보이지 않는다 (판정은 그대로)")]
    [SerializeField]
    private LineRenderer linkLine;
    [Tooltip("두 구체 사이를 나누는 마디 수 (1 이면 직선)")]
    [Min(1)]
    [SerializeField]
    private int segmentsPerLink = 6;
    [Tooltip("마디가 옆으로 흔들리는 최대 거리")]
    [Min(0f)]
    [SerializeField]
    private float jitter = 0.2f;
    [Tooltip("지그재그 모양을 다시 뽑는 간격 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float jitterInterval = 0.05f;

    [Header("visual")]
    [Tooltip("정렬 레이어를 맞출 렌더러. 비워두면 자식의 모든 렌더러")]
    [SerializeField]
    private Renderer[] renderers;
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다). 비워두면 바꾸지 않는다")]
    [SerializeField]
    private string sortingLayerName = "attack";

    private readonly Dictionary<DamagePipeline, float> nextHitTimes = new Dictionary<DamagePipeline, float>();
    private readonly List<DamagePipeline> pruneBuffer = new List<DamagePipeline>();
    private readonly List<Vector3> linePositions = new List<Vector3>();

    private float[] jitterOffsets;
    private LinkGroup linkGroup;
    private float nextPruneTime;
    private float nextJitterTime;
    private bool isLineVisible;

    /// <summary>
    /// 업그레이드 / 패시브가 반영된 공전 반경
    /// </summary>
    public float FinalOrbitRadius
    {
        get
        {
            if (null == weapon)
            {
                return defaultOrbitRadius;
            }

            float baseRadius = weapon.range > 0f ? weapon.FinalRange : defaultOrbitRadius;
            return Mathf.Max(0f, weapon.GetParam(WeaponParam.OrbitRadius, baseRadius));
        }
    }

    /// <summary>
    /// 업그레이드가 반영된 회전 속도 (도/초)
    /// </summary>
    public float FinalAngularSpeed
    {
        get
        {
            float rate = null != weapon ? Mathf.Max(0f, weapon.GetParam(WeaponParam.OrbitSpeedRate, 1f)) : 1f;
            return 360f / orbitPeriod * rate;
        }
    }

    private void Awake()
    {
        if (null == orbitMovement)
        {
            orbitMovement = GetComponent<OrbitMovement>();
        }

        jitterOffsets = new float[Mathf.Max(0, segmentsPerLink - 1)];

        if (null != linkLine)
        {
            linkLine.useWorldSpace = true;
            linkLine.positionCount = 0;
        }

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
    /// 이동 컴포넌트 초기화(BlackBoard) 전에 반경을 넣어 첫 위치가 맞게 한다
    /// </summary>
    protected override bool OnLaunch()
    {
        ResetState();
        JoinLinkGroup();
        RefreshOrbit();
        return true;
    }

    private void Update()
    {
        if (null == weapon)
        {
            return;
        }

        // 플레이어 패시브는 UpgradeVersion 을 바꾸지 않으므로 매 프레임 다시 넣는다
        RefreshOrbit();
        PruneHitTimes();
    }

    private void RefreshOrbit()
    {
        if (null == orbitMovement)
        {
            return;
        }

        orbitMovement.radius = FinalOrbitRadius;
        orbitMovement.angularSpeed = FinalAngularSpeed;
    }

    #region Hit
    private void OnTriggerEnter2D(Collider2D _other)
    {
        TryHit(_other);
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        TryHit(_other);
    }

    private void TryHit(Collider2D _other)
    {
        if (null == weapon || 0 == (weapon.targetLayer.value & (1 << _other.gameObject.layer)))
        {
            return;
        }

        if (false == _other.TryGetComponent(out DamagePipeline dp))
        {
            return;
        }

        if (nextHitTimes.TryGetValue(dp, out float nextTime) && Time.time < nextTime)
        {
            return;
        }

        if (false == IsAlive(dp.transform))
        {
            return;
        }

        nextHitTimes[dp] = Time.time + hitInterval;

        // 데미지 업그레이드 / 플레이어 패시브를 유지 중인 구체에도 반영한다
        RefreshDamage();
        ApplyDamage(dp);
    }

    /// <summary>
    /// 간격이 지난 타격 기록을 지운다 (사라진 적이 사전에 쌓이지 않게)
    /// </summary>
    private void PruneHitTimes()
    {
        if (Time.time < nextPruneTime)
        {
            return;
        }
        nextPruneTime = Time.time + pruneInterval;

        pruneBuffer.Clear();
        foreach (KeyValuePair<DamagePipeline, float> pair in nextHitTimes)
        {
            if (Time.time >= pair.Value)
            {
                pruneBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < pruneBuffer.Count; ++i)
        {
            nextHitTimes.Remove(pruneBuffer[i]);
        }
        pruneBuffer.Clear();
    }
    #endregion

    #region Link
    private void JoinLinkGroup()
    {
        if (false == Groups.TryGetValue(weapon, out linkGroup))
        {
            linkGroup = new LinkGroup();
            Groups.Add(weapon, linkGroup);
        }
        linkGroup.orbs.Add(this);
    }

    private void LeaveLinkGroup()
    {
        if (null == linkGroup)
        {
            return;
        }

        linkGroup.orbs.Remove(this);
        if (0 == linkGroup.orbs.Count)
        {
            Groups.Remove(weapon);
        }
        linkGroup = null;
    }

    private void LateUpdate()
    {
        if (null == weapon || null == linkGroup)
        {
            return;
        }

        if (false == weapon.HasFlag(WeaponParam.FlagStormOrbLink))
        {
            HideLine();
            return;
        }

        DrawLink();

        // 틱은 무기 단위로 하나. 이번 프레임에 먼저 도는 구체가 처리한다
        if (Time.time >= linkGroup.nextTickTime)
        {
            linkGroup.nextTickTime = Time.time + linkTickInterval;
            ApplyLinkDamage();
        }
    }

    /// <summary>
    /// 이 구체에서 이어지는 다음 구체 (고리 순서). 2 개면 0 번 구체만 선을 갖는다 (같은 선이 겹치지 않게). 선이 없으면 null
    /// </summary>
    private StormOrbProjectile FindLinkTarget()
    {
        if (null == linkGroup || formationCount < 2 || (2 == formationCount && 0 != formationIndex))
        {
            return null;
        }

        int nextIndex = (formationIndex + 1) % formationCount;
        for (int i = 0; i < linkGroup.orbs.Count; ++i)
        {
            StormOrbProjectile orb = linkGroup.orbs[i];
            if (orb != this && orb.formationIndex == nextIndex)
            {
                return orb;
            }
        }
        return null;
    }

    /// <summary>
    /// 모든 전기선 위의 적에게 데미지 * linkDamageRate 를 한 번씩 준다
    /// </summary>
    private void ApplyLinkDamage()
    {
        RefreshDamage();
        float baseDamage = damageMassage.damage;
        damageMassage.damage = baseDamage * linkDamageRate;

        linkGroup.hitSet.Clear();
        // 데미지 처리 중 구체가 꺼질 수 있어 (플레이어 사망 등) 매번 그룹을 다시 확인한다
        for (int i = 0; null != linkGroup && i < linkGroup.orbs.Count; ++i)
        {
            StormOrbProjectile orb = linkGroup.orbs[i];
            StormOrbProjectile next = orb.FindLinkTarget();
            if (null != next)
            {
                HitSegment(orb.transform.position, next.transform.position);
            }
        }

        if (null != linkGroup)
        {
            linkGroup.hitSet.Clear();
            linkGroup.overlaps.Clear();
        }
        damageMassage.damage = baseDamage;
    }

    /// <summary>
    /// _from ~ _to 를 잇는 직사각형 안의 적을 타격한다. 결과는 크기가 늘어나는 List 로 받아 적이 많아도 빠뜨리지 않는다
    /// </summary>
    private void HitSegment(Vector2 _from, Vector2 _to)
    {
        Vector2 delta = _to - _from;
        float length = delta.magnitude;
        if (length < 0.001f)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(weapon.targetLayer);

        Vector2 center = (_from + _to) * 0.5f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        List<Collider2D> overlaps = linkGroup.overlaps;
        int count = Physics2D.OverlapBox(center, new Vector2(length, linkWidth), angle, filter, overlaps);

        HashSet<DamagePipeline> hitSet = linkGroup.hitSet;
        for (int i = 0; i < count && i < overlaps.Count; ++i)
        {
            Collider2D other = overlaps[i];
            if (null == other || false == IsAlive(other.transform))
            {
                continue;
            }

            if (other.TryGetComponent(out DamagePipeline dp) && true == hitSet.Add(dp))
            {
                SendDamage(dp, center);
            }
        }
    }

    /// <summary>
    /// 다음 구체까지 선을 그린다. 구체가 매 프레임 움직이므로 위치는 매 프레임, 지그재그 모양만 jitterInterval 마다 바꾼다
    /// </summary>
    private void DrawLink()
    {
        if (null == linkLine)
        {
            return;
        }

        StormOrbProjectile next = FindLinkTarget();
        if (null == next)
        {
            HideLine();
            return;
        }

        if (Time.time >= nextJitterTime)
        {
            nextJitterTime = Time.time + jitterInterval;
            for (int i = 0; i < jitterOffsets.Length; ++i)
            {
                jitterOffsets[i] = Random.Range(-jitter, jitter);
            }
        }

        Vector3 from = transform.position;
        Vector3 to = next.transform.position;
        to.z = from.z;
        Vector3 normal = new Vector3(-(to.y - from.y), to.x - from.x, 0f).normalized;
        int segments = jitterOffsets.Length + 1;

        linePositions.Clear();
        linePositions.Add(from);
        for (int s = 1; s < segments; ++s)
        {
            linePositions.Add(Vector3.Lerp(from, to, (float)s / segments) + normal * jitterOffsets[s - 1]);
        }
        linePositions.Add(to);

        LightningLineUtil.Apply(linkLine, linePositions);
        linePositions.Clear();
        isLineVisible = true;
    }

    private void HideLine()
    {
        if (false == isLineVisible)
        {
            return;
        }

        LightningLineUtil.Clear(linkLine);
        isLineVisible = false;
    }
    #endregion

    private void ResetState()
    {
        nextHitTimes.Clear();
        pruneBuffer.Clear();
        linePositions.Clear();
        nextPruneTime = 0f;
        nextJitterTime = 0f;
        HideLine();
    }

    protected override void OnDisable()
    {
        LeaveLinkGroup();
        ResetState();

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.8f);
        Vector3 center = null != orbitMovement && null != orbitMovement.target ? orbitMovement.target.position : transform.position;
        Gizmos.DrawWireSphere(center, Application.isPlaying ? FinalOrbitRadius : defaultOrbitRadius);
    }
}
