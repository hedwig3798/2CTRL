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

    [SerializeField]
    private bool isInvincibility = false;

    private DropManager boundDropManager;
    private float baseMaxHP;

    // 사망 시 꺼서 추가 피격/접촉/타깃 탐색을 막는다
    private Collider2D[] colliders;

    [SerializeField]
    private SliderUI sliderUI;

    [Header("데미지 텍스트")]
    [Tooltip("데미지 텍스트가 뜰 위치 (비어있으면 자기 자신)")]
    [SerializeField]
    private Transform damageTextTarget;
    [Tooltip("데미지 텍스트가 그려질 화면\nAuto : 대상의 레이어로 결정")]
    [SerializeField]
    private UI_SCREEN damageTextScreen = UI_SCREEN.Auto;

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
        currHP = _val;
        sliderUI.CurrValue = _val;
    }

    public void SetMaxHP(float _val)
    {
        maxHP = _val;
        sliderUI.MaxValue = _val;
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
        DamageTextManager damageTextManager = DamageTextManager.Instance;
        if (0f < appliedDamage && null != damageTextManager)
        {
            Transform textTarget = (null != damageTextTarget) ? damageTextTarget : transform;
            damageTextManager.Show(appliedDamage, textTarget, damageTextScreen);
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
