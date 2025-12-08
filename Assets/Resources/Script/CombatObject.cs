using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using System;

class HitInfo
{
    public float coolTime;
    public bool isColliding;
}

public class CombatObject 
    : MonoBehaviour
{
    public HashSet<string> damagableTag = new HashSet<string>();
    public HashSet<string> attakableTag = new HashSet<string>();

    private Dictionary<CombatObject, HitInfo> hitInfoDict = new Dictionary<CombatObject, HitInfo>();
    private List<CombatObject> deadCombat = new List<CombatObject>();
    public Stats stats;


    // 데미지 처리
    void Damaged(CombatObject attaker)
    {
        if (0 < stats.hp)
        {
            stats.hp -= attaker.stats.atk;
        }
    }

    private void Start()
    {
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        deadCombat.Clear();


        foreach (var pair in hitInfoDict)
        {
            CombatObject k = pair.Key;

            if (null == k 
                || false == k.isActiveAndEnabled)
            {
                deadCombat.Add(k);
                continue;
            }

            hitInfoDict[k].coolTime -= dt;

            while (hitInfoDict[k].coolTime <= 0
                && true == hitInfoDict[k].isColliding)
            {
                Damaged(k);
                hitInfoDict[k].coolTime += k.stats.atkDelay;

                if (hitInfoDict[k].coolTime <= 0)
                {
                    return;
                }
            }
        }

        foreach (CombatObject k in deadCombat)
        {
            hitInfoDict.Remove(k);
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Combat Object 끼리 검사
        if (false == other.collider.TryGetComponent<CombatObject>(out var combat))
        {
            return;
        }

        if (true == hitInfoDict.TryGetValue(combat, out HitInfo hit))
        {
            hit.isColliding = true;
            return;
        }

        // 새로운 데미지 종류
        HitInfo hitinfo = new HitInfo();
        hitinfo.isColliding = true;
        hitinfo.coolTime = combat.stats.atkDelay;

        hitInfoDict.Add(combat, hitinfo);
        Damaged(combat);
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        if (false == other.collider.TryGetComponent<CombatObject>(out var combat))
        {
            return;
        }

        if (true == hitInfoDict.TryGetValue(combat, out HitInfo hit))
        {
            hit.isColliding = false;
            return;
        }
    }

}
