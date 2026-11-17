using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 분광 레이저 한 갈래 (발사 지점 → 맞힌 대상)
/// </summary>
public struct PrismLink<T>
{
    /// <summary>
    /// 부모 갈래 번호 (1차 레이저면 -1)
    /// </summary>
    public int parent;

    /// <summary>
    /// 단계 (1차 레이저 0, 분기 1 ~ stages)
    /// </summary>
    public int stage;

    public Vector2 from;
    public Vector2 to;
    public T target;
}

/// <summary>
/// 분광 레이저의 대상 탐색 (화면 공간)
/// - TryFindNearest 는 이미 맞은 대상을 제외해야 한다
/// - OnBeam 은 갈래가 정해질 때마다 바로 불린다. 여기서 맞은 대상을 제외 목록에 넣는다 (다음 탐색에 반영)
/// </summary>
public interface IPrismTargetSource<T>
{
    bool TryFindNearest(Vector2 _from, float _maxRange, out T _target, out Vector2 _pos);

    void OnBeam(in PrismLink<T> _link);
}

/// <summary>
/// 분광 레이저 분기 계획 (순수 로직)
/// 1. 각 발사 지점에서 가장 가까운 대상에게 1차 레이저 (단계 0)
/// 2. 직전 단계에서 맞은 대상마다 그 대상 기준 가장 가까운 대상 _fanOut 명에게 분기 (단계 1 ~ _stages, 너비 우선)
/// 3. 이미 맞은 대상은 제외 (source 책임), 갈래 수는 _maxBeams 를 넘지 않는다
/// </summary>
public static class PrismBranchPlanner
{
    public static int Plan<T>(IPrismTargetSource<T> _source, Vector2[] _origins, int _originCount,
        float _primaryRange, float _branchRange, int _fanOut, int _stages, int _maxBeams, List<PrismLink<T>> _links)
    {
        _links.Clear();
        if (null == _source || null == _origins || _maxBeams <= 0)
        {
            return 0;
        }

        int originCount = Mathf.Min(_originCount, _origins.Length);
        for (int i = 0; i < originCount; ++i)
        {
            if (_links.Count >= _maxBeams)
            {
                return _links.Count;
            }
            if (false == _source.TryFindNearest(_origins[i], _primaryRange, out T target, out Vector2 pos))
            {
                continue;
            }
            AddLink(_source, _links, -1, 0, _origins[i], pos, target);
        }

        int stageStart = 0;
        for (int stage = 1; stage <= _stages; ++stage)
        {
            int stageEnd = _links.Count;
            for (int parent = stageStart; parent < stageEnd; ++parent)
            {
                Vector2 from = _links[parent].to;
                for (int k = 0; k < _fanOut; ++k)
                {
                    if (_links.Count >= _maxBeams)
                    {
                        return _links.Count;
                    }
                    if (false == _source.TryFindNearest(from, _branchRange, out T target, out Vector2 pos))
                    {
                        break;
                    }
                    AddLink(_source, _links, parent, stage, from, pos, target);
                }
            }
            if (stageEnd == _links.Count)
            {
                break;
            }
            stageStart = stageEnd;
        }
        return _links.Count;
    }

    /// <summary>
    /// 1차 레이저 수 × (fanOut + fanOut² + ... + fanOut^stages) 의 최대 분기 갈래 수 (대상이 충분할 때)
    /// </summary>
    public static int MaxBranchCount(int _primaryCount, int _fanOut, int _stages)
    {
        int total = 0;
        int layer = Mathf.Max(0, _primaryCount);
        for (int s = 0; s < _stages; ++s)
        {
            layer *= Mathf.Max(0, _fanOut);
            total += layer;
        }
        return total;
    }

    private static void AddLink<T>(IPrismTargetSource<T> _source, List<PrismLink<T>> _links, int _parent, int _stage, Vector2 _from, Vector2 _to, T _target)
    {
        PrismLink<T> link = new PrismLink<T>
        {
            parent = _parent,
            stage = _stage,
            from = _from,
            to = _to,
            target = _target,
        };
        _links.Add(link);
        _source.OnBeam(link);
    }
}
