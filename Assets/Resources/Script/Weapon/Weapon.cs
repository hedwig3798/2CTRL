using NUnit.Framework.Constraints;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public struct TargetData
{
    public Transform transform;
    public float sqrDistance;
}

public abstract class Weapon : MonoBehaviour
{
    public Stats ownerStats;

    public WeaponData weaponData;

    public float cooltimer = 0.0f;

    public int maxCount;

    public LayerMask layerMask;

    public int preloadCount;

    protected List<TargetData> targetDataList;

    protected Queue<WeaponInstance> weaponInstances = new();

    protected void Awake()
    {
        if (null == ownerStats)
        {
            enabled = false;
            Debug.LogError($"{gameObject.name} has no stats but weapon");
        }

        if (null == weaponData.weaponObject)
        {
            enabled = false;
            Debug.LogError($"{gameObject.name} has no attak object but weapon");
        }

        targetDataList = new List<TargetData>(maxCount);

        if (0 != preloadCount)
        {
            for (int i = 0; i < preloadCount; i++)
            {
                WeaponInstance instance = Instantiate(weaponData.weaponObject);
                if(null == instance)
                {
                    Debug.LogError("cannot craete weapon instance");
                }
                instance.gameObject.SetActive(false);
                // instance.transform.parent = transform;
                weaponInstances.Enqueue(instance);
            }

        }
    }

    public void ReturnWeaponInstance(WeaponInstance _instance)
    {
        _instance.gameObject.SetActive(false);
        weaponInstances.Enqueue(_instance);
    }

    protected void Update()
    {
        cooltimer += Time.deltaTime;

        float finalCoolTime = weaponData.baseCooltime * ownerStats.coolTimeReduce;

        if (cooltimer > finalCoolTime)
        {
            cooltimer -= finalCoolTime;
            Attack();
        }
    }

    protected Collider2D[] FindTargetCandidate()
    {
        return Physics2D.OverlapCircleAll(transform.position, 100, layerMask);
    }

    protected abstract void Attack();
}
