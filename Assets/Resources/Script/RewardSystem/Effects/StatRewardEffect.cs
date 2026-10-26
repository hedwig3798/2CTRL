using UnityEngine;

/// <summary>
/// 보상을 고른 쪽 플레이어의 스텟을 RewardData 의 key / value 만큼 바꾼다
/// - key = 바꿀 스텟, value = 변화량 (음수면 감소)
/// - mode 별로 에셋을 하나씩 만들어 모든 스텟 보상이 공유한다 (StatAddEffect / StatPercentEffect)
/// </summary>
[CreateAssetMenu(fileName = "StatRewardEffect", menuName = "Scriptable Objects/Reward Effect/Stat")]
public class StatRewardEffect
    : RewardEffect
{
    [Tooltip("Add: value 를 그대로 더함 / Multiply: value 를 비율로 더함 (0.1 = +10%)")]
    [SerializeField]
    private StatModifierMode mode = StatModifierMode.Add;

    public override void Apply(RewardContext _context, RewardData _data)
    {
        // key / value 가 비어 있는 보상은 무시
        if (false == _data.TryGetKey(out ValueType key) || false == _data.TryGetValue(out float value))
        {
            return;
        }

        if (null == _context.Player)
        {
            Debug.LogWarning($"[StatRewardEffect] 적용할 플레이어가 없습니다. ({_data.name})", _data);
            return;
        }

        _context.Player.AddModifier(key, mode, value);
    }
}
