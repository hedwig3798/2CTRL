using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Weapon 이 발사하는 발사체의 베이스
/// 타겟 탐색 / 추적 / 타격 로직은 파생 클래스가 OnLaunch 에서 결정한다
/// </summary>
public abstract class Projectile
    : MonoBehaviour
{
    [Header("projectile damage")]
    public float baseDamage;

    protected Weapon weapon;
    protected DamageMassage damageMassage;

    private IObjectPool<Projectile> pool;
    private bool returningToPool;

    private static Collider2D[] candinate = new Collider2D[64];
    private static ContactFilter2D filter = new ContactFilter2D();

    public void SetPool(IObjectPool<Projectile> _pool)
    {
        pool = _pool;
    }

    public void MarkReturningToPool()
    {
        returningToPool = true;
    }

    /// <summary>
    /// 무기 스펙을 받아 발사한다
    /// 타겟이 없어 발사하지 못하면 false 를 반환하고, 무기가 즉시 풀로 반환한다
    /// </summary>
    public bool Launch(Weapon _weapon)
    {
        weapon = _weapon;

        float rate = weapon.damageRate;
        if (rate <= 0f)
        {
            rate = 1f;
        }
        damageMassage.damage = rate * baseDamage;
        damageMassage.isReflected = false;
        damageMassage.attacker = null;

        return OnLaunch();
    }

    protected abstract bool OnLaunch();

    #region Target
    /// <summary>
    /// 범위 안에서 가장 가까운 살아있는 적을 찾는다
    /// </summary>
    protected Transform FindNearest(Vector3 _center, float _range, HashSet<Transform> _exclude = null)
    {
        Transform result = null;

        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(weapon.targetLayer);

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
            if (curr == weapon.transform || (null != weapon.owner && curr == weapon.owner))
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
    protected static bool IsAlive(Transform _target)
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
    #endregion

    protected virtual void OnEnable()
    {
        returningToPool = false;
    }

    protected virtual void OnDisable()
    {
        if (returningToPool || null == pool)
        {
            return;
        }

        returningToPool = true;
        pool.Release(this);
    }
}
