/// <summary>
/// ObjectDescription 에 담기는 수치 종류 (임시값)
/// System.ValueType 과 이름이 같으므로, 그쪽이 필요하면 전체 이름으로 쓴다
/// </summary>
public enum ValueType
{
    Damage,
    HP,
    MoveSpeed,
    AttackSpeed,
    Range,
}

/// <summary>
/// ValueType 의 화면 표시용 이름 (StringTable 키 stat.*)
/// - ValueType 에 값을 추가하면 여기와 StringTable.csv 에도 키를 추가한다 (키가 없으면 enum 이름 그대로 표시)
/// </summary>
public static class ValueTypeExtensions
{
    public static string ToStringKey(this ValueType _type)
    {
        switch (_type)
        {
            case ValueType.Damage:
                return "stat.damage";
            case ValueType.HP:
                return "stat.hp";
            case ValueType.MoveSpeed:
                return "stat.move_speed";
            case ValueType.AttackSpeed:
                return "stat.attack_speed";
            case ValueType.Range:
                return "stat.range";
            default:
                return null;
        }
    }

    /// <summary>
    /// 현재 언어 표시 이름
    /// </summary>
    public static string ToDisplayName(this ValueType _type)
    {
        string key = _type.ToStringKey();
        return null != key && true == StringTable.TryGet(key, out string value) ? value : _type.ToString();
    }
}
