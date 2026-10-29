using UnityEngine;

/// <summary>
/// 몬스터 한 종류의 기본 수치와 키워드 조합
/// 키워드가 비어 있으면 기본 추적 이동 + 접촉 데미지만 한다
/// </summary>
[CreateAssetMenu(fileName = "MonsterData", menuName = "2CTRL/Monster/MonsterData")]
public class MonsterData
    : ScriptableObject
{
    [Tooltip("기본 이동 속도. 실제 속도 = baseMoveSpeed × moveSpeedRate")]
    [Min(0f)]
    public float baseMoveSpeed = 1f;

    [Tooltip("공격 대상 레이어 (플레이어). 자폭/장판 데미지에 쓴다")]
    public LayerMask targetLayer;

    [Tooltip("행동 키워드. 이동 처리와 행동 검사는 이 순서대로 한다")]
    public MonsterKeyword[] keywords;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (null == keywords)
        {
            return;
        }

        int moveCount = 0;
        foreach (MonsterKeyword k in keywords)
        {
            if (null != k && MonsterKeywordCategory.Move == k.Category)
            {
                ++moveCount;
            }

            // 자식 몬스터에 키워드를 붙여 생긴 순환도 잡는다
            if (null != k && null != k.SpawnTarget)
            {
                MonsterSpawnGraph.WarnIfCycle(k, k.SpawnTarget);
            }
        }

        if (moveCount >= 2)
        {
            Debug.LogWarning($"[MonsterData] {name} : Move 키워드가 {moveCount} 개다. 리스트 앞쪽 키워드가 우선한다", this);
        }
    }
#endif
}
