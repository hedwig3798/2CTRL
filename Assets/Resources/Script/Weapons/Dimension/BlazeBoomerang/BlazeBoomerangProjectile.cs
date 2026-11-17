using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화염 부메랑 한 개 (BlazeBoomerang 이 풀에서 꺼내 쓴다)
/// - 이동은 BoomerangFlight (ReturnMovement 와 같은 규칙). 되돌아서는 순간 발사한 플레이어의 현재 화면 위치로 귀환 방향을 정한다
/// - 1회 타격 없음. 비행 중 틱 간격마다 주변 원(BlazeBoomerang.FlameRadius) 안의 모든 적(양 진영)에게 화염 데미지. 첫 틱은 발사 직후
/// - 수명(BlazeBoomerang.FlightLifetime)이 끝나면 풀로 돌아간다 (원본처럼 플레이어에게 닿아도 사라지지 않음)
/// - 시간은 Time.deltaTime (일시정지 시 멈춤). 렌더러가 없어도 동작한다
/// </summary>
public class BlazeBoomerangProjectile
    : MonoBehaviour
    , IPoolable
{
    [Header("visual")]
    [Tooltip("회전시킬 대상 (비우면 회전하지 않음)")]
    [SerializeField]
    private Transform spinTransform;
    [Tooltip("초당 회전 각도")]
    [SerializeField]
    private float spinSpeed = 720f;
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";
    [Tooltip("발사 시 재생할 화염 파티클 (없어도 된다)")]
    [SerializeField]
    private ParticleSystem[] particles;

    private readonly List<StageTarget> hitBuffer = new List<StageTarget>();

    private BlazeBoomerang owner;
    private PlayerSide side;
    private BoomerangFlight flight;
    private float remaining;
    private float tickTimer;

    public PlayerSide Side => side;
    public bool IsReturning => flight.isReturning;

    private void Awake()
    {
        ApplySortingLayer();
    }

    /// <summary>
    /// _start(화면 공간)에서 _targetPoint 쪽으로 발사
    /// </summary>
    public void Launch(BlazeBoomerang _owner, PlayerSide _side, Vector2 _start, Vector2 _targetPoint, float _lifetime)
    {
        owner = _owner;
        side = _side;
        flight = BoomerangFlight.Launch(_start, _targetPoint, _owner.FlightSpeed, _owner.Overshoot, _owner.MinDistance, _owner.ReturnSpeedRate);
        remaining = _lifetime;
        tickTimer = float.MaxValue; // 첫 프레임에 바로 1틱

        transform.position = new Vector3(_start.x, _start.y, transform.position.z);
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
        remaining -= dt;
        if (remaining <= 0f)
        {
            owner.Release(this);
            return;
        }

        Vector2 pos = transform.position;
        if (false == flight.isReturning)
        {
            if (true == flight.StepOut(ref pos, dt))
            {
                bool has = owner.TryGetPlayerStagePos(side, out Vector2 home);
                flight.BeginReturn(pos, has, home);
            }
        }
        else
        {
            flight.StepReturn(ref pos, dt);
        }
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);

        if (null != spinTransform && 0f != spinSpeed)
        {
            spinTransform.Rotate(0f, 0f, spinSpeed * dt);
        }

        tickTimer += dt;
        if (tickTimer >= owner.FlameTickInterval)
        {
            tickTimer = 0f;
            Burn(pos);
        }
    }

    private void Burn(Vector2 _pos)
    {
        StageQuadTree tree = StageSpace.Targets;
        if (null == tree)
        {
            return;
        }

        float damage = owner.FlameTickDamage;
        tree.QueryCircle(_pos, owner.FlameRadius, hitBuffer);
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
