using UnityEngine;

/// <summary>
/// 플레이어 주변의 아이템 자석 범위 (트리거 CircleCollider2D)
/// - 범위에 들어온 아이템의 SlingshotMovement 대상을 owner 로 바꿔 끌어당긴다
/// - 원 반경 = PlayerStats.Get(PickupRange). base 는 프리팹에 지정된 반경(BaseRadius) 이고, 획득 범위 버프 / 디버프(Multiply) 가 곱해진다
/// - PlayerStats 가 없으면 프리팹 반경을 그대로 쓴다
/// </summary>
public class ItemTrigger : MonoBehaviour
{
    [SerializeField]
    private GameObject owner;

    private CircleCollider2D circle;
    private PlayerStats stats;
    private float baseRadius = -1f;

    /// <summary>
    /// 버프 적용 전 반경 (프리팹에 지정된 CircleCollider2D 반경, 콜라이더가 없으면 0)
    /// PlayerStats 가 PickupRange 의 base 로 쓴다
    /// </summary>
    public float BaseRadius
    {
        get
        {
            EnsureBase();
            return baseRadius;
        }
    }

    /// <summary>
    /// 지금 적용된 반경 (콜라이더가 없으면 0)
    /// </summary>
    public float CurrentRadius => null != circle ? circle.radius : 0f;

    private void EnsureBase()
    {
        if (baseRadius >= 0f)
        {
            return;
        }

        circle = GetComponent<CircleCollider2D>();
        baseRadius = null != circle ? circle.radius : 0f;
    }

    private void OnEnable()
    {
        EnsureBase();

        if (null == stats)
        {
            stats = null != owner ? owner.GetComponentInParent<PlayerStats>() : null;
            if (null == stats)
            {
                stats = GetComponentInParent<PlayerStats>();
            }
        }

        if (null != stats)
        {
            stats.OnStatChanged += HandleStatChanged;
            ApplyRadius();
        }
    }

    private void OnDisable()
    {
        if (null != stats)
        {
            stats.OnStatChanged -= HandleStatChanged;
        }
    }

    private void HandleStatChanged(PlayerStats _stats)
    {
        ApplyRadius();
    }

    private void ApplyRadius()
    {
        if (null == circle || null == stats)
        {
            return;
        }

        float radius = stats.Get(ValueType.PickupRange);
        if (false == Mathf.Approximately(circle.radius, radius))
        {
            circle.radius = radius;
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (false == _other.CompareTag("item"))
        {
            return;
        }

        if (false == _other.TryGetComponent(out SlingshotMovement movement))
        {
            return;
        }

        Transform magnet = null != owner ? owner.transform : transform;
        movement.target = magnet;
        movement.isStop = false;
    }
}
