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
    : MonoBehaviour
{
    // 데미지를 받을 수 있는 레이어 마스크
    public LayerMask dmgLayer;

    // 한번 받은 데미지는 일정 시간 못받는다
    private Dictionary<Attackable, HitInfo> hitInfoDict = new ();
    private List<Attackable> deadCombat = new ();

    // 이 컴포넌트가 있는 오브젝트의 스텟 컴포넌트 레퍼런스
    public Stats stats;

    // 죽은 이후의 행동
    public Action DeadCallback;

    private void Awake()
    {
        stats = gameObject.GetComponent<Stats>();
        if (null == stats)
        {
            Debug.LogError($"{gameObject.name} has no stats but Damagable");
            enabled = false;
        }
    }

    private void Update()
    {
        if (true == stats.isDead)
        {
            return;
        }

        float dt = Time.deltaTime;
        deadCombat.Clear();

        foreach (var pair in hitInfoDict)
        {
            Attackable k = pair.Key;

            if (null == k
                || false == k.isActiveAndEnabled
                || true == k.stats.isDead)
            {
                deadCombat.Add(k);
                continue;
            }

            hitInfoDict[k].coolTime -= dt;

            while (0 >= hitInfoDict[k].coolTime 
                && true == hitInfoDict[k].isColliding)
            {
                Damaged(k);
                hitInfoDict[k].coolTime += k.stats.atkSpeed;

                if (0 >= hitInfoDict[k].coolTime)
                {
                    return;
                }
            }
        }

        foreach (Attackable k in deadCombat)
        {
            hitInfoDict.Remove(k);
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

    private void OnCollisionEnter2D(Collision2D other)
    {
        OnTriggerAndCollisionEnter(other.collider);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        OnTriggerAndCollisionEnter(collision);
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        OnTriggerAndCollisionExit(other.collider);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        OnTriggerAndCollisionExit(collision);
    }

    // 데미지 처리
    void Damaged(Attackable attaker)
    {
        float finalDmg = attaker.stats.GetFinalDamage();

        // 계산된 데미지 만큼 데미지
        stats.hp -= finalDmg;

        // 타격자 흡혈
        attaker.stats.hp += finalDmg * attaker.stats.lifeSteal;
        // 타격자 데미지 반사
        attaker.stats.hp -= finalDmg * stats.reflectRate;

        // Debug.Log($"Damged {finalDmg}");
    }

    private void OnTriggerAndCollisionEnter(Collider2D other)
    {
        // Combat Object 끼리 검사
        if (false == other.TryGetComponent<Attackable>(out var attaker))
        {
            return;
        }

        // 데미지 레이어가 없다면 무시한다.
        if (0 == ((1 << other.gameObject.layer) & dmgLayer.value))
        {
            Debug.Log($"{other.gameObject.layer}, {dmgLayer.value}");
            Debug.Log($"{other.gameObject.name}, {gameObject.name}");
            return;
        }

        // 처리중인 데미지인지 확인
        if (true == hitInfoDict.TryGetValue(attaker, out HitInfo hit))
        {
            hit.isColliding = true;
            return;
        }


        // 새로운 데미지 종류
        HitInfo hitinfo = new()
        {
            isColliding = true,
            coolTime = attaker.stats.atkSpeed
        };

        hitInfoDict.Add(attaker, hitinfo);
        Damaged(attaker);
    }

    private void OnTriggerAndCollisionExit(Collider2D other)
    {
        if (false == other.TryGetComponent<Attackable>(out var attaker))
        {
            return;
        }

        if (true == hitInfoDict.TryGetValue(attaker, out HitInfo hit))
        {
            hit.isColliding = false;
            return;
        }
    }
}
