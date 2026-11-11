using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전 칼날 (공장 10분 보스): 칼날이 보스 주위를 천천히 돌며 닿은 대상에게 데미지 (대상별 쿨타임)
/// HP 가 enrageHPRatio 이하면 칼날 수가 enragedCount 로 늘어난다
/// </summary>
[CreateAssetMenu(fileName = "OrbitBladesKeyword", menuName = "2CTRL/Monster/Keyword/OrbitBlades")]
public class OrbitBladesKeyword
    : MonsterKeyword
{
    [Tooltip("칼날 외형 프리팹 (스프라이트만. 보스 자식으로 생성하고 보스 레이어로 맞춘다)")]
    public GameObject bladePrefab;

    [Min(1)]
    public int count = 3;

    [Tooltip("광폭화 시 칼날 수")]
    [Min(1)]
    public int enragedCount = 4;

    [Range(0f, 1f)]
    public float enrageHPRatio = 0.5f;

    [Tooltip("보스 중심에서 칼날까지 거리")]
    [Min(0f)]
    public float radius = 2.2f;

    [Tooltip("회전 속도 (도/초)")]
    public float angularSpeed = 45f;

    [Tooltip("칼날 하나의 판정 반경")]
    [Min(0.01f)]
    public float hitRadius = 0.5f;

    [Tooltip("칼날 데미지. damageRate 를 곱한다 (보스는 1)")]
    [Min(0f)]
    public float damage = 15f;

    [Tooltip("같은 대상 재피격 간격 (초, 칼날끼리 공유)")]
    [Min(0f)]
    public float hitCooldown = 0.5f;

    [Tooltip("칼날 스프라이트가 회전 방향을 보도록 돌릴지")]
    public bool rotateBlades = true;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Attack;

    public override KeywordRuntime CreateRuntime()
    {
        return new OrbitBladesRuntime(this);
    }
}

public class OrbitBladesRuntime
    : KeywordRuntime<OrbitBladesKeyword>
{
    private readonly List<Transform> blades = new List<Transform>();
    private readonly Dictionary<DamagePipeline, float> nextHitTimes = new Dictionary<DamagePipeline, float>();
    private float angle;
    private int activeCount;

    /// <summary>
    /// 지금 보이는 칼날 수
    /// </summary>
    public int ActiveCount => activeCount;

    /// <summary>
    /// 칼날 i 의 현재 위치 (연출·테스트용)
    /// </summary>
    public Vector2 GetBladePosition(MonsterContext _ctx, int _index)
    {
        int n = Mathf.Max(1, activeCount);
        float a = (angle + 360f * _index / n) * Mathf.Deg2Rad;
        return _ctx.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * data.radius;
    }

    public OrbitBladesRuntime(OrbitBladesKeyword _data)
        : base(_data)
    {
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        angle = 0f;
        nextHitTimes.Clear();
        EnsureBlades(_ctx, Mathf.Max(data.count, data.enragedCount));
        SetActiveCount(data.count);
    }

    private void EnsureBlades(MonsterContext _ctx, int _count)
    {
        // 파괴된(씬 전환) 칼날은 버린다
        blades.RemoveAll(b => null == b);

        while (blades.Count < _count)
        {
            Transform blade;
            if (null != data.bladePrefab)
            {
                blade = Object.Instantiate(data.bladePrefab, _ctx.root).transform;
            }
            else
            {
                Debug.LogWarning($"[OrbitBlades] {data.name} 에 bladePrefab 이 없어 빈 칼날(판정만)을 만듭니다.", _ctx.root);
                blade = new GameObject("Blade").transform;
                blade.SetParent(_ctx.root, false);
            }

            // 보스 스케일과 상관없이 프리팹 크기를 유지한다
            Vector3 lossy = _ctx.root.lossyScale;
            Vector3 baseScale = null != data.bladePrefab ? data.bladePrefab.transform.localScale : Vector3.one;
            blade.localScale = new Vector3(
                baseScale.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x))
                , baseScale.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y))
                , baseScale.z);

            LayerUtils.SetLayer(blade.gameObject, _ctx.root.gameObject.layer);
            blades.Add(blade);
        }
    }

    private void SetActiveCount(int _count)
    {
        activeCount = Mathf.Clamp(_count, 0, blades.Count);
        for (int i = 0; i < blades.Count; ++i)
        {
            if (null != blades[i])
            {
                blades[i].gameObject.SetActive(i < activeCount);
            }
        }
    }

    public override void Tick(MonsterContext _ctx, float _dt)
    {
        int wanted = true == BossKeywordUtil.IsEnraged(_ctx, data.enrageHPRatio) ? data.enragedCount : data.count;
        if (wanted != activeCount)
        {
            EnsureBlades(_ctx, wanted);
            SetActiveCount(wanted);
        }

        angle = Mathf.Repeat(angle + data.angularSpeed * _dt, 360f);

        LayerMask mask = _ctx.TargetLayer;
        float dmg = data.damage * _ctx.damageRate;
        for (int i = 0; i < activeCount; ++i)
        {
            Transform blade = blades[i];
            if (null == blade)
            {
                continue;
            }

            Vector2 pos = GetBladePosition(_ctx, i);
            blade.position = new Vector3(pos.x, pos.y, blade.position.z);
            if (true == data.rotateBlades)
            {
                float a = angle + 360f * i / Mathf.Max(1, activeCount);
                blade.rotation = Quaternion.Euler(0f, 0f, a + (data.angularSpeed >= 0f ? 90f : -90f));
            }

            MonsterAreaDamage.ApplyWithCooldown(pos, data.hitRadius, mask, dmg, _ctx.pipeline, nextHitTimes, data.hitCooldown);
        }
    }

    public override void OnDeath(MonsterContext _ctx)
    {
        SetActiveCount(0);
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        SetActiveCount(0);
        nextHitTimes.Clear();
    }
}
