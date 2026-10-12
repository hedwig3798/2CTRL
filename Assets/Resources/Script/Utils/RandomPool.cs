using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가중치 랜덤 선택 풀
/// - 어떤 타입이든 담을 수 있다
/// - 같은 데이터를 중복으로 넣을 수 있고, 항목마다 가중치를 따로 가진다
/// - Add / Remove 로 자유롭게 추가, 삭제한다
/// - Get 으로 가중치 랜덤 요소를 가져온다 (풀에서 빼지 않는다)
/// - 직렬화 가능하므로 인스펙터에서 항목과 가중치를 채울 수 있다
/// </summary>
[System.Serializable]
public class RandomPool<T>
    : ISerializationCallbackReceiver
{
    [System.Serializable]
    private struct Entry
    {
        [SerializeField]
        public T item;
        [SerializeField]
        public float weight;
    }

    private static readonly EqualityComparer<T> comparer = EqualityComparer<T>.Default;

    [SerializeField]
    private List<Entry> entries = new List<Entry>();
    private float totalWeight;

    public int Count => entries.Count;
    public float TotalWeight => totalWeight;

    /// <summary>
    /// 항목을 추가한다. 중복을 허용하고, 음수 가중치는 0 으로 보정한다
    /// </summary>
    public void Add(T _item, float _weight)
    {
        float weight = Mathf.Max(0f, _weight);
        entries.Add(new Entry { item = _item, weight = weight });
        totalWeight += weight;
    }

    /// <summary>
    /// 일치하는 첫 항목 하나를 제거한다
    /// </summary>
    public bool Remove(T _item)
    {
        int index = IndexOf(_item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// 일치하는 항목을 모두 제거한다
    /// </summary>
    /// <returns>제거한 항목 수</returns>
    public int RemoveAll(T _item)
    {
        int removed = 0;
        for (int i = entries.Count - 1; i >= 0; --i)
        {
            if (true == comparer.Equals(entries[i].item, _item))
            {
                RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    public void RemoveAt(int _index)
    {
        totalWeight -= entries[_index].weight;
        entries.RemoveAt(_index);

        if (0 == entries.Count)
        {
            totalWeight = 0f;
        }
    }

    public bool Contains(T _item)
    {
        return 0 <= IndexOf(_item);
    }

    /// <summary>
    /// 일치하는 항목(중복 포함)의 개수
    /// </summary>
    public int CountOf(T _item)
    {
        int count = 0;
        foreach (Entry entry in entries)
        {
            if (true == comparer.Equals(entry.item, _item))
            {
                count++;
            }
        }
        return count;
    }

    public int IndexOf(T _item)
    {
        for (int i = 0; i < entries.Count; ++i)
        {
            if (true == comparer.Equals(entries[i].item, _item))
            {
                return i;
            }
        }
        return -1;
    }

    public T GetItem(int _index)
    {
        return entries[_index].item;
    }

    public float GetWeight(int _index)
    {
        return entries[_index].weight;
    }

    /// <summary>
    /// 항목의 가중치를 바꾼다. 음수 가중치는 0 으로 보정한다
    /// </summary>
    public void SetWeight(int _index, float _weight)
    {
        Entry entry = entries[_index];
        float weight = Mathf.Max(0f, _weight);
        totalWeight += weight - entry.weight;
        entry.weight = weight;
        entries[_index] = entry;
    }

    public void Clear()
    {
        entries.Clear();
        totalWeight = 0f;
    }

    /// <summary>
    /// 가중치 랜덤으로 요소 하나를 가져온다 (풀에서 빼지 않는다)
    /// 풀이 비었거나 총 가중치가 0 이면 default 를 반환한다
    /// </summary>
    public T Get()
    {
        TryGet(out T item);
        return item;
    }

    /// <summary>
    /// 가중치 랜덤으로 요소 하나를 가져온다 (풀에서 빼지 않는다)
    /// weight 가 0 인 항목은 뽑히지 않는다
    /// </summary>
    /// <returns>풀이 비었거나 총 가중치가 0 이면 false</returns>
    public bool TryGet(out T _item)
    {
        if (totalWeight <= 0f)
        {
            _item = default;
            return false;
        }

        return TryGetByWeight(Random.Range(0f, totalWeight), out _item);
    }

    /// <summary>
    /// 주어진 가중치 값(0 ~ TotalWeight)에 해당하는 요소를 가져온다 (풀에서 빼지 않는다)
    /// 앞에서부터 항목의 가중치를 빼 나가다가 값이 0 미만이 되는 항목을 고른다
    /// weight 가 0 인 항목은 뽑히지 않고, 범위를 벗어난 값은 0 ~ TotalWeight 로 보정한다
    /// </summary>
    public T GetByWeight(float _weight)
    {
        TryGetByWeight(_weight, out T item);
        return item;
    }

    /// <returns>풀이 비었거나 총 가중치가 0 이면 false</returns>
    public bool TryGetByWeight(float _weight, out T _item)
    {
        _item = default;
        if (0 == entries.Count || totalWeight <= 0f)
        {
            return false;
        }

        float roll = Mathf.Clamp(_weight, 0f, totalWeight);
        int lastValid = -1;
        for (int i = 0; i < entries.Count; ++i)
        {
            float weight = entries[i].weight;
            if (weight <= 0f)
            {
                continue;
            }

            lastValid = i;
            roll -= weight;
            if (roll < 0f)
            {
                _item = entries[i].item;
                return true;
            }
        }

        // 부동소수 오차로 루프를 벗어난 경우 마지막 유효 항목으로 처리
        if (0 <= lastValid)
        {
            _item = entries[lastValid].item;
            return true;
        }
        return false;
    }

    public void OnBeforeSerialize()
    {
    }

    /// <summary>
    /// 인스펙터에서 채운 값으로 총 가중치를 다시 계산한다 (음수 가중치는 0 으로 보정)
    /// </summary>
    public void OnAfterDeserialize()
    {
        if (null == entries)
        {
            entries = new List<Entry>();
        }

        totalWeight = 0f;
        for (int i = 0; i < entries.Count; ++i)
        {
            Entry entry = entries[i];
            if (entry.weight < 0f)
            {
                entry.weight = 0f;
                entries[i] = entry;
            }
            totalWeight += entry.weight;
        }
    }
}
