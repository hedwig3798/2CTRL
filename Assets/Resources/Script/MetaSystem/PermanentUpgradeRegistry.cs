using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영구 강화 목록 (타이틀 강화 패널의 표시 순서, PermanentUpgradeApplier 가 적용할 목록)
/// </summary>
[CreateAssetMenu(fileName = "PermanentUpgradeRegistry", menuName = "2CTRL/Meta/PermanentUpgradeRegistry")]
public class PermanentUpgradeRegistry
    : ScriptableObject
{
    [Tooltip("패널에 이 순서로 표시한다 (id 중복 금지)")]
    [SerializeField]
    private PermanentUpgradeData[] upgrades = new PermanentUpgradeData[0];

    public IReadOnlyList<PermanentUpgradeData> Upgrades => null != upgrades ? upgrades : System.Array.Empty<PermanentUpgradeData>();

    /// <summary>
    /// id 가 같은 강화 (없으면 null)
    /// </summary>
    public PermanentUpgradeData Find(string _id)
    {
        if (null == upgrades || true == string.IsNullOrEmpty(_id))
        {
            return null;
        }

        foreach (PermanentUpgradeData data in upgrades)
        {
            if (null != data && data.Id == _id)
            {
                return data;
            }
        }
        return null;
    }

    /// <summary>
    /// 목록을 바꾼다 (에디터 빌더·테스트용)
    /// </summary>
    public void SetUpgrades(PermanentUpgradeData[] _upgrades)
    {
        upgrades = null != _upgrades ? _upgrades : new PermanentUpgradeData[0];
    }
}
