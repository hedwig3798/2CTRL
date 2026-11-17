using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 공간 대상 하나 (쿼드트리 원소)
/// - stagePos 는 화면 공간(StageSpace) 좌표, radius 는 화면 공간 반지름
/// - health 는 타격 대상. 쿼리 시점에 이미 죽었을 수 있으니 쓰기 전에 IsAlive 확인
/// </summary>
public struct StageTarget
{
    public Vector2 stagePos;
    public float radius;
    public PlayerSide side;
    public HealthSystem health;

    public StageTarget(Vector2 _stagePos, float _radius, PlayerSide _side, HealthSystem _health)
    {
        stagePos = _stagePos;
        radius = Mathf.Max(0f, _radius);
        side = _side;
        health = _health;
    }

    /// <summary>
    /// 대상이 아직 살아 있고 활성 상태인지 (쿼드트리 재구성 뒤 같은 프레임 안에서 죽었을 수 있다)
    /// </summary>
    public bool IsAlive => null != health && false == health.isDead && true == health.isActiveAndEnabled;
}

/// <summary>
/// 화면 공간 쿼드트리 (순수 로직, MonoBehaviour 아님)
/// - 원소는 중심점으로 칸을 정한다. 노드마다 하위 원소들의 실제 범위(중심 ± 반지름) AABB 를 기록해 가지치기한다
///   → 반지름이 큰 원소, 경계 밖 원소도 놓치지 않는다
/// - 노드 용량을 넘으면 4분할, 최대 깊이에서는 분할하지 않고 쌓는다
/// - 노드·원소·탐색 스택은 배열을 재사용한다 (워밍업 후 Clear/Insert/Query 에서 GC 0)
/// - 원소 수가 linearThreshold 미만이면 쿼리는 선형 탐색으로 처리한다
/// </summary>
public class StageQuadTree
{
    private struct Node
    {
        public Rect bounds;
        public int depth;
        public int firstChild;
        public int head;
        public int count;
        public float minX;
        public float minY;
        public float maxX;
        public float maxY;
    }

    private readonly int capacity;
    private readonly int maxDepth;
    private readonly int linearThreshold;

    private Node[] nodes = new Node[64];
    private int nodeCount;
    private StageTarget[] items = new StageTarget[128];
    private int[] next = new int[128];
    private int itemCount;
    private int[] stack = new int[64];
    private int deepest;

    /// <param name="_capacity">노드 용량 (넘으면 분할)</param>
    /// <param name="_maxDepth">최대 깊이 (루트 0)</param>
    /// <param name="_linearThreshold">원소 수가 이보다 적으면 쿼리를 선형 탐색으로 처리</param>
    public StageQuadTree(int _capacity = 8, int _maxDepth = 6, int _linearThreshold = 32)
    {
        capacity = Mathf.Max(1, _capacity);
        maxDepth = Mathf.Max(0, _maxDepth);
        linearThreshold = Mathf.Max(0, _linearThreshold);
        Clear(new Rect(0f, 0f, 1f, 1f));
    }

    public int Count => itemCount;
    public int NodeCount => nodeCount;
    public int Capacity => capacity;
    public int MaxDepth => maxDepth;
    public Rect Bounds => nodes[0].bounds;

    /// <summary>
    /// 지금까지 만들어진 가장 깊은 노드의 깊이 (루트 0)
    /// </summary>
    public int DeepestDepth => deepest;

    /// <summary>
    /// _index 번째 원소 (삽입 순서)
    /// </summary>
    public StageTarget GetItem(int _index)
    {
        return items[_index];
    }

    #region 구성
    /// <summary>
    /// 모든 원소를 지우고 경계를 다시 정한다
    /// </summary>
    public void Clear(Rect _bounds)
    {
        itemCount = 0;
        nodeCount = 0;
        deepest = 0;
        AddNode(_bounds, 0);
    }

    public void Insert(StageTarget _target)
    {
        int idx = itemCount;
        EnsureItemCapacity(itemCount + 1);
        items[idx] = _target;
        next[idx] = -1;
        ++itemCount;

        float minX = _target.stagePos.x - _target.radius;
        float minY = _target.stagePos.y - _target.radius;
        float maxX = _target.stagePos.x + _target.radius;
        float maxY = _target.stagePos.y + _target.radius;

        int node = 0;
        while (true)
        {
            Expand(node, minX, minY, maxX, maxY);

            if (nodes[node].firstChild < 0)
            {
                if (nodes[node].count < capacity || nodes[node].depth >= maxDepth)
                {
                    Push(node, idx);
                    return;
                }
                Split(node);
            }
            node = ChildFor(node, _target.stagePos);
        }
    }

    private int AddNode(Rect _bounds, int _depth)
    {
        if (nodeCount >= nodes.Length)
        {
            Array.Resize(ref nodes, nodes.Length * 2);
        }
        int i = nodeCount++;
        nodes[i].bounds = _bounds;
        nodes[i].depth = _depth;
        nodes[i].firstChild = -1;
        nodes[i].head = -1;
        nodes[i].count = 0;
        nodes[i].minX = float.PositiveInfinity;
        nodes[i].minY = float.PositiveInfinity;
        nodes[i].maxX = float.NegativeInfinity;
        nodes[i].maxY = float.NegativeInfinity;
        if (_depth > deepest)
        {
            deepest = _depth;
        }
        return i;
    }

    private void EnsureItemCapacity(int _size)
    {
        if (_size <= items.Length)
        {
            return;
        }
        int size = items.Length * 2;
        while (size < _size)
        {
            size *= 2;
        }
        Array.Resize(ref items, size);
        Array.Resize(ref next, size);
    }

    private void Expand(int _node, float _minX, float _minY, float _maxX, float _maxY)
    {
        if (_minX < nodes[_node].minX) nodes[_node].minX = _minX;
        if (_minY < nodes[_node].minY) nodes[_node].minY = _minY;
        if (_maxX > nodes[_node].maxX) nodes[_node].maxX = _maxX;
        if (_maxY > nodes[_node].maxY) nodes[_node].maxY = _maxY;
    }

    private void Push(int _node, int _item)
    {
        next[_item] = nodes[_node].head;
        nodes[_node].head = _item;
        ++nodes[_node].count;
    }

    /// <summary>
    /// 잎 노드를 4분할하고 들어 있던 원소를 자식으로 옮긴다
    /// </summary>
    private void Split(int _node)
    {
        Rect b = nodes[_node].bounds;
        int depth = nodes[_node].depth + 1;
        float hw = b.width * 0.5f;
        float hh = b.height * 0.5f;

        // 자식 순서: (좌하, 우하, 좌상, 우상) = ix + iy * 2
        int first = AddNode(new Rect(b.xMin, b.yMin, hw, hh), depth);
        AddNode(new Rect(b.xMin + hw, b.yMin, hw, hh), depth);
        AddNode(new Rect(b.xMin, b.yMin + hh, hw, hh), depth);
        AddNode(new Rect(b.xMin + hw, b.yMin + hh, hw, hh), depth);

        int item = nodes[_node].head;
        nodes[_node].firstChild = first;
        nodes[_node].head = -1;
        nodes[_node].count = 0;

        while (item >= 0)
        {
            int following = next[item];
            StageTarget t = items[item];
            int child = ChildFor(_node, t.stagePos);
            Expand(child, t.stagePos.x - t.radius, t.stagePos.y - t.radius, t.stagePos.x + t.radius, t.stagePos.y + t.radius);
            Push(child, item);
            item = following;
        }
    }

    private int ChildFor(int _node, Vector2 _p)
    {
        Vector2 mid = nodes[_node].bounds.center;
        int ix = _p.x >= mid.x ? 1 : 0;
        int iy = _p.y >= mid.y ? 1 : 0;
        return nodes[_node].firstChild + ix + iy * 2;
    }
    #endregion

    #region 탐색 공통
    private bool UseLinear => itemCount < linearThreshold;

    private int PushStack(int _top, int _node)
    {
        if (_top >= stack.Length)
        {
            Array.Resize(ref stack, stack.Length * 2);
        }
        stack[_top] = _node;
        return _top + 1;
    }

    private bool NodeIsEmpty(int _node)
    {
        return nodes[_node].minX > nodes[_node].maxX;
    }

    /// <summary>
    /// 점에서 노드 내용 AABB 까지 거리 제곱 (안이면 0)
    /// </summary>
    private float NodeDistSq(int _node, Vector2 _p)
    {
        float dx = Mathf.Max(0f, Mathf.Max(nodes[_node].minX - _p.x, _p.x - nodes[_node].maxX));
        float dy = Mathf.Max(0f, Mathf.Max(nodes[_node].minY - _p.y, _p.y - nodes[_node].maxY));
        return dx * dx + dy * dy;
    }

    private bool NodeOverlapsBox(int _node, float _minX, float _minY, float _maxX, float _maxY)
    {
        return false == NodeIsEmpty(_node)
            && nodes[_node].minX <= _maxX && nodes[_node].maxX >= _minX
            && nodes[_node].minY <= _maxY && nodes[_node].maxY >= _minY;
    }

    /// <summary>
    /// 점에서 선분 ab 까지 거리 제곱
    /// </summary>
    public static float SegmentDistSq(Vector2 _p, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float lenSq = ab.sqrMagnitude;
        float t = lenSq > 0f ? Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / lenSq) : 0f;
        Vector2 closest = _a + ab * t;
        return (_p - closest).sqrMagnitude;
    }

    private static bool CircleHit(in StageTarget _t, Vector2 _c, float _r)
    {
        float reach = _r + _t.radius;
        return (_t.stagePos - _c).sqrMagnitude <= reach * reach;
    }

    private static bool SegmentHit(in StageTarget _t, Vector2 _a, Vector2 _b, float _halfWidth)
    {
        float reach = _halfWidth + _t.radius;
        return SegmentDistSq(_t.stagePos, _a, _b) <= reach * reach;
    }

    private static bool RectHit(in StageTarget _t, Rect _rect)
    {
        return BossSpace.CircleOverlapsRect(_t.stagePos, _t.radius, _rect);
    }
    #endregion

    #region 쿼리
    /// <summary>
    /// 원(_c, _r)과 겹치는 원소를 _result 에 담는다 (_result 는 먼저 비운다). 담은 수를 돌려준다
    /// </summary>
    public int QueryCircle(Vector2 _c, float _r, List<StageTarget> _result)
    {
        _result.Clear();
        _r = Mathf.Max(0f, _r);
        if (true == UseLinear)
        {
            for (int i = 0; i < itemCount; ++i)
            {
                if (true == CircleHit(items[i], _c, _r))
                {
                    _result.Add(items[i]);
                }
            }
            return _result.Count;
        }

        float rSq = _r * _r;
        int top = PushStack(0, 0);
        while (top > 0)
        {
            int node = stack[--top];
            if (true == NodeIsEmpty(node) || NodeDistSq(node, _c) > rSq)
            {
                continue;
            }
            if (nodes[node].firstChild >= 0)
            {
                for (int k = 0; k < 4; ++k)
                {
                    top = PushStack(top, nodes[node].firstChild + k);
                }
                continue;
            }
            for (int item = nodes[node].head; item >= 0; item = next[item])
            {
                if (true == CircleHit(items[item], _c, _r))
                {
                    _result.Add(items[item]);
                }
            }
        }
        return _result.Count;
    }

    /// <summary>
    /// 사각형과 겹치는 원소를 _result 에 담는다 (_result 는 먼저 비운다)
    /// </summary>
    public int QueryRect(Rect _rect, List<StageTarget> _result)
    {
        _result.Clear();
        if (true == UseLinear)
        {
            for (int i = 0; i < itemCount; ++i)
            {
                if (true == RectHit(items[i], _rect))
                {
                    _result.Add(items[i]);
                }
            }
            return _result.Count;
        }

        int top = PushStack(0, 0);
        while (top > 0)
        {
            int node = stack[--top];
            if (false == NodeOverlapsBox(node, _rect.xMin, _rect.yMin, _rect.xMax, _rect.yMax))
            {
                continue;
            }
            if (nodes[node].firstChild >= 0)
            {
                for (int k = 0; k < 4; ++k)
                {
                    top = PushStack(top, nodes[node].firstChild + k);
                }
                continue;
            }
            for (int item = nodes[node].head; item >= 0; item = next[item])
            {
                if (true == RectHit(items[item], _rect))
                {
                    _result.Add(items[item]);
                }
            }
        }
        return _result.Count;
    }

    /// <summary>
    /// 선분 ab 를 반폭 _halfWidth 로 두껍게 한 캡슐과 겹치는 원소를 _result 에 담는다 (레이저용, _result 는 먼저 비운다)
    /// </summary>
    public int QuerySegment(Vector2 _a, Vector2 _b, float _halfWidth, List<StageTarget> _result)
    {
        _result.Clear();
        _halfWidth = Mathf.Max(0f, _halfWidth);
        if (true == UseLinear)
        {
            for (int i = 0; i < itemCount; ++i)
            {
                if (true == SegmentHit(items[i], _a, _b, _halfWidth))
                {
                    _result.Add(items[i]);
                }
            }
            return _result.Count;
        }

        // 노드 가지치기는 캡슐의 AABB 로 (보수적)
        float minX = Mathf.Min(_a.x, _b.x) - _halfWidth;
        float minY = Mathf.Min(_a.y, _b.y) - _halfWidth;
        float maxX = Mathf.Max(_a.x, _b.x) + _halfWidth;
        float maxY = Mathf.Max(_a.y, _b.y) + _halfWidth;

        int top = PushStack(0, 0);
        while (top > 0)
        {
            int node = stack[--top];
            if (false == NodeOverlapsBox(node, minX, minY, maxX, maxY))
            {
                continue;
            }
            if (nodes[node].firstChild >= 0)
            {
                for (int k = 0; k < 4; ++k)
                {
                    top = PushStack(top, nodes[node].firstChild + k);
                }
                continue;
            }
            for (int item = nodes[node].head; item >= 0; item = next[item])
            {
                if (true == SegmentHit(items[item], _a, _b, _halfWidth))
                {
                    _result.Add(items[item]);
                }
            }
        }
        return _result.Count;
    }

    /// <summary>
    /// _c 에서 가장 가까운 원소 (거리 = 중심 거리 - 원소 반지름, 0 미만은 0). 원(_c, _maxR)과 겹치는 원소만 본다
    /// _filter 가 false 를 돌려주는 원소는 건너뛴다 (null 이면 모두 허용)
    /// </summary>
    public bool QueryNearest(Vector2 _c, float _maxR, Predicate<StageTarget> _filter, out StageTarget _nearest)
    {
        return FindNearest(_c, _maxR, _filter, null, out _nearest);
    }

    /// <summary>
    /// _exclude 에 든 HealthSystem 을 뺀 가장 가까운 원소 (_exclude 가 null 이면 제외 없음)
    /// </summary>
    public bool QueryNearest(Vector2 _c, float _maxR, ICollection<HealthSystem> _exclude, out StageTarget _nearest)
    {
        return FindNearest(_c, _maxR, null, _exclude, out _nearest);
    }

    private bool FindNearest(Vector2 _c, float _maxR, Predicate<StageTarget> _filter, ICollection<HealthSystem> _exclude, out StageTarget _nearest)
    {
        _nearest = default;
        float bestDist = Mathf.Max(0f, _maxR);
        bool found = false;

        if (true == UseLinear)
        {
            for (int i = 0; i < itemCount; ++i)
            {
                ConsiderNearest(i, _c, _filter, _exclude, ref bestDist, ref found, ref _nearest);
            }
            return found;
        }

        int top = PushStack(0, 0);
        while (top > 0)
        {
            int node = stack[--top];
            if (true == NodeIsEmpty(node) || NodeDistSq(node, _c) > bestDist * bestDist)
            {
                continue;
            }
            if (nodes[node].firstChild >= 0)
            {
                for (int k = 0; k < 4; ++k)
                {
                    top = PushStack(top, nodes[node].firstChild + k);
                }
                continue;
            }
            for (int item = nodes[node].head; item >= 0; item = next[item])
            {
                ConsiderNearest(item, _c, _filter, _exclude, ref bestDist, ref found, ref _nearest);
            }
        }
        return found;
    }

    private void ConsiderNearest(int _item, Vector2 _c, Predicate<StageTarget> _filter, ICollection<HealthSystem> _exclude,
        ref float _bestDist, ref bool _found, ref StageTarget _nearest)
    {
        StageTarget t = items[_item];
        float dist = Mathf.Max(0f, Vector2.Distance(t.stagePos, _c) - t.radius);
        // 찾은 뒤에는 더 가까운 것만, 찾기 전에는 최대 거리 이내 (경계 포함)
        if (dist > _bestDist || (true == _found && dist >= _bestDist))
        {
            return;
        }
        if (null != _exclude && null != t.health && true == _exclude.Contains(t.health))
        {
            return;
        }
        if (null != _filter && false == _filter(t))
        {
            return;
        }
        _bestDist = dist;
        _nearest = t;
        _found = true;
    }
    #endregion
}
