using UnityEngine;

/// <summary>
/// 분열: 최종 사망 시 주변에 자식 몬스터를 스폰한다
/// 자식 프리팹의 MonsterData 에는 분열 키워드를 넣지 않는다 (무한 분열 방지)
/// </summary>
[CreateAssetMenu(fileName = "SplitKeyword", menuName = "2CTRL/Monster/Keyword/Split")]
public class SplitKeyword
    : MonsterKeyword
{
    public Spawnable childPrefab;

    [Min(1)]
    public int count = 2;

    [Tooltip("사망 위치에서 흩어질 반경")]
    [Min(0f)]
    public float scatterRadius = 0.6f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Death;

    public override KeywordRuntime CreateRuntime()
    {
        return new SplitRuntime(this);
    }

    public override Spawnable SpawnTarget => childPrefab;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (null == childPrefab)
        {
            return;
        }

        // 프리팹 그래프 순환 (자기 자신, A → B → A)
        MonsterSpawnGraph.WarnIfCycle(this, childPrefab);
    }
#endif
}

public class SplitRuntime
    : KeywordRuntime<SplitKeyword>
{
    public SplitRuntime(SplitKeyword _data)
        : base(_data)
    {
    }

    public override void OnDeath(MonsterContext _ctx)
    {
        if (null == data.childPrefab || null == _ctx.spawner)
        {
            return;
        }

        Vector2 center = _ctx.Position;
        for (int i = 0; i < data.count; ++i)
        {
            Vector2 pos = center + Random.insideUnitCircle * data.scatterRadius;
            _ctx.spawner.SpawnAt(data.childPrefab, pos);
        }
    }
}
