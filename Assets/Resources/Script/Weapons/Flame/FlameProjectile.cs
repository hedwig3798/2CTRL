using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화염 방사(Flame) 발사체 (무기는 WEAPON_TYPE.Capped, fireCount 1)
/// 분사가 끝나고 공격 간격이 지나면 발사되어 분사 시간 동안 주인(무기 위치)을 따라다니며,
/// 주인의 마지막 이동 방향(ArrowMovemnt.LastMoveDirection) 으로 펼친 부채꼴(반지름 = 무기 최종 범위) 안의 모든 적을 tickInterval 마다 타격한다
/// - 적이 없어도 분사한다 (주기적 분사). 분사가 끝나면 비활성화되어 풀로 돌아간다
/// - 넉백은 주인 위치에서 바깥쪽으로 밀어낸다
/// - 분사 중에는 무기 타이머가 멈추므로 분사끼리 겹치지 않는다 (쿨타임은 분사가 끝난 뒤부터)
/// - 업그레이드 : 분사 시간 = weapon.ApplyDuration(weapon.GetParam(BurstDuration, burstDuration)), 부채꼴 각도 = weapon.GetParam(FanAngle, fanAngle) (발사할 때마다 다시 읽는다)
/// - ★ FlagBurn : 맞은 적에 화상(FlameBurn). burnInterval 마다 타격 데미지 * burnDamageRate, burnDuration 초. 다시 맞으면 지속시간만 갱신
/// </summary>
public class FlameProjectile
    : Projectile
{
    [Header("flame")]
    [Tooltip("기본 분사 시간 (초). 실제 값 = weapon.GetParam(BurstDuration, burstDuration) 에 플레이어 지속 시간 적용. 분사가 끝난 뒤 공격 간격이 돈다")]
    [Min(0.05f)]
    [SerializeField]
    private float burstDuration = 0.8f;
    [Tooltip("분사 중 부채꼴 안의 적을 타격하는 간격 (초). 분사 시작 순간 첫 타격")]
    [Min(0.05f)]
    [SerializeField]
    private float tickInterval = 0.2f;
    [Tooltip("기본 부채꼴 전체 각도 (도). 실제 값 = weapon.GetParam(FanAngle, fanAngle)")]
    [Range(0f, 360f)]
    [SerializeField]
    private float fanAngle = 60f;
    [Tooltip("주인과 이 거리 안의 적은 방향과 상관없이 타격한다 (몸에 붙은 적)")]
    [Min(0f)]
    [SerializeField]
    private float innerRadius = 0.4f;

    [Header("burn (★)")]
    [Tooltip("화상 틱 데미지 = 타격 데미지 * 이 값")]
    [Min(0f)]
    [SerializeField]
    private float burnDamageRate = 0.3f;
    [Tooltip("화상 틱 간격 (초)")]
    [Min(0.05f)]
    [SerializeField]
    private float burnInterval = 0.5f;
    [Tooltip("화상 지속 시간 (초)")]
    [Min(0f)]
    [SerializeField]
    private float burnDuration = 3f;

    [Header("visual")]
    [Tooltip("범위에 맞춰 크기를 바꿀 대상 (+x 방향이 분사 방향). 비워두면 크기를 바꾸지 않는다")]
    [SerializeField]
    private Transform visualRoot;
    [Tooltip("visualRoot 기본 크기일 때 화염이 닿는 길이. 크기 = 기본 크기 * (최종 범위 / visualBaseRange)")]
    [Min(0.01f)]
    [SerializeField]
    private float visualBaseRange = 3f;
    [Tooltip("분사 시작 시 재생할 파티클")]
    [SerializeField]
    private ParticleSystem[] particles;
    [Tooltip("정렬 레이어를 맞출 렌더러. 비워두면 자식의 모든 렌더러")]
    [SerializeField]
    private Renderer[] renderers;
    [Tooltip("렌더러 정렬 레이어 (Default 면 BG 에 가려진다). 비워두면 바꾸지 않는다")]
    [SerializeField]
    private string sortingLayerName = "attack";

    private List<Transform> hitBuffer = new List<Transform>();

    private ArrowMovemnt ownerMovement;
    private Weapon movementWeapon;

    private Vector2 direction = Vector2.right;
    private float currFanAngle;
    private float endTime;
    private float nextTickTime;
    private Vector3 visualBaseScale = Vector3.one;

    /// <summary>
    /// 현재 분사 방향 (정규화)
    /// </summary>
    public Vector2 Direction => direction;

    /// <summary>
    /// 이번 분사의 부채꼴 전체 각도 (도)
    /// </summary>
    public float CurrentFanAngle => currFanAngle;

    private void Awake()
    {
        if (null != visualRoot)
        {
            visualBaseScale = visualRoot.localScale;
        }

        ApplySortingLayer();
    }

    private void ApplySortingLayer()
    {
        if (true == string.IsNullOrEmpty(sortingLayerName))
        {
            return;
        }

        if (null == renderers || 0 == renderers.Length)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer r in renderers)
        {
            if (null != r)
            {
                r.sortingLayerName = sortingLayerName;
            }
        }
    }

    protected override bool OnLaunch()
    {
        FindOwnerMovement();

        // 분사 시간 = 업그레이드 BurstDuration 에 플레이어 Duration 을 적용 (태그 없으면 그대로)
        // 무기가 Capped(fireCount 1) 라 분사가 끝난 뒤에 쿨타임이 돌므로 공격 간격으로 자르지 않는다
        float duration = Mathf.Max(0.05f, weapon.ApplyDuration(weapon.GetParam(WeaponParam.BurstDuration, burstDuration)));

        currFanAngle = Mathf.Clamp(weapon.GetParam(WeaponParam.FanAngle, fanAngle), 0f, 360f);

        float now = Time.time;
        endTime = now + duration;
        nextTickTime = now;

        Follow();
        PlayParticles();
        return true;
    }

    /// <summary>
    /// 주인의 ArrowMovemnt 를 찾는다 (무기 주인의 부모 계층, 없으면 무기의 부모 계층). 같은 무기면 다시 찾지 않는다
    /// </summary>
    private void FindOwnerMovement()
    {
        if (weapon == movementWeapon && null != ownerMovement)
        {
            return;
        }

        movementWeapon = weapon;
        ownerMovement = null != weapon.owner ? weapon.owner.GetComponentInParent<ArrowMovemnt>() : null;
        if (null == ownerMovement)
        {
            ownerMovement = weapon.GetComponentInParent<ArrowMovemnt>();
        }
    }

    private void PlayParticles()
    {
        if (null == particles)
        {
            return;
        }

        foreach (ParticleSystem ps in particles)
        {
            if (null != ps)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }

    // 주인 이동(Update) 이 끝난 뒤 위치와 방향을 맞춘다
    private void LateUpdate()
    {
        if (null == weapon)
        {
            return;
        }

        Follow();

        if (Time.time >= nextTickTime)
        {
            // 누적 오차로 분사 끝 시각에 한 번 더 타격하지 않도록 조금 일찍 자른다
            if (nextTickTime >= endTime - 0.001f)
            {
                gameObject.SetActive(false);
                return;
            }

            Tick();
            nextTickTime += tickInterval;
        }

        if (Time.time >= endTime)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 무기 위치로 옮기고 주인의 마지막 이동 방향을 바라본다. 화염 크기를 최종 범위에 맞춘다
    /// </summary>
    private void Follow()
    {
        transform.position = weapon.transform.position;

        if (null != ownerMovement)
        {
            Vector2 dir = ownerMovement.LastMoveDirection;
            if (dir.sqrMagnitude > 0.0001f)
            {
                direction = dir.normalized;
            }
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (null != visualRoot)
        {
            visualRoot.localScale = visualBaseScale * (weapon.FinalRange / visualBaseRange);
        }
    }

    /// <summary>
    /// 부채꼴 안의 모든 적을 타격한다. ★ 화상이 켜져 있으면 맞은 적에 화상을 건다
    /// </summary>
    private void Tick()
    {
        Vector2 center = weapon.transform.position;
        float range = weapon.FinalRange;
        if (range <= 0f)
        {
            return;
        }

        float halfAngle = currFanAngle * 0.5f;
        float innerSqr = innerRadius * innerRadius;
        bool isBurn = weapon.HasFlag(WeaponParam.FlagBurn);
        float burnDamage = damageMassage.damage * burnDamageRate;

        int count = FindAll(center, range, hitBuffer);
        for (int i = 0; i < count; ++i)
        {
            Transform t = hitBuffer[i];
            Vector2 toTarget = (Vector2)t.position - center;
            if (toTarget.sqrMagnitude > innerSqr && Vector2.Angle(direction, toTarget) > halfAngle)
            {
                continue;
            }

            if (false == t.TryGetComponent(out DamagePipeline dp))
            {
                continue;
            }

            SendDamage(dp, center);

            if (true == isBurn)
            {
                FlameBurn.Apply(t, weapon, burnDamage, burnInterval, burnDuration);
            }
        }
        hitBuffer.Clear();
    }

    protected override void OnDisable()
    {
        hitBuffer.Clear();
        endTime = 0f;
        nextTickTime = 0f;

        base.OnDisable();
    }

    private void OnDrawGizmosSelected()
    {
        if (null == weapon)
        {
            return;
        }

        Vector3 center = weapon.transform.position;
        float range = weapon.FinalRange;
        float half = currFanAngle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, 0f, half) * (Vector3)direction * range;
        Vector3 right = Quaternion.Euler(0f, 0f, -half) * (Vector3)direction * range;

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawLine(center, center + left);
        Gizmos.DrawLine(center, center + right);
        Gizmos.DrawWireSphere(center, range);
    }
}
