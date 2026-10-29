using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중심에서 바깥으로 퍼지는 충격파 발사체
/// ScaleOverLifetime 이 커지게 만든 현재 반지름 안에 들어온 적을 한 번씩만 타격한다
/// 타격 지점은 충격파 중심 (발사체 위치) 이므로 넉백은 중심에서 바깥쪽으로 밀어낸다
/// 쿨타임이 수명보다 짧아져 충격파가 겹쳐도 각 충격파는 자기 타격 기록만 쓰므로 한 파동당 한 번만 맞는다
/// 이동(주인 따라가기)은 BlackBoard 로 초기화되는 이동 컴포넌트(Follow 등)가 담당한다
/// </summary>
public class ShockwaveProjectile
    : Projectile
{
    [Header("shockwave")]
    [Tooltip("켜면 주인 주변 areaRadius 안에 적이 있을 때만 발사한다. 끄면 적이 없어도 쿨타임마다 발사한다")]
    [SerializeField]
    private bool requireTargetInRange;
    [Tooltip("현재 반지름을 계산할 기준 트랜스폼 (ScaleOverLifetime 이 크기를 바꾸는 대상). 비워두면 이 오브젝트")]
    [SerializeField]
    private Transform radiusSource;
    [Tooltip("크기 1 일 때의 반지름 (ScaleOverLifetime 의 unitRadius 와 같게 맞춘다)")]
    [Min(0.01f)]
    [SerializeField]
    private float unitRadius = 0.5f;

    [Header("fade")]
    [Tooltip("수명에 따라 투명해질 스프라이트. 비워두면 페이드하지 않는다")]
    [SerializeField]
    private SpriteRenderer fadeRenderer;
    [Tooltip("진행률 계산에 쓸 Lifetime. 비워두면 같은 오브젝트의 Lifetime")]
    [SerializeField]
    private Lifetime lifetime;
    [Tooltip("이 진행률 (0 ~ 1) 부터 투명해지기 시작해 수명이 끝날 때 완전히 사라진다")]
    [Range(0f, 1f)]
    [SerializeField]
    private float fadeStartRate = 0.5f;

    // 이번 파동에서 이미 타격한 대상
    private HashSet<Transform> hitTargets = new HashSet<Transform>();
    private List<Transform> buffer = new List<Transform>();

    private Color baseColor = Color.white;

    /// <summary>
    /// 현재 충격파 반지름 (기준 트랜스폼의 크기 * unitRadius)
    /// </summary>
    private float CurrentRadius => Mathf.Abs(radiusSource.lossyScale.x) * unitRadius;

    private void Awake()
    {
        if (null == radiusSource)
        {
            radiusSource = transform;
        }
        if (null == lifetime)
        {
            lifetime = GetComponent<Lifetime>();
        }
        if (null != fadeRenderer)
        {
            baseColor = fadeRenderer.color;
        }
    }

    protected override bool OnLaunch()
    {
        hitTargets.Clear();
        if (false == requireTargetInRange)
        {
            return true;
        }

        Transform center = null != weapon.owner ? weapon.owner : weapon.transform;
        return null != FindNearest(center.position, weapon.areaRadius);
    }

    protected override bool OnLaunchAt()
    {
        hitTargets.Clear();
        return true;
    }

    /// <summary>
    /// ScaleOverLifetime(Update) 이 크기를 바꾼 뒤에 현재 반지름으로 타격한다
    /// </summary>
    private void LateUpdate()
    {
        UpdateFade();

        if (null == weapon)
        {
            return;
        }

        float radius = CurrentRadius;
        if (radius <= 0f)
        {
            return;
        }

        Vector3 center = transform.position;
        int count = FindAll(center, radius, buffer, hitTargets);
        for (int i = 0; i < count; ++i)
        {
            Transform target = buffer[i];
            hitTargets.Add(target);
            if (target.TryGetComponent(out DamagePipeline dp))
            {
                // 타격 지점 = 발사체 위치 = 충격파 중심
                ApplyDamage(dp);
            }
        }
        buffer.Clear();
    }

    private void UpdateFade()
    {
        if (null == fadeRenderer)
        {
            return;
        }

        float progress = null != lifetime ? 1f - lifetime.RemainRate : 0f;
        float alpha = 1f;
        if (progress > fadeStartRate)
        {
            alpha = fadeStartRate < 1f ? 1f - (progress - fadeStartRate) / (1f - fadeStartRate) : 0f;
        }

        Color color = baseColor;
        color.a = baseColor.a * Mathf.Clamp01(alpha);
        fadeRenderer.color = color;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        hitTargets.Clear();
        if (null != fadeRenderer)
        {
            fadeRenderer.color = baseColor;
        }
    }

    protected override void OnDisable()
    {
        hitTargets.Clear();
        buffer.Clear();

        base.OnDisable();
    }
}
