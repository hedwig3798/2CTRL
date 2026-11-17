using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 역병 지뢰 한 개 (PlagueMine 이 풀에서 꺼내 쓴다)
/// - 화면 공간에서 최근접 적(양 진영, 탐색 반지름 = PlagueMine.FinalRange)을 chaseSpeed 로 천천히 추적
/// - 적(중심 거리 - 적 반지름)이 triggerRadius 안에 들어오면 PlagueMine.Explode 를 부르고 풀로 돌아간다
/// - 활성화 지연(armDelay) 동안은 추적만 하고 터지지 않는다. 수명이 끝나면 터지지 않고 사라진다
/// - 시간은 Time.deltaTime (일시정지 시 멈춤). 렌더러가 없어도 동작한다
/// </summary>
public class PlagueMineBomb
    : MonoBehaviour
    , IPoolable
{
    [Header("visual")]
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다)")]
    [SerializeField]
    private string sortingLayerName = "attack";
    [Tooltip("생성 시 재생할 파티클 (없어도 된다)")]
    [SerializeField]
    private ParticleSystem[] particles;

    private PlagueMine owner;
    private float remaining;
    private float armRemaining;
    private bool exploded;

    public bool IsArmed => armRemaining <= 0f;

    private void Awake()
    {
        ApplySortingLayer();
    }

    /// <summary>
    /// _stagePos(화면 공간)에 설치한다
    /// </summary>
    public void Activate(PlagueMine _owner, Vector2 _stagePos, float _lifetime, float _armDelay)
    {
        owner = _owner;
        remaining = _lifetime > 0f ? _lifetime : float.MaxValue;
        armRemaining = Mathf.Max(0f, _armDelay);
        exploded = false;

        transform.position = new Vector3(_stagePos.x, _stagePos.y, transform.position.z);
        gameObject.SetActive(true);
        PlayParticles();
    }

    private void Update()
    {
        if (null == owner || true == exploded)
        {
            return;
        }

        float dt = Time.deltaTime;
        remaining -= dt;
        if (remaining <= 0f)
        {
            owner.ReleaseMine(this);
            return;
        }
        if (armRemaining > 0f)
        {
            armRemaining -= dt;
        }

        StageQuadTree tree = StageSpace.Targets;
        if (null == tree)
        {
            return;
        }

        Vector2 pos = transform.position;
        if (false == tree.QueryNearest(pos, owner.FinalRange, (ICollection<HealthSystem>)null, out StageTarget target)
            || false == target.IsAlive)
        {
            return;
        }

        Vector2 toTarget = target.stagePos - pos;
        float edge = toTarget.magnitude - target.radius;
        if (true == IsArmed && edge <= owner.TriggerRadius)
        {
            exploded = true;
            owner.Explode(this, pos);
            return;
        }

        float step = owner.ChaseSpeed * dt;
        float dist = toTarget.magnitude;
        if (dist > 0.0001f && step > 0f)
        {
            pos += toTarget / dist * Mathf.Min(step, dist);
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }
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
}
