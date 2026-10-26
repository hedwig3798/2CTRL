using UnityEngine;

/// <summary>
/// 레벨업 보상을 골랐을 때 실제로 실행되는 효과의 베이스
/// - RewardData 는 "무엇을 보여줄지", RewardEffect 는 "무엇을 할지" 를 담당한다
/// - 효과는 RewardData 의 key / value 를 읽어서 쓰므로, 같은 효과 에셋 하나를 여러 RewardData 가 공유할 수 있다
///   (예: StatAddEffect 하나로 HP +1, 데미지 +1, 이동 속도 -1 ... 을 모두 처리)
///
/// 새 보상 종류 추가 방법
///   1. RewardEffect 를 상속해 Apply 를 구현하고 CreateAssetMenu 를 붙인다
///   2. 효과 에셋을 만들고 RewardData 의 effect 에 연결한다
///   3. RewardData 를 RewardManager 의 풀에 넣는다
///   - UI / LevelUpUI / RewardManager 는 고치지 않는다
///   - 새로운 적용 대상이 필요하면 RewardContext 에 필드를 추가한다
/// </summary>
public abstract class RewardEffect
    : ScriptableObject
{
    /// <param name="_context">적용 대상 (보상을 고른 쪽 플레이어, Spawner 등)</param>
    /// <param name="_data">효과를 가진 보상 데이터 (key / value 를 읽는다)</param>
    public abstract void Apply(RewardContext _context, RewardData _data);
}
