using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 범위 공격 공용 헬퍼 (자폭, 장판)
/// 정적 버퍼를 써서 호출 시 GC 할당이 없다. 같은 대상은 한 번만 맞는다
/// 데미지 처리 중 다시 호출(중첩)돼도 안전하다: 콜라이더 버퍼는 데미지를 보내기 전에 다 읽고,
/// 맞은 대상 목록은 호출마다 자기 구간(start ~ end)만 쓰고 끝날 때 그 구간만 지운다
/// </summary>
public static class MonsterAreaDamage
{
    private static Collider2D[] buffer = new Collider2D[32];
    private static List<DamagePipeline> hitList = new List<DamagePipeline>(32);
    private static ContactFilter2D filter = new ContactFilter2D();

    /// <summary>
    /// _center 반지름 _radius 안의 _layer 대상 DamagePipeline 에 데미지를 준다
    /// 넉백은 _center 에서 바깥쪽으로 밀어낸다. 맞은 대상 수를 반환한다
    /// </summary>
    public static int Apply(
        Vector2 _center
        , float _radius
        , LayerMask _layer
        , float _damage
        , float _knockbackForce
        , DamagePipeline _attacker)
    {
        if (_radius <= 0f || 0 == _layer.value)
        {
            return 0;
        }

        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(_layer);

        int count = Physics2D.OverlapCircle(_center, _radius, filter, buffer);

        int start = hitList.Count;
        for (int i = 0; i < count; ++i)
        {
            Collider2D c = buffer[i];
            buffer[i] = null;
            if (null == c)
            {
                continue;
            }

            if (false == c.TryGetComponent(out DamagePipeline dp))
            {
                dp = c.GetComponentInParent<DamagePipeline>();
            }

            // 자기 자신과 이미 맞은 대상(콜라이더 여러 개)은 건너뛴다
            if (null == dp || dp == _attacker || 0 <= hitList.IndexOf(dp, start, hitList.Count - start))
            {
                continue;
            }
            hitList.Add(dp);
        }

        // 목록을 먼저 다 만든 뒤 보낸다. 중첩 호출은 end 뒤에 붙였다가 스스로 지우므로 이 구간은 그대로다
        int end = hitList.Count;
        int hitCount = end - start;
        try
        {
            for (int i = start; i < end; ++i)
            {
                DamageMassage msg = new DamageMassage();
                msg.damage = _damage;
                msg.attacker = _attacker;
                msg.knockbackForce = _knockbackForce;
                msg.hitPoint = _center;
                hitList[i].ProcessDamage(ref msg);
            }
        }
        finally
        {
            // 예외가 나도 공유 목록에 이 호출의 구간이 남지 않게 한다
            hitList.RemoveRange(start, hitList.Count - start);
        }

        return hitCount;
    }

    /// <summary>
    /// Apply 와 같지만 대상별 쿨타임을 둔다 (회전 칼날처럼 매 프레임 판정하는 공격)
    /// _nextHitTimes 에 대상별 다음 피격 가능 Time.time 을 기록한다. 여러 칼날이 같은 사전을 쓰면 쿨타임을 공유한다
    /// </summary>
    public static int ApplyWithCooldown(
        Vector2 _center
        , float _radius
        , LayerMask _layer
        , float _damage
        , DamagePipeline _attacker
        , Dictionary<DamagePipeline, float> _nextHitTimes
        , float _cooldown)
    {
        if (_radius <= 0f || 0 == _layer.value || null == _nextHitTimes)
        {
            return 0;
        }

        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(_layer);

        int count = Physics2D.OverlapCircle(_center, _radius, filter, buffer);
        float now = Time.time;

        int start = hitList.Count;
        for (int i = 0; i < count; ++i)
        {
            Collider2D c = buffer[i];
            buffer[i] = null;
            if (null == c)
            {
                continue;
            }

            if (false == c.TryGetComponent(out DamagePipeline dp))
            {
                dp = c.GetComponentInParent<DamagePipeline>();
            }

            if (null == dp || dp == _attacker || 0 <= hitList.IndexOf(dp, start, hitList.Count - start))
            {
                continue;
            }

            if (true == _nextHitTimes.TryGetValue(dp, out float next) && now < next)
            {
                continue;
            }
            hitList.Add(dp);
        }

        int end = hitList.Count;
        int hitCount = end - start;
        try
        {
            for (int i = start; i < end; ++i)
            {
                DamagePipeline dp = hitList[i];
                _nextHitTimes[dp] = now + _cooldown;

                DamageMassage msg = new DamageMassage();
                msg.damage = _damage;
                msg.attacker = _attacker;
                msg.hitPoint = _center;
                dp.ProcessDamage(ref msg);
            }
        }
        finally
        {
            hitList.RemoveRange(start, hitList.Count - start);
        }

        return hitCount;
    }

    /// <summary>
    /// _center 반지름 _radius 안의 _layer 플레이어에게 임시 둔화를 건다 (PlayerStats.ApplyTimedSlow). 걸린 콜라이더 수를 반환한다
    /// 같은 플레이어에게 여러 번 걸어도 최대값이라 결과는 같다
    /// </summary>
    public static int ApplyPlayerSlow(Vector2 _center, float _radius, LayerMask _layer, float _rate, float _duration)
    {
        if (_radius <= 0f || 0 == _layer.value || _rate <= 0f || _duration <= 0f)
        {
            return 0;
        }

        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(_layer);

        int count = Physics2D.OverlapCircle(_center, _radius, filter, buffer);
        int slowed = 0;
        for (int i = 0; i < count; ++i)
        {
            Collider2D c = buffer[i];
            buffer[i] = null;
            if (null == c)
            {
                continue;
            }

            PlayerStats stats = c.GetComponentInParent<PlayerStats>();
            if (null == stats)
            {
                continue;
            }

            stats.ApplyTimedSlow(_rate, _duration);
            ++slowed;
        }
        return slowed;
    }
}
