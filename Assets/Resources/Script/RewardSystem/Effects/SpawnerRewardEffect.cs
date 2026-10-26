using UnityEngine;

/// <summary>
/// 몬스터 개체수 / HP / 데미지 / 속도 비율을 RewardData 의 value 만큼 바꾼다
/// - 플레이어와 무관하게 Spawner(전역) 에 적용된다
/// - key 는 쓰지 않으므로 RewardData 의 hasKey 는 꺼 두고, 설명은 explain 에 직접 쓴다
/// </summary>
[CreateAssetMenu(fileName = "SpawnerRewardEffect", menuName = "Scriptable Objects/Reward Effect/Spawner")]
public class SpawnerRewardEffect
    : RewardEffect
{
    [Tooltip("바꿀 몬스터 비율")]
    [SerializeField]
    private SpawnModifierType type = SpawnModifierType.Count;

    [Tooltip("Add: value 를 그대로 더함 (비율 1 기준 +0.1 = +10%) / Multiply: value 를 비율로 더함")]
    [SerializeField]
    private StatModifierMode mode = StatModifierMode.Add;

    public override void Apply(RewardContext _context, RewardData _data)
    {
        // value 가 비어 있는 보상은 무시
        if (false == _data.TryGetValue(out float value))
        {
            return;
        }

        if (null == _context.Spawner)
        {
            Debug.LogWarning($"[SpawnerRewardEffect] Spawner 가 없습니다. ({_data.name})", _data);
            return;
        }

        _context.Spawner.AddRate(type, mode, value);
    }
}
