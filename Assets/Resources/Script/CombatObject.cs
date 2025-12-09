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
    public LayerMask dmgLayer;

    private Dictionary<CombatObject, HitInfo> hitInfoDict = new Dictionary<CombatObject, HitInfo>();
    private List<CombatObject> deadCombat = new List<CombatObject>();
    public Stats stats;

    // 데미지 처리
    void Damaged(CombatObject attaker)
    {
        Stats atkStats = attaker.stats;

        // 최종 데미지 계산
        float finalDmg = atkStats.atk;

        // 크리티컬 확률 계산
        float crit = UnityEngine.Random.value;
        // 크리티컬인 경우 데미지 계산
        if (crit <= atkStats.criticalRate)
        {
            finalDmg += atkStats.atk * atkStats.criticalDMG;
        }

        // 계산된 데미지 만큼 데미지
        stats.hp -= finalDmg;

        // 타격자 흡혈
        atkStats.hp += finalDmg * atkStats.lifeSteal;
        // 타격자 데미지 반사
        atkStats.hp -= finalDmg * stats.reflectRate;

        Debug.Log($"Damged {finalDmg}");
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

            while (0 >= hitInfoDict[k].coolTime 
                && true == hitInfoDict[k].isColliding)
            {
                Damaged(k);
                hitInfoDict[k].coolTime += k.stats.atkDelay;

                if (0 >= hitInfoDict[k].coolTime)
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
        // 데미지 레이어가 없다면 무시한다.
        if (0 == ((1 << other.gameObject.layer) & dmgLayer.value))
        {
            // Debug.Log($"{other.gameObject.layer}, {dmgLayer.value}");
            return;
        }

        // Combat Object 끼리 검사
        if (false == other.collider.TryGetComponent<CombatObject>(out var combat))
        {
            return;
        }

        // 처리중인 데미지인지 확인
        if (true == hitInfoDict.TryGetValue(combat, out HitInfo hit))
        {
            hit.isColliding = true;
            return;
        }


        // 새로운 데미지 종류
        HitInfo hitinfo = new()
        {
            isColliding = true,
            coolTime = combat.stats.atkDelay
        };

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
