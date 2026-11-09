using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체인 번개 발사체. 이동하지 않고 발사한 그 프레임에 전이 대상 전부를 타격한다
/// 무기 사거리(FinalRange) 안의 가장 가까운 적 → 그 적의 chainRange 안의 다음 적 순으로 chainCount 번 전이하며, 같은 적은 다시 맞지 않는다 (적이 없으면 발사하지 않는다)
/// 타격 후 무기 위치와 타격 지점을 잇는 지그재그 선을 holdDuration 초 보여주고 fadeDuration 초에 걸쳐 흐리게 한 뒤 풀로 돌아간다
/// - 타격 수 상한 : 1 + chainCount 와 weapon.hitCount 중 작은 값
/// - ★ FlagChainAmplify : n 번째 타격(0 부터) 데미지 = 기본 데미지 * (1 + amplifyRate * n)
/// </summary>
public class ChainLightningProjectile
    : Projectile
{
    [Header("chain")]
    [Tooltip("★ 증폭: 전이 한 번마다 더해지는 데미지 비율 (0.15 = +15%)")]
    [Min(0f)]
    [SerializeField]
    private float amplifyRate = 0.15f;

    [Header("visual")]
    [Tooltip("번개 선 (자식 LineRenderer, 정렬 레이어 attack). 비워두면 자식에서 찾는다")]
    [SerializeField]
    private LineRenderer line;
    [Tooltip("두 점 사이를 나누는 마디 수 (1 이면 직선)")]
    [Min(1)]
    [SerializeField]
    private int segmentsPerLink = 4;
    [Tooltip("마디가 옆으로 흔들리는 최대 거리")]
    [Min(0f)]
    [SerializeField]
    private float jitter = 0.15f;
    [Tooltip("선이 그대로 보이는 시간 (초)")]
    [Min(0f)]
    [SerializeField]
    private float holdDuration = 0.1f;
    [Tooltip("선이 서서히 사라지는 시간 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeDuration = 0.3f;

    private readonly HashSet<Transform> visited = new HashSet<Transform>();
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Vector3> linePositions = new List<Vector3>();

    // 선 페이드용 그라디언트 (프리팹 색 유지, 알파만 줄인다)
    private Gradient fadeGradient;
    private GradientAlphaKey[] baseAlphaKeys;
    private GradientAlphaKey[] fadeAlphaKeys;

    private float strikeTime;

    /// <summary>
    /// 이번 발사에서 타격한 적 수
    /// </summary>
    public int StruckCount => Mathf.Max(0, points.Count - 1);

    private void Awake()
    {
        if (null == line)
        {
            line = GetComponentInChildren<LineRenderer>(true);
        }
        if (null == line)
        {
            return;
        }

        line.useWorldSpace = true;
        line.positionCount = 0;
        fadeGradient = line.colorGradient;
        baseAlphaKeys = fadeGradient.alphaKeys;
        fadeAlphaKeys = (GradientAlphaKey[])baseAlphaKeys.Clone();
    }

    protected override bool OnLaunch()
    {
        points.Clear();

        Vector3 start = weapon.transform.position;
        Transform target = FindNearest(start, weapon.FinalRange);
        if (null == target)
        {
            return false;
        }

        transform.position = start;
        points.Add(start);
        StrikeChain(target, start);
        DrawLine();
        strikeTime = Time.time;
        return true;
    }

    /// <summary>
    /// _first 부터 전이 대상을 차례로 찾아 전부 타격하고, 타격 지점을 points 에 기록한다
    /// </summary>
    private void StrikeChain(Transform _first, Vector3 _start)
    {
        int maxHits = Mathf.Min(1 + Mathf.Max(0, weapon.chainCount), Mathf.Max(1, weapon.hitCount));
        float amplify = true == weapon.HasFlag(WeaponParam.FlagChainAmplify) ? amplifyRate : 0f;
        float baseDamage = damageMassage.damage;

        visited.Clear();
        Transform target = _first;
        Vector3 from = _start;
        for (int i = 0; i < maxHits && null != target; ++i)
        {
            // 타격으로 죽으면 풀로 돌아갈 수 있으므로 위치를 먼저 기록한다
            Vector3 point = target.position;
            visited.Add(target);
            points.Add(point);

            if (target.TryGetComponent(out DamagePipeline dp))
            {
                damageMassage.damage = baseDamage * (1f + amplify * i);
                SendDamage(dp, from);
            }

            from = point;
            target = i + 1 < maxHits ? FindNearest(point, weapon.chainRange, visited) : null;
        }

        damageMassage.damage = baseDamage;
        visited.Clear();
    }

    private void Update()
    {
        float elapsed = Time.time - strikeTime;
        if (elapsed >= holdDuration + fadeDuration)
        {
            gameObject.SetActive(false);
            return;
        }

        if (elapsed > holdDuration)
        {
            SetLineAlpha(1f - (elapsed - holdDuration) / fadeDuration);
        }
    }

    private void DrawLine()
    {
        if (null == line)
        {
            return;
        }

        linePositions.Clear();
        for (int i = 0; i + 1 < points.Count; ++i)
        {
            LightningLineUtil.AddLink(linePositions, points[i], points[i + 1], segmentsPerLink, jitter);
        }
        linePositions.Add(points[points.Count - 1]);

        SetLineAlpha(1f);
        LightningLineUtil.Apply(line, linePositions);
        linePositions.Clear();
    }

    private void SetLineAlpha(float _alpha)
    {
        if (null == line)
        {
            return;
        }

        for (int i = 0; i < fadeAlphaKeys.Length; ++i)
        {
            fadeAlphaKeys[i].alpha = baseAlphaKeys[i].alpha * _alpha;
        }
        fadeGradient.alphaKeys = fadeAlphaKeys;
        line.colorGradient = fadeGradient;
    }

    protected override void OnDisable()
    {
        points.Clear();
        LightningLineUtil.Clear(line);

        base.OnDisable();
    }
}
