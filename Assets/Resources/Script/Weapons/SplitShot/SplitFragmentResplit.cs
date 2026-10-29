using UnityEngine;

/// <summary>
/// 분열탄 조각의 ★ 재분열 (조각 프리팹의 OneHitProjectile 과 같은 오브젝트에 붙인다)
/// SplitShotProjectile 이 FlagResplit 일 때 Arm 으로 켜며, 켜진 조각은 처음 맞힌 적 자리에서 resplitCount 개로 한 번 더 분열한다
/// 재분열로 나온 조각은 켜지 않으므로 다시 분열하지 않는다. 꺼지면(풀 반환) 자동으로 해제된다
/// </summary>
[RequireComponent(typeof(OneHitProjectile))]
public class SplitFragmentResplit
    : MonoBehaviour
    , IProjectileHitListener
{
    [Tooltip("재분열할 때 흩뿌릴 조각 수")]
    [Min(1)]
    [SerializeField]
    private int resplitCount = 3;

    private bool isArmed;
    private Projectile prefab;
    private float damageRate;
    private float distance;
    private float lifetimeRate;

    /// <summary>
    /// 이번 발사에서 재분열하도록 켠다 (Weapon.FireSub 로 발사한 직후 호출)
    /// </summary>
    public void Arm(Projectile _prefab, float _damageRate, float _distance, float _lifetimeRate)
    {
        isArmed = null != _prefab;
        prefab = _prefab;
        damageRate = _damageRate;
        distance = _distance;
        lifetimeRate = _lifetimeRate;
    }

    public void OnProjectileHit(OneHitProjectile _projectile, DamagePipeline _target, int _hitIndex)
    {
        if (false == isArmed)
        {
            return;
        }
        isArmed = false;

        Vector3 point = null != _target ? _target.transform.position : _projectile.transform.position;
        SplitShotProjectile.FireFragments(
            _projectile.OwnerWeapon
            , prefab
            , point
            , _target
            , resplitCount
            , damageRate
            , distance
            , lifetimeRate
            , false
        );
    }

    private void OnEnable()
    {
        isArmed = false;
    }

    private void OnDisable()
    {
        isArmed = false;
        prefab = null;
    }
}
