using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 역병 지뢰 독 장판 (PlagueMine 이 풀에서 꺼내 쓴다)
/// - 화면 공간 좌표에 고정. 틱 간격마다 쿼드트리 원 쿼리로 안의 모든 적(양 진영)에게 데미지
/// - 틱 데미지는 틱 시점의 PlagueMine.ZoneTickDamage (스텟 변화 즉시 반영). 첫 틱은 생성 직후
/// - 지속 시간이 끝나면 PlagueMine 풀로 돌아간다. 시간은 Time.deltaTime (일시정지 시 멈춤)
/// - 렌더러가 없어도 동작한다
/// </summary>
public class PlagueZone
    : MonoBehaviour
    , IPoolable
{
    [Header("visual")]
    [Tooltip("크기 1 일 때 보이는 반지름. 크기 = 판정 반지름 / unitRadius")]
    [Min(0.01f)]
    [SerializeField]
    private float unitRadius = 0.5f;
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";
    [Tooltip("생성 시 재생할 파티클 (없어도 된다)")]
    [SerializeField]
    private ParticleSystem[] particles;

    private readonly List<StageTarget> hitBuffer = new List<StageTarget>();

    private PlagueMine owner;
    private Vector3 baseScale = Vector3.one;
    private float radius;
    private float remaining;
    private float tickTimer;

    public float Radius => radius;
    public float Remaining => remaining;

    private void Awake()
    {
        baseScale = transform.localScale;
        ApplySortingLayer();
    }

    /// <summary>
    /// _center(화면 공간)에 _radius 반지름으로 _duration 초 동안 켠다
    /// </summary>
    public void Activate(PlagueMine _owner, Vector2 _center, float _radius, float _duration)
    {
        owner = _owner;
        radius = Mathf.Max(0f, _radius);
        remaining = Mathf.Max(0f, _duration);
        tickTimer = float.MaxValue; // 첫 프레임에 바로 1틱

        transform.position = new Vector3(_center.x, _center.y, transform.position.z);
        transform.localScale = baseScale * (radius / unitRadius);
        gameObject.SetActive(true);
        PlayParticles();
    }

    private void Update()
    {
        if (null == owner)
        {
            gameObject.SetActive(false);
            return;
        }

        float dt = Time.deltaTime;
        tickTimer += dt;
        float interval = owner.ZoneTickInterval;
        if (tickTimer >= interval)
        {
            tickTimer = 0f;
            Tick();
        }

        remaining -= dt;
        if (remaining <= 0f)
        {
            owner.ReleaseZone(this);
        }
    }

    private void Tick()
    {
        StageQuadTree tree = StageSpace.Targets;
        if (null == tree)
        {
            return;
        }

        float damage = owner.ZoneTickDamage;
        tree.QueryCircle(transform.position, radius, hitBuffer);
        for (int i = 0; i < hitBuffer.Count; ++i)
        {
            if (true == hitBuffer[i].IsAlive)
            {
                StageDamage.Apply(hitBuffer[i], damage, owner);
            }
        }
        hitBuffer.Clear();
    }

    private void PlayParticles()
    {
        if (null == particles)
        {
            return;
        }
        for (int i = 0; i < particles.Length; ++i)
        {
            if (null != particles[i])
            {
                particles[i].Play(true);
            }
        }
    }

    private void ApplySortingLayer()
    {
        if (true == string.IsNullOrEmpty(sortingLayerName))
        {
            return;
        }
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; ++i)
        {
            renderers[i].sortingLayerName = sortingLayerName;
        }
    }

    private void OnDisable()
    {
        hitBuffer.Clear();
    }
}
