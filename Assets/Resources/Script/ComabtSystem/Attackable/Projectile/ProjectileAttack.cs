using UnityEngine;
using UnityEngine.Pool;

public class ProjectileAttack 
    : MonoBehaviour
    , IAttackable
{
    Transform owner;

    int preLoadCount;

    Spawnable projectile;

    IObjectPool<Spawnable> projectilePool;

    public void Attack(Transform _target)
    {

    }

    private void Awake()
    {

    }
}
