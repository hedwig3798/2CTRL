using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// 실제 체력의 증감 및 사망 처리
/// </summary>
public class HealthSystem
    : MonoBehaviour
    , IDamageable
    , Initializable
{
    public float maxHP;
    public float currHP;

    public bool isDead;

    public Action<GameObject> OnDeath;


    public void ProcessDamage(ref DamageMassage _msg)
    {
        if (true == isDead)
        {
            return;
        }

        currHP -= _msg.damage;
        if (currHP < 0)
        {
            currHP = 0;
            OnDeath?.Invoke(gameObject);
            isDead = true;
        }
    }

    public void Initialize(BlackBoard _data)
    {
        isDead = false;
        currHP = maxHP;

        OnDeath += _data.dropManager.DropItem;
    }

    private void Awake()
    {
        isDead = false;
    }
}
