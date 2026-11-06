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
    // 아래 두 값은 무기 전용 (플레이어 자체 base 는 0, 태그가 있는 무기의 지속시간 / 발사체 수에 더해진다)
    // 보상 에셋에 정수로 직렬화되므로 순서를 바꾸지 말고 끝에만 추가한다
    Duration,
    ProjectileCount,
    // 플레이어 초당 체력 회복량 (base 0, HealthSystem 이 사용. 0 미만은 0)
    HPRegen,
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
            case ValueType.Duration:
                return "stat.duration";
            case ValueType.ProjectileCount:
                return "stat.projectile_count";
            case ValueType.HPRegen:
                return "stat.hp_regen";
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
