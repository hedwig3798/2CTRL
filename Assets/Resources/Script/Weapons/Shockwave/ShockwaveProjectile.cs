using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중심에서 바깥으로 퍼지는 충격파 발사체
/// ScaleOverLifetime 이 커지게 만든 현재 반지름 안에 들어온 적을 한 번씩만 타격한다
/// 타격 지점은 충격파 중심 (발사체 위치) 이므로 넉백은 중심에서 바깥쪽으로 밀어낸다
/// 쿨타임이 수명보다 짧아져 충격파가 겹쳐도 각 충격파는 자기 타격 기록만 쓰므로 한 파동당 한 번만 맞는다
/// 이동(주인 따라가기)은 BlackBoard 로 초기화되는 이동 컴포넌트(Follow 등)가 담당한다
/// - ★ FlagContractWave : 파동이 수명을 다해 사라지면 다음 프레임에 그 자리에서 수축 파동을 한 번 더 쏜다
///   수축 파동은 무기의 발사체 프리팹을 Weapon.FireSub 로 데미지 contractDamageRate 배로 발사하고,
///   ScaleOverLifetime 의 곡선을 좌우로 뒤집어 최대 반경에서 줄어들며 링이 지나간 적을 한 번씩 타격한다
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

    [Header("contract wave (★)")]
    [Tooltip("수축 파동의 데미지 배율")]
    [Min(0f)]
    [SerializeField]
    private float contractDamageRate = 0.5f;
    [Tooltip("크기 곡선을 뒤집을 ScaleOverLifetime. 비워두면 이 오브젝트나 자식에서 찾는다")]
    [SerializeField]
    private ScaleOverLifetime scaler;

    // 확장 / 수축 크기 곡선
    private AnimationCurve expandCurve;
    private AnimationCurve contractCurve;

    // 수축 파동인지, 수축 파동이 지금까지 가장 컸던 반지름
    private bool contracting;
    private float contractMaxRadius;

    // 이번 발사에서 한 번이라도 판정을 했는지 (발사 실패로 꺼질 때 수축 파동을 쏘지 않도록)
    private bool hasWaved;

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
        if (null == scaler)
        {
            scaler = GetComponentInChildren<ScaleOverLifetime>(true);
        }
        if (null != scaler)
        {
            expandCurve = scaler.curve;
            contractCurve = MirrorCurve(expandCurve);
        }
    }

    /// <summary>
    /// 수축 파동으로 바꾼다 (Weapon.FireSub 로 발사한 직후 호출)
    /// </summary>
    public void BeginContract()
    {
        SetContracting(true);

        // LaunchAt 의 InitializeBlackBoard 가 확장 곡선으로 크기를 정했으므로, 첫 프레임부터 수축 곡선으로 그리도록 다시 초기화한다
        BlackBoard b = GetBlackBoard();
        if (null != scaler && null != b)
        {
            scaler.Initialize(b);
        }
    }

    private void SetContracting(bool _contracting)
    {
        contracting = _contracting;
        contractMaxRadius = 0f;
        hitTargets.Clear();
        if (null != scaler && null != expandCurve)
        {
            scaler.curve = true == _contracting ? contractCurve : expandCurve;
        }
    }

    /// <summary>
    /// 0 ~ 1 구간의 곡선을 좌우로 뒤집는다 (진행률 t 의 값 = 원래 곡선의 1 - t 값)
    /// </summary>
    private static AnimationCurve MirrorCurve(AnimationCurve _curve)
    {
        if (null == _curve)
        {
            return null;
        }

        Keyframe[] keys = _curve.keys;
        Keyframe[] mirrored = new Keyframe[keys.Length];
        for (int i = 0; i < keys.Length; ++i)
        {
            Keyframe k = keys[i];
            mirrored[keys.Length - 1 - i] = new Keyframe(1f - k.time, k.value, -k.outTangent, -k.inTangent);
        }

        AnimationCurve result = new AnimationCurve(mirrored);
        result.preWrapMode = _curve.postWrapMode;
        result.postWrapMode = _curve.preWrapMode;
        return result;
    }

    protected override bool OnLaunch()
    {
        SetContracting(false);
        hasWaved = false;
        if (false == requireTargetInRange)
        {
            return true;
        }

        Transform center = null != weapon.owner ? weapon.owner : weapon.transform;
        return null != FindNearest(center.position, weapon.areaRadius);
    }

    protected override bool OnLaunchAt()
    {
        SetContracting(false);
        hasWaved = false;
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

        hasWaved = true;

        float radius = CurrentRadius;
        if (true == contracting)
        {
            HitContract(radius);
            return;
        }

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

    /// <summary>
    /// 수축 파동 : 가장 컸던 반지름 안에서 현재 반지름 바깥(링이 지나간 곳)에 있는 적을 한 번씩 타격한다
    /// </summary>
    private void HitContract(float _radius)
    {
        contractMaxRadius = Mathf.Max(contractMaxRadius, _radius);
        if (contractMaxRadius <= 0f)
        {
            return;
        }

        Vector3 center = transform.position;
        float sqrRadius = _radius * _radius;
        int count = FindAll(center, contractMaxRadius, buffer, hitTargets);
        for (int i = 0; i < count; ++i)
        {
            Transform target = buffer[i];
            Vector2 offset = target.position - center;
            if (offset.sqrMagnitude < sqrRadius)
            {
                continue;
            }

            hitTargets.Add(target);
            if (target.TryGetComponent(out DamagePipeline dp))
            {
                ApplyDamage(dp);
            }
        }
        buffer.Clear();
    }

    /// <summary>
    /// ★ 확장 파동이 끝나 꺼질 때 그 자리에서 수축 파동을 예약한다
    /// 꺼지는 도중에 다른 발사체를 켜지 않도록 무기의 코루틴으로 다음 프레임에 쏜다
    /// </summary>
    private void TryStartContractWave()
    {
        if (true == contracting || false == hasWaved)
        {
            return;
        }

        if (null == weapon
            || null == weapon.projectile
            || false == weapon.isActiveAndEnabled
            || false == gameObject.scene.isLoaded
            || false == weapon.HasFlag(WeaponParam.FlagContractWave))
        {
            return;
        }

        weapon.StartCoroutine(ContractWaveRoutine(weapon, transform.position, contractDamageRate));
    }

    private static IEnumerator ContractWaveRoutine(Weapon _weapon, Vector3 _center, float _damageRate)
    {
        yield return null;

        if (null == _weapon)
        {
            yield break;
        }

        Projectile p = _weapon.FireSub(_weapon.projectile, _center, _center + Vector3.right, _damageRate);
        if (p is ShockwaveProjectile wave)
        {
            wave.BeginContract();
        }
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
        TryStartContractWave();
        hasWaved = false;

        hitTargets.Clear();
        buffer.Clear();

        base.OnDisable();
    }
}
