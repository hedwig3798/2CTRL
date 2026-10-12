using UnityEngine;

/// <summary>
/// 오브젝트 최상단에 붙여 ObjectDescription 을 연결하는 컴포넌트
/// - 다른 기능 없이 데이터 참조만 가진다
/// </summary>
[DisallowMultipleComponent]
public class DescriptionHolder
    : MonoBehaviour
{
    [SerializeField]
    private ObjectDescription description;

    #region Getter
    public ObjectDescription GetDescription()
    {
        return description;
    }

    public string GetName()
    {
        return null != description ? description.GetName() : string.Empty;
    }

    public Sprite GetSprite()
    {
        return null != description ? description.GetSprite() : null;
    }

    public string GetDescriptionText()
    {
        return null != description ? description.GetDescription() : string.Empty;
    }

    /// <summary>
    /// 해당 수치를 가져온다 (없으면 0)
    /// </summary>
    public float GetStat(ValueType _type)
    {
        return null != description ? description.GetStat(_type) : 0.0f;
    }

    /// <returns>description 이 없거나 해당 수치가 없으면 false</returns>
    public bool TryGetStat(ValueType _type, out float _value)
    {
        _value = 0.0f;
        return null != description && description.TryGetStat(_type, out _value);
    }
    #endregion
}
