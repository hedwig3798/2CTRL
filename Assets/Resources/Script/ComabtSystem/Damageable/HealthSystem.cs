using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 실제 체력의 증감 및 사망 처리
/// </summary>
public class HealthSystem 
    : MonoBehaviour
    , IDamageable
{
    public float maxHP;
    public float currHP;

    public bool isDead { get; private set; }

    private Spawnable spawnable;

    public UnityEvent<float, float> OnValueChanged;

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
            isDead = true;
        }
    }

    private void Awake()
    {
        spawnable = GetComponent<Spawnable>();
        isDead = false;
    }

    private void ResetHP()
    {
        currHP = maxHP;
        isDead = false;
    }
}
