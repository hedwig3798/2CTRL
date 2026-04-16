using System.Threading;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;


public abstract class WeaponInstance : MonoBehaviour
{
    public Transform target;
    public Weapon weapon;

    protected Vector3 direction;

    public void Initialize(Transform _target, Weapon _weapon)
    {
        target = _target;
        weapon = _weapon;

        transform.position = _weapon.gameObject.transform.position;

        direction = (target.position - transform.position);
        // direction = Vector3.one;
        direction.z = 0;
        direction = direction.normalized;

        transform.up = direction;
    }

}
