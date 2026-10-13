using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 리워드 풀 매니져 (씬에 하나 배치)
/// - RewardPoolType 의 값마다 RandomPool 이 하나씩 생긴다 (처음엔 비어 있음)
/// - 풀은 private 이지만 인스펙터에서 항목과 가중치를 채울 수 있다
/// - 항목은 RewardData 이며, Get / GetAny 로 가중치 랜덤으로 꺼낸다
/// - RewardPoolType 으로 풀에 접근해서 추가 / 삭제 / 가중치 조절을 한다
/// </summary>
[DisallowMultipleComponent]
public class RewardManager
    : MonoBehaviour
{
    [System.Serializable]
    private class PoolSlot
    {
        public RewardPoolType type;
        public RandomPool<RewardData> pool = new RandomPool<RewardData>();
    }

    private static RewardManager instance;

    public static RewardManager Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindFirstObjectByType<RewardManager>();
            }
            return instance;
        }
    }

    public static bool HasInstance => null != instance;

    [SerializeField]
    private List<PoolSlot> pools = new List<PoolSlot>();

    private Dictionary<RewardPoolType, RandomPool<RewardData>> poolMap;

    /// <summary>
    /// type 에 해당하는 풀을 가져온다 (값 조절용)
    /// </summary>
    public RandomPool<RewardData> GetPool(RewardPoolType _type)
    {
        if (null == poolMap)
        {
            EnsurePools();
        }

        poolMap.TryGetValue(_type, out RandomPool<RewardData> pool);
        return pool;
    }

    public void Add(RewardPoolType _type, RewardData _item, float _weight)
    {
        GetPool(_type)?.Add(_item, _weight);
    }

    public bool Remove(RewardPoolType _type, RewardData _item)
    {
        RandomPool<RewardData> pool = GetPool(_type);
        return null != pool && pool.Remove(_item);
    }

    public int RemoveAll(RewardPoolType _type, RewardData _item)
    {
        RandomPool<RewardData> pool = GetPool(_type);
        return null != pool ? pool.RemoveAll(_item) : 0;
    }

    public void SetWeight(RewardPoolType _type, int _index, float _weight)
    {
        GetPool(_type)?.SetWeight(_index, _weight);
    }

    public void Clear(RewardPoolType _type)
    {
        GetPool(_type)?.Clear();
    }

    public int Count(RewardPoolType _type)
    {
        RandomPool<RewardData> pool = GetPool(_type);
        return null != pool ? pool.Count : 0;
    }

    /// <summary>
    /// type 풀에서 가중치 랜덤으로 하나를 가져온다 (풀에서 빼지 않는다)
    /// 풀이 비었거나 뽑힌 항목이 비어 있으면 null
    /// </summary>
    public RewardData Get(RewardPoolType _type)
    {
        TryGet(_type, out RewardData item);
        return item;
    }

    /// <returns>풀이 비었거나 뽑힌 항목이 비어 있으면 false</returns>
    public bool TryGet(RewardPoolType _type, out RewardData _item)
    {
        _item = null;
        RandomPool<RewardData> pool = GetPool(_type);
        if (null == pool || false == pool.TryGet(out _item))
        {
            return false;
        }

        return null != _item;
    }

    /// <summary>
    /// 모든 풀의 총 가중치 합
    /// </summary>
    public float TotalWeight
    {
        get
        {
            if (null == poolMap)
            {
                EnsurePools();
            }

            float total = 0f;
            foreach (RandomPool<RewardData> pool in poolMap.Values)
            {
                total += pool.TotalWeight;
            }
            return total;
        }
    }

    /// <summary>
    /// 등록된 모든 풀에서 가중치 랜덤으로 하나를 가져온다 (풀에서 빼지 않는다)
    /// 모든 풀이 비었거나 뽑힌 항목이 비어 있으면 null
    /// </summary>
    public RewardData GetAny()
    {
        TryGetAny(out RewardData item, out _);
        return item;
    }

    /// <summary>
    /// 등록된 모든 풀에서 가중치 랜덤으로 하나를 가져온다 (풀에서 빼지 않는다)
    /// 1. 0 ~ 모든 풀의 가중치 합 사이의 랜덤 값을 뽑는다
    /// 2. enum 순서로 풀의 총 가중치를 빼 나가며 그 값이 속한 풀을 고른다
    /// 3. 고른 풀에서 남은 가중치 값에 해당하는 항목을 가져온다
    /// </summary>
    /// <param name="_type">뽑힌 항목이 속한 풀</param>
    /// <returns>모든 풀이 비었거나 뽑힌 항목이 비어 있으면 false</returns>
    public bool TryGetAny(out RewardData _item, out RewardPoolType _type)
    {
        _item = null;
        _type = default;

        float total = TotalWeight;
        if (total <= 0f)
        {
            return false;
        }

        float roll = Random.Range(0f, total);
        RandomPool<RewardData> picked = null;
        foreach (PoolSlot slot in pools)
        {
            float weight = slot.pool.TotalWeight;
            if (weight <= 0f)
            {
                continue;
            }

            // 부동소수 오차로 루프를 벗어나는 경우를 대비해 마지막 유효 풀을 기억한다
            picked = slot.pool;
            _type = slot.type;
            if (roll < weight)
            {
                break;
            }
            roll -= weight;
        }

        if (null == picked || false == picked.TryGetByWeight(roll, out _item))
        {
            return false;
        }

        return null != _item;
    }

    // GetRandomUnique 에서 재사용하는 후보 버퍼
    private readonly List<RewardData> candidates = new List<RewardData>();
    private readonly HashSet<RewardData> candidateSet = new HashSet<RewardData>();

    /// <summary>
    /// 등록된 모든 풀에서 가중치를 무시하고 균등 랜덤으로 중복 없이 최대 _count 개를 가져온다 (풀에서 빼지 않는다)
    /// - null 항목, weight 가 0 인 항목은 제외하고, 같은 데이터는 한 번만 뽑힌다
    /// - 후보가 _count 보다 적으면 후보 수만큼만 가져온다
    /// </summary>
    /// <param name="_result">결과를 담을 리스트 (먼저 비운다)</param>
    /// <returns>가져온 개수</returns>
    public int GetRandomUnique(int _count, List<RewardData> _result)
    {
        _result.Clear();
        if (_count <= 0)
        {
            return 0;
        }

        if (null == poolMap)
        {
            EnsurePools();
        }

        candidates.Clear();
        candidateSet.Clear();
        foreach (PoolSlot slot in pools)
        {
            RandomPool<RewardData> pool = slot.pool;
            for (int i = 0; i < pool.Count; ++i)
            {
                RewardData item = pool.GetItem(i);
                if (null == item || pool.GetWeight(i) <= 0f || false == candidateSet.Add(item))
                {
                    continue;
                }
                candidates.Add(item);
            }
        }

        // 부분 Fisher-Yates 셔플로 앞에서부터 count 개를 고른다
        int count = Mathf.Min(_count, candidates.Count);
        for (int i = 0; i < count; ++i)
        {
            int pick = Random.Range(i, candidates.Count);
            (candidates[i], candidates[pick]) = (candidates[pick], candidates[i]);
            _result.Add(candidates[i]);
        }

        Debug.Log($"[RewardManager] 보상 {count}/{_count} 개 뽑음 (후보 {candidates.Count}) : {string.Join(", ", _result.ConvertAll(_data => _data.name))}", this);

        candidates.Clear();
        candidateSet.Clear();
        return count;
    }

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogError("RewardManager already exists");
            return;
        }
        instance = this;

        EnsurePools();
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    private void Reset()
    {
        EnsurePools();
    }

    private void OnValidate()
    {
        EnsurePools();
    }

    /// <summary>
    /// enum 의 모든 값에 대해 풀이 하나씩 있도록 맞춘다
    /// 없는 풀은 빈 상태로 만들고, enum 에 없거나 중복된 풀은 제거하며, enum 순서로 정렬한다
    /// </summary>
    private void EnsurePools()
    {
        if (null == pools)
        {
            pools = new List<PoolSlot>();
        }

        Dictionary<RewardPoolType, PoolSlot> existing = new Dictionary<RewardPoolType, PoolSlot>();
        foreach (PoolSlot slot in pools)
        {
            if (null == slot || existing.ContainsKey(slot.type))
            {
                continue;
            }
            existing.Add(slot.type, slot);
        }

        RewardPoolType[] types = (RewardPoolType[])System.Enum.GetValues(typeof(RewardPoolType));
        pools.Clear();
        poolMap = new Dictionary<RewardPoolType, RandomPool<RewardData>>(types.Length);

        foreach (RewardPoolType type in types)
        {
            if (false == existing.TryGetValue(type, out PoolSlot slot))
            {
                slot = new PoolSlot { type = type };
            }
            if (null == slot.pool)
            {
                slot.pool = new RandomPool<RewardData>();
            }

            pools.Add(slot);
            poolMap[type] = slot.pool;
        }
    }
}
