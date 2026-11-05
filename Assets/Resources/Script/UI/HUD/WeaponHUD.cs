using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 진영 플레이어의 보유 무기 표시 (인게임 HUD, 왼쪽 = 좌상단 / 오른쪽 = 우상단)
/// - PlayerStats.AcquiredWeapons 의 획득 순서대로 slots[0], slots[1] ... 에 채운다 (좌→우 / 우→좌 는 슬롯 배치 순서로 정한다)
/// - 남는 슬롯은 비운다. 무기가 슬롯보다 많으면 넘치는 무기는 표시하지 않는다
/// - 아이콘은 같은 진영 DimensionApplier.Dimension.WeaponRewards 에서
///   WeaponRewardEffect.WeaponPrefab 이 획득 프리팹과 같은 보상의 이미지를 쓴다 (없으면 fallbackRewards 에서 찾음)
///   못 찾으면 아이콘을 숨기고 이름을 표시하며 무기마다 경고를 한 번 남긴다
/// - PlayerStats 는 OnEnable 에서 등록되므로 Update 에서 찾힐 때까지 다시 시도한다 (지연 바인딩)
/// - 업그레이드 이벤트가 없으므로 Update 에서 슬롯마다 UpgradeLevel 을 비교해 바뀔 때만 갱신한다
/// </summary>
[DisallowMultipleComponent]
public class WeaponHUD
    : MonoBehaviour
{
    [Tooltip("표시할 플레이어 진영")]
    [SerializeField]
    private PlayerSide side = PlayerSide.Left;

    [Tooltip("표시할 플레이어 (비어 있으면 PlayerStats.Find(side))")]
    [SerializeField]
    private PlayerStats player;

    [Tooltip("무기 슬롯 (획득 순서대로 채운다)")]
    [SerializeField]
    private WeaponSlotUI[] slots;

    [Tooltip("아이콘을 찾을 차원 적용기 (비어 있으면 씬에서 같은 진영을 찾음)")]
    [SerializeField]
    private DimensionApplier[] dimensionAppliers;

    [Tooltip("차원에서 아이콘을 못 찾았을 때 찾아볼 무기 보상 (비워도 됨)")]
    [SerializeField]
    private RewardData[] fallbackRewards;

    private PlayerStats boundPlayer;

    // 프리팹 -> 아이콘 / 대체 이름 (한 번 찾은 결과를 기억)
    private readonly Dictionary<Weapon, Sprite> iconCache = new Dictionary<Weapon, Sprite>();
    private readonly Dictionary<Weapon, string> nameCache = new Dictionary<Weapon, string>();

    private bool hasSearchedAppliers = false;
    private bool hasWarnedOverflow = false;

    public PlayerSide Side => side;

    /// <summary>
    /// 지금 표시 중인 플레이어 (바인딩 전이면 null)
    /// </summary>
    public PlayerStats BoundPlayer => boundPlayer;

    private void OnEnable()
    {
        ClearSlots(0);
        TryBind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Update()
    {
        // 바인딩한 플레이어가 파괴되면 다시 찾는다
        if (null == boundPlayer)
        {
            if (false == ReferenceEquals(null, boundPlayer))
            {
                Unbind();
                ClearSlots(0);
            }

            if (false == TryBind())
            {
                return;
            }
        }

        if (null == slots)
        {
            return;
        }

        foreach (WeaponSlotUI slot in slots)
        {
            if (null != slot)
            {
                slot.RefreshLevel();
            }
        }
    }

    /// <summary>
    /// 플레이어를 찾아 무기 추가 이벤트를 구독하고 슬롯을 채운다
    /// </summary>
    /// <returns>바인딩했으면 true</returns>
    private bool TryBind()
    {
        if (null != boundPlayer)
        {
            return true;
        }

        PlayerStats target = null != player ? player : PlayerStats.Find(side);
        if (null == target)
        {
            return false;
        }

        boundPlayer = target;
        boundPlayer.OnWeaponAdded += HandleWeaponAdded;
        Rebuild();
        return true;
    }

    private void Unbind()
    {
        if (false == ReferenceEquals(null, boundPlayer))
        {
            boundPlayer.OnWeaponAdded -= HandleWeaponAdded;
        }
        boundPlayer = null;
    }

    private void HandleWeaponAdded(PlayerStats _player, Weapon _prefab, Weapon _weapon)
    {
        Rebuild();
    }

    /// <summary>
    /// 획득 순서대로 슬롯을 다시 채운다
    /// </summary>
    public void Rebuild()
    {
        if (null == slots || null == boundPlayer)
        {
            return;
        }

        IReadOnlyList<Weapon> prefabs = boundPlayer.AcquiredWeaponPrefabs;
        IReadOnlyList<Weapon> weapons = boundPlayer.AcquiredWeapons;
        int count = Mathf.Min(prefabs.Count, slots.Length);

        if (prefabs.Count > slots.Length && false == hasWarnedOverflow)
        {
            hasWarnedOverflow = true;
            Debug.LogWarning($"[WeaponHUD] 무기({prefabs.Count}) 가 슬롯({slots.Length}) 보다 많아 일부를 표시하지 않습니다. ({name})", this);
        }

        for (int i = 0; i < count; i++)
        {
            if (null == slots[i])
            {
                continue;
            }

            Weapon prefab = prefabs[i];
            Sprite icon = GetIcon(prefab, out string fallbackName);
            slots[i].SetWeapon(weapons[i], icon, fallbackName);
        }

        ClearSlots(count);
    }

    private void ClearSlots(int _from)
    {
        if (null == slots)
        {
            return;
        }

        for (int i = _from; i < slots.Length; i++)
        {
            if (null != slots[i])
            {
                slots[i].Clear();
            }
        }
    }

    /// <summary>
    /// 무기 프리팹의 아이콘 (못 찾으면 null 이고 _fallbackName 에 대체 이름)
    /// </summary>
    private Sprite GetIcon(Weapon _prefab, out string _fallbackName)
    {
        if (null == _prefab)
        {
            _fallbackName = string.Empty;
            return null;
        }

        if (true == iconCache.TryGetValue(_prefab, out Sprite cached))
        {
            nameCache.TryGetValue(_prefab, out _fallbackName);
            return cached;
        }

        RewardData reward = FindReward(_prefab);
        Sprite icon = null != reward ? reward.GetImage() : null;
        // 번역 전 키를 넘긴다 (WeaponSlotUI 가 현재 언어로 표시하고 언어가 바뀌면 다시 표시)
        _fallbackName = null != reward && true == reward.HasName ? reward.GetNameKey() : _prefab.name;

        if (null == icon)
        {
            string reason = null == reward ? "무기 보상을 찾지 못해" : "무기 보상에 이미지가 없어";
            Debug.LogWarning($"[WeaponHUD] {_prefab.name} 의 {reason} 아이콘 대신 이름({_fallbackName}) 을 표시합니다. ({side})", this);
        }

        iconCache[_prefab] = icon;
        nameCache[_prefab] = _fallbackName;
        return icon;
    }

    /// <summary>
    /// 같은 진영 차원의 WeaponRewards → fallbackRewards 순서로 _prefab 을 주는 무기 보상을 찾는다
    /// </summary>
    private RewardData FindReward(Weapon _prefab)
    {
        if (false == hasSearchedAppliers && (null == dimensionAppliers || 0 == dimensionAppliers.Length))
        {
            hasSearchedAppliers = true;
            dimensionAppliers = FindObjectsByType<DimensionApplier>();
        }

        if (null != dimensionAppliers)
        {
            foreach (DimensionApplier applier in dimensionAppliers)
            {
                if (null == applier || side != applier.Side || null == applier.Dimension)
                {
                    continue;
                }

                RewardData found = FindReward(applier.Dimension.WeaponRewards, _prefab);
                if (null != found)
                {
                    return found;
                }
            }
        }

        return FindReward(fallbackRewards, _prefab);
    }

    private static RewardData FindReward(RewardData[] _rewards, Weapon _prefab)
    {
        if (null == _rewards)
        {
            return null;
        }

        foreach (RewardData reward in _rewards)
        {
            if (null != reward && reward.GetEffect() is WeaponRewardEffect effect && _prefab == effect.WeaponPrefab)
            {
                return reward;
            }
        }
        return null;
    }
}
