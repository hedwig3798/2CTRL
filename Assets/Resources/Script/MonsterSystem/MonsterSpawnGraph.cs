using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 분열/소환 키워드가 만드는 프리팹 스폰 그래프 검사 (에디터 OnValidate 용, 런타임에서 부르지 않는다)
/// 프리팹 → MonsterBrain.Data.keywords → SpawnTarget 을 따라가며 순환을 찾는다
/// </summary>
public static class MonsterSpawnGraph
{
    /// <summary>
    /// _keyword 가 _start 를 스폰할 때 무한 스폰이 생기는지 검사한다
    /// - _start 에서 이어지는 프리팹 중 _keyword 를 다시 쓰는 프리팹이 있으면 순환 (자기 자신 포함)
    /// - _start 아래 그래프 자체에 순환(A → B → A)이 있어도 순환
    /// 순환이면 true 와 경로 문자열을 돌려준다
    /// </summary>
    public static bool HasCycle(MonsterKeyword _keyword, Spawnable _start, out string _path)
    {
        _path = null;
        if (null == _keyword || null == _start)
        {
            return false;
        }

        HashSet<Spawnable> onStack = new HashSet<Spawnable>();
        HashSet<Spawnable> done = new HashSet<Spawnable>();
        List<Spawnable> stack = new List<Spawnable>();

        if (false == Visit(_keyword, _start, onStack, done, stack))
        {
            return false;
        }

        StringBuilder sb = new StringBuilder(_keyword.name);
        foreach (Spawnable s in stack)
        {
            sb.Append(" → ").Append(null != s ? s.name : "null");
        }
        _path = sb.ToString();
        return true;
    }

    private static bool Visit(
        MonsterKeyword _keyword
        , Spawnable _node
        , HashSet<Spawnable> _onStack
        , HashSet<Spawnable> _done
        , List<Spawnable> _stack)
    {
        _stack.Add(_node);

        // 현재 경로에 이미 있으면 순환
        if (true == _onStack.Contains(_node))
        {
            return true;
        }

        if (true == _done.Contains(_node))
        {
            _stack.RemoveAt(_stack.Count - 1);
            return false;
        }

        _onStack.Add(_node);

        MonsterBrain brain = _node.GetComponentInChildren<MonsterBrain>(true);
        if (null != brain && null != brain.Data && null != brain.Data.keywords)
        {
            foreach (MonsterKeyword k in brain.Data.keywords)
            {
                if (null == k)
                {
                    continue;
                }

                // 검사 중인 키워드를 다시 쓰면 그 키워드가 또 _start 를 스폰한다
                if (k == _keyword)
                {
                    return true;
                }

                Spawnable next = k.SpawnTarget;
                if (null != next && true == Visit(_keyword, next, _onStack, _done, _stack))
                {
                    return true;
                }
            }
        }

        _onStack.Remove(_node);
        _done.Add(_node);
        _stack.RemoveAt(_stack.Count - 1);
        return false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 순환이면 경고를 남긴다 (키워드 OnValidate 에서 호출)
    /// </summary>
    public static void WarnIfCycle(MonsterKeyword _keyword, Spawnable _start)
    {
        if (true == HasCycle(_keyword, _start, out string path))
        {
            Debug.LogWarning($"[{_keyword.GetType().Name}] {_keyword.name} : 무한 스폰 순환 ({path})", _keyword);
        }
    }
#endif
}
