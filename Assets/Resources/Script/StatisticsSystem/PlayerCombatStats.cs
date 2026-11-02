using System.Collections.Generic;

/// <summary>
/// 플레이어 한 명의 전투 통계 (킬 수, 받은 데미지, 무기별 데미지, 획득 EXP)
/// - StatisticsManager 가 큐를 처리하며 Apply 로 누적한다
/// </summary>
public class PlayerCombatStats
{
    private int kills;
    private float damageTaken;
    private float exp;
    private readonly Dictionary<string, float> weaponDamage = new Dictionary<string, float>();

    #region Getter
    public int Kills => kills;

    public float DamageTaken => damageTaken;

    public float Exp => exp;

    /// <summary>
    /// 무기 이름별 누적 데미지 (읽기 전용)
    /// </summary>
    public IReadOnlyDictionary<string, float> WeaponDamage => weaponDamage;

    /// <summary>
    /// 모든 무기 데미지 합
    /// </summary>
    public float TotalWeaponDamage
    {
        get
        {
            float total = 0f;
            foreach (KeyValuePair<string, float> pair in weaponDamage)
            {
                total += pair.Value;
            }
            return total;
        }
    }

    /// <summary>
    /// 무기별 데미지를 큰 순서로 _result 에 채운다 (_result 는 먼저 비운다)
    /// </summary>
    public void GetSortedWeaponDamage(List<KeyValuePair<string, float>> _result)
    {
        if (null == _result)
        {
            return;
        }

        _result.Clear();
        foreach (KeyValuePair<string, float> pair in weaponDamage)
        {
            _result.Add(pair);
        }
        _result.Sort((a, b) => b.Value.CompareTo(a.Value));
    }
    #endregion

    /// <summary>
    /// 기록 한 건을 누적한다 (side 는 호출하는 쪽이 맞춰서 넘긴다)
    /// </summary>
    public void Apply(StatRecord _record)
    {
        switch (_record.type)
        {
            case STAT_TYPE.Kill:
                // 킬 기록은 한 건이 1 킬
                kills++;
                break;
            case STAT_TYPE.DamageTaken:
                damageTaken += _record.value;
                break;
            case STAT_TYPE.WeaponDamage:
                if (true == string.IsNullOrEmpty(_record.weaponKey))
                {
                    break;
                }
                weaponDamage.TryGetValue(_record.weaponKey, out float prev);
                weaponDamage[_record.weaponKey] = prev + _record.value;
                break;
            case STAT_TYPE.Exp:
                exp += _record.value;
                break;
        }
    }

    public void Reset()
    {
        kills = 0;
        damageTaken = 0f;
        exp = 0f;
        weaponDamage.Clear();
    }
}
