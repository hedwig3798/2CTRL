using System;

[AttributeUsage(AttributeTargets.Field)]
public class PipelineAttribute : Attribute
{
    public Type targetType;

    public PipelineAttribute(Type _type)
    {
        targetType = _type;
    }
}

public enum DAMAGE_PIPELINE
{
    INVINCIBLE = 0,     // 公利

    DODGE,          // 雀乔 魄沥

    FIXED,          // 绊沥 单固瘤 贸府

    DEFENSE,        // 规绢仿 贸府

    SHIELD,         // 角靛 贸府

    [Pipeline(typeof(HealthSystem))]
    HEALTH,         // 眉仿 贸府

    KNOCKBACK,      // 乘归 贸府

    PAYBACK,        // 单固瘤 其捞归 贸府

    END,
}
