using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보상 데이터를 담는 랜덤 선택 풀
/// - 게임 중 Add / Remove 로 자유롭게 추가, 삭제한다
/// - MarkPicked 로 선택 횟수를 기록하고 maxPickCount 에 도달하면 풀에서 뺀다
/// </summary>
public class RewardPool<T>
    where T : RewardData
{
    private readonly List<T> items = new List<T>();
    private readonly Dictionary<T, int> pickCounts = new Dictionary<T, int>();

    public int Count => items.Count;
    public IReadOnlyList<T> Items => items;

    public bool Add(T _item)
    {
        if (null == _item || true == items.Contains(_item))
        {
            return false;
        }

        items.Add(_item);
        return true;
    }

    public bool Remove(T _item)
    {
        if (null == _item)
        {
            return false;
        }

        return items.Remove(_item);
    }

    public bool Contains(T _item)
    {
        return null != _item && true == items.Contains(_item);
    }

    /// <summary>
    /// 풀과 선택 횟수를 모두 비운다
    /// </summary>
    public void Clear()
    {
        items.Clear();
        pickCounts.Clear();
    }

    public int GetPickCount(T _item)
    {
        if (null == _item)
        {
            return 0;
        }

        return true == pickCounts.TryGetValue(_item, out int count) ? count : 0;
    }

    /// <summary>
    /// 선택 횟수를 올리고, maxPickCount 에 도달하면 풀에서 제거한다
    /// </summary>
    /// <returns>풀에서 제거되었는지</returns>
    public bool MarkPicked(T _item)
    {
        if (null == _item)
        {
            return false;
        }

        int count = GetPickCount(_item) + 1;
        pickCounts[_item] = count;

        if (0 < _item.maxPickCount && count >= _item.maxPickCount)
        {
            return Remove(_item);
        }
        return false;
    }

    /// <summary>
    /// 가중치 랜덤으로 중복 없이 최대 _count 개를 _result 에 추가한다
    /// </summary>
    public void Pick(int _count, List<T> _result)
    {
        RewardRandom.PickWeighted(items, _count, _result);
    }
}

/// <summary>
/// RewardData 가중치 랜덤 선택
/// </summary>
public static class RewardRandom
{
    /// <summary>
    /// _candidates 에서 가중치 랜덤으로 중복 없이 최대 _count 개를 _result 에 추가한다
    /// weight 가 0 이하인 항목은 뽑히지 않는다
    /// </summary>
    public static void PickWeighted<T>(IReadOnlyList<T> _candidates, int _count, List<T> _result)
        where T : RewardData
    {
        if (null == _candidates || null == _result || _count <= 0)
        {
            return;
        }

        List<T> remain = new List<T>(_candidates.Count);
        float totalWeight = 0f;
        foreach (T c in _candidates)
        {
            if (null != c && 0f < c.weight)
            {
                remain.Add(c);
                totalWeight += c.weight;
            }
        }

        while (0 < _count && 0 < remain.Count)
        {
            float roll = Random.Range(0f, totalWeight);
            int index = remain.Count - 1;
            for (int i = 0; i < remain.Count; ++i)
            {
                roll -= remain[i].weight;
                if (roll < 0f)
                {
                    index = i;
                    break;
                }
            }

            T picked = remain[index];
            _result.Add(picked);
            totalWeight -= picked.weight;
            remain.RemoveAt(index);
            _count--;
        }
    }
}
