using UnityEngine;

/// <summary>
/// 보상을 고른 쪽 플레이어에게 새 무기를 준다
/// - 이미 같은 무기를 얻은 플레이어에게는 다시 주지 않는다
/// - 무기 풀은 두 플레이어가 공유하므로 얻어도 풀에서 빼지 않는다
/// </summary>
[CreateAssetMenu(fileName = "WeaponRewardEffect", menuName = "Scriptable Objects/Reward Effect/Weapon")]
public class WeaponRewardEffect
    : RewardEffect
{
    [Tooltip("지급할 무기 프리팹")]
    [SerializeField]
    private Weapon weaponPrefab;

    public override void Apply(RewardContext _context, RewardData _data)
    {
        if (null == weaponPrefab)
        {
            Debug.LogWarning($"[WeaponRewardEffect] weaponPrefab 이 연결되지 않았습니다. ({name})", this);
            return;
        }

        if (null == _context.Player)
        {
            Debug.LogWarning($"[WeaponRewardEffect] 적용할 플레이어가 없습니다. ({_data.name})", _data);
            return;
        }

        if (false == _context.Player.AddWeapon(weaponPrefab))
        {
            Debug.Log($"[WeaponRewardEffect] {_context.Player.name} 은(는) 이미 {weaponPrefab.name} 을(를) 가지고 있습니다.", this);
        }
    }
}
