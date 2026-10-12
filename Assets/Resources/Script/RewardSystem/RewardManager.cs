using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 리워드 풀 매니져 (씬에 하나 배치)
/// - RewardPoolType 의 값마다 RandomPool 이 하나씩 생긴다 (처음엔 비어 있음)
/// - 풀은 private 이지만 인스펙터에서 항목과 가중치를 채울 수 있다
/// - 항목은 어떤 에셋이든 넣을 수 있고, 꺼낼 때 Get&lt;T&gt; 로 원하는 타입으로 받는다
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
        public RandomPool<Object> pool = new RandomPool<Object>();
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

    private Dictionary<RewardPoolType, RandomPool<Object>> poolMap;

    /// <summary>
    /// type 에 해당하는 풀을 가져온다 (값 조절용)
    /// </summary>
    public RandomPool<Object> GetPool(RewardPoolType _type)
    {
        if (null == poolMap)
        {
            EnsurePools();
        }

        poolMap.TryGetValue(_type, out RandomPool<Object> pool);
        return pool;
    }

    public void Add(RewardPoolType _type, Object _item, float _weight)
    {
        GetPool(_type)?.Add(_item, _weight);
    }

    public bool Remove(RewardPoolType _type, Object _item)
    {
        RandomPool<Object> pool = GetPool(_type);
        return null != pool && pool.Remove(_item);
    }

    public int RemoveAll(RewardPoolType _type, Object _item)
    {
        RandomPool<Object> pool = GetPool(_type);
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
        RandomPool<Object> pool = GetPool(_type);
        return null != pool ? pool.Count : 0;
    }

    /// <summary>
    /// type 풀에서 가중치 랜덤으로 하나를 T 로 가져온다 (풀에서 빼지 않는다)
    /// 풀이 비었거나 뽑힌 항목이 T 가 아니면 null
    /// </summary>
    public T Get<T>(RewardPoolType _type) where T : Object
    {
        TryGet(_type, out T item);
        return item;
    }

    /// <returns>풀이 비었거나 뽑힌 항목이 T 가 아니면 false</returns>
    public bool TryGet<T>(RewardPoolType _type, out T _item) where T : Object
    {
        _item = null;
        RandomPool<Object> pool = GetPool(_type);
        if (null == pool || false == pool.TryGet(out Object picked))
        {
            return false;
        }

        _item = picked as T;
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
            foreach (RandomPool<Object> pool in poolMap.Values)
            {
                total += pool.TotalWeight;
            }
            return total;
        }
    }

    /// <summary>
    /// 등록된 모든 풀에서 가중치 랜덤으로 하나를 T 로 가져온다 (풀에서 빼지 않는다)
    /// 모든 풀이 비었거나 뽑힌 항목이 T 가 아니면 null
    /// </summary>
    public T GetAny<T>() where T : Object
    {
        TryGetAny(out T item, out _);
        return item;
    }

    /// <summary>
    /// 등록된 모든 풀에서 가중치 랜덤으로 하나를 T 로 가져온다 (풀에서 빼지 않는다)
    /// 1. 0 ~ 모든 풀의 가중치 합 사이의 랜덤 값을 뽑는다
    /// 2. enum 순서로 풀의 총 가중치를 빼 나가며 그 값이 속한 풀을 고른다
    /// 3. 고른 풀에서 남은 가중치 값에 해당하는 항목을 가져온다
    /// </summary>
    /// <param name="_type">뽑힌 항목이 속한 풀</param>
    /// <returns>모든 풀이 비었거나 뽑힌 항목이 T 가 아니면 false</returns>
    public bool TryGetAny<T>(out T _item, out RewardPoolType _type) where T : Object
    {
        _item = null;
        _type = default;

        float total = TotalWeight;
        if (total <= 0f)
        {
            return false;
        }

        float roll = Random.Range(0f, total);
        RandomPool<Object> picked = null;
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

        if (null == picked || false == picked.TryGetByWeight(roll, out Object item))
        {
            return false;
        }

        _item = item as T;
        return null != _item;
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
        poolMap = new Dictionary<RewardPoolType, RandomPool<Object>>(types.Length);

        foreach (RewardPoolType type in types)
        {
            if (false == existing.TryGetValue(type, out PoolSlot slot))
            {
                slot = new PoolSlot { type = type };
            }
            if (null == slot.pool)
            {
                slot.pool = new RandomPool<Object>();
            }

            pools.Add(slot);
            poolMap[type] = slot.pool;
        }
    }
}
