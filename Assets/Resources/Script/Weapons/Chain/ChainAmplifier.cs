using UnityEngine;

/// <summary>
/// 체인 번개 ★ 증폭 (OneHitProjectile + ChainMovement 와 같은 오브젝트에 붙인다)
/// FlagChainAmplify 가 켜지면 전이할 때마다 데미지가 amplifyRate 씩 누적해 오른다
/// (n 번째 타격(0 부터) 데미지 = 기본 데미지 * (1 + amplifyRate * n))
/// </summary>
[RequireComponent(typeof(OneHitProjectile))]
public class ChainAmplifier
    : MonoBehaviour
    , IHitDamageModifier
{
    [Tooltip("전이 한 번마다 더해지는 데미지 비율 (0.15 = +15%)")]
    [Min(0f)]
    [SerializeField]
    private float amplifyRate = 0.15f;

    public float GetHitDamageRate(OneHitProjectile _projectile, int _hitIndex)
    {
        Weapon weapon = _projectile.OwnerWeapon;
        if (null == weapon || false == weapon.HasFlag(WeaponParam.FlagChainAmplify))
        {
            return 1f;
        }
        return 1f + amplifyRate * Mathf.Max(0, _hitIndex);
    }
}
