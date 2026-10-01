using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범위 안의 공격 대상을 찾는 공용 헬퍼
/// Projectile, 이동 컴포넌트 등에서 함께 사용한다
/// </summary>
public static class TargetFinder
{
    private static Collider2D[] candinate = new Collider2D[64];
    private static ContactFilter2D filter = new ContactFilter2D();

    /// <summary>
    /// 범위 안에서 가장 가까운 살아있는 적을 찾는다
    /// _exclude 에 포함된 대상과 _ignoreA / _ignoreB 는 제외한다
    /// </summary>
    public static Transform FindNearest(
        Vector3 _center
        , float _range
        , LayerMask _layer
        , HashSet<Transform> _exclude = null
        , Transform _ignoreA = null
        , Transform _ignoreB = null)
    {
        Transform result = null;

        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(_layer);

        int hitCount = Physics2D.OverlapCircle(
            _center
            , _range
            , filter
            , candinate
        );

        float minDistance = float.MaxValue;
        for (int i = 0; i < hitCount; ++i)
        {
            Transform curr = candinate[i].transform;
            if ((null != _ignoreA && curr == _ignoreA) || (null != _ignoreB && curr == _ignoreB))
            {
                continue;
            }

            if (null != _exclude && true == _exclude.Contains(curr))
            {
                continue;
            }

            if (false == IsAlive(curr))
            {
                continue;
            }

            float currDistance = (curr.position - _center).sqrMagnitude;
            if (currDistance < minDistance)
            {
                minDistance = currDistance;
                result = curr;
            }
        }

        return result;
    }

    /// <summary>
    /// 데미지를 받을 수 있고 죽지 않은 대상인지
    /// </summary>
    public static bool IsAlive(Transform _target)
    {
        if (null == _target || false == _target.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (false == _target.TryGetComponent(out DamagePipeline _))
        {
            return false;
        }

        if (_target.TryGetComponent(out HealthSystem hs) && true == hs.isDead)
        {
            return false;
        }

        return true;
    }
}
