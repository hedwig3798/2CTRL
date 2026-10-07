using System;

/// <summary>
/// 무기 base 스탯 (Weapon 의 weapon spec, chain spec 필드와 1:1)
/// </summary>
public enum WEAPON_STAT
{
    FireCount,
    CoolTime,
    Range,
    DamageRate,
    SpeedRate,
    HitCount,
    ChainCount,
    ChainRange,
}

/// <summary>
/// 캐릭터 base 스탯
/// </summary>
public enum CHARACTER_STAT
{
    MaxHP,
    MoveSpeed,
    PickupRange,
    DamageRate,
    CoolTimeRate,
}

/// <summary>
/// 스탯 변경 방식
/// </summary>
public enum MODIFIER_MODE
{
    /// <summary>
    /// 기존 값에 value 를 더한다
    /// </summary>
    Add,
    /// <summary>
    /// 기존 값에 value 를 곱한다
    /// </summary>
    Multiply,
}

[Serializable]
public struct WeaponStatModifier
{
    public WEAPON_STAT stat;
    public MODIFIER_MODE mode;
    public float value;
}

[Serializable]
public struct CharacterStatModifier
{
    public CHARACTER_STAT stat;
    public MODIFIER_MODE mode;
    public float value;
}
