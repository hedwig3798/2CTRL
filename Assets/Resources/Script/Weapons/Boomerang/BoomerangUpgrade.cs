using UnityEngine;

/// <summary>
/// 부메랑 업그레이드 효과 (OneHitProjectile + ReturnMovement 와 같은 오브젝트에 붙인다)
/// - 크기 : 발사할 때마다 프리팹 기본 크기 * weapon.GetParam(ProjectileScale, 1) 로 맞춘다
/// - ★ FlagReturnDamage : 되돌아오는 중의 타격은 returnDamageRate 배
/// </summary>
[RequireComponent(typeof(OneHitProjectile))]
public class BoomerangUpgrade
    : MonoBehaviour
    , Initializable
    , IHitDamageModifier
{
    [Header("scale")]
    [Tooltip("크기를 바꿀 대상. 비워두면 이 오브젝트 (콜라이더도 같이 커진다)")]
    [SerializeField]
    private Transform scaleTarget;

    [Header("return damage")]
    [Tooltip("FlagReturnDamage 가 켜졌을 때 되돌아오는 중 타격의 데미지 배율")]
    [Min(0f)]
    [SerializeField]
    private float returnDamageRate = 2f;

    private OneHitProjectile oneHitProjectile;
    private ReturnMovement returnMovement;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        oneHitProjectile = GetComponent<OneHitProjectile>();
        returnMovement = GetComponent<ReturnMovement>();
        if (null == scaleTarget)
        {
            scaleTarget = transform;
        }
        baseScale = scaleTarget.localScale;
    }

    public void Initialize(BlackBoard _data)
    {
        Weapon weapon = oneHitProjectile.OwnerWeapon;
        float scale = null != weapon ? weapon.GetParam(WeaponParam.ProjectileScale, 1f) : 1f;
        scaleTarget.localScale = baseScale * Mathf.Max(0.01f, scale);
    }

    public float GetHitDamageRate(OneHitProjectile _projectile, int _hitIndex)
    {
        if (null == returnMovement || false == returnMovement.IsReturning)
        {
            return 1f;
        }

        Weapon weapon = _projectile.OwnerWeapon;
        if (null == weapon || false == weapon.HasFlag(WeaponParam.FlagReturnDamage))
        {
            return 1f;
        }
        return returnDamageRate;
    }
}
