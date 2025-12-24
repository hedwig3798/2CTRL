using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NearFinder : Weapon
{
    public int targetCount;


    protected override void Attack()
    {
        // Å¸±ê Ã£±â
        Collider2D[] targetCandidates = FindTargetCandidate();
        int candidatesCount = targetCandidates.Length;
        if (0 >= candidatesCount)
        {
            // Debug.Log($"{candidatesCount}");
            return;
        }
        
        targetDataList.Clear();
        foreach (Collider2D c in targetCandidates)
        {
            TargetData newData = new TargetData();
            newData.transform = c.transform;
            newData.sqrDistance = (c.transform.position - transform.position).sqrMagnitude;
            targetDataList.Add(newData);
        }
        targetDataList.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));


        // °ø°Ý È½¼ö
        int atkCount = weaponData.baseCount + ownerStats.addCount;
        atkCount = Mathf.Min(atkCount, targetDataList.Count);
        atkCount = Mathf.Min(atkCount, weaponInstances.Count);
        // Debug.Log($"{atkCount}");

        for (int i = 0; i < atkCount; i++)
        {
            // Debug.Log("Shoot");

            WeaponInstance w = weaponInstances.Dequeue();
            w.gameObject.SetActive(true);

            w.Initialize(targetDataList[i].transform, this);
        }
    }
}
