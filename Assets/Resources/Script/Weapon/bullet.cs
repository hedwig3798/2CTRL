using System.Threading;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class bullet : WeaponInstance
{
    private void Update()
    {
        float movement = weapon.weaponData.baseSpeed * Time.deltaTime;
        transform.Translate(Vector3.up * movement);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        weapon.ReturnWeaponInstance(this);
    }
}
