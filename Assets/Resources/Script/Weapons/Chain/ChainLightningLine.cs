using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체인 번개 표시 (OneHitProjectile + ChainMovement 와 같은 오브젝트에 붙인다)
/// 발사 지점 → 타격한 적 위치들 → 현재 발사체 위치를 지그재그 선(LineRenderer, 월드 좌표)으로 잇는다
/// 지그재그 모양은 jitterInterval 마다 다시 뽑는다. 발사체가 꺼지면 선도 같이 사라진다
/// </summary>
[RequireComponent(typeof(OneHitProjectile))]
public class ChainLightningLine
    : MonoBehaviour
    , Initializable
    , IProjectileHitListener
{
    [Tooltip("번개 선. 비워두면 자식에서 찾는다 (useWorldSpace 는 켜서 쓴다)")]
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
    [Tooltip("지그재그 모양을 다시 뽑는 간격 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float jitterInterval = 0.05f;
    [Tooltip("선에 남길 최대 타격 지점 수 (오래된 지점부터 지운다)")]
    [Min(1)]
    [SerializeField]
    private int maxPoints = 12;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Vector3> linePositions = new List<Vector3>();
    private float nextJitterTime;

    private void Awake()
    {
        if (null == line)
        {
            line = GetComponentInChildren<LineRenderer>(true);
        }
        if (null != line)
        {
            line.useWorldSpace = true;
            line.positionCount = 0;
        }
    }

    /// <summary>
    /// 발사 지점부터 다시 그린다
    /// </summary>
    public void Initialize(BlackBoard _data)
    {
        points.Clear();
        if (true == _data.TryGetStartPoint(out Vector3 start))
        {
            points.Add(start);
        }
        else
        {
            points.Add(transform.position);
        }
        nextJitterTime = 0f;
    }

    public void OnProjectileHit(OneHitProjectile _projectile, DamagePipeline _target, int _hitIndex)
    {
        if (null == _target)
        {
            return;
        }

        if (points.Count >= maxPoints)
        {
            points.RemoveAt(0);
        }
        points.Add(_target.transform.position);
    }

    private void LateUpdate()
    {
        if (null == line || 0 == points.Count)
        {
            return;
        }

        if (Time.time < nextJitterTime)
        {
            // 마지막 점만 현재 위치로 맞춘다
            if (line.positionCount > 0)
            {
                line.SetPosition(line.positionCount - 1, transform.position);
            }
            return;
        }
        nextJitterTime = Time.time + jitterInterval;

        linePositions.Clear();
        Vector3 end = transform.position;
        for (int i = 0; i < points.Count; ++i)
        {
            Vector3 from = points[i];
            Vector3 to = i + 1 < points.Count ? points[i + 1] : end;
            AddLink(from, to);
        }
        linePositions.Add(end);

        line.positionCount = linePositions.Count;
        for (int i = 0; i < linePositions.Count; ++i)
        {
            line.SetPosition(i, linePositions[i]);
        }
    }

    /// <summary>
    /// _from 에서 _to 직전까지 지그재그 마디를 추가한다 (_to 는 다음 구간의 시작점으로 들어간다)
    /// </summary>
    private void AddLink(Vector3 _from, Vector3 _to)
    {
        linePositions.Add(_from);

        Vector3 delta = _to - _from;
        delta.z = 0f;
        if (delta.sqrMagnitude < 0.0001f || segmentsPerLink <= 1)
        {
            return;
        }

        Vector3 normal = new Vector3(-delta.y, delta.x, 0f).normalized;
        for (int s = 1; s < segmentsPerLink; ++s)
        {
            float t = (float)s / segmentsPerLink;
            linePositions.Add(_from + delta * t + normal * Random.Range(-jitter, jitter));
        }
    }

    private void OnDisable()
    {
        points.Clear();
        if (null != line)
        {
            line.positionCount = 0;
        }
    }
}
