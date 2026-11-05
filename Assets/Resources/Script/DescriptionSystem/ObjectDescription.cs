using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오브젝트 설명 데이터 (이름, 스프라이트, 설명, 수치)
/// - 같은 종류의 오브젝트끼리 에셋 하나를 공유한다
/// - 다른 기능 없이 데이터만 담는다
/// - objectName / description 은 StringTable 키 (키가 없으면 적힌 문자열을 그대로 표시)
/// </summary>
[CreateAssetMenu(fileName = "ObjectDescription", menuName = "Scriptable Objects/ObjectDescription")]
public class ObjectDescription
    : ScriptableObject
{
    [System.Serializable]
    public struct StatEntry
    {
        public ValueType type;
        public float value;
    }

    [Tooltip("이름 StringTable 키 (예: weapon.chain_lightning.name)")]
    [SerializeField]
    private string objectName;

    [SerializeField]
    private Sprite sprite;

    [Tooltip("설명 StringTable 키")]
    [TextArea]
    [SerializeField]
    private string description;

    [SerializeField]
    private List<StatEntry> stats = new List<StatEntry>();

    #region Getter
    /// <summary>
    /// 현재 언어 이름
    /// </summary>
    public string GetName()
    {
        return StringTable.Resolve(objectName);
    }

    /// <summary>
    /// 번역 전 이름 키 (통계 집계 등 언어와 무관한 식별용)
    /// </summary>
    public string GetNameKey()
    {
        return null != objectName ? objectName : string.Empty;
    }

    public Sprite GetSprite()
    {
        return sprite;
    }

    /// <summary>
    /// 현재 언어 설명
    /// </summary>
    public string GetDescription()
    {
        return StringTable.Resolve(description);
    }

    /// <summary>
    /// 해당 수치를 가져온다 (없으면 0)
    /// </summary>
    public float GetStat(ValueType _type)
    {
        TryGetStat(_type, out float value);
        return value;
    }

    /// <returns>해당 수치가 없으면 false</returns>
    public bool TryGetStat(ValueType _type, out float _value)
    {
        _value = 0.0f;
        if (null == stats)
        {
            return false;
        }

        foreach (StatEntry entry in stats)
        {
            if (entry.type == _type)
            {
                _value = entry.value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 전체 수치 목록 (UI 나열용)
    /// </summary>
    public IReadOnlyList<StatEntry> GetStats()
    {
        return stats;
    }
    #endregion
}
