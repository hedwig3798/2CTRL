using UnityEngine;

/// <summary>
/// 보상을 고른 쪽 플레이어의 무기에 업그레이드 한 단계를 적용한다
/// - 에셋으로 만들지 않고 CreateRewardData 가 단계마다 런타임에 RewardData 와 함께 만든다 (WeaponRewardEffect 가 1단계를 만든다)
/// - 적용하면 이 보상을 그 플레이어 전용 WeaponUpgrade 풀에서 빼고, 다음 단계가 있으면 다음 단계 보상을 넣는다
/// </summary>
public class WeaponUpgradeRewardEffect
    : RewardEffect
{
    [SerializeField]
    private WeaponUpgradeTable table;
    [SerializeField]
    private Weapon weaponPrefab;
    [Tooltip("보상 이름에 쓰는 무기 이름")]
    [SerializeField]
    private string weaponName;
    [SerializeField]
    private Sprite image;
    [Tooltip("적용할 단계 (1 부터)")]
    [Min(1)]
    [SerializeField]
    private int level = 1;

    public WeaponUpgradeTable Table => table;
    public Weapon WeaponPrefab => weaponPrefab;
    public int Level => level;

    /// <summary>
    /// _table 의 _level 단계 업그레이드 보상을 만든다 (단계가 없으면 null)
    /// - 이름 "<_weaponName> Lv.N", 이미지 _image, explain1 = 단계 이름, explain2 = 단계 설명
    /// </summary>
    public static RewardData CreateRewardData(WeaponUpgradeTable _table, Weapon _prefab, string _weaponName, Sprite _image, int _level)
    {
        if (null == _table || null == _prefab)
        {
            return null;
        }

        WeaponUpgradeStep step = _table.GetStep(_level);
        if (null == step)
        {
            return null;
        }

        string rewardName = $"{_weaponName} Lv.{_level}";

        WeaponUpgradeRewardEffect effect = CreateInstance<WeaponUpgradeRewardEffect>();
        effect.name = $"{rewardName} Effect";
        effect.table = _table;
        effect.weaponPrefab = _prefab;
        effect.weaponName = _weaponName;
        effect.image = _image;
        effect.level = _level;

        RewardData data = CreateInstance<RewardData>();
        data.name = rewardName;
        data.SetName(rewardName);
        if (null != _image)
        {
            data.SetImage(_image);
        }
        else
        {
            data.ClearImage();
        }
        data.ClearKey();
        data.ClearValue();
        data.SetExplain1(EscapeFormat(step.Title));
        data.SetExplain2(EscapeFormat(step.Description));
        data.SetEffect(effect);
        return data;
    }

    /// <summary>
    /// 보상 컨텍스트의 매니져 (없으면 씬의 RewardManager, 그것도 없으면 null)
    /// </summary>
    public static RewardManager ResolveManager(RewardContext _context)
    {
        if (null != _context && null != _context.Manager)
        {
            return _context.Manager;
        }
        return RewardManager.Instance;
    }

    public override void Apply(RewardContext _context, RewardData _data)
    {
        if (null == table || null == weaponPrefab)
        {
            Debug.LogWarning($"[WeaponUpgradeRewardEffect] table / weaponPrefab 이 비어 있습니다. ({name})", this);
            return;
        }

        if (null == _context.Player)
        {
            Debug.LogWarning($"[WeaponUpgradeRewardEffect] 적용할 플레이어가 없습니다. ({_data.name})", _data);
            return;
        }

        Weapon weapon = _context.Player.GetWeapon(weaponPrefab);
        if (null == weapon)
        {
            Debug.LogWarning($"[WeaponUpgradeRewardEffect] {_context.Player.name} 에게 {weaponPrefab.name} 무기가 없습니다. ({_data.name})", _data);
            RemoveFromPool(_context, _data);
            return;
        }

        WeaponUpgradeStep step = table.GetStep(level);
        if (null == step)
        {
            Debug.LogWarning($"[WeaponUpgradeRewardEffect] {table.name} 에 {level} 단계가 없습니다.", table);
            RemoveFromPool(_context, _data);
            return;
        }

        weapon.ApplyUpgrade(step);

        RewardManager manager = RemoveFromPool(_context, _data);
        if (null == manager)
        {
            return;
        }

        RewardData next = CreateRewardData(table, weaponPrefab, weaponName, image, level + 1);
        if (null != next)
        {
            manager.AddFor(_context.Side, RewardPoolType.WeaponUpgrade, next, table.RewardWeight);
        }
    }

    /// <summary>
    /// 이 보상을 _context 플레이어 전용 WeaponUpgrade 풀에서 뺀다 (쓸 수 없는 보상이 풀에 남지 않도록 실패 경로에서도 부른다)
    /// </summary>
    /// <returns>사용한 매니져 (없으면 null)</returns>
    private static RewardManager RemoveFromPool(RewardContext _context, RewardData _data)
    {
        RewardManager manager = ResolveManager(_context);
        if (null != manager)
        {
            manager.RemoveFor(_context.Side, RewardPoolType.WeaponUpgrade, _data);
        }
        return manager;
    }

    /// <summary>
    /// RewardData 의 explain 은 string.Format 으로 포멧팅되므로 중괄호를 이스케이프한다
    /// </summary>
    private static string EscapeFormat(string _text)
    {
        if (true == string.IsNullOrEmpty(_text))
        {
            return string.Empty;
        }
        return _text.Replace("{", "{{").Replace("}", "}}");
    }
}
