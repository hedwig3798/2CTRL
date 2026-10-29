using UnityEngine;

/// <summary>
/// 주인 주변을 천천히 돌며 자신의 무기로 가장 가까운 적을 쏘는 드론
/// - 드론 자체는 접촉 데미지가 없다 (콜라이더 없음). 이동은 OrbitMovement 가 담당한다
/// - 자식 오브젝트의 Weapon(Repeat) 이 드론 위치에서 탄을 발사한다
/// - 드론은 부모 없이 풀에서 생성되므로 자식 무기의 Awake 에서 PlayerStats 를 찾지 못한다
///   발사(Launch) 시 부모 무기의 타겟 레이어 / 주인 스텟을 자식 무기에 넘겨준다
/// - 레이어는 Weapon.CreateObject 가 드론과 모든 자식(자식 무기 포함)을 부모 무기 레이어로 맞추고,
///   자식 무기의 탄은 자식 무기의 레이어를 따라간다
/// </summary>
public class DroneProjectile
    : Projectile
{
    [Header("drone")]
    [Tooltip("드론이 들고 있는 무기 (Repeat). 비어 있으면 자식에서 찾는다")]
    [SerializeField]
    private Weapon childWeapon;

    private void Awake()
    {
        if (null == childWeapon)
        {
            childWeapon = GetComponentInChildren<Weapon>(true);
        }
    }

    protected override bool OnLaunch()
    {
        SetupChildWeapon();
        return true;
    }

    protected override bool OnLaunchAt()
    {
        SetupChildWeapon();
        return true;
    }

    /// <summary>
    /// 부모 무기의 정보를 자식 무기에 넘겨준다
    /// - owner : 드론 자신 (탐색 시 제외 대상 / 기본 이동 대상. 자식 탄은 AimAtNearest 로 이동 대상을 덮어쓴다)
    /// - targetLayer : 부모 무기의 타겟 레이어
    /// - ownerStats : 부모 무기의 주인 스텟 (플레이어 Damage / AttackSpeed / Range 가 자식 무기에 더해진다)
    /// </summary>
    private void SetupChildWeapon()
    {
        if (null == childWeapon || null == weapon)
        {
            return;
        }

        childWeapon.owner = transform;
        childWeapon.targetLayer = weapon.targetLayer;
        childWeapon.SetOwnerStats(weapon.OwnerStats);
    }
}
