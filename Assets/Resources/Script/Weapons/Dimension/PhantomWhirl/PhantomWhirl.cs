using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 무기: 잔영 회전검 (휘두르기 + 회전 칼날)
/// - 화면 중앙(StageCenter)에서 칼날(FinalFireCount 개, 등간격)이 천천히 계속 회전한다
/// - 쿨타임(FinalCoolTime)마다 칼날 선분(QuerySegment)에 닿은 적에게 FinalDamage (한 틱에 같은 적 1회)
/// - phantomInterval 마다 잔상이 phantomDuration 동안 빠르게 1바퀴 돌며 닿는 적을 즉사 (StageDamage.Execute, 같은 적 1회)
///   즉사는 매 프레임 이번 프레임에 쓸고 지나간 각도 구간만 처리해 한 프레임 대량 처치를 피한다 (프레임당 최대 maxPhantomStepDeg)
/// - 반대 진영 화면의 적도 대상 (화면 공간 거리 기준). 일시정지(timeScale 0)면 회전·틱·잔상 모두 멈춘다
/// 수치 근거: 접촉 유효 DPS 288 = 프리팹 damage 72 × attackSpeed 4틱/초 (Sweep 10레벨 288), 잔상 즉사 주기 30초 (설계서 데미지 기준)
/// </summary>
public class PhantomWhirl
    : DimensionWeapon
{
    [Header("blade")]
    [Tooltip("칼날 길이 = 화면 공간 높이(StageRect.height) × 이 값 (중앙에서 바깥으로)")]
    [Min(0f)]
    [SerializeField]
    private float bladeLengthRate = 0.5f;

    [Tooltip("StageSpace 가 준비되지 않았을 때 쓰는 칼날 길이 (화면 공간 단위)")]
    [Min(0f)]
    [SerializeField]
    private float fallbackBladeLength = 5f;

    [Tooltip("칼날 판정 반폭 (화면 공간 단위)")]
    [Min(0f)]
    [SerializeField]
    private float bladeHalfWidth = 0.4f;

    [Tooltip("칼날 회전 속도 (도/초). 음수면 시계 방향")]
    [SerializeField]
    private float rotateSpeed = 30f;

    [Header("phantom (잔상 즉사)")]
    [Tooltip("잔상 주기 (초). 잔상이 시작될 때마다 다음 주기를 센다")]
    [Min(0.1f)]
    [SerializeField]
    private float phantomInterval = 30f;

    [Tooltip("잔상이 1바퀴 도는 시간 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float phantomDuration = 0.4f;

    [Tooltip("한 프레임에 잔상이 쓸 수 있는 최대 각도 (도). 프레임이 튀어도 대량 처치를 여러 프레임에 나눈다")]
    [Range(1f, 360f)]
    [SerializeField]
    private float maxPhantomStepDeg = 30f;

    [Tooltip("즉사 면역 대상(최종 보스 등)에게 주는 데미지 (양쪽 플레이어 Damage 스텟 적용)")]
    [Min(0f)]
    [SerializeField]
    private float phantomFallbackDamage = 3000f;

    [Header("visual (비워두면 렌더러 없이 동작)")]
    [Tooltip("칼날 스프라이트 (중심 피벗, 길이 방향 = x). 칼날 길이·visualWidth 에 맞춰 늘린다")]
    [SerializeField]
    private Sprite bladeSprite;

    [Tooltip("칼날 머티리얼 (비우면 SpriteRenderer 기본)")]
    [SerializeField]
    private Material bladeMaterial;

    [SerializeField]
    private Color bladeColor = Color.white;

    [SerializeField]
    private Color phantomColor = new Color(0.6f, 0.85f, 1f, 0.6f);

    [Tooltip("칼날 표시 두께 (화면 공간 단위)")]
    [Min(0.01f)]
    [SerializeField]
    private float visualWidth = 0.8f;

    [Tooltip("렌더러 정렬 레이어 (Default 금지)")]
    [SerializeField]
    private string sortingLayerName = "attack";

    [SerializeField]
    private int sortingOrder;

    private readonly List<StageTarget> queryBuffer = new List<StageTarget>(64);
    private readonly HashSet<HealthSystem> tickHits = new HashSet<HealthSystem>();
    private readonly HashSet<HealthSystem> executed = new HashSet<HealthSystem>();
    private readonly List<SpriteRenderer> bladeRenderers = new List<SpriteRenderer>(4);
    private SpriteRenderer phantomRenderer;

    private float bladeAngle;
    private float phantomTimer;
    private bool isPhantomActive;
    private float phantomStartAngle;
    private float phantomSwept;

    /// <summary>
    /// 현재 칼날 각도 (도, 0 ~ 360. 0 = +x)
    /// </summary>
    public float BladeAngle => bladeAngle;

    /// <summary>
    /// 잔상이 도는 중인지
    /// </summary>
    public bool IsPhantomActive => isPhantomActive;

    /// <summary>
    /// 다음 잔상까지 남은 시간 (초)
    /// </summary>
    public float RemainingPhantomTime => Mathf.Max(0f, phantomInterval - phantomTimer);

    /// <summary>
    /// 칼날 길이 (화면 공간 단위)
    /// </summary>
    public float BladeLength
    {
        get
        {
            StageSpace space = StageSpace.Instance;
            if (null == space || false == space.IsReady)
            {
                return fallbackBladeLength;
            }
            return space.StageRect.height * bladeLengthRate;
        }
    }

    /// <summary>
    /// 칼날 수 (FinalFireCount)
    /// </summary>
    public int BladeCount => FinalFireCount;

    protected override void OnActivated()
    {
        bladeAngle = 0f;
        phantomTimer = 0f;
        isPhantomActive = false;
        executed.Clear();
        SetPhantomVisible(false);
    }

    protected override void Update()
    {
        base.Update();
        if (false == IsActivated || false == IsPlaced)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            bladeAngle = Mathf.Repeat(bladeAngle + rotateSpeed * dt, 360f);
            UpdatePhantom(dt);
        }
        UpdateVisuals();
    }

    /// <summary>
    /// 칼날 틱 데미지
    /// </summary>
    protected override void Fire()
    {
        StageQuadTree tree = StageSpace.Targets;
        int count = BladeCount;
        if (null == tree || count <= 0)
        {
            return;
        }

        float damage = FinalDamage;
        if (damage <= 0f)
        {
            return;
        }

        Vector2 center = transform.position;
        float length = BladeLength;
        float step = 360f / count;
        tickHits.Clear();
        for (int b = 0; b < count; ++b)
        {
            Vector2 tip = center + AngleToDir(bladeAngle + step * b) * length;
            tree.QuerySegment(center, tip, bladeHalfWidth, queryBuffer);
            for (int i = 0; i < queryBuffer.Count; ++i)
            {
                StageTarget target = queryBuffer[i];
                if (false == target.IsAlive || false == tickHits.Add(target.health))
                {
                    continue;
                }
                StageDamage.Apply(target, damage, this);
            }
        }
        queryBuffer.Clear();
    }

    #region phantom
    private void UpdatePhantom(float _dt)
    {
        if (false == isPhantomActive)
        {
            phantomTimer += _dt;
            if (phantomTimer < phantomInterval)
            {
                return;
            }
            phantomTimer -= phantomInterval;
            isPhantomActive = true;
            phantomStartAngle = bladeAngle;
            phantomSwept = 0f;
            executed.Clear();
            SetPhantomVisible(true);
        }
        else
        {
            phantomTimer += _dt;
        }

        float step = ComputePhantomStep(_dt, phantomDuration, maxPhantomStepDeg, phantomSwept);
        float sign = rotateSpeed >= 0f ? 1f : -1f;
        float from = phantomStartAngle + sign * phantomSwept;
        ExecuteSweep(from, sign * step);
        phantomSwept += step;

        if (phantomSwept >= 360f)
        {
            isPhantomActive = false;
            executed.Clear();
            SetPhantomVisible(false);
        }
    }

    private void ExecuteSweep(float _fromDeg, float _sweepDeg)
    {
        StageQuadTree tree = StageSpace.Targets;
        if (null == tree || 0f == _sweepDeg)
        {
            return;
        }

        Vector2 center = transform.position;
        float length = BladeLength;
        float fallback = Mathf.Max(0f, ApplyCombinedStat(phantomFallbackDamage, ValueType.Damage));
        tree.QueryCircle(center, length, queryBuffer);
        for (int i = 0; i < queryBuffer.Count; ++i)
        {
            StageTarget target = queryBuffer[i];
            if (null == target.health || true == executed.Contains(target.health))
            {
                continue;
            }
            if (false == IsInSweep(center, target.stagePos, target.radius, length, bladeHalfWidth, _fromDeg, _sweepDeg))
            {
                continue;
            }
            executed.Add(target.health);
            StageDamage.Execute(target, fallback, this);
        }
        queryBuffer.Clear();
    }

    /// <summary>
    /// 이번 프레임에 잔상이 쓸 각도 (도). 1바퀴(360) 중 남은 각도와 프레임당 최대 각도를 넘지 않는다
    /// </summary>
    public static float ComputePhantomStep(float _dt, float _duration, float _maxStepDeg, float _swept)
    {
        float speed = 360f / Mathf.Max(0.0001f, _duration);
        float step = Mathf.Min(speed * Mathf.Max(0f, _dt), Mathf.Max(0f, _maxStepDeg));
        return Mathf.Clamp(step, 0f, Mathf.Max(0f, 360f - _swept));
    }

    /// <summary>
    /// 중심 _center 에서 뻗은 길이 _length · 반폭 _halfWidth 의 칼날이 _fromDeg 에서 _sweepDeg 만큼(음수면 시계 방향) 돌 때
    /// 위치 _pos · 반지름 _radius 인 대상에 닿는지
    /// - 대상 원이 칼날 길이 밖이면 false, 중심 근처(거리 ≤ 반지름 + 반폭)면 true
    /// - 각도 허용치는 대상 반지름 + 반폭이 이루는 각
    /// </summary>
    public static bool IsInSweep(Vector2 _center, Vector2 _pos, float _radius, float _length, float _halfWidth, float _fromDeg, float _sweepDeg)
    {
        if (_sweepDeg < 0f)
        {
            _fromDeg += _sweepDeg;
            _sweepDeg = -_sweepDeg;
        }

        Vector2 delta = _pos - _center;
        float dist = delta.magnitude;
        if (dist - _radius > _length)
        {
            return false;
        }

        float reach = Mathf.Max(0f, _radius) + Mathf.Max(0f, _halfWidth);
        if (dist <= reach || _sweepDeg >= 360f)
        {
            return true;
        }

        float tolerance = Mathf.Asin(Mathf.Clamp01(reach / dist)) * Mathf.Rad2Deg;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float offset = Mathf.Repeat(angle - _fromDeg + tolerance, 360f);
        return offset <= _sweepDeg + 2f * tolerance;
    }
    #endregion

    #region visual
    private void UpdateVisuals()
    {
        if (null == bladeSprite)
        {
            return;
        }

        int count = BladeCount;
        EnsureBladeRenderers(count);

        Vector2 center = transform.position;
        float length = BladeLength;
        float step = count > 0 ? 360f / count : 0f;
        for (int i = 0; i < bladeRenderers.Count; ++i)
        {
            SpriteRenderer sr = bladeRenderers[i];
            bool isOn = i < count && length > 0f;
            if (sr.gameObject.activeSelf != isOn)
            {
                sr.gameObject.SetActive(isOn);
            }
            if (true == isOn)
            {
                PlaceBlade(sr.transform, center, bladeAngle + step * i, length);
            }
        }

        if (true == isPhantomActive && null != phantomRenderer)
        {
            float sign = rotateSpeed >= 0f ? 1f : -1f;
            PlaceBlade(phantomRenderer.transform, center, phantomStartAngle + sign * phantomSwept, length);
        }
    }

    private void PlaceBlade(Transform _blade, Vector2 _center, float _angle, float _length)
    {
        Vector2 mid = _center + AngleToDir(_angle) * (_length * 0.5f);
        _blade.SetPositionAndRotation(new Vector3(mid.x, mid.y, transform.position.z), Quaternion.Euler(0f, 0f, _angle));
        Vector2 size = bladeSprite.bounds.size;
        _blade.localScale = new Vector3(
            size.x > 0f ? _length / size.x : 1f,
            size.y > 0f ? visualWidth / size.y : 1f,
            1f);
    }

    private void EnsureBladeRenderers(int _count)
    {
        while (bladeRenderers.Count < _count)
        {
            bladeRenderers.Add(CreateRenderer($"Blade_{bladeRenderers.Count}", bladeColor, sortingOrder));
        }
    }

    private void SetPhantomVisible(bool _visible)
    {
        if (null == bladeSprite)
        {
            return;
        }
        if (null == phantomRenderer)
        {
            if (false == _visible)
            {
                return;
            }
            phantomRenderer = CreateRenderer("Phantom", phantomColor, sortingOrder + 1);
        }
        phantomRenderer.gameObject.SetActive(_visible);
    }

    private SpriteRenderer CreateRenderer(string _name, Color _color, int _order)
    {
        GameObject go = new GameObject(_name);
        int layer = StageSpace.StageLayer;
        go.layer = layer >= 0 ? layer : gameObject.layer;
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = bladeSprite;
        if (null != bladeMaterial)
        {
            sr.sharedMaterial = bladeMaterial;
        }
        sr.color = _color;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = _order;
        return sr;
    }
    #endregion

    private static Vector2 AngleToDir(float _deg)
    {
        float rad = _deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
