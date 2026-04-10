using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Tilemaps;

class HitInfo
{
    public float coolTime;
    public bool isColliding;
}

public class Damagable
    : MonoBehaviour, IDamageable
{
    // 데미지를 받을 수 있는 레이어 마스크
    public LayerMask dmgLayer;

    // 이 컴포넌트가 있는 오브젝트의 스텟 컴포넌트 레퍼런스
    public Stats stats;

    // 죽은 이후의 행동
    public Action DeadCallback;

    // stats 가 없다면 문제 있는거
    private void Awake()
    {
        stats = gameObject.GetComponent<Stats>();
        if (null == stats)
        {
            Debug.LogError($"{gameObject.name} has no stats but Damagable");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (null == stats)
        {
            Debug.LogError($"{gameObject.name} has no stats but Damagable");
        }

        if (0 >= stats.hp)
        {
            stats.isDead = true;

            DeadCallback?.Invoke();
        }
    }

    // 데미지 처리
    public DamageResult Damaged(DamageInfo attaker)
    {
        // 계산된 데미지 만큼 데미지
        stats.hp -= attaker.rawDamage;

        DamageResult result;
        result.actualDamage = attaker.rawDamage;
        result.relectDamage = attaker.rawDamage * stats.reflectRate;

        return result;
    }
}
