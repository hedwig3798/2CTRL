using System.Threading;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[RequireComponent(typeof(Attackable))]

public abstract class WeaponInstance : MonoBehaviour
{
    public Transform target;
    public Weapon weapon;
    public Stats ownerStats;

    protected Vector3 direction;
    protected Attackable attackable;

    public void Initialize(Transform _target, Weapon _weapon)
    {
        target = _target;
        weapon = _weapon;
        ownerStats = weapon.ownerStats;

        transform.position = _weapon.gameObject.transform.position;

        direction = (target.position - transform.position);
        // direction = Vector3.one;
        direction.z = 0;
        direction = direction.normalized;

        transform.up = direction;

        attackable.stats = ownerStats;
        attackable.baseAtk = weapon.weaponData.baseDamage;
    }

    private void Awake()
    {
        attackable = GetComponent<Attackable>();
        if(null == attackable)
        {
            Debug.LogError($"has no attackable but weapon instance");
        }
    }
}
