/// <summary>
/// 통계 기록 종류
/// </summary>
public enum STAT_TYPE
{
    Kill,
    DamageTaken,
    WeaponDamage,
    Exp,
}

/// <summary>
/// 통계 큐에 쌓이는 기록 한 건
/// - 이벤트 핸들러는 이 값을 만들어 큐에 넣기만 하고, 집계는 StatisticsManager 가 나중에 처리한다
/// </summary>
public struct StatRecord
{
    public STAT_TYPE type;
    public PlayerSide side;
    public float value;

    /// <summary>
    /// 무기 데미지 기록일 때의 무기 이름 (그 외는 null)
    /// </summary>
    public string weaponKey;

    public StatRecord(STAT_TYPE _type, PlayerSide _side, float _value, string _weaponKey = null)
    {
        type = _type;
        side = _side;
        value = _value;
        weaponKey = _weaponKey;
    }
}
