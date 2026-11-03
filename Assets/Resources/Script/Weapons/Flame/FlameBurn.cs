using UnityEngine;

/// <summary>
/// 화염 방사 ★ 화상 상태 (FlameProjectile 이 FlameBurn.Apply 로 건다)
/// 처음 화상을 입힐 때 대상(DamagePipeline 이 있는 오브젝트) 에 AddComponent 로 붙고, 이후에는 같은 컴포넌트를 재사용한다
/// - 화상 중이 아니면 비활성화되어 Update 가 돌지 않는다
/// - 화상 중 다시 맞으면 지속시간만 갱신한다 (중첩 없음, 틱 데미지 / 틱 주기 / 무기는 처음 값 유지)
/// - 대상이 죽거나 비활성화(풀 반환) 되면 화상을 정리한다
/// - 틱 데미지는 DamageMassage.weapon 에 화염 무기를 넣어 통계에 귀속된다. 넉백 없음
/// </summary>
[DisallowMultipleComponent]
public class FlameBurn
    : MonoBehaviour
{
    private DamagePipeline pipeline;
    private HealthSystem health;

    private Weapon sourceWeapon;
    private float tickDamage;
    private float tickInterval;
    private float nextTickTime;
    private float endTime;
    private bool isBurning;

    /// <summary>
    /// 화상 중인지
    /// </summary>
    public bool IsBurning => isBurning;

    /// <summary>
    /// 화상이 끝나는 시각 (Time.time 기준). 화상 중이 아니면 0
    /// </summary>
    public float EndTime => endTime;

    /// <summary>
    /// _target 에 화상을 건다. 이미 화상 중이면 지속시간만 갱신한다
    /// 살아 있지 않은 대상이거나 값이 잘못되면 null
    /// </summary>
    public static FlameBurn Apply(Transform _target, Weapon _weapon, float _tickDamage, float _interval, float _duration)
    {
        if (_tickDamage <= 0f || _duration <= 0f)
        {
            return null;
        }

        if (false == TargetFinder.IsAlive(_target))
        {
            return null;
        }

        if (false == _target.TryGetComponent(out FlameBurn burn))
        {
            burn = _target.gameObject.AddComponent<FlameBurn>();
        }

        burn.Refresh(_weapon, _tickDamage, _interval, _duration);
        return burn;
    }

    private void Awake()
    {
        pipeline = GetComponent<DamagePipeline>();
        health = GetComponent<HealthSystem>();
    }

    private void Refresh(Weapon _weapon, float _tickDamage, float _interval, float _duration)
    {
        float now = Time.time;
        if (true == isBurning)
        {
            endTime = Mathf.Max(endTime, now + _duration);
            return;
        }

        sourceWeapon = _weapon;
        tickDamage = _tickDamage;
        tickInterval = Mathf.Max(0.05f, _interval);
        nextTickTime = now + tickInterval;
        endTime = now + _duration;
        isBurning = true;
        enabled = true;
    }

    private void Update()
    {
        if (false == isBurning)
        {
            enabled = false;
            return;
        }

        if (null != health && true == health.isDead)
        {
            Stop();
            return;
        }

        float now = Time.time;
        // 끝나는 시각과 겹치는 틱까지 준다 (3 초 / 0.5 초 = 6 틱)
        if (now >= nextTickTime && nextTickTime <= endTime + 0.001f)
        {
            Tick();
            nextTickTime += tickInterval;
        }

        if (true == isBurning && now >= endTime && nextTickTime > endTime + 0.001f)
        {
            Stop();
        }
    }

    private void Tick()
    {
        if (null == pipeline)
        {
            Stop();
            return;
        }

        DamageMassage msg = new DamageMassage();
        msg.damage = tickDamage;
        msg.isReflected = false;
        msg.attacker = null;
        msg.knockbackForce = 0f;
        msg.hitPoint = transform.position;
        // 무기가 사라졌으면 (Unity null) 통계에 귀속하지 않는다
        msg.weapon = null != sourceWeapon ? sourceWeapon : null;
        pipeline.ProcessDamage(ref msg);

        if (null != health && true == health.isDead)
        {
            Stop();
        }
    }

    private void Stop()
    {
        ClearState();
        enabled = false;
    }

    private void ClearState()
    {
        isBurning = false;
        sourceWeapon = null;
        tickDamage = 0f;
        nextTickTime = 0f;
        endTime = 0f;
    }

    // 대상이 비활성화(풀 반환) 되면 화상을 정리한다. 다시 꺼내졌을 때 이전 화상이 이어지지 않는다
    private void OnDisable()
    {
        ClearState();
    }
}
