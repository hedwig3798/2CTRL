using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 차원 무기 가시 톱날 (덩굴 + 톱날)
/// - Fire 마다 톱날 FinalFireCount × sawCountMultiplier 개 (원본 톱날 fireCount 2 × 3 = 6). 각 톱날은 화면 전체(StageRect)의
///   왼쪽/오른쪽 끝(무작위) 바깥, 무작위 높이에서 반대편 끝까지 가로로 관통 이동. 같은 적은 톱날마다 1회만 타격 (QueryCircle)
/// - 톱날이 vineSpacing 만큼 지날 때마다 그 자리에 덩굴 조각을 남긴다. 조각은 vineLifetime(Duration 적용) 동안
///   vineTickInterval 마다 반지름 vineRadius 안의 모든 적을 타격 (QueryCircle)
/// - 모든 생성 오브젝트는 이 무기의 자식(레이어 = 무기 레이어 = Stage), 렌더러 정렬 레이어 sortingLayerName. Time.deltaTime 기준 (timeScale 0 이면 멈춤)
/// 수치 근거 (프리팹 권장 damage 31.2, attackSpeed 0.5, fireCount 2): 톱날 6개 × 31.2 × 0.5/초 × 평균 2.5타 = 234 + 덩굴 (31.2 / 틱 0.5) × 조각 1.5개 × 2.5타 = 234 → 468
/// </summary>
public class ThornSaw
    : DimensionWeapon
{
    #region 내부 상태
    private class Saw
    {
        public GameObject go;
        public Transform tr;
        public Vector2 dir;
        public float travelLeft;
        public float vineAccum;
        public readonly List<HealthSystem> hit = new List<HealthSystem>(16);
    }

    private class Vine
    {
        public GameObject go;
        public Transform tr;
        public SpriteRenderer sr;
        public Color baseColor;
        public float lifeLeft;
        public float tickTimer;
    }
    #endregion

    [Header("saw")]
    [Tooltip("톱날 수 = FinalFireCount × 이 값 (원본 톱날 개수의 3배)")]
    [Min(0)]
    [SerializeField]
    private int sawCountMultiplier = 3;

    [Tooltip("톱날 직격 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float sawDamageRate = 1f;

    [Tooltip("톱날 이동 속도 (화면 단위/초)")]
    [Min(0.01f)]
    [SerializeField]
    private float sawSpeed = 8f;

    [Tooltip("톱날 판정 반지름")]
    [Min(0f)]
    [SerializeField]
    private float sawRadius = 0.5f;

    [Tooltip("톱날 회전 속도 (도/초, 외형만)")]
    [SerializeField]
    private float sawSpinSpeed = 720f;

    [Tooltip("화면 끝 바깥 시작/끝 여유 거리")]
    [Min(0f)]
    [SerializeField]
    private float edgeMargin = 1f;

    [Tooltip("톱날 높이를 고를 때 화면 위/아래 끝에서 비우는 거리")]
    [Min(0f)]
    [SerializeField]
    private float verticalPadding = 0.5f;

    [Header("vine")]
    [Tooltip("톱날이 이 거리만큼 지날 때마다 덩굴 조각 1개 (0 이하면 만들지 않음)")]
    [Min(0f)]
    [SerializeField]
    private float vineSpacing = 1f;

    [Tooltip("덩굴 조각 판정 반지름")]
    [Min(0f)]
    [SerializeField]
    private float vineRadius = 0.6f;

    [Tooltip("덩굴 조각 지속 시간 (초, 플레이어 Duration 적용)")]
    [Min(0.01f)]
    [SerializeField]
    private float vineLifetime = 2f;

    [Tooltip("덩굴 조각 타격 간격 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float vineTickInterval = 0.5f;

    [Tooltip("덩굴 틱 데미지 = FinalDamage × 이 값")]
    [Min(0f)]
    [SerializeField]
    private float vineDamageRate = 1f;

    [Tooltip("동시에 있을 수 있는 덩굴 조각 최대 수 (넘으면 새 조각을 만들지 않음)")]
    [Min(1)]
    [SerializeField]
    private int maxVinePieces = 400;

    [Tooltip("사라지기 전 투명해지는 시간 (초, 외형만)")]
    [Min(0f)]
    [SerializeField]
    private float vineFadeTime = 0.3f;

    [Header("visual (비워두면 렌더러 없이 동작)")]
    [Tooltip("톱날 외형 프리팹 (있으면 sprite 대신 사용)")]
    [SerializeField]
    private GameObject sawPrefab;
    [SerializeField]
    private Sprite sawSprite;
    [SerializeField]
    private Color sawColor = Color.white;
    [Min(0f)]
    [SerializeField]
    private float sawScale = 1f;

    [Tooltip("덩굴 조각 외형 프리팹 (있으면 sprite 대신 사용)")]
    [SerializeField]
    private GameObject vinePrefab;
    [SerializeField]
    private Sprite vineSprite;
    [SerializeField]
    private Color vineColor = Color.white;
    [Min(0f)]
    [SerializeField]
    private float vineScale = 1f;

    [Tooltip("생성 렌더러 정렬 레이어 (Default 면 바닥에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";
    [Tooltip("톱날 정렬 순서 (덩굴은 이 값 - 1)")]
    [SerializeField]
    private int sortingOrder = 10;

    private readonly List<Saw> saws = new List<Saw>();
    private readonly List<Vine> vines = new List<Vine>();
    private readonly List<StageTarget> hitBuffer = new List<StageTarget>();

    private ObjectPool<Saw> sawPool;
    private ObjectPool<Vine> vinePool;

    public int SawCount => saws.Count;
    public int VineCount => vines.Count;

    #region 순수 로직 (테스트용)
    /// <summary>
    /// 한 번에 나가는 톱날 수 = 발사 수 × 배수 (음수는 0)
    /// </summary>
    public static int GetSawCount(int _fireCount, int _multiplier)
    {
        return Mathf.Max(0, _fireCount) * Mathf.Max(0, _multiplier);
    }

    /// <summary>
    /// 톱날 시작점·방향·이동 거리. _fromLeft 면 왼쪽 끝 바깥(xMin - _margin)에서 오른쪽으로, 아니면 반대
    /// 높이는 [yMin + _padding, yMax - _padding] 를 _yNormalized(0~1) 로 보간 (패딩이 높이보다 크면 중앙)
    /// </summary>
    public static void GetSawPath(Rect _rect, bool _fromLeft, float _yNormalized, float _margin, float _padding,
        out Vector2 _start, out Vector2 _dir, out float _travel)
    {
        float yMin = _rect.yMin + _padding;
        float yMax = _rect.yMax - _padding;
        float y = yMin <= yMax ? Mathf.Lerp(yMin, yMax, Mathf.Clamp01(_yNormalized)) : _rect.center.y;
        _start = new Vector2(true == _fromLeft ? _rect.xMin - _margin : _rect.xMax + _margin, y);
        _dir = true == _fromLeft ? Vector2.right : Vector2.left;
        _travel = _rect.width + _margin * 2f;
    }

    /// <summary>
    /// 이동 누적 거리 _accum 이 _spacing 이상이면 덩굴 1개를 소비한다 (_accum -= _spacing)
    /// _backOffset = 조각 위치가 현재 위치에서 뒤로 떨어진 거리. _spacing 이 0 이하면 항상 false
    /// </summary>
    public static bool TryConsumeVineDrop(ref float _accum, float _spacing, out float _backOffset)
    {
        _backOffset = 0f;
        if (_spacing <= 0f || _accum < _spacing)
        {
            return false;
        }
        _accum -= _spacing;
        _backOffset = _accum;
        return true;
    }

    /// <summary>
    /// 이 톱날이 _health 를 이미 때렸는지 (null 은 때린 것으로 본다). 처음이면 기록하고 false
    /// </summary>
    public static bool CheckAndMarkHit(List<HealthSystem> _hit, HealthSystem _health)
    {
        if (null == _health || true == _hit.Contains(_health))
        {
            return true;
        }
        _hit.Add(_health);
        return false;
    }
    #endregion

    private void Awake()
    {
        sawPool = new ObjectPool<Saw>(CreateSaw, OnGetObject, OnReleaseObject, OnDestroyObject, false, 8, 64);
        vinePool = new ObjectPool<Vine>(CreateVine, OnGetObject, OnReleaseObject, OnDestroyObject, false, 64, 512);
    }

    protected override void Fire()
    {
        StageSpace space = StageSpace.Instance;
        if (null == space || false == space.IsReady)
        {
            return;
        }

        Rect rect = space.StageRect;
        int count = GetSawCount(FinalFireCount, sawCountMultiplier);
        for (int i = 0; i < count; ++i)
        {
            GetSawPath(rect, Random.value < 0.5f, Random.value, edgeMargin, verticalPadding,
                out Vector2 start, out Vector2 dir, out float travel);
            Saw s = sawPool.Get();
            s.dir = dir;
            s.travelLeft = travel;
            s.vineAccum = 0f;
            s.hit.Clear();
            s.tr.position = ToWorld(start, s.tr.position.z);
            saws.Add(s);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (false == IsActivated || false == IsPlaced)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        StageQuadTree tree = StageSpace.Targets;
        UpdateSaws(tree, dt);
        UpdateVines(tree, dt);
    }

    #region 톱날
    private void UpdateSaws(StageQuadTree _tree, float _dt)
    {
        float damage = FinalDamage * sawDamageRate;
        float step = sawSpeed * _dt;
        float spin = sawSpinSpeed * _dt;

        for (int i = saws.Count - 1; i >= 0; --i)
        {
            Saw s = saws[i];
            Vector2 from = s.tr.position;
            Vector2 to = from + s.dir * step;
            s.tr.position = ToWorld(to, s.tr.position.z);
            s.tr.Rotate(0f, 0f, s.dir.x > 0f ? -spin : spin);
            s.travelLeft -= step;

            if (null != _tree)
            {
                // 빠르게 지나가도 놓치지 않게 이번 프레임 이동 구간 전체를 본다
                _tree.QuerySegment(from, to, sawRadius, hitBuffer);
                for (int j = 0; j < hitBuffer.Count; ++j)
                {
                    StageTarget t = hitBuffer[j];
                    if (false == t.IsAlive || true == CheckAndMarkHit(s.hit, t.health))
                    {
                        continue;
                    }
                    StageDamage.Apply(t, damage, this);
                }
            }

            s.vineAccum += step;
            while (true == TryConsumeVineDrop(ref s.vineAccum, vineSpacing, out float back))
            {
                SpawnVine(to - s.dir * back);
            }

            if (s.travelLeft <= 0f)
            {
                int last = saws.Count - 1;
                saws[i] = saws[last];
                saws.RemoveAt(last);
                s.hit.Clear();
                sawPool.Release(s);
            }
        }
    }
    #endregion

    #region 덩굴
    private void SpawnVine(Vector2 _pos)
    {
        if (vines.Count >= maxVinePieces)
        {
            return;
        }
        Vine v = vinePool.Get();
        v.lifeLeft = ApplyDuration(vineLifetime, vineLifetime);
        v.tickTimer = 0f;
        v.tr.position = ToWorld(_pos, v.tr.position.z);
        if (null != v.sr)
        {
            v.sr.color = v.baseColor;
        }
        vines.Add(v);
    }

    private void UpdateVines(StageQuadTree _tree, float _dt)
    {
        float damage = FinalDamage * vineDamageRate;
        for (int i = vines.Count - 1; i >= 0; --i)
        {
            Vine v = vines[i];
            v.lifeLeft -= _dt;
            v.tickTimer += _dt;

            if (v.tickTimer >= vineTickInterval)
            {
                v.tickTimer -= vineTickInterval;
                if (null != _tree && _tree.QueryCircle(v.tr.position, vineRadius, hitBuffer) > 0)
                {
                    for (int j = 0; j < hitBuffer.Count; ++j)
                    {
                        if (true == hitBuffer[j].IsAlive)
                        {
                            StageDamage.Apply(hitBuffer[j], damage, this);
                        }
                    }
                }
            }

            if (v.lifeLeft <= 0f)
            {
                int last = vines.Count - 1;
                vines[i] = vines[last];
                vines.RemoveAt(last);
                vinePool.Release(v);
                continue;
            }

            if (null != v.sr && vineFadeTime > 0f && v.lifeLeft < vineFadeTime)
            {
                Color c = v.baseColor;
                c.a *= v.lifeLeft / vineFadeTime;
                v.sr.color = c;
            }
        }
    }
    #endregion

    #region 풀
    private Saw CreateSaw()
    {
        GameObject go = CreateVisual("ThornSaw", sawPrefab, sawSprite, sawColor, sawScale, sortingOrder, out _);
        return new Saw { go = go, tr = go.transform };
    }

    private Vine CreateVine()
    {
        GameObject go = CreateVisual("ThornVine", vinePrefab, vineSprite, vineColor, vineScale, sortingOrder - 1, out SpriteRenderer sr);
        return new Vine { go = go, tr = go.transform, sr = sr, baseColor = null != sr ? sr.color : Color.white };
    }

    private static void OnGetObject(Saw _s) => _s.go.SetActive(true);
    private static void OnReleaseObject(Saw _s) => _s.go.SetActive(false);
    private static void OnDestroyObject(Saw _s) => DestroyGo(_s.go);
    private static void OnGetObject(Vine _v) => _v.go.SetActive(true);
    private static void OnReleaseObject(Vine _v) => _v.go.SetActive(false);
    private static void OnDestroyObject(Vine _v) => DestroyGo(_v.go);

    private static void DestroyGo(GameObject _go)
    {
        if (null != _go)
        {
            Destroy(_go);
        }
    }

    /// <summary>
    /// 이 무기의 자식으로 외형을 만든다 (레이어 = 무기 레이어, 렌더러 정렬 레이어 = sortingLayerName)
    /// _prefab 이 있으면 복제, 없으면 빈 오브젝트 + (_sprite 가 있으면) SpriteRenderer
    /// </summary>
    private GameObject CreateVisual(string _name, GameObject _prefab, Sprite _sprite, Color _color, float _scale, int _order, out SpriteRenderer _renderer)
    {
        GameObject go;
        _renderer = null;
        if (null != _prefab)
        {
            go = Instantiate(_prefab, transform);
            go.name = _name;
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].sortingLayerName = sortingLayerName;
            }
            _renderer = renderers.Length > 0 ? renderers[0] : null;
        }
        else
        {
            go = new GameObject(_name);
            go.transform.SetParent(transform, false);
            if (null != _sprite)
            {
                _renderer = go.AddComponent<SpriteRenderer>();
                _renderer.sprite = _sprite;
                _renderer.color = _color;
                _renderer.sortingLayerName = sortingLayerName;
                _renderer.sortingOrder = _order;
            }
        }
        go.transform.localScale *= _scale;
        LayerUtils.SetLayer(go, gameObject.layer);
        go.SetActive(false);
        return go;
    }
    #endregion

    private static Vector3 ToWorld(Vector2 _pos, float _z)
    {
        return new Vector3(_pos.x, _pos.y, _z);
    }
}
