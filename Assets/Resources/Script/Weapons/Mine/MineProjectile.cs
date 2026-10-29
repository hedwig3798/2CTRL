using UnityEngine;

/// <summary>
/// 주인 발밑에 설치되는 지뢰 발사체
/// 설치 후 armDelay 가 지나면 활성화되고, 활성화된 뒤 처음 닿은 적에 의해 폭발한다
/// 폭발은 지뢰 위치를 중심으로 무기 areaRadius 안의 적 모두에게 데미지를 주고 바깥쪽으로 밀어낸다
/// 터지지 않은 지뢰는 Lifetime 이 끝나면 사라진다
/// 유지형(Persistent) 무기는 발사체 수가 바뀔 때 위치를 다시 초기화하므로 반복형(Repeat) 무기에 붙여 쓴다
/// </summary>
public class MineProjectile
    : Projectile
{
    [Header("mine")]
    [Tooltip("설치 후 활성화까지의 시간 (초). 활성화 전에는 적이 닿아도 터지지 않는다")]
    [Min(0f)]
    [SerializeField]
    private float armDelay = 0.5f;
    [Tooltip("켜면 무기 범위(FinalRange) 안에 적이 있을 때만 설치한다 (지뢰 낭비 방지)")]
    [SerializeField]
    private bool requireEnemyInRange = true;
    [Tooltip("적을 감지하는 트리거 콜라이더. 활성화 순간 이미 올라와 있는 적도 이 범위로 검사한다. 비워두면 같은 오브젝트의 CircleCollider2D")]
    [SerializeField]
    private CircleCollider2D triggerCollider;

    [Header("explosion")]
    [Tooltip("폭발 연출 (EffectPool 로 꺼낸다). 크기는 무기 areaRadius 에 맞춘다")]
    [SerializeField]
    private PooledEffect explosionEffect;

    [Header("visual")]
    [Tooltip("활성화 상태에 따라 색을 바꿀 렌더러")]
    [SerializeField]
    private SpriteRenderer visual;
    [Tooltip("활성화 전 색")]
    [SerializeField]
    private Color unarmedColor = new Color(1f, 0.85f, 0.3f, 0.4f);
    [Tooltip("활성화 후 색")]
    [SerializeField]
    private Color armedColor = new Color(1f, 0.25f, 0.2f, 1f);
    [Tooltip("활성화 후 깜빡일 때 섞이는 색")]
    [SerializeField]
    private Color armedBlinkColor = new Color(0.5f, 0.1f, 0.1f, 1f);
    [Tooltip("활성화 후 깜빡이는 주기 (초). 0 이하면 깜빡이지 않는다")]
    [Min(0f)]
    [SerializeField]
    private float blinkPeriod = 0.8f;

    private float armTime;
    private bool isArmed;
    private bool isExploded;

    protected override bool OnLaunch()
    {
        if (true == requireEnemyInRange
            && null == FindNearest(weapon.transform.position, weapon.FinalRange))
        {
            return false;
        }

        // 무기(주인 발밑) 위치에 설치한다
        Vector3 point = weapon.transform.position;
        BlackBoard b = GetBlackBoard();
        if (null != b && true == b.TryGetStartPoint(out Vector3 start))
        {
            point = start;
        }
        point.z = 0f;
        transform.position = point;

        ResetMine();
        return true;
    }

    protected override bool OnLaunchAt()
    {
        // 위치는 LaunchAt 이 시작 지점으로 정한다
        ResetMine();
        return true;
    }

    /// <summary>
    /// 설치 직후 상태(비활성화)로 되돌린다
    /// </summary>
    private void ResetMine()
    {
        armTime = Time.time + armDelay;
        isArmed = false;
        isExploded = false;
        ApplyColor();
    }

    private void Awake()
    {
        if (null == triggerCollider)
        {
            triggerCollider = GetComponent<CircleCollider2D>();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        isArmed = false;
        isExploded = false;
        ApplyColor();
    }

    private void Update()
    {
        if (null == weapon || true == isExploded)
        {
            return;
        }

        if (false == isArmed)
        {
            if (Time.time < armTime)
            {
                return;
            }

            isArmed = true;
            ApplyColor();

            // 활성화 순간 이미 올라와 있는 적 (잠든 Rigidbody 는 OnTriggerStay2D 가 오지 않을 수 있다)
            if (null != FindNearest(transform.position, GetTriggerRadius()))
            {
                Explode();
            }
            return;
        }

        ApplyColor();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryTrigger(other);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTrigger(other);
    }

    private void TryTrigger(Collider2D _other)
    {
        if (false == isArmed || true == isExploded)
        {
            return;
        }

        if (false == IsTargetLayer(_other.gameObject.layer))
        {
            return;
        }

        if (false == IsAlive(_other.transform))
        {
            return;
        }

        Explode();
    }

    /// <summary>
    /// 지뢰 위치를 중심으로 areaRadius 안의 적을 모두 타격하고 사라진다
    /// </summary>
    private void Explode()
    {
        if (true == isExploded)
        {
            return;
        }
        isExploded = true;

        Vector2 center = transform.position;
        float radius = weapon.areaRadius;

        ApplyAreaDamage(center, radius);
        EffectPool.Spawn(explosionEffect, center, radius);

        gameObject.SetActive(false);
    }

    private float GetTriggerRadius()
    {
        if (null == triggerCollider)
        {
            return 0.3f;
        }

        Vector3 scale = triggerCollider.transform.lossyScale;
        return triggerCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }

    private void ApplyColor()
    {
        if (null == visual)
        {
            return;
        }

        if (false == isArmed)
        {
            visual.color = unarmedColor;
            return;
        }

        if (blinkPeriod <= 0f)
        {
            visual.color = armedColor;
            return;
        }

        float t = Mathf.PingPong((Time.time - armTime) * 2f / blinkPeriod, 1f);
        visual.color = Color.Lerp(armedColor, armedBlinkColor, t);
    }

    private bool IsTargetLayer(int _layer)
    {
        if (_layer == gameObject.layer)
        {
            return false;
        }

        if (null == weapon)
        {
            return false;
        }

        return 0 != (weapon.targetLayer.value & (1 << _layer));
    }

    protected override void OnDisable()
    {
        isArmed = false;
        isExploded = false;

        base.OnDisable();
    }
}
