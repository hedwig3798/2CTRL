using UnityEngine;

/// <summary>
/// 몬스터 개체수 / HP / 데미지 / 속도 비율을 RewardData 의 value 만큼 바꾼다
/// - 보상을 받는 진영(RewardContext.Side) 의 플레이어를 노리는 스포너에 적용된다 (RewardManager.GetSpawnerFor)
///   버프로 받으면 내 쪽 몬스터, 디버프로 받으면(반대쪽에 적용) 상대 쪽 몬스터가 바뀐다
/// - type 과 extraTypes 에 같은 value 를 적용한다 (예: 스폰율과 체력을 함께 증감)
/// - key 는 쓰지 않으므로 RewardData 의 hasKey 는 꺼 두고, 설명은 explain 에 직접 쓴다
/// </summary>
[CreateAssetMenu(fileName = "SpawnerRewardEffect", menuName = "Scriptable Objects/Reward Effect/Spawner")]
public class SpawnerRewardEffect
    : RewardEffect
{
    [Tooltip("바꿀 몬스터 비율")]
    [SerializeField]
    private SpawnModifierType type = SpawnModifierType.Count;

    [Tooltip("같은 value 로 함께 바꿀 비율 (type 과 중복이면 무시)")]
    [SerializeField]
    private SpawnModifierType[] extraTypes = new SpawnModifierType[0];

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

        if (null == extraTypes)
        {
            return;
        }

        for (int i = 0; i < extraTypes.Length; ++i)
        {
            SpawnModifierType extra = extraTypes[i];
            if (extra == type || System.Array.IndexOf(extraTypes, extra) < i)
            {
                continue;
            }
            _context.Spawner.AddRate(extra, mode, value);
        }
    }
}
