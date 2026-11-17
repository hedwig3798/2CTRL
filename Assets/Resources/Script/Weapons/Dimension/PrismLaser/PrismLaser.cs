using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 차원 무기: 분광 레이저 (분열탄 + 레이저)
/// - 쿨타임마다 양쪽 플레이어 화면 위치에서 각자 가장 가까운 적에게 즉발 레이저 (길이 FinalRange, 원본 레이저처럼 광선 위 적을 가까운 순서로 primaryHitCount 명까지 관통)
/// - 1차로 맞힌 적 기준 가장 가까운 적 fanOut(2) 명에게 분기 레이저, 이를 FinalFireCount 단계만큼 반복 (이미 맞은 적 제외, 한 번 발사에 같은 적 1회)
/// - 반대 진영 화면의 적도 대상 (화면 공간 거리 기준). 레이저 모양은 LineRenderer 를 풀에서 꺼내 beamVisualTime 동안 가늘어지며 사라진다
/// - 일시정지(timeScale 0)면 쿨타임·연출 모두 멈춘다
/// 수치 근거: 1차 234 = 프리팹 damage 234 × 2발(양쪽) × attackSpeed 0.5, 분기 234 = 234 × branchDamageRate 1/6 × 12갈래(2 × (2 + 4), fireCount 2) × 0.5
/// </summary>
public class PrismLaser
    : DimensionWeapon
    , IPrismTargetSource<HealthSystem>
{
    [Header("beam")]
    [Tooltip("레이저 판정 반폭 (화면 공간 단위)")]
    [Min(0f)]
    [SerializeField]
    private float beamHalfWidth = 0.15f;

    [Tooltip("1차 레이저가 관통해 때리는 최대 수 (조준 대상 포함, 가까운 순)")]
    [Min(1)]
    [SerializeField]
    private int primaryHitCount = 3;

    [Tooltip("분기 레이저가 때리는 최대 수 (조준 대상 포함, 가까운 순)")]
    [Min(1)]
    [SerializeField]
    private int branchHitCount = 1;

    [Header("branch")]
    [Tooltip("맞은 적 하나에서 갈라지는 분기 수")]
    [Min(0)]
    [SerializeField]
    private int fanOut = 2;

    [Tooltip("분기 탐색 반지름 (화면 공간 단위, 양쪽 플레이어 Range 스텟 적용)")]
    [Min(0f)]
    [SerializeField]
    private float branchRange = 6f;

    [Tooltip("분기 레이저 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float branchDamageRate = 1f / 6f;

    [Tooltip("한 번 발사의 최대 갈래 수 (1차 포함). fireCount 가 늘어도 이 이상 분기하지 않는다")]
    [Min(1)]
    [SerializeField]
    private int maxBeams = 64;

    [Header("visual (beamMaterial 이 없으면 렌더러 없이 동작)")]
    [SerializeField]
    private Material beamMaterial;

    [SerializeField]
    private Color primaryColor = new Color(1f, 0.95f, 0.8f, 1f);

    [SerializeField]
    private Color branchColor = new Color(0.7f, 0.6f, 1f, 1f);

    [Tooltip("1차 레이저 표시 두께")]
    [Min(0.01f)]
    [SerializeField]
    private float primaryVisualWidth = 0.3f;

    [Tooltip("분기 레이저 표시 두께")]
    [Min(0.01f)]
    [SerializeField]
    private float branchVisualWidth = 0.18f;

    [Tooltip("레이저가 남아 있는 시간 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float beamVisualTime = 0.2f;

    [Tooltip("렌더러 정렬 레이어 (Default 금지)")]
    [SerializeField]
    private string sortingLayerName = "attack";

    [SerializeField]
    private int sortingOrder;

    private struct ActiveBeam
    {
        public LineRenderer line;
        public float remain;
        public float width;
        public Color color;
    }

    private readonly Vector2[] origins = new Vector2[2];
    private readonly List<PrismLink<HealthSystem>> links = new List<PrismLink<HealthSystem>>(64);
    private readonly HashSet<HealthSystem> hitSet = new HashSet<HealthSystem>();
    private readonly List<StageTarget> queryBuffer = new List<StageTarget>(64);
    private readonly List<ActiveBeam> activeBeams = new List<ActiveBeam>(64);
    private ObjectPool<LineRenderer> beamPool;
    private System.Predicate<StageTarget> candidateFilter;

    private StageQuadTree currentTree;
    private float currentPrimaryDamage;
    private float currentBranchDamage;
    private float currentPrimaryRange;

    /// <summary>
    /// 마지막 발사의 갈래 (1차 + 분기). 다음 발사 때 덮어쓴다
    /// </summary>
    public IReadOnlyList<PrismLink<HealthSystem>> LastLinks => links;

    /// <summary>
    /// 분기 탐색 반지름 (양쪽 Range 스텟 적용)
    /// </summary>
    public float FinalBranchRange => Mathf.Max(0f, ApplyCombinedStat(branchRange, ValueType.Range));

    protected virtual void Awake()
    {
        candidateFilter = IsCandidate;
    }

    protected override void Update()
    {
        base.Update();
        UpdateBeams(Time.deltaTime);
    }

    protected virtual void OnDestroy()
    {
        activeBeams.Clear();
        beamPool?.Clear();
        beamPool = null;
    }

    protected override void Fire()
    {
        StageQuadTree tree = StageSpace.Targets;
        if (null == tree)
        {
            return;
        }

        int originCount = 0;
        if (true == TryGetPlayerStagePos(PlayerSide.Left, out Vector2 left))
        {
            origins[originCount++] = left;
        }
        if (true == TryGetPlayerStagePos(PlayerSide.Right, out Vector2 right))
        {
            origins[originCount++] = right;
        }
        if (0 == originCount)
        {
            return;
        }

        currentTree = tree;
        currentPrimaryDamage = FinalDamage;
        currentBranchDamage = currentPrimaryDamage * branchDamageRate;
        currentPrimaryRange = FinalRange;
        hitSet.Clear();

        PrismBranchPlanner.Plan(this, origins, originCount, currentPrimaryRange, FinalBranchRange,
            fanOut, FinalFireCount, maxBeams, links);

        currentTree = null;
        queryBuffer.Clear();
    }

    #region IPrismTargetSource
    public bool TryFindNearest(Vector2 _from, float _maxRange, out HealthSystem _target, out Vector2 _pos)
    {
        _target = null;
        _pos = _from;
        if (null == currentTree || null == candidateFilter)
        {
            return false;
        }
        if (false == currentTree.QueryNearest(_from, _maxRange, candidateFilter, out StageTarget nearest))
        {
            return false;
        }
        _target = nearest.health;
        _pos = nearest.stagePos;
        return true;
    }

    public void OnBeam(in PrismLink<HealthSystem> _link)
    {
        bool isPrimary = 0 == _link.stage;
        float damage = isPrimary ? currentPrimaryDamage : currentBranchDamage;

        Vector2 from = _link.from;
        Vector2 end = _link.to;
        if (true == isPrimary)
        {
            // 원본 레이저처럼 조준 방향으로 사거리 끝까지 뻗는다
            Vector2 delta = _link.to - _link.from;
            float dist = delta.magnitude;
            if (dist > 0.0001f && currentPrimaryRange > dist)
            {
                end = _link.from + delta / dist * currentPrimaryRange;
            }
        }

        // 조준 대상은 반드시 맞는다
        hitSet.Add(_link.target);
        StageDamage.Apply(_link.target, damage, this);

        int extra = (isPrimary ? primaryHitCount : branchHitCount) - 1;
        if (extra > 0 && null != currentTree)
        {
            currentTree.QuerySegment(from, end, beamHalfWidth, queryBuffer);
            for (int n = 0; n < extra; ++n)
            {
                int best = FindClosestCandidate(from);
                if (best < 0)
                {
                    break;
                }
                HealthSystem health = queryBuffer[best].health;
                hitSet.Add(health);
                StageDamage.Apply(health, damage, this);
            }
        }

        SpawnBeam(from, end, isPrimary);
    }
    #endregion

    private bool IsCandidate(StageTarget _target)
    {
        return true == _target.IsAlive && false == hitSet.Contains(_target.health);
    }

    /// <summary>
    /// queryBuffer 중 아직 맞지 않은 살아 있는 대상 가운데 _from 에서 가장 가까운 것 (없으면 -1)
    /// </summary>
    private int FindClosestCandidate(Vector2 _from)
    {
        int best = -1;
        float bestSq = float.MaxValue;
        for (int i = 0; i < queryBuffer.Count; ++i)
        {
            StageTarget t = queryBuffer[i];
            if (false == IsCandidate(t))
            {
                continue;
            }
            float sq = (t.stagePos - _from).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }
        return best;
    }

    #region visual
    private void SpawnBeam(Vector2 _from, Vector2 _to, bool _isPrimary)
    {
        if (null == beamMaterial)
        {
            return;
        }
        if (null == beamPool)
        {
            beamPool = new ObjectPool<LineRenderer>(CreateBeam, OnGetBeam, OnReleaseBeam, DestroyBeam, false, 16, Mathf.Max(16, maxBeams));
        }

        LineRenderer line = beamPool.Get();
        float z = transform.position.z;
        line.SetPosition(0, new Vector3(_from.x, _from.y, z));
        line.SetPosition(1, new Vector3(_to.x, _to.y, z));

        ActiveBeam beam = new ActiveBeam
        {
            line = line,
            remain = beamVisualTime,
            width = _isPrimary ? primaryVisualWidth : branchVisualWidth,
            color = _isPrimary ? primaryColor : branchColor,
        };
        ApplyBeamLook(beam);
        activeBeams.Add(beam);
    }

    private void UpdateBeams(float _dt)
    {
        if (_dt <= 0f || 0 == activeBeams.Count)
        {
            return;
        }

        for (int i = activeBeams.Count - 1; i >= 0; --i)
        {
            ActiveBeam beam = activeBeams[i];
            beam.remain -= _dt;
            if (beam.remain <= 0f || null == beam.line)
            {
                if (null != beam.line)
                {
                    beamPool.Release(beam.line);
                }
                int last = activeBeams.Count - 1;
                activeBeams[i] = activeBeams[last];
                activeBeams.RemoveAt(last);
                continue;
            }
            activeBeams[i] = beam;
            ApplyBeamLook(beam);
        }
    }

    private void ApplyBeamLook(in ActiveBeam _beam)
    {
        float rate = Mathf.Clamp01(_beam.remain / beamVisualTime);
        _beam.line.widthMultiplier = _beam.width * rate;
        Color color = _beam.color;
        color.a *= rate;
        _beam.line.startColor = color;
        _beam.line.endColor = color;
    }

    private LineRenderer CreateBeam()
    {
        GameObject go = new GameObject("PrismBeam");
        int layer = StageSpace.StageLayer;
        go.layer = layer >= 0 ? layer : gameObject.layer;
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.sharedMaterial = beamMaterial;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        go.SetActive(false);
        return line;
    }

    private static void OnGetBeam(LineRenderer _line)
    {
        _line.gameObject.SetActive(true);
    }

    private static void OnReleaseBeam(LineRenderer _line)
    {
        if (null != _line)
        {
            _line.gameObject.SetActive(false);
        }
    }

    private static void DestroyBeam(LineRenderer _line)
    {
        if (null != _line)
        {
            Destroy(_line.gameObject);
        }
    }
    #endregion
}
