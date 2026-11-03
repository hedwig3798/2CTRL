using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 낙뢰 발사체 (무기 Repeat, fireCount 1 로 쓴다. 발사체 1 개가 여러 대상을 맡는다)
/// 발사 시 무기 사거리(FinalRange) 안의 살아있는 적 중 StrikeCount 명을 중복 없이 무작위로 고르고 (적이 없으면 발사하지 않는다),
/// 대상 위에 표식을 markDelay 초 띄운 뒤 그 자리에 낙뢰를 떨어뜨려 반경(areaRadius) 안의 적 모두에게 데미지를 준다
/// - 표식은 대상을 따라간다. 대상이 표식 중 죽거나 사라지면 마지막으로 본 위치에 떨어진다
/// - 낙뢰 표시가 effectDuration 초 보인 뒤 풀로 돌아간다 (Lifetime 을 붙이지 않는다)
/// - 대상 수 : weapon.GetParamInt(StrikeCount, strikeCount)
/// - ★ FlagStormLink : 낙뢰 직후 낙뢰 지점들을 전기선으로 잇는다
///   3 지점 이상이면 볼록 껍질(3 명이면 삼각형) 안쪽 + 테두리 선 위, 2 지점이면 두 점 사이 선(두께 linkWidth) 위의 적에게 데미지 * linkDamageRate (같은 적 1 회)
/// </summary>
public class LightningProjectile
    : Projectile
{
    [Header("strike")]
    [Tooltip("한 번에 낙뢰를 맞히는 대상 수 기본값. 업그레이드 StrikeCount 가 더해진다")]
    [Min(1)]
    [SerializeField]
    private int strikeCount = 1;
    [Tooltip("표식이 뜬 뒤 낙뢰가 떨어질 때까지의 시간 (초)")]
    [Min(0f)]
    [SerializeField]
    private float markDelay = 0.5f;
    [Tooltip("무기 areaRadius 가 0 이하일 때 쓰는 낙뢰 반경")]
    [Min(0.01f)]
    [SerializeField]
    private float defaultAreaRadius = 0.8f;
    [Tooltip("낙뢰 / 연결선이 보이는 시간 (초). 끝나면 풀로 돌아간다")]
    [Min(0.01f)]
    [SerializeField]
    private float effectDuration = 0.25f;

    [Header("storm link (★)")]
    [Tooltip("FlagStormLink 연결 데미지 비율 (0.6 = 60%)")]
    [Min(0f)]
    [SerializeField]
    private float linkDamageRate = 0.6f;
    [Tooltip("연결선 판정 두께 (2 명일 때 선 위 판정, 3 명 이상일 때 테두리 판정)")]
    [Min(0.01f)]
    [SerializeField]
    private float linkWidth = 0.6f;

    [Header("visual")]
    [Tooltip("표식 원본 (자식 오브젝트, 정렬 레이어 attack). 대상 수만큼 복제해 쓴다. 비워두면 표식을 보이지 않는다")]
    [SerializeField]
    private GameObject markerTemplate;
    [Tooltip("표식 원본 크기 1 일 때의 반지름. 표식 크기를 낙뢰 반경에 맞춘다 (0 이면 크기를 바꾸지 않는다)")]
    [Min(0f)]
    [SerializeField]
    private float markerUnitRadius = 0.5f;
    [Tooltip("낙뢰 선 원본 (자식 LineRenderer, 정렬 레이어 attack). 대상 수만큼 복제해 쓴다. 비워두면 낙뢰 선을 보이지 않는다")]
    [SerializeField]
    private LineRenderer boltTemplate;
    [Tooltip("낙뢰 선이 시작하는 높이 (낙뢰 지점 위쪽)")]
    [Min(0f)]
    [SerializeField]
    private float boltHeight = 6f;
    [Tooltip("★ 연결선 (자식 LineRenderer, 정렬 레이어 attack). 비워두면 연결선을 보이지 않는다 (판정은 그대로)")]
    [SerializeField]
    private LineRenderer linkLine;
    [Tooltip("두 점 사이를 나누는 마디 수 (1 이면 직선)")]
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

    private readonly List<Transform> candidates = new List<Transform>();
    private readonly List<Transform> targets = new List<Transform>();
    private readonly List<bool> tracking = new List<bool>();
    private readonly List<Vector3> strikePoints = new List<Vector3>();
    private readonly List<Vector2> hull = new List<Vector2>();
    private readonly List<Vector2> sortBuffer = new List<Vector2>();
    private readonly List<Transform> linkBuffer = new List<Transform>();
    private readonly List<Vector3> linePositions = new List<Vector3>();

    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<LineRenderer> bolts = new List<LineRenderer>();

    private Vector3 markerBaseScale = Vector3.one;
    private bool isStriking;
    private bool isLinked;
    private float strikeTime;
    private float endTime;
    private float nextJitterTime;

    /// <summary>
    /// 이번 발사의 대상 수
    /// </summary>
    public int TargetCount => targets.Count;

    /// <summary>
    /// 낙뢰가 이미 떨어졌는지
    /// </summary>
    public bool IsStriking => isStriking;

    /// <summary>
    /// 낙뢰 반경 (무기 areaRadius, 0 이하면 defaultAreaRadius)
    /// </summary>
    public float FinalAreaRadius => null != weapon && weapon.areaRadius > 0f ? weapon.areaRadius : defaultAreaRadius;

    private void Awake()
    {
        if (null != markerTemplate)
        {
            markerBaseScale = markerTemplate.transform.localScale;
            markerTemplate.SetActive(false);
        }
        if (null != boltTemplate)
        {
            boltTemplate.useWorldSpace = true;
            boltTemplate.positionCount = 0;
            boltTemplate.gameObject.SetActive(false);
        }
        if (null != linkLine)
        {
            linkLine.useWorldSpace = true;
            linkLine.positionCount = 0;
        }
    }

    protected override bool OnLaunch()
    {
        ResetState(true);

        int want = Mathf.Max(1, weapon.GetParamInt(WeaponParam.StrikeCount, strikeCount));
        int found = FindAll(weapon.transform.position, weapon.FinalRange, candidates);
        if (0 == found)
        {
            return false;
        }

        // 앞에서부터 무작위로 섞으며 want 명만 뽑는다 (중복 없음)
        int pick = Mathf.Min(want, found);
        for (int i = 0; i < pick; ++i)
        {
            int j = Random.Range(i, found);
            Transform t = candidates[j];
            candidates[j] = candidates[i];
            candidates[i] = t;

            targets.Add(t);
            tracking.Add(true);
            strikePoints.Add(t.position);
        }
        candidates.Clear();

        transform.position = weapon.transform.position;
        strikeTime = Time.time + markDelay;
        ShowMarkers();
        return true;
    }

    private void Update()
    {
        if (null == weapon || 0 == targets.Count)
        {
            return;
        }

        if (false == isStriking)
        {
            TrackTargets();
            UpdateMarkers();

            if (Time.time >= strikeTime)
            {
                Strike();
            }
            return;
        }

        if (Time.time >= endTime)
        {
            gameObject.SetActive(false);
            return;
        }

        if (Time.time >= nextJitterTime)
        {
            DrawLines();
        }
    }

    /// <summary>
    /// 살아 있는 대상의 위치를 낙뢰 지점으로 기록한다. 한 번 죽거나 사라진 대상은 다시 따라가지 않는다 (풀 재사용 대비)
    /// </summary>
    private void TrackTargets()
    {
        for (int i = 0; i < targets.Count; ++i)
        {
            if (false == tracking[i])
            {
                continue;
            }

            if (false == IsAlive(targets[i]))
            {
                tracking[i] = false;
                continue;
            }

            strikePoints[i] = targets[i].position;
        }
    }

    private void Strike()
    {
        TrackTargets();
        HideMarkers();
        isStriking = true;
        endTime = Time.time + effectDuration;

        float radius = FinalAreaRadius;
        for (int i = 0; i < strikePoints.Count; ++i)
        {
            ApplyAreaDamage(strikePoints[i], radius);
        }

        isLinked = strikePoints.Count >= 2 && true == weapon.HasFlag(WeaponParam.FlagStormLink);
        if (true == isLinked)
        {
            BuildHull();
            ApplyLinkDamage();
        }

        DrawLines();
    }

    #region Storm Link
    /// <summary>
    /// 낙뢰 지점들의 볼록 껍질을 반시계 방향으로 만든다 (Andrew monotone chain)
    /// 일직선이면 양 끝 2 점, 모두 같은 점이면 1 점이 된다
    /// </summary>
    private void BuildHull()
    {
        hull.Clear();
        sortBuffer.Clear();
        for (int i = 0; i < strikePoints.Count; ++i)
        {
            sortBuffer.Add(strikePoints[i]);
        }
        sortBuffer.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        int n = sortBuffer.Count;
        // 아래 껍질
        for (int i = 0; i < n; ++i)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], sortBuffer[i]) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(sortBuffer[i]);
        }
        // 위 껍질
        int lower = hull.Count + 1;
        for (int i = n - 2; i >= 0; --i)
        {
            while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], sortBuffer[i]) <= 0f)
            {
                hull.RemoveAt(hull.Count - 1);
            }
            hull.Add(sortBuffer[i]);
        }
        // 마지막 점은 첫 점과 같다
        hull.RemoveAt(hull.Count - 1);

        // 모두 같은 점이면 1 점만 남긴다
        if (2 == hull.Count && (hull[0] - hull[1]).sqrMagnitude < 0.0001f)
        {
            hull.RemoveAt(1);
        }
        sortBuffer.Clear();
    }

    private static float Cross(Vector2 _o, Vector2 _a, Vector2 _b)
    {
        return (_a.x - _o.x) * (_b.y - _o.y) - (_a.y - _o.y) * (_b.x - _o.x);
    }

    /// <summary>
    /// 연결 영역(껍질 안쪽 + 테두리 선) 위의 적에게 데미지 * linkDamageRate 를 한 번씩 준다
    /// </summary>
    private void ApplyLinkDamage()
    {
        if (hull.Count < 2)
        {
            return;
        }

        // 껍질을 감싸는 원으로 후보를 찾고 영역 판정으로 거른다 (FindAll 이 같은 적을 한 번만 담는다)
        Vector2 center = Vector2.zero;
        for (int i = 0; i < hull.Count; ++i)
        {
            center += hull[i];
        }
        center /= hull.Count;

        float halfWidth = linkWidth * 0.5f;
        float radius = 0f;
        for (int i = 0; i < hull.Count; ++i)
        {
            radius = Mathf.Max(radius, Vector2.Distance(center, hull[i]));
        }
        radius += halfWidth;

        int count = FindAll(center, radius, linkBuffer);
        if (0 == count)
        {
            return;
        }

        float baseDamage = damageMassage.damage;
        damageMassage.damage = baseDamage * linkDamageRate;
        for (int i = 0; i < count; ++i)
        {
            Transform t = linkBuffer[i];
            if (false == IsInLink(t.position, halfWidth))
            {
                continue;
            }

            if (t.TryGetComponent(out DamagePipeline dp))
            {
                SendDamage(dp, center);
            }
        }
        damageMassage.damage = baseDamage;
        linkBuffer.Clear();
    }

    /// <summary>
    /// 껍질 안쪽(3 점 이상)이거나 테두리 선에서 _halfWidth 안인지
    /// </summary>
    private bool IsInLink(Vector2 _point, float _halfWidth)
    {
        if (hull.Count >= 3 && true == IsInsideHull(_point))
        {
            return true;
        }

        int edgeCount = hull.Count >= 3 ? hull.Count : 1;
        float sqrHalf = _halfWidth * _halfWidth;
        for (int i = 0; i < edgeCount; ++i)
        {
            Vector2 a = hull[i];
            Vector2 b = hull[(i + 1) % hull.Count];
            if (SqrDistanceToSegment(_point, a, b) <= sqrHalf)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 반시계 방향 볼록 껍질 안쪽인지 (경계 포함)
    /// </summary>
    private bool IsInsideHull(Vector2 _point)
    {
        for (int i = 0; i < hull.Count; ++i)
        {
            if (Cross(hull[i], hull[(i + 1) % hull.Count], _point) < 0f)
            {
                return false;
            }
        }
        return true;
    }

    private static float SqrDistanceToSegment(Vector2 _p, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float lengthSqr = ab.sqrMagnitude;
        if (lengthSqr < 0.0001f)
        {
            return (_p - _a).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / lengthSqr);
        return (_p - (_a + ab * t)).sqrMagnitude;
    }
    #endregion

    #region Visual
    private void ShowMarkers()
    {
        if (null == markerTemplate)
        {
            return;
        }

        float scale = markerUnitRadius > 0f ? FinalAreaRadius / markerUnitRadius : 1f;
        for (int i = 0; i < targets.Count; ++i)
        {
            GameObject marker = GetMarker(i);
            marker.transform.localScale = markerBaseScale * scale;
            marker.SetActive(true);
        }
        UpdateMarkers();
    }

    private void UpdateMarkers()
    {
        if (null == markerTemplate)
        {
            return;
        }

        for (int i = 0; i < targets.Count && i < markers.Count; ++i)
        {
            Vector3 p = strikePoints[i];
            markers[i].transform.position = new Vector3(p.x, p.y, markers[i].transform.position.z);
        }
    }

    private void HideMarkers()
    {
        for (int i = 0; i < markers.Count; ++i)
        {
            if (null != markers[i])
            {
                markers[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// _index 번째 표식. 모자라면 원본을 복제한다 (원본은 꺼둔 채 둔다)
    /// </summary>
    private GameObject GetMarker(int _index)
    {
        while (markers.Count <= _index)
        {
            GameObject clone = Instantiate(markerTemplate, markerTemplate.transform.parent);
            clone.SetActive(false);
            markers.Add(clone);
        }
        return markers[_index];
    }

    private LineRenderer GetBolt(int _index)
    {
        while (bolts.Count <= _index)
        {
            LineRenderer clone = Instantiate(boltTemplate, boltTemplate.transform.parent);
            clone.useWorldSpace = true;
            clone.positionCount = 0;
            clone.gameObject.SetActive(false);
            bolts.Add(clone);
        }
        return bolts[_index];
    }

    /// <summary>
    /// 낙뢰 선과 연결선을 지그재그로 다시 그린다
    /// </summary>
    private void DrawLines()
    {
        nextJitterTime = Time.time + jitterInterval;

        if (null != boltTemplate)
        {
            for (int i = 0; i < strikePoints.Count; ++i)
            {
                LineRenderer bolt = GetBolt(i);
                Vector3 end = strikePoints[i];
                Vector3 start = end + Vector3.up * boltHeight;

                linePositions.Clear();
                LightningLineUtil.AddLink(linePositions, start, end, segmentsPerLink, jitter);
                linePositions.Add(end);
                LightningLineUtil.Apply(bolt, linePositions);
                bolt.gameObject.SetActive(true);
            }
        }

        if (null != linkLine)
        {
            if (true == isLinked && hull.Count >= 2)
            {
                float z = transform.position.z;
                linePositions.Clear();
                int edgeCount = hull.Count >= 3 ? hull.Count : 1;
                for (int i = 0; i < edgeCount; ++i)
                {
                    Vector2 a = hull[i];
                    Vector2 b = hull[(i + 1) % hull.Count];
                    LightningLineUtil.AddLink(linePositions, new Vector3(a.x, a.y, z), new Vector3(b.x, b.y, z), segmentsPerLink, jitter);
                }
                // 마지막 구간의 끝점 (3 점 이상이면 첫 점으로 닫힌다)
                Vector2 last = hull[edgeCount % hull.Count];
                linePositions.Add(new Vector3(last.x, last.y, z));
                LightningLineUtil.Apply(linkLine, linePositions);
            }
            else
            {
                LightningLineUtil.Clear(linkLine);
            }
        }
        linePositions.Clear();
    }

    private void HideLines()
    {
        for (int i = 0; i < bolts.Count; ++i)
        {
            if (null != bolts[i])
            {
                LightningLineUtil.Clear(bolts[i]);
                bolts[i].gameObject.SetActive(false);
            }
        }
        LightningLineUtil.Clear(linkLine);
    }
    #endregion

    /// <summary>
    /// 상태를 초기화한다. _hideVisuals 면 표식 / 낙뢰 선 오브젝트도 끈다
    /// (OnDisable 중에는 자식 SetActive 를 피하고 선만 비운다. 다음 발사 OnLaunch 에서 끈다)
    /// </summary>
    private void ResetState(bool _hideVisuals)
    {
        isStriking = false;
        isLinked = false;
        strikeTime = 0f;
        endTime = 0f;
        nextJitterTime = 0f;
        targets.Clear();
        tracking.Clear();
        strikePoints.Clear();
        candidates.Clear();
        hull.Clear();
        linkBuffer.Clear();

        if (true == _hideVisuals)
        {
            HideMarkers();
            HideLines();
            return;
        }

        for (int i = 0; i < bolts.Count; ++i)
        {
            LightningLineUtil.Clear(bolts[i]);
        }
        LightningLineUtil.Clear(linkLine);
    }

    protected override void OnDisable()
    {
        ResetState(false);

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.95f, 0.4f, 0.8f);
        for (int i = 0; i < strikePoints.Count; ++i)
        {
            Gizmos.DrawWireSphere(strikePoints[i], FinalAreaRadius);
        }
    }
}
