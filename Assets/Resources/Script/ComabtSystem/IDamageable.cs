using UnityEngine;

// 데미지 정보
public struct DamageInfo
{
    public float rawDamage;
}

// 데미지의 결과
public struct DamageResult
{
    public float actualDamage;
    public float relectDamage;
}

public interface IDamageable
{
    DamageResult Damaged(DamageInfo _damage);
}
