using System;

/// <summary>
/// 무기 태그: 이 무기가 플레이어의 어떤 수치 버프 / 디버프에 영향을 받는지
/// - 태그가 있는 수치만 (무기 값 + 플레이어 ΣAdd) × 플레이어 배율 로 계산하고, 없으면 무기 값 그대로 쓴다
/// - 무기에 쓰이는 ValueType (Damage / AttackSpeed / Range) 과 1:1 대응한다
///   ValueType 에 무기 수치를 추가하면 여기에도 같은 이름의 비트를 추가하고 WeaponStatTagUtils.FromValueType 에 연결한다
/// </summary>
[Flags]
public enum WeaponStatTag
{
    None = 0,
    Damage = 1 << 0,
    AttackSpeed = 1 << 1,
    Range = 1 << 2,
    All = Damage | AttackSpeed | Range,
}

public static class WeaponStatTagUtils
{
    /// <summary>
    /// ValueType 에 대응하는 태그 (무기 수치가 아니면 None)
    /// </summary>
    public static WeaponStatTag FromValueType(ValueType _type)
    {
        switch (_type)
        {
            case ValueType.Damage:
                return WeaponStatTag.Damage;
            case ValueType.AttackSpeed:
                return WeaponStatTag.AttackSpeed;
            case ValueType.Range:
                return WeaponStatTag.Range;
            default:
                return WeaponStatTag.None;
        }
    }

    /// <summary>
    /// _tags 가 _type 수치의 태그를 가지고 있는지 (무기 수치가 아니면 false)
    /// </summary>
    public static bool Has(this WeaponStatTag _tags, ValueType _type)
    {
        WeaponStatTag tag = FromValueType(_type);
        return WeaponStatTag.None != tag && tag == (_tags & tag);
    }
}
