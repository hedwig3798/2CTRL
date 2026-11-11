using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

/// <summary>
/// 보스 공격 예고 표시 (런타임 생성, 정적 풀)
/// - 원 채움(SpriteRenderer) 1개, 띠 채움(굵은 LineRenderer) 1개, 링(LineRenderer, 실선/점선) 2개로 구성
/// - Spawn 할 때 낸 보스의 레이어로 맞춘다 (분할 카메라 culling)
/// - ArmBlast 로 지연 폭발(지뢰)로 쓸 수 있다: delay 뒤 반경 안 대상에게 MonsterAreaDamage 로 데미지를 주고 풀로 돌아간다
/// - 연출 다듬기(색·애니메이션)는 visual-designer 몫
/// </summary>
public class BossTelegraph
    : MonoBehaviour
    , IPoolable
{
    public const int RingCount = 2;

    private static readonly string[] FallbackSortingLayers = { "attack", "item", "Player" };

    private SpriteRenderer disc;
    private LineRenderer band;
    // 직선(캡슐) 예고: 레이저 경고선·돌진 화살표 (월드 좌표)
    private LineRenderer line;
    private readonly LineRenderer[] rings = new LineRenderer[RingCount];
    private int segments = 64;
    private float ringWidth = 0.12f;
    private Vector3[] points;
    private Material[] currentMaterials;

    // 마지막으로 그린 반지름 (같으면 점 재계산 생략, 음수 = 없음)
    private float bandDrawnRadius = -1f;
    private readonly float[] ringDrawnRadius = { -1f, -1f };

    private IObjectPool<BossTelegraph> pool;
    private bool released;

    // 지연 폭발
    private bool blastArmed;
    private float blastStartTime;
    private float blastDelay;
    private float blastRadius;
    private float blastDamage;
    private LayerMask blastLayer;
    private DamagePipeline blastAttacker;
    private Color blastColor;

    /// <summary>
    /// 지연 폭발이 터질 때 (위치, 반경). 효과음·이펙트 연결용
    /// </summary>
    public static event Action<Vector2, float> AnyBlast;

    public Vector2 Center => transform.position;

    /// <summary>
    /// 지연 폭발 진행률 (0 → 1, 무장 안 했으면 0)
    /// </summary>
    public float BlastProgress => true == blastArmed && blastDelay > 0f
        ? Mathf.Clamp01((Time.time - blastStartTime) / blastDelay)
        : 0f;

    #region Spawn / Pool
    private static IObjectPool<BossTelegraph> sharedPool;
    private static Sprite discSprite;
    private static Texture2D solidTexture;
    private static Texture2D dashTexture;
    private static readonly Dictionary<Material, Material[]> materialCache = new Dictionary<Material, Material[]>();
    private static Material fallbackMaterial;
    private static bool sceneHooked;
    private static bool sortingWarned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveTelegraphs.Clear();
        sharedPool = null;
        AnyBlast = null;
        materialCache.Clear();
        sortingWarned = false;
        if (true == sceneHooked)
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            sceneHooked = false;
        }
    }

    /// <summary>
    /// 풀에서 꺼내 _center 에 둔다. 모든 표시는 꺼진 상태로 시작한다
    /// </summary>
    public static BossTelegraph Spawn(BossTelegraphStyle _style, int _layer, Vector2 _center)
    {
        if (null == sharedPool)
        {
            sharedPool = CreatePool();
        }
        if (false == sceneHooked)
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            sceneHooked = true;
        }

        // 씬이 바뀌어 파괴된 오브젝트는 건너뛴다
        BossTelegraph t = sharedPool.Get();
        while (null == t)
        {
            t = sharedPool.Get();
        }

        t.released = false;
        t.ApplyStyle(_style);
        LayerUtils.SetLayer(t.gameObject, _layer);
        t.transform.position = new Vector3(_center.x, _center.y, 0f);
        t.HideAll();
        t.gameObject.SetActive(true);
        ActiveTelegraphs.Add(t);
        return t;
    }

    private static IObjectPool<BossTelegraph> CreatePool()
    {
        IObjectPool<BossTelegraph> p = null;
        p = new ObjectPool<BossTelegraph>
            (
                createFunc: () =>
                {
                    BossTelegraph created = Create();
                    created.pool = p;
                    return created;
                }
                , actionOnGet: null
                , actionOnRelease: null
                , actionOnDestroy: (t) =>
                {
                    if (null != t)
                    {
                        Destroy(t.gameObject);
                    }
                }
                , collectionCheck: false
                , defaultCapacity: 16
                , maxSize: 64
            );
        return p;
    }

    private static BossTelegraph Create()
    {
        GameObject root = new GameObject(nameof(BossTelegraph));
        root.SetActive(false);
        BossTelegraph t = root.AddComponent<BossTelegraph>();

        GameObject discObject = new GameObject("Disc");
        discObject.transform.SetParent(root.transform, false);
        t.disc = discObject.AddComponent<SpriteRenderer>();
        t.disc.sprite = GetDiscSprite();

        t.band = CreateLine(root.transform, "Band");
        for (int i = 0; i < RingCount; ++i)
        {
            t.rings[i] = CreateLine(root.transform, "Ring" + i);
        }

        t.line = CreateLine(root.transform, "Line");
        t.line.useWorldSpace = true;
        t.line.loop = false;
        t.line.positionCount = 2;
        t.line.textureMode = LineTextureMode.Stretch;
        return t;
    }

    private static LineRenderer CreateLine(Transform _parent, string _name)
    {
        GameObject go = new GameObject(_name);
        go.transform.SetParent(_parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    private void ApplyStyle(BossTelegraphStyle _style)
    {
        segments = null != _style ? Mathf.Clamp(_style.segments, 16, 128) : 64;
        ringWidth = null != _style ? _style.ringWidth : 0.12f;
        if (null == points || points.Length != segments)
        {
            points = new Vector3[segments];
        }

        Material[] mats = GetMaterials(null != _style ? _style.material : null);
        currentMaterials = mats;

        // 분할 수가 바뀌었을 수 있으므로 다시 그리게 한다
        bandDrawnRadius = -1f;
        for (int i = 0; i < RingCount; ++i)
        {
            ringDrawnRadius[i] = -1f;
        }
        int layerID = ResolveSortingLayer(null != _style ? _style.sortingLayerName : null);
        int order = null != _style ? _style.sortingOrder : -20;

        if (null != mats)
        {
            disc.sharedMaterial = mats[0];
            band.sharedMaterial = mats[0];
        }
        disc.sortingLayerID = layerID;
        disc.sortingOrder = order;
        band.sortingLayerID = layerID;
        band.sortingOrder = order + 1;

        for (int i = 0; i < RingCount; ++i)
        {
            rings[i].sortingLayerID = layerID;
            rings[i].sortingOrder = order + 2;
            rings[i].positionCount = segments;
        }
        band.positionCount = segments;

        if (null != mats)
        {
            line.sharedMaterial = mats[0];
        }
        line.sortingLayerID = layerID;
        line.sortingOrder = order + 3;
    }

    private static int ResolveSortingLayer(string _name)
    {
        if (false == string.IsNullOrEmpty(_name))
        {
            int id = SortingLayer.NameToID(_name);
            if (0 != id && true == SortingLayer.IsValid(id))
            {
                return id;
            }
        }

        foreach (string layerName in FallbackSortingLayers)
        {
            int id = SortingLayer.NameToID(layerName);
            if (0 != id && true == SortingLayer.IsValid(id))
            {
                if (false == string.IsNullOrEmpty(_name))
                {
                    WarnSorting($"[BossTelegraph] 정렬 레이어 '{_name}' 가 없어 '{layerName}' 를 씁니다.");
                }
                return id;
            }
        }

        WarnSorting("[BossTelegraph] 쓸 수 있는 정렬 레이어가 없어 Default 를 씁니다. BG 에 가려질 수 있습니다.");
        return 0;
    }

    private static void WarnSorting(string _message)
    {
        if (true == sortingWarned)
        {
            return;
        }
        sortingWarned = true;
        Debug.LogWarning(_message);
    }

    /// <summary>
    /// [0] 실선(흰 텍스처), [1] 점선 텍스처 머티리얼
    /// </summary>
    private static Material[] GetMaterials(Material _base)
    {
        if (null == _base)
        {
            if (null == fallbackMaterial)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (null == shader)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                if (null == shader)
                {
                    Debug.LogError("[BossTelegraph] 예고 표시용 셰이더를 찾지 못했습니다. BossTelegraphStyle.material 을 넣어 주세요.");
                    return null;
                }
                fallbackMaterial = new Material(shader);
                fallbackMaterial.name = "BossTelegraph (Runtime)";
            }
            _base = fallbackMaterial;
        }

        if (true == materialCache.TryGetValue(_base, out Material[] mats) && null != mats[0] && null != mats[1])
        {
            return mats;
        }

        Material solid = new Material(_base);
        solid.mainTexture = GetSolidTexture();
        Material dashed = new Material(_base);
        dashed.mainTexture = GetDashTexture();
        mats = new Material[] { solid, dashed };
        materialCache[_base] = mats;
        return mats;
    }

    private static Texture2D GetSolidTexture()
    {
        if (null == solidTexture)
        {
            solidTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            solidTexture.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            solidTexture.Apply();
        }
        return solidTexture;
    }

    // 왼쪽 절반만 보이는 텍스처 (RepeatPerSegment 로 분할마다 한 칸씩 → 점선)
    private static Texture2D GetDashTexture()
    {
        if (null == dashTexture)
        {
            dashTexture = new Texture2D(8, 1, TextureFormat.RGBA32, false);
            dashTexture.wrapMode = TextureWrapMode.Clamp;
            dashTexture.filterMode = FilterMode.Point;
            for (int x = 0; x < 8; ++x)
            {
                dashTexture.SetPixel(x, 0, x < 4 ? Color.white : Color.clear);
            }
            dashTexture.Apply();
        }
        return dashTexture;
    }

    // 지름 1 유닛의 부드러운 원 스프라이트
    private static Sprite GetDiscSprite()
    {
        if (null == discSprite)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = size * 0.5f;
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                    float a = Mathf.Clamp01(r - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            discSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            discSprite.name = "BossTelegraphDisc (Runtime)";
        }
        return discSprite;
    }

    /// <summary>
    /// 풀로 돌려보낸다 (여러 번 불러도 한 번만)
    /// </summary>
    // 사용 중인 예고 전체 (지뢰 ArmBlast 포함, 최종 보스 진입 시 일괄 회수)
    private static readonly HashSet<BossTelegraph> ActiveTelegraphs = new HashSet<BossTelegraph>();
    private static readonly List<BossTelegraph> releaseBuffer = new List<BossTelegraph>();

    /// <summary>
    /// 사용 중인 예고를 모두 풀로 돌린다 (무장한 지뢰는 터지지 않는다). 돌린 개수
    /// </summary>
    public static int ReleaseAll()
    {
        releaseBuffer.Clear();
        releaseBuffer.AddRange(ActiveTelegraphs);
        int count = 0;
        foreach (BossTelegraph t in releaseBuffer)
        {
            if (null != t && false == t.released)
            {
                t.Release();
                ++count;
            }
        }
        releaseBuffer.Clear();
        ActiveTelegraphs.Clear();
        return count;
    }

    public void Release()
    {
        ActiveTelegraphs.Remove(this);
        if (true == released)
        {
            return;
        }
        released = true;
        blastArmed = false;
        blastAttacker = null;

        if (null != gameObject && true == gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }

        if (null != pool)
        {
            pool.Release(this);
        }
    }

    private void OnDisable()
    {
        ActiveTelegraphs.Remove(this);

        // Release 를 거치지 않고 꺼졌으면 씬 언로드·파괴 중이다. 파괴될 객체는 풀에 넣지 않는다
        if (false == released)
        {
            released = true;
            blastArmed = false;
            blastAttacker = null;
        }
    }

    // 씬이 내려가면 풀에 남은(파괴된) 항목을 비운다
    private static void HandleSceneUnloaded(Scene _scene)
    {
        if (null != sharedPool)
        {
            sharedPool.Clear();
        }
    }
    #endregion

    #region Display
    public void SetCenter(Vector2 _center)
    {
        transform.position = new Vector3(_center.x, _center.y, transform.position.z);
    }

    /// <summary>
    /// 반지름 _radius 원을 _color 로 채운다
    /// </summary>
    public void ShowDisc(float _radius, Color _color)
    {
        float d = Mathf.Max(0f, _radius) * 2f;
        disc.transform.localScale = new Vector3(d, d, 1f);
        disc.color = _color;
        disc.enabled = true;
    }

    public void HideDisc()
    {
        disc.enabled = false;
    }

    /// <summary>
    /// _inner ~ _outer 띠를 _color 로 채운다 (굵은 링)
    /// </summary>
    public void ShowBand(float _inner, float _outer, Color _color)
    {
        float inner = Mathf.Max(0f, Mathf.Min(_inner, _outer));
        float outer = Mathf.Max(_inner, _outer);
        float width = outer - inner;
        if (width <= 0f)
        {
            HideBand();
            return;
        }

        SetCircle(band, inner + width * 0.5f, ref bandDrawnRadius);
        band.widthMultiplier = width;
        band.startColor = _color;
        band.endColor = _color;
        band.enabled = true;
    }

    public void HideBand()
    {
        band.enabled = false;
    }

    /// <summary>
    /// _index(0 ~ 1) 번 링을 반지름 _radius 로 그린다. _dashed 면 점선
    /// </summary>
    public void ShowRing(int _index, float _radius, Color _color, bool _dashed)
    {
        if (_index < 0 || _index >= RingCount)
        {
            return;
        }

        LineRenderer lr = rings[_index];
        if (null != currentMaterials)
        {
            lr.sharedMaterial = true == _dashed ? currentMaterials[1] : currentMaterials[0];
        }
        lr.textureMode = true == _dashed ? LineTextureMode.RepeatPerSegment : LineTextureMode.Stretch;
        lr.widthMultiplier = ringWidth;
        lr.startColor = _color;
        lr.endColor = _color;
        SetCircle(lr, Mathf.Max(0f, _radius), ref ringDrawnRadius[_index]);
        lr.enabled = true;
    }

    public void HideRing(int _index)
    {
        if (_index >= 0 && _index < RingCount)
        {
            rings[_index].enabled = false;
        }
    }

    /// <summary>
    /// 월드 좌표 _from ~ _to 직선(폭 _width)을 _color 로 그린다. SetCenter 로 옮겨도 따라가지 않는다
    /// </summary>
    public void ShowLine(Vector2 _from, Vector2 _to, float _width, Color _color)
    {
        line.SetPosition(0, new Vector3(_from.x, _from.y, 0f));
        line.SetPosition(1, new Vector3(_to.x, _to.y, 0f));
        line.widthMultiplier = Mathf.Max(0f, _width);
        line.startColor = _color;
        line.endColor = _color;
        line.enabled = true;
    }

    public void HideLine()
    {
        line.enabled = false;
    }

    public void HideAll()
    {
        HideDisc();
        HideBand();
        HideLine();
        for (int i = 0; i < RingCount; ++i)
        {
            HideRing(i);
        }
    }

    private void SetCircle(LineRenderer _lr, float _radius, ref float _drawnRadius)
    {
        // 반지름이 같으면 점을 다시 계산하지 않는다 (매 프레임 갱신하는 예고용)
        if (true == Mathf.Approximately(_drawnRadius, _radius) && _lr.positionCount == segments)
        {
            return;
        }
        _drawnRadius = _radius;

        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; ++i)
        {
            float a = step * i;
            points[i] = new Vector3(Mathf.Cos(a) * _radius, Mathf.Sin(a) * _radius, 0f);
        }
        _lr.positionCount = segments;
        _lr.SetPositions(points);
    }
    #endregion

    #region Blast
    /// <summary>
    /// 지연 폭발: _delay 초 동안 경고 원(링 + 차오르는 원)을 보인 뒤 반경 _radius 안 _targetLayer 대상에게 _damage 를 주고 풀로 돌아간다
    /// </summary>
    public void ArmBlast(float _delay, float _radius, float _damage, LayerMask _targetLayer, DamagePipeline _attacker, Color _color)
    {
        blastArmed = true;
        blastStartTime = Time.time;
        blastDelay = Mathf.Max(0f, _delay);
        blastRadius = Mathf.Max(0f, _radius);
        blastDamage = _damage;
        blastLayer = _targetLayer;
        blastAttacker = _attacker;
        blastColor = _color;

        ShowRing(0, blastRadius, _color, false);
        UpdateBlastVisual();
    }

    private void UpdateBlastVisual()
    {
        Color fill = blastColor;
        fill.a *= Mathf.Lerp(0.15f, 0.6f, BlastProgress);
        ShowDisc(blastRadius * BlastProgress, fill);
    }

    private void Update()
    {
        if (false == blastArmed)
        {
            return;
        }

        if (Time.time - blastStartTime < blastDelay)
        {
            UpdateBlastVisual();
            return;
        }

        blastArmed = false;
        Vector2 center = transform.position;
        MonsterAreaDamage.Apply(center, blastRadius, blastLayer, blastDamage, 0f, blastAttacker);
        AnyBlast?.Invoke(center, blastRadius);
        Release();
    }
    #endregion
}
