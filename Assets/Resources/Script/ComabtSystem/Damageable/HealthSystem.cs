using System;
using UnityEngine;

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

    private DropManager boundDropManager;
    private float baseMaxHP;

    // 사망 시 꺼서 추가 피격/접촉/타깃 탐색을 막는다
    private Collider2D[] colliders;

    private void SetCollidersEnabled(bool _enabled)
    {
        if (null == colliders)
        {
            return;
        }

        foreach (Collider2D c in colliders)
        {
            if (null != c)
            {
                c.enabled = _enabled;
            }
        }
    }

    public void ProcessDamage(ref DamageMassage _msg)
    {
        if (true == isDead)
        {
            return;
        }

        currHP -= _msg.damage;
        if (currHP <= 0)
        {
            currHP = 0;
            isDead = true;
            SetCollidersEnabled(false);
            OnDeath?.Invoke(gameObject);
        }
    }

    public void Initialize(BlackBoard _data)
    {
        isDead = false;
        SetCollidersEnabled(true);

        float rate = 1f;
        if (null != _data)
        {
            rate = _data.GetFloat(DATA_TYPE.HPRate);
            if (rate <= 0f)
            {
                rate = 1f;
            }
        }

        maxHP = baseMaxHP * rate;
        currHP = maxHP;

        if (null != boundDropManager)
        {
            OnDeath -= boundDropManager.DropItem;
            boundDropManager = null;
        }

        if (null != _data && null != _data.dropManager)
        {
            boundDropManager = _data.dropManager;
            OnDeath -= boundDropManager.DropItem;
            OnDeath += boundDropManager.DropItem;
        }
    }

    private void Awake()
    {
        isDead = false;
        colliders = GetComponentsInChildren<Collider2D>(true);
        if (baseMaxHP <= 0f)
        {
            baseMaxHP = maxHP;
        }
    }

    private void OnDisable()
    {
        if (null != boundDropManager)
        {
            OnDeath -= boundDropManager.DropItem;
            boundDropManager = null;
        }
    }
}
