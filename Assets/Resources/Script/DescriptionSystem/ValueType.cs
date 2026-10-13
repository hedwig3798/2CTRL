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
/// ValueType 의 화면 표시용 이름 (한글)
/// - ValueType 에 값을 추가하면 여기에도 이름을 추가한다 (없으면 enum 이름 그대로 표시)
/// </summary>
public static class ValueTypeExtensions
{
    public static string ToDisplayName(this ValueType _type)
    {
        switch (_type)
        {
            case ValueType.Damage:
                return "데미지";
            case ValueType.HP:
                return "체력";
            case ValueType.MoveSpeed:
                return "이동 속도";
            case ValueType.AttackSpeed:
                return "공격 속도";
            case ValueType.Range:
                return "범위";
            default:
                return _type.ToString();
        }
    }
}
