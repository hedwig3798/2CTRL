using UnityEngine;

/// <summary>
/// 보상을 고른 쪽 플레이어에게 새 무기를 준다
/// - 이미 같은 무기를 얻은 플레이어에게는 다시 주지 않는다
/// - 무기 풀은 두 플레이어가 공유하므로 풀에서 빼지 않고, 얻은 플레이어에게만 숨긴다 (RewardManager.ExcludeFor)
/// - 플레이어가 무기 보유 상한(PlayerStats.maxWeaponCount)에 도달하면 그 플레이어의 Weapon 풀을 막는다 (RewardManager.SetPoolBlockedFor)
/// - upgradeTable 이 있으면 무기를 얻을 때 1단계 업그레이드를 그 플레이어 전용 WeaponUpgrade 풀에 넣는다
///   (업그레이드 보상은 런타임에 만들며, 이름은 "<무기 보상 이름> Lv.N", 이미지는 무기 보상 이미지)
/// </summary>
[CreateAssetMenu(fileName = "WeaponRewardEffect", menuName = "Scriptable Objects/Reward Effect/Weapon")]
public class WeaponRewardEffect
    : RewardEffect
{
    [Tooltip("지급할 무기 프리팹")]
    [SerializeField]
    private Weapon weaponPrefab;

    [Tooltip("이 무기의 업그레이드 단계. 비어 있으면 업그레이드 보상을 넣지 않는다")]
    [SerializeField]
    private WeaponUpgradeTable upgradeTable;

    public Weapon WeaponPrefab => weaponPrefab;
    public WeaponUpgradeTable UpgradeTable => upgradeTable;

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

        bool added = _context.Player.AddWeapon(weaponPrefab);
        bool owned = _context.Player.HasWeapon(weaponPrefab);
        if (false == added && true == owned)
        {
            Debug.Log($"[WeaponRewardEffect] {_context.Player.name} 은(는) 이미 {weaponPrefab.name} 을(를) 가지고 있습니다.", this);
        }

        RewardManager manager = WeaponUpgradeRewardEffect.ResolveManager(_context);
        if (null == manager)
        {
            return;
        }

        // 가진 무기는 그 플레이어에게만 숨긴다 (다른 플레이어는 여전히 얻을 수 있다)
        if (true == owned)
        {
            manager.ExcludeFor(_context.Side, _data);
        }

        // 보유 상한에 도달하면 그 플레이어의 Weapon 풀 전체를 막는다 (시작 무기 포함 기준)
        if (true == _context.Player.IsWeaponFull)
        {
            manager.SetPoolBlockedFor(_context.Side, RewardPoolType.Weapon, true);
        }

        if (false == added || null == upgradeTable || upgradeTable.StepCount <= 0)
        {
            return;
        }

        // 번역 전 키를 넘긴다 (업그레이드 보상이 표시할 때 현재 언어로 바꾼다)
        string weaponName = true == _data.HasName ? _data.GetNameKey() : weaponPrefab.name;
        RewardData upgrade = WeaponUpgradeRewardEffect.CreateRewardData(upgradeTable, weaponPrefab, weaponName, _data.GetImage(), 1);
        if (null != upgrade)
        {
            manager.AddFor(_context.Side, RewardPoolType.WeaponUpgrade, upgrade, upgradeTable.RewardWeight);
        }
    }
}
