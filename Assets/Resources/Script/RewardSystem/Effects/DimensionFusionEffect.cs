using UnityEngine;

/// <summary>
/// 차원 무기 합성 보상: 컨텍스트(고른 쪽) 와 무관하게 양쪽 플레이어를 처리한다
/// - 양쪽 재료 무기 1개씩 제거 → 결과 프리팹 1개 생성(부모 없음, 화면 공간 중앙) → 양쪽 차원 무기 슬롯에 등록
/// - 제거한 무기의 남은 업그레이드 보상을 그 플레이어 전용 WeaponUpgrade 풀에서 뺀다
/// - 이미 적용했거나 어느 한쪽이라도 같은 차원 무기를 가졌으면 아무것도 하지 않는다 (중복 적용 방지)
/// - 에셋으로 만들지 않고 CreateReward 로 런타임에 RewardData 와 함께 만든다
/// </summary>
public class DimensionFusionEffect
    : RewardEffect
{
    [SerializeField]
    private DimensionWeaponData recipe;

    private bool isApplied;
    private bool isFused;

    public DimensionWeaponData Recipe => recipe;

    /// <summary>
    /// 이 효과로 합성을 마쳤는지
    /// </summary>
    public bool IsApplied => isApplied;

    /// <summary>
    /// 이 효과가 실제로 재료를 소모하고 차원 무기를 만들었는지 (이미 보유해 건너뛴 경우 false)
    /// </summary>
    public bool IsFused => isFused;

    /// <summary>
    /// _manager 로 _data(CreateReward 로 만든 합성 보상) 를 _side 컨텍스트에 적용하고, 실제로 합성했는지 돌려준다
    /// - _manager / _data 가 없거나 합성 효과가 아니면 false
    /// - 합성법 무효, 플레이어 없음, 재료 부족, 이미 보유로 합성하지 않았으면 false
    /// </summary>
    public static bool TryApply(RewardManager _manager, RewardData _data, PlayerSide _side)
    {
        DimensionFusionEffect effect = null != _data ? _data.GetEffect() as DimensionFusionEffect : null;
        if (null == _manager || null == effect)
        {
            return false;
        }

        _manager.ApplyReward(_data, _side);
        return true == effect.isFused;
    }

    /// <summary>
    /// _recipe 합성 보상을 만든다 (_recipe 가 없거나 유효하지 않으면 null)
    /// - 이름 = nameKey, 이미지 = icon, explain1 = explainKey, explain2 없음, key / value 없음
    /// </summary>
    public static RewardData CreateReward(DimensionWeaponData _recipe)
    {
        if (null == _recipe || false == _recipe.IsValid)
        {
            return null;
        }

        DimensionFusionEffect effect = CreateInstance<DimensionFusionEffect>();
        effect.name = $"{_recipe.name} Fusion Effect";
        effect.recipe = _recipe;

        RewardData data = CreateInstance<RewardData>();
        data.name = $"{_recipe.name} Fusion";
        data.SetName(_recipe.DisplayNameKey);
        if (null != _recipe.Icon)
        {
            data.SetImage(_recipe.Icon);
        }
        else
        {
            data.ClearImage();
        }
        data.ClearKey();
        data.ClearValue();
        if (true == string.IsNullOrEmpty(_recipe.ExplainKey))
        {
            data.ClearExplain1();
        }
        else
        {
            data.SetExplain1(ToExplain(_recipe.ExplainKey));
        }
        data.ClearExplain2();
        data.SetEffect(effect);
        return data;
    }

    public override void Apply(RewardContext _context, RewardData _data)
    {
        if (true == isApplied)
        {
            return;
        }

        if (null == recipe || false == recipe.IsValid)
        {
            Debug.LogWarning($"[DimensionFusionEffect] 합성법이 비어 있거나 유효하지 않습니다. ({name})", this);
            return;
        }

        PlayerStats left = PlayerStats.Find(PlayerSide.Left);
        PlayerStats right = PlayerStats.Find(PlayerSide.Right);
        if (null == left || null == right)
        {
            Debug.LogWarning($"[DimensionFusionEffect] 양쪽 플레이어를 찾지 못했습니다. ({recipe.name})", this);
            return;
        }

        if (true == left.HasDimensionWeapon(recipe) || true == right.HasDimensionWeapon(recipe))
        {
            Debug.Log($"[DimensionFusionEffect] 이미 {recipe.name} 을(를) 보유하고 있습니다.", this);
            isApplied = true;
            return;
        }

        // 어느 쪽이 어느 재료를 가졌는지 정한다
        Weapon leftMaterial;
        Weapon rightMaterial;
        if (true == left.HasWeapon(recipe.MaterialA) && true == right.HasWeapon(recipe.MaterialB))
        {
            leftMaterial = recipe.MaterialA;
            rightMaterial = recipe.MaterialB;
        }
        else if (true == left.HasWeapon(recipe.MaterialB) && true == right.HasWeapon(recipe.MaterialA))
        {
            leftMaterial = recipe.MaterialB;
            rightMaterial = recipe.MaterialA;
        }
        else
        {
            Debug.LogWarning($"[DimensionFusionEffect] 양쪽 플레이어가 {recipe.name} 재료를 하나씩 가지고 있지 않아 합성하지 않습니다.", this);
            return;
        }

        isApplied = true;
        isFused = true;

        left.RemoveWeapon(leftMaterial);
        right.RemoveWeapon(rightMaterial);

        DimensionWeapon instance = Instantiate(recipe.ResultPrefab);
        instance.Initialize(recipe);

        left.AddDimensionWeapon(instance, recipe);
        right.AddDimensionWeapon(instance, recipe);

        RewardManager manager = WeaponUpgradeRewardEffect.ResolveManager(_context);
        if (null == manager)
        {
            return;
        }

        RefreshPools(manager, left, leftMaterial);
        RefreshPools(manager, right, rightMaterial);
    }

    /// <summary>
    /// _removed 무기의 남은 업그레이드 보상을 _player 전용 풀에서 빼고, 보유 상한이면 Weapon 풀을 막는다
    /// - 제거한 무기의 획득 보상은 WeaponRewardEffect 가 ExcludeFor 로 숨겨 두었으므로 그대로 숨겨진다
    /// </summary>
    private static void RefreshPools(RewardManager _manager, PlayerStats _player, Weapon _removed)
    {
        RandomPool<RewardData> pool = _manager.GetPoolFor(_player.Side, RewardPoolType.WeaponUpgrade);
        if (null != pool)
        {
            for (int i = pool.Count - 1; i >= 0; --i)
            {
                RewardData item = pool.GetItem(i);
                if (null != item && item.GetEffect() is WeaponUpgradeRewardEffect upgrade && _removed == upgrade.WeaponPrefab)
                {
                    pool.RemoveAt(i);
                }
            }
        }

        if (true == _player.IsWeaponFull)
        {
            _manager.SetPoolBlockedFor(_player.Side, RewardPoolType.Weapon, true);
        }
    }

    /// <summary>
    /// StringTable 키면 그대로, 원문이면 중괄호를 이스케이프 (RewardData 가 string.Format 으로 포멧팅하므로)
    /// </summary>
    private static string ToExplain(string _text)
    {
        if (true == StringTable.HasKey(_text))
        {
            return _text;
        }
        return _text.Replace("{", "{{").Replace("}", "}}");
    }
}
