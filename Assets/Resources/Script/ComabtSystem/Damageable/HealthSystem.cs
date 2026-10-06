using System;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// HP 가 바뀔 때 호출 (현재치, 최대치)
    /// </summary>
    public Action<float, float> OnHPChanged;

    /// <summary>
    /// 데미지를 받았을 때 호출 (표시할 데미지 양)
    /// 무적이면 받은 데미지 그대로, 아니면 실제로 깎인 양
    /// </summary>
    public Action<float> OnDamaged;

    [SerializeField]
    private bool isInvincibility = false;

    private DropManager boundDropManager;
    private float baseMaxHP;

    // 사망 시 꺼서 추가 피격/접촉/타깃 탐색을 막는다
    private Collider2D[] colliders;

    public float CurrHP
    {
        get => currHP;
        set => SetCurrHP(value);
    }

    public float MaxHP
    {
        get => maxHP;
        set => SetMaxHP(value);
    }

    public void SetCurrHP(float _val)
    {
        if (true == Mathf.Approximately(currHP, _val))
        {
            return;
        }
        currHP = _val;
        OnHPChanged?.Invoke(currHP, maxHP);
    }

    public void SetMaxHP(float _val)
    {
        if (true == Mathf.Approximately(maxHP, _val))
        {
            return;
        }
        maxHP = _val;
        OnHPChanged?.Invoke(currHP, maxHP);
    }

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

        float prevHP = CurrHP;
        CurrHP -= _msg.damage;
        if (CurrHP <= 0)
        {
            CurrHP = 0;
        }

        // 현재 HP 보다 큰 데미지는 실제로 깎인 양만 출력
        float appliedDamage = prevHP - CurrHP;

        // 무적이면 HP 가 다시 채워지므로 받은 데미지를 그대로 출력
        float displayDamage = (true == isInvincibility) ? _msg.damage : appliedDamage;
        if (0f < displayDamage)
        {
            OnDamaged?.Invoke(displayDamage);
        }

        if (true == isInvincibility)
        {
            CurrHP = MaxHP;
            return;
        }

        if (CurrHP <= 0)
        {
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

        MaxHP = baseMaxHP * rate;
        CurrHP = MaxHP;

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
            baseMaxHP = MaxHP;
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
