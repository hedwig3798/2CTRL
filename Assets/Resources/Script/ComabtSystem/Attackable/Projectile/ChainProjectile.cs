using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전이 발사체의 타겟 정보
/// </summary>
public struct ChainTarget
{
    public Transform transform;
    public DamagePipeline damagePipeline;
    public HealthSystem healthSystem;
}

/// <summary>
/// 가장 가까운 적부터 시작해 전이 횟수만큼 적 사이를 튕기는 전이 발사체
/// 발사 시점에 타격할 적 목록을 미리 결정하고 순서대로 추적하며 타격한다
/// 충돌 판정을 쓰지 않고 거리로 도착을 판정하므로 경로 위의 다른 적은 무시한다
/// </summary>
public class ChainProjectile
    : Projectile
{
    [Header("line effect")]
    public LineRenderer line;

    [Header("chain value")]
    [Tooltip("전이 횟수 (총 타격 수 = 1 + chainCount)")]
    public int chainCount;
    [Tooltip("이전 타겟 기준 전이 탐색 범위")]
    public float chainRange;
    [Tooltip("기본 이동 속도 (무기의 speedRate 가 곱해진다)")]
    public float speed;
    [Tooltip("타겟과의 거리가 이 값 이하가 되면 타격")]
    public float hitDistance = 0.2f;
    [Tooltip("타격 후 다음 타겟으로 이동하기 전 딜레이 (이 동안 구간 선이 사라진다)")]
    public float hitDelay = 0.05f;

    private List<ChainTarget> targets = new List<ChainTarget>();
    private HashSet<Transform> visited = new HashSet<Transform>();
    private float currSpeed;

    private Color lineStartColor;
    private Color lineEndColor;

    private Coroutine chainRoutine;

    protected override bool OnLaunch()
    {
        transform.position = weapon.transform.position;

        BuildChainTargets();
        if (0 == targets.Count)
        {
            return false;
        }

        float rate = weapon.speedRate;
        if (rate <= 0f)
        {
            rate = 1f;
        }
        currSpeed = speed * rate;

        if (null != chainRoutine)
        {
            StopCoroutine(chainRoutine);
        }
        chainRoutine = StartCoroutine(ChainRoutine());
        return true;
    }

    #region Target
    private void BuildChainTargets()
    {
        targets.Clear();
        visited.Clear();

        Transform curr = FindNearest(weapon.transform.position, weapon.range);
        for (int i = 0; i <= chainCount && null != curr; ++i)
        {
            if (false == TryMakeTarget(curr, out ChainTarget target))
            {
                break;
            }

            targets.Add(target);
            visited.Add(curr);
            curr = FindNearest(curr.position, chainRange, visited);
        }
    }

    private bool TryMakeTarget(Transform _transform, out ChainTarget _target)
    {
        _target = new ChainTarget();
        if (false == _transform.TryGetComponent(out DamagePipeline dp))
        {
            return false;
        }

        _transform.TryGetComponent(out HealthSystem hs);

        _target.transform = _transform;
        _target.damagePipeline = dp;
        _target.healthSystem = hs;
        return true;
    }

    private bool IsValid(ChainTarget _target)
    {
        if (null == _target.transform || null == _target.damagePipeline)
        {
            return false;
        }

        if (false == _target.transform.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (null != _target.healthSystem && true == _target.healthSystem.isDead)
        {
            return false;
        }

        return true;
    }
    #endregion

    private IEnumerator ChainRoutine()
    {
        Vector3 segmentStart = transform.position;
        float sqrHitDistance = hitDistance * hitDistance;

        foreach (ChainTarget target in targets)
        {
            if (false == IsValid(target))
            {
                continue;
            }

            BeginLine(segmentStart);

            // 타겟의 실시간 위치를 추적
            bool lost = false;
            while ((target.transform.position - transform.position).sqrMagnitude > sqrHitDistance)
            {
                if (false == IsValid(target))
                {
                    lost = true;
                    break;
                }

                transform.position = Vector3.MoveTowards(
                    transform.position
                    , target.transform.position
                    , currSpeed * Time.deltaTime
                );
                UpdateLine();
                yield return null;
            }

            if (true == lost)
            {
                EndLine();
                segmentStart = transform.position;
                continue;
            }

            transform.position = target.transform.position;
            UpdateLine();

            DamageMassage duplicateMassage = damageMassage;
            target.damagePipeline.ProcessDamage(ref duplicateMassage);

            segmentStart = transform.position;

            // 딜레이 동안 구간 선 페이드 아웃
            float timer = 0f;
            while (timer < hitDelay)
            {
                timer += Time.deltaTime;
                SetLineAlpha(1f - Mathf.Clamp01(timer / hitDelay));
                yield return null;
            }
            EndLine();
        }

        chainRoutine = null;
        gameObject.SetActive(false);
    }

    #region Line
    private void BeginLine(Vector3 _start)
    {
        if (null == line)
        {
            return;
        }

        line.positionCount = 2;
        line.SetPosition(0, _start);
        line.SetPosition(1, transform.position);
        SetLineAlpha(1f);
        line.enabled = true;
    }

    private void UpdateLine()
    {
        if (null == line || false == line.enabled)
        {
            return;
        }

        line.SetPosition(1, transform.position);
    }

    private void SetLineAlpha(float _alpha)
    {
        if (null == line)
        {
            return;
        }

        Color start = lineStartColor;
        Color end = lineEndColor;
        start.a *= _alpha;
        end.a *= _alpha;
        line.startColor = start;
        line.endColor = end;
    }

    private void EndLine()
    {
        if (null == line)
        {
            return;
        }

        line.enabled = false;
    }
    #endregion

    private void Awake()
    {
        if (null == line)
        {
            line = GetComponentInChildren<LineRenderer>(true);
        }

        if (null != line)
        {
            line.useWorldSpace = true;
            lineStartColor = line.startColor;
            lineEndColor = line.endColor;
            line.enabled = false;
        }
    }

    protected override void OnDisable()
    {
        if (null != chainRoutine)
        {
            StopCoroutine(chainRoutine);
            chainRoutine = null;
        }
        targets.Clear();
        visited.Clear();
        EndLine();

        base.OnDisable();
    }
}
