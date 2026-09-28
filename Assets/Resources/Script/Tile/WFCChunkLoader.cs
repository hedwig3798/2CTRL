using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public struct CellState : IComparable<CellState>
{
    public int bitMask;
    public int entropy;
    public bool isFixed;

    public int CompareTo(CellState _obj)
    {
        return entropy - _obj.entropy;
    }
}

public struct TileWeightPair
{
    public float weight;
    public int index;
}

public class WFCChunkLoader : MonoBehaviour
{
    [Header("tile set")]
    public TileData[] tileDatas;

    [Header("tile scale")]
    public int tileScale = 1;

    [Header("chunk")]
    // 청크 하나의 크기 (타일 개수)
    [Min(1)] public int chunkWidth = 16;
    [Min(1)] public int chunkHeight = 16;

    // 기준 카메라. 비워두면 이 오브젝트의 레이어를 렌더링하는 카메라 -> Camera.main 순으로 찾는다
    public Camera targetCamera;

    // 카메라에 보이는 청크 바깥으로 미리 로드할 청크 수
    [Min(0)] public int preloadChunks = 1;

    // 로드 범위(보이는 범위 + preloadChunks)에서 이만큼 더 벗어나야 언로드 (경계에서 로드/언로드 반복 방지)
    [Min(0)] public int unloadMargin = 1;

    // 타일이 놓이는 평면의 z (카메라 시야 계산용)
    public float tilePlaneZ = 0f;

    [Header("performance")]
    // 한 프레임에 청크 생성/배치에 쓸 최대 시간(ms). 넘으면 다음 프레임으로 넘긴다.
    public float frameBudgetMs = 4f;
    // 한 번의 시도에서 허용할 백트래킹 횟수 (청크 셀 수 * 이 값). 넘으면 처음부터 다시 시도(재시작)
    public float backtrackBudgetPerCell = 2f;
    // 재시작 최대 횟수. 넘으면 청크 밖 고정 타일 제약을 무시하고 생성한다
    public int maxRestarts = 30;

    private Vector2 tileSize;
    private Vector2 cellSize;       // 타일 1칸의 월드 크기 (스프라이트 크기 * tileScale)
    private Vector2 worldOrigin;    // 타일 (0,0) 의 월드 위치 = 최초 시작 위치

    // 확정된 타일만 저장 (isFixed == true). 언로드해도 지우지 않아 다시 로드하면 같은 맵이 나온다
    private Dictionary<Vector2Int, CellState> states = new Dictionary<Vector2Int, CellState>();
    private Dictionary<TileData, int> tileIndex = new Dictionary<TileData, int>();

    // adjustMatrix[i, d] : i 타일의 d 방향에 올 수 있는 타일 비트마스크
    private int[,] adjustMatrix;
    private int fullMask;

    // DIRECTION 순서 (LEFT, RIGHT, UP, DOWN) 와 동일
    private readonly int[] dx = { -1, 1, 0, 0 };
    private readonly int[] dy = { 0, 0, 1, -1 };

    #region Chunk data

    private class Chunk
    {
        public Vector2Int coord;
        public bool generated;          // WFC 결과(tiles)가 있음
        public int[] tiles;             // 청크 내부 타일 인덱스 (x + y * width), -1 은 비어 있음
        public bool wantVisible;        // 현재 로드 범위 안에 있음
        public bool shown;              // 타일 오브젝트 배치 완료
        public Transform root;
        public readonly List<GameObject> objects = new List<GameObject>();
    }

    private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    // 로드 범위 안에 있지만 아직 배치가 끝나지 않은 청크
    private readonly HashSet<Vector2Int> pendingChunks = new HashSet<Vector2Int>();
    // 타일 종류별 오브젝트 풀
    private readonly Dictionary<int, Stack<GameObject>> pools = new Dictionary<int, Stack<GameObject>>();
    private Transform poolRoot;

    private RectInt loadRange;          // 현재 로드 범위 (청크 좌표)
    private bool hasLoadRange = false;
    private Vector2 focusChunk;         // 카메라 중심의 청크 좌표 (우선순위 계산용)
    private bool initialized = false;
    private bool isInitialLoad = false;
    private readonly Stopwatch frameTimer = new Stopwatch();
    // 이웃 청크 경계를 다시 생성하면서 바뀐 타일 수 (디버그용)
    private int relaxedTileChanges = 0;
    private int relaxedVisibleTileChanges = 0;

    public int LoadedChunkCount
    {
        get
        {
            int count = 0;
            foreach (var pair in chunks)
            {
                if (pair.Value.shown)
                {
                    ++count;
                }
            }
            return count;
        }
    }

    public int PendingChunkCount => pendingChunks.Count;

    #endregion

    private static int Opposite(int _dir)
    {
        // LEFT(0) <-> RIGHT(1), UP(2) <-> DOWN(3)
        return _dir ^ 1;
    }

    private static int PopCount(int _mask)
    {
        int count = 0;
        while (0 != _mask)
        {
            _mask &= _mask - 1;
            ++count;
        }
        return count;
    }

    private void SetTileScale()
    {
        tileSize = tileDatas[0].GetSpriteSize();
    }

    private void InitializeMatrix()
    {
        adjustMatrix = new int[tileDatas.Length, (int)DIRECTION.END];
        fullMask = (1 << tileDatas.Length) - 1;

        for (int i = 0; i < tileDatas.Length; ++i)
        {
            tileIndex[tileDatas[i]] = i;
        }

        for (int i = 0; i < tileDatas.Length; ++i)
        {
            for (int j = 0; j < tileDatas.Length; ++j)
            {
                if (tileDatas[i].upSocket == tileDatas[j].downSocket)
                {
                    adjustMatrix[i, (int)DIRECTION.UP] |= 1 << j;
                }

                if (tileDatas[i].downSocket == tileDatas[j].upSocket)
                {
                    adjustMatrix[i, (int)DIRECTION.DOWN] |= 1 << j;
                }

                if (tileDatas[i].leftSocket == tileDatas[j].rightSocket)
                {
                    adjustMatrix[i, (int)DIRECTION.LEFT] |= 1 << j;
                }

                if (tileDatas[i].rightSocket == tileDatas[j].leftSocket)
                {
                    adjustMatrix[i, (int)DIRECTION.RIGHT] |= 1 << j;
                }
            }
        }
    }

    // _mask 에 속한 타일들 중 하나라도 허용하는, _dir 방향 이웃의 타일 마스크
    private int Support(int _mask, int _dir)
    {
        int result = 0;
        while (0 != _mask)
        {
            int low = _mask & -_mask;
            result |= adjustMatrix[PopCount(low - 1), _dir];
            _mask ^= low;
        }
        return result;
    }

    #region Solver

    // 한 영역을 푸는 동안만 쓰는 작업 데이터. 영역을 1차원 배열로 다룬다.
    // active 가 false 인 칸은 풀지 않는다 (영역 밖과 동일하게 취급, 확정 타일이면 제약으로만 사용)
    private class ChunkSolver
    {
        public WFCChunkLoader owner;
        public int originX, originY, width, height;
        public bool[] active;     // null 이면 전체가 대상
        public int[] domain;      // 각 셀에 올 수 있는 타일 비트마스크
        public int[] initial;     // 재시작용 초기 도메인
        public bool useOuterConstraints = true;

        // 되돌리기 기록 (셀 인덱스, 이전 마스크)
        private readonly List<int> trailCell = new List<int>();
        private readonly List<int> trailMask = new List<int>();
        private readonly Queue<int> propagateQueue = new Queue<int>();

        private struct Decision
        {
            public int cell;
            public int tile;
            public int trailMark;
        }
        private readonly Stack<Decision> decisions = new Stack<Decision>();

        public int backtrackCount;

        public int Count => width * height;

        public bool InRect(int _x, int _y)
        {
            return originX <= _x && _x < originX + width && originY <= _y && _y < originY + height;
        }

        public bool Inside(int _x, int _y)
        {
            return InRect(_x, _y) && (null == active || active[ToIndex(_x, _y)]);
        }

        public bool IsActive(int _index)
        {
            return null == active || active[_index];
        }

        public int ToIndex(int _x, int _y)
        {
            return (_x - originX) + (_y - originY) * width;
        }

        // 풀 대상 칸은 모두 미정 상태에서 시작하고, 대상 밖의 확정 타일로부터 초기 도메인 계산 후 전파. 모순이면 false
        public bool BuildInitialDomain()
        {
            domain = new int[Count];
            propagateQueue.Clear();

            for (int y = originY; y < originY + height; ++y)
            {
                for (int x = originX; x < originX + width; ++x)
                {
                    int ci = ToIndex(x, y);
                    if (false == IsActive(ci))
                    {
                        domain[ci] = 0;
                        continue;
                    }

                    domain[ci] = owner.fullMask;

                    if (useOuterConstraints)
                    {
                        for (int d = 0; d < 4; ++d)
                        {
                            int nx = x + owner.dx[d];
                            int ny = y + owner.dy[d];
                            if (Inside(nx, ny))
                            {
                                continue;
                            }

                            CellState neighbor;
                            if (owner.states.TryGetValue(new Vector2Int(nx, ny), out neighbor) && neighbor.isFixed)
                            {
                                // 이웃(nx,ny)에서 보면 현재 셀은 반대 방향에 있다
                                domain[ci] &= owner.Support(neighbor.bitMask, Opposite(d));
                            }
                        }
                    }

                    if (0 == domain[ci])
                    {
                        return false;
                    }

                    propagateQueue.Enqueue(ci);
                }
            }

            trailCell.Clear();
            trailMask.Clear();
            decisions.Clear();

            if (false == Propagate())
            {
                return false;
            }

            // 초기 전파 결과는 되돌릴 필요가 없으므로 기록을 지운다
            trailCell.Clear();
            trailMask.Clear();
            initial = (int[])domain.Clone();
            return true;
        }

        public void Restart()
        {
            domain = (int[])initial.Clone();
            trailCell.Clear();
            trailMask.Clear();
            decisions.Clear();
            propagateQueue.Clear();
            backtrackCount = 0;
        }

        private void SetDomain(int _cell, int _mask)
        {
            trailCell.Add(_cell);
            trailMask.Add(domain[_cell]);
            domain[_cell] = _mask;
        }

        private void Undo(int _mark)
        {
            for (int i = trailCell.Count - 1; i >= _mark; --i)
            {
                domain[trailCell[i]] = trailMask[i];
            }
            trailCell.RemoveRange(_mark, trailCell.Count - _mark);
            trailMask.RemoveRange(_mark, trailMask.Count - _mark);
        }

        // 큐에 들어있는 셀들로부터 제약을 끝까지 전파 (AC-3). 모순이면 false
        private bool Propagate()
        {
            while (0 < propagateQueue.Count)
            {
                int ci = propagateQueue.Dequeue();
                int cx = originX + ci % width;
                int cy = originY + ci / width;
                int mask = domain[ci];

                for (int d = 0; d < 4; ++d)
                {
                    int nx = cx + owner.dx[d];
                    int ny = cy + owner.dy[d];
                    if (false == Inside(nx, ny))
                    {
                        continue;
                    }

                    int ni = ToIndex(nx, ny);
                    int before = domain[ni];
                    int after = before & owner.Support(mask, d);
                    if (after == before)
                    {
                        continue;
                    }

                    if (0 == after)
                    {
                        propagateQueue.Clear();
                        return false;
                    }

                    SetDomain(ni, after);
                    propagateQueue.Enqueue(ni);
                }
            }
            return true;
        }

        // 엔트로피가 가장 낮은(>1) 셀. 모두 확정이면 -1
        private int SelectCell()
        {
            int best = -1;
            int bestEntropy = int.MaxValue;
            int ties = 0;

            for (int i = 0; i < domain.Length; ++i)
            {
                int e = PopCount(domain[i]);
                if (1 >= e)
                {
                    continue;
                }

                if (e < bestEntropy)
                {
                    bestEntropy = e;
                    best = i;
                    ties = 1;
                }
                else if (e == bestEntropy)
                {
                    // 동점은 무작위로 (reservoir sampling)
                    ++ties;
                    if (0 == UnityEngine.Random.Range(0, ties))
                    {
                        best = i;
                    }
                }
            }
            return best;
        }

        private int PickWeighted(int _mask)
        {
            float total = 0;
            for (int t = 0; t < owner.tileDatas.Length; ++t)
            {
                if (0 != (_mask & (1 << t)))
                {
                    total += Mathf.Max(0f, owner.tileDatas[t].weight);
                }
            }

            if (0 >= total)
            {
                // 가중치가 모두 0이면 균등 선택
                int n = UnityEngine.Random.Range(0, PopCount(_mask));
                for (int t = 0; t < owner.tileDatas.Length; ++t)
                {
                    if (0 != (_mask & (1 << t)) && 0 > --n)
                    {
                        return t;
                    }
                }
            }

            float r = UnityEngine.Random.Range(0f, total);
            int last = -1;
            for (int t = 0; t < owner.tileDatas.Length; ++t)
            {
                if (0 == (_mask & (1 << t)))
                {
                    continue;
                }
                last = t;
                float w = Mathf.Max(0f, owner.tileDatas[t].weight);
                if (0 >= w)
                {
                    continue;
                }
                r -= w;
                if (0 >= r)
                {
                    return t;
                }
            }
            return last;
        }

        // 한 번의 결정(또는 백트래킹)을 수행.
        // 반환: 1 = 완료, 0 = 계속, -1 = 이번 시도 실패(재시작 필요)
        public int Step(int _backtrackBudget)
        {
            int cell = SelectCell();
            if (-1 == cell)
            {
                return 1;
            }

            int tile = PickWeighted(domain[cell]);
            int mark = trailCell.Count;
            decisions.Push(new Decision { cell = cell, tile = tile, trailMark = mark });

            SetDomain(cell, 1 << tile);
            propagateQueue.Enqueue(cell);
            if (Propagate())
            {
                return 0;
            }

            // 모순: 직전 결정을 되돌리고 그 타일을 후보에서 제거. 그것도 모순이면 더 위로 올라간다
            while (0 < decisions.Count)
            {
                ++backtrackCount;
                if (backtrackCount > _backtrackBudget)
                {
                    return -1;
                }

                Decision last = decisions.Pop();
                Undo(last.trailMark);

                int remain = domain[last.cell] & ~(1 << last.tile);
                if (0 == remain)
                {
                    continue;
                }

                // 이 제거는 상위 결정의 결과로 기록되므로, 상위가 되돌려질 때 함께 복구된다
                SetDomain(last.cell, remain);
                propagateQueue.Enqueue(last.cell);
                if (Propagate())
                {
                    return 0;
                }
            }

            // 모든 결정을 되돌려도 해가 없음
            return -1;
        }
    }

    #endregion

    #region Chunk streaming

    private static int FloorDiv(int _a, int _b)
    {
        return 0 <= _a ? _a / _b : -((-_a + _b - 1) / _b);
    }

    private bool OverFrameBudget()
    {
        // 시작 시 화면에 보이는 청크는 한 번에 로드 (첫 프레임에 빈 화면 방지)
        return false == isInitialLoad && frameTimer.Elapsed.TotalMilliseconds >= frameBudgetMs;
    }

    // 청크 (cx, cy) 의 왼쪽 아래 타일 좌표. 청크 (0,0) 이 시작 위치를 중심으로 놓이도록 반만큼 당긴다
    private Vector2Int ChunkToTileMin(Vector2Int _chunk)
    {
        return new Vector2Int(_chunk.x * chunkWidth - chunkWidth / 2, _chunk.y * chunkHeight - chunkHeight / 2);
    }

    private Vector2Int WorldToTile(Vector2 _world)
    {
        return new Vector2Int(
            Mathf.FloorToInt((_world.x - worldOrigin.x) / cellSize.x + 0.5f),
            Mathf.FloorToInt((_world.y - worldOrigin.y) / cellSize.y + 0.5f));
    }

    private Vector2Int TileToChunk(Vector2Int _tile)
    {
        return new Vector2Int(
            FloorDiv(_tile.x + chunkWidth / 2, chunkWidth),
            FloorDiv(_tile.y + chunkHeight / 2, chunkHeight));
    }

    private Vector3 TileToWorld(int _x, int _y)
    {
        return new Vector3(worldOrigin.x + _x * cellSize.x, worldOrigin.y + _y * cellSize.y, tilePlaneZ);
    }

    // 카메라 뷰포트 네 모서리를 타일 평면에 투영해서 보이는 월드 영역을 구한다 (직교/원근 모두 지원)
    private bool TryGetCameraWorldBounds(out Vector2 _min, out Vector2 _max)
    {
        _min = new Vector2(float.MaxValue, float.MaxValue);
        _max = new Vector2(float.MinValue, float.MinValue);

        if (null == targetCamera)
        {
            return false;
        }

        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, tilePlaneZ));
        for (int i = 0; i < 4; ++i)
        {
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(i & 1, (i >> 1) & 1, 0f));
            float enter;
            if (false == plane.Raycast(ray, out enter))
            {
                return false;
            }

            Vector3 p = ray.GetPoint(enter);
            _min = Vector2.Min(_min, new Vector2(p.x, p.y));
            _max = Vector2.Max(_max, new Vector2(p.x, p.y));
        }
        return true;
    }

    // 로드해야 할 청크 범위 = 카메라에 보이는 청크 + preloadChunks
    private RectInt CalculateLoadRange()
    {
        RectInt visible = CalculateVisibleRange();
        return new RectInt(
            visible.xMin - preloadChunks,
            visible.yMin - preloadChunks,
            visible.width + preloadChunks * 2,
            visible.height + preloadChunks * 2);
    }

    // 카메라에 보이는 청크 범위
    private RectInt CalculateVisibleRange()
    {
        Vector2Int minChunk;
        Vector2Int maxChunk;

        Vector2 min, max;
        if (TryGetCameraWorldBounds(out min, out max))
        {
            minChunk = TileToChunk(WorldToTile(min));
            maxChunk = TileToChunk(WorldToTile(max));
            Vector2Int center = TileToChunk(WorldToTile((min + max) * 0.5f));
            focusChunk = new Vector2(center.x, center.y);
        }
        else
        {
            // 카메라가 없으면 시작 위치 청크만 기준으로
            minChunk = Vector2Int.zero;
            maxChunk = Vector2Int.zero;
            focusChunk = Vector2.zero;
        }

        return new RectInt(minChunk.x, minChunk.y, maxChunk.x - minChunk.x + 1, maxChunk.y - minChunk.y + 1);
    }

    private bool AreVisibleChunksShown()
    {
        RectInt visible = CalculateVisibleRange();
        for (int cy = visible.yMin; cy < visible.yMax; ++cy)
        {
            for (int cx = visible.xMin; cx < visible.xMax; ++cx)
            {
                Chunk chunk;
                if (false == chunks.TryGetValue(new Vector2Int(cx, cy), out chunk) || false == chunk.shown)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void RefreshChunks(RectInt _range)
    {
        loadRange = _range;
        hasLoadRange = true;

        RectInt keepRange = new RectInt(
            _range.xMin - unloadMargin,
            _range.yMin - unloadMargin,
            _range.width + unloadMargin * 2,
            _range.height + unloadMargin * 2);

        // 유지 범위를 벗어난 청크 언로드
        foreach (var pair in chunks)
        {
            Chunk chunk = pair.Value;
            if (chunk.wantVisible && false == keepRange.Contains(chunk.coord))
            {
                HideChunk(chunk);
            }
        }

        // 로드 범위 안의 청크 로드 요청
        for (int cy = _range.yMin; cy < _range.yMax; ++cy)
        {
            for (int cx = _range.xMin; cx < _range.xMax; ++cx)
            {
                Vector2Int coord = new Vector2Int(cx, cy);
                Chunk chunk;
                if (false == chunks.TryGetValue(coord, out chunk))
                {
                    chunk = new Chunk { coord = coord };
                    chunks.Add(coord, chunk);
                }

                if (false == chunk.wantVisible)
                {
                    chunk.wantVisible = true;
                }

                if (false == chunk.shown)
                {
                    pendingChunks.Add(coord);
                }
            }
        }
    }

    // 대기 중인 청크 중 카메라에 가장 가까운 것
    private Chunk FindNextChunk()
    {
        Chunk best = null;
        float bestDistance = float.MaxValue;

        foreach (Vector2Int coord in pendingChunks)
        {
            float distance = (new Vector2(coord.x, coord.y) - focusChunk).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = chunks[coord];
            }
        }
        return best;
    }

    private IEnumerator ChunkWorker()
    {
        while (true)
        {
            Chunk next = FindNextChunk();
            if (null == next)
            {
                yield return null;
                continue;
            }

            if (false == next.generated)
            {
                IEnumerator generate = GenerateChunk(next);
                while (generate.MoveNext())
                {
                    yield return generate.Current;
                }
            }

            IEnumerator show = ShowChunk(next);
            while (show.MoveNext())
            {
                yield return show.Current;
            }

            if (OverFrameBudget())
            {
                yield return null;
            }
        }
    }

    // 청크 생성.
    // 1) 주변에 이미 생성된 타일에 맞춰서 청크만 생성
    // 2) 불가능하면(주변 타일끼리 맞물리는 조합이 없으면) 이웃 청크의 경계 타일을 relax 칸만큼 함께 다시 생성 (1, 2, 4 ... 칸)
    // 3) 그래도 불가능하면 경계 제약을 무시하고 생성 (이음새가 어긋날 수 있음)
    private IEnumerator GenerateChunk(Chunk _chunk)
    {
        Vector2Int tileMin = ChunkToTileMin(_chunk.coord);

        _chunk.tiles = new int[chunkWidth * chunkHeight];
        for (int i = 0; i < _chunk.tiles.Length; ++i)
        {
            _chunk.tiles[i] = -1;
        }

        int maxRelax = Mathf.Max(chunkWidth, chunkHeight);

        // 먼저 화면에 보이는 타일은 건드리지 않고 시도, 안 되면 보이는 타일도 다시 생성 허용
        for (int pass = 0; pass < 2; ++pass)
        {
            bool protectVisible = 0 == pass;
            int relax = 0 == pass ? 0 : 1;
            while (true)
            {
                ChunkSolver solver = CreateSolver(_chunk, tileMin, relax, protectVisible);
                bool solved = false;
                IEnumerator run = RunSolver(solver, _result => solved = _result);
                while (run.MoveNext())
                {
                    yield return run.Current;
                }

                if (solved)
                {
                    ApplySolver(solver, _chunk, tileMin);
                    _chunk.generated = true;
                    yield break;
                }

                if (relax >= maxRelax)
                {
                    break;
                }
                relax = 0 == relax ? 1 : Mathf.Min(relax * 2, maxRelax);
            }
        }

        UnityEngine.Debug.LogWarning($"[WFC] 청크 {_chunk.coord} 를 주변 타일과 맞게 생성하지 못해 경계 제약을 무시합니다.");
        {
            ChunkSolver solver = CreateSolver(_chunk, tileMin, 0, false);
            solver.useOuterConstraints = false;
            bool solved = false;
            IEnumerator run = RunSolver(solver, _result => solved = _result);
            while (run.MoveNext())
            {
                yield return run.Current;
            }

            if (solved)
            {
                ApplySolver(solver, _chunk, tileMin);
            }
            else
            {
                UnityEngine.Debug.LogError($"[WFC] 청크 {_chunk.coord} 를 생성할 수 없습니다. 타일 소켓 설정을 확인하세요.");
            }
        }

        _chunk.generated = true;
    }

    // 청크 영역 + 사방 _relax 칸. 청크 밖 칸은 이미 생성된 이웃 청크의 칸만 다시 풀 대상으로 포함한다
    // _protectVisible 이면 카메라에 보이는 칸은 다시 생성하지 않는다
    private ChunkSolver CreateSolver(Chunk _chunk, Vector2Int _tileMin, int _relax, bool _protectVisible)
    {
        Vector2 viewMin, viewMax;
        bool hasView = TryGetCameraWorldBounds(out viewMin, out viewMax) && _protectVisible;

        ChunkSolver solver = new ChunkSolver
        {
            owner = this,
            originX = _tileMin.x - _relax,
            originY = _tileMin.y - _relax,
            width = chunkWidth + _relax * 2,
            height = chunkHeight + _relax * 2,
        };

        if (0 == _relax)
        {
            return solver;
        }

        solver.active = new bool[solver.Count];
        for (int i = 0; i < solver.Count; ++i)
        {
            int x = solver.originX + i % solver.width;
            int y = solver.originY + i / solver.width;

            Vector2Int coord = TileToChunk(new Vector2Int(x, y));
            if (coord == _chunk.coord)
            {
                solver.active[i] = true;
                continue;
            }

            if (hasView && IsTileInView(x, y, viewMin, viewMax))
            {
                continue;
            }

            Chunk neighbor;
            if (chunks.TryGetValue(coord, out neighbor) && neighbor.generated && null != neighbor.tiles)
            {
                Vector2Int neighborMin = ChunkToTileMin(coord);
                int local = (x - neighborMin.x) + (y - neighborMin.y) * chunkWidth;
                solver.active[i] = 0 <= neighbor.tiles[local];
            }
        }
        return solver;
    }

    private bool IsTileInView(int _x, int _y, Vector2 _viewMin, Vector2 _viewMax)
    {
        Vector3 center = TileToWorld(_x, _y);
        // 타일 크기만큼 여유를 둔다
        return center.x + cellSize.x >= _viewMin.x && center.x - cellSize.x <= _viewMax.x
            && center.y + cellSize.y >= _viewMin.y && center.y - cellSize.y <= _viewMax.y;
    }

    private IEnumerator RunSolver(ChunkSolver _solver, Action<bool> _onFinish)
    {
        if (false == _solver.BuildInitialDomain())
        {
            _onFinish(false);
            yield break;
        }

        int activeCount = 0;
        for (int i = 0; i < _solver.Count; ++i)
        {
            if (_solver.IsActive(i))
            {
                ++activeCount;
            }
        }

        int backtrackBudget = Mathf.Max(100, (int)(activeCount * backtrackBudgetPerCell));
        int restarts = 0;

        while (true)
        {
            int result = _solver.Step(backtrackBudget);

            if (1 == result)
            {
                _onFinish(true);
                yield break;
            }

            if (-1 == result)
            {
                ++restarts;
                if (restarts > maxRestarts)
                {
                    _onFinish(false);
                    yield break;
                }
                _solver.Restart();
            }

            if (OverFrameBudget())
            {
                yield return null;
            }
        }
    }

    // 결과를 states 와 청크 데이터에 반영. 다시 생성된 이웃 칸이 이미 배치되어 있으면 오브젝트도 교체
    private void ApplySolver(ChunkSolver _solver, Chunk _chunk, Vector2Int _tileMin)
    {
        for (int i = 0; i < _solver.Count; ++i)
        {
            if (false == _solver.IsActive(i))
            {
                continue;
            }

            int x = _solver.originX + i % _solver.width;
            int y = _solver.originY + i / _solver.width;
            int mask = _solver.domain[i];
            int tile = PopCount((mask & -mask) - 1);

            states[new Vector2Int(x, y)] = new CellState { bitMask = 1 << tile, entropy = 1, isFixed = true };

            Vector2Int coord = TileToChunk(new Vector2Int(x, y));
            Chunk owner = coord == _chunk.coord ? _chunk : chunks[coord];
            Vector2Int ownerMin = coord == _chunk.coord ? _tileMin : ChunkToTileMin(coord);
            int local = (x - ownerMin.x) + (y - ownerMin.y) * chunkWidth;

            int oldTile = owner.tiles[local];
            if (oldTile == tile)
            {
                continue;
            }
            owner.tiles[local] = tile;

            if (owner != _chunk)
            {
                ++relaxedTileChanges;
                Vector2 viewMin, viewMax;
                if (TryGetCameraWorldBounds(out viewMin, out viewMax) && IsTileInView(x, y, viewMin, viewMax))
                {
                    ++relaxedVisibleTileChanges;
                }
                if (local < owner.objects.Count && null != owner.objects[local])
                {
                    DespawnTile(oldTile, owner.objects[local]);
                    GameObject tileObject = SpawnTile(tile, owner.root);
                    tileObject.transform.position = TileToWorld(x, y);
                    tileObject.name = $"{x}_{y}";
                    owner.objects[local] = tileObject;
                }
            }
        }
    }

    private IEnumerator ShowChunk(Chunk _chunk)
    {
        if (null == _chunk.root)
        {
            GameObject rootObject = new GameObject($"Chunk_{_chunk.coord.x}_{_chunk.coord.y}");
            rootObject.layer = gameObject.layer;
            _chunk.root = rootObject.transform;
            _chunk.root.SetParent(transform, false);
        }

        Vector2Int tileMin = ChunkToTileMin(_chunk.coord);
        int total = chunkWidth * chunkHeight;

        // 중간에 언로드되면(wantVisible == false) 멈춘다. 이미 배치한 것은 HideChunk 가 회수함
        while (_chunk.wantVisible && _chunk.objects.Count < total)
        {
            int i = _chunk.objects.Count;
            int tile = _chunk.tiles[i];

            GameObject tileObject = null;
            if (0 <= tile)
            {
                int x = tileMin.x + i % chunkWidth;
                int y = tileMin.y + i / chunkWidth;
                tileObject = SpawnTile(tile, _chunk.root);
                tileObject.transform.position = TileToWorld(x, y);
                tileObject.name = $"{x}_{y}";
            }
            // 빈 칸도 자리를 채워 인덱스를 맞춘다
            _chunk.objects.Add(tileObject);

            if (OverFrameBudget())
            {
                yield return null;
            }
        }

        if (_chunk.wantVisible)
        {
            _chunk.shown = true;
            pendingChunks.Remove(_chunk.coord);
        }
    }

    private void HideChunk(Chunk _chunk)
    {
        _chunk.wantVisible = false;
        _chunk.shown = false;
        pendingChunks.Remove(_chunk.coord);

        for (int i = 0; i < _chunk.objects.Count; ++i)
        {
            GameObject tileObject = _chunk.objects[i];
            if (null != tileObject)
            {
                DespawnTile(_chunk.tiles[i], tileObject);
            }
        }
        _chunk.objects.Clear();
    }

    private GameObject SpawnTile(int _tile, Transform _parent)
    {
        Stack<GameObject> pool;
        if (pools.TryGetValue(_tile, out pool) && 0 < pool.Count)
        {
            GameObject pooled = pool.Pop();
            pooled.transform.SetParent(_parent, false);
            pooled.SetActive(true);
            return pooled;
        }

        GameObject created = Instantiate(tileDatas[_tile], _parent).gameObject;
        created.transform.rotation = Quaternion.identity;
        created.transform.localScale = new Vector3(tileScale, tileScale, tileScale);
        created.layer = gameObject.layer;
        return created;
    }

    private void DespawnTile(int _tile, GameObject _object)
    {
        Stack<GameObject> pool;
        if (false == pools.TryGetValue(_tile, out pool))
        {
            pool = new Stack<GameObject>();
            pools.Add(_tile, pool);
        }

        _object.SetActive(false);
        _object.transform.SetParent(poolRoot, false);
        pool.Push(_object);
    }

    // 이 오브젝트의 레이어를 그리는 카메라를 찾는다 (분할 화면에서 각 로더가 자기 카메라를 찾도록)
    private Camera FindCameraForLayer()
    {
        int layerBit = 1 << gameObject.layer;
        foreach (Camera cam in Camera.allCameras)
        {
            if (0 != (cam.cullingMask & layerBit))
            {
                return cam;
            }
        }
        return Camera.main;
    }

    #endregion

    private void Awake()
    {
        if (null == tileDatas || 0 == tileDatas.Length)
        {
            enabled = false;
            return;
        }

        // int 비트마스크를 사용하므로 31개까지만 지원
        if (31 < tileDatas.Length)
        {
            UnityEngine.Debug.LogError($"[WFC] 타일 종류는 최대 31개까지 지원합니다. (현재 {tileDatas.Length}개)");
            enabled = false;
            return;
        }

        for (int i = 0; i < tileDatas.Length; ++i)
        {
            if (null == tileDatas[i])
            {
                UnityEngine.Debug.LogError($"[WFC] {name} 의 tileDatas[{i}] 가 비어 있습니다.");
                enabled = false;
                return;
            }
        }

        chunkWidth = Mathf.Max(1, chunkWidth);
        chunkHeight = Mathf.Max(1, chunkHeight);
        tileScale = Mathf.Max(1, tileScale);

        InitializeMatrix();
    }

    private void Start()
    {
        if (false == enabled)
        {
            return;
        }

        SetTileScale();
        cellSize = new Vector2(tileSize.x * tileScale, tileSize.y * tileScale);

        if (null == targetCamera)
        {
            targetCamera = FindCameraForLayer();
            if (null == targetCamera)
            {
                UnityEngine.Debug.LogWarning($"[WFC] {name} : 카메라를 찾지 못해 시작 위치 주변 청크만 로드합니다.");
            }
        }

        // 최초 시작 위치 = 카메라 위치 (없으면 이 오브젝트 위치). 청크 (0,0) 이 이 위치를 중심으로 생성된다
        Vector3 start = null != targetCamera ? targetCamera.transform.position : transform.position;
        worldOrigin = new Vector2(start.x, start.y);

        GameObject poolObject = new GameObject("TilePool");
        poolObject.SetActive(false);
        poolRoot = poolObject.transform;
        poolRoot.SetParent(transform, false);

        initialized = true;
        frameTimer.Restart();
        RefreshChunks(CalculateLoadRange());

        // 최초 시작 위치에서 보이는 청크는 Start 안에서 바로 생성/배치하고, 나머지(미리 로드분)는 코루틴에서 나눠서 처리
        IEnumerator worker = ChunkWorker();
        isInitialLoad = true;
        int guard = 0;
        while (false == AreVisibleChunksShown() && guard++ < 100000)
        {
            worker.MoveNext();
        }
        isInitialLoad = false;

        StartCoroutine(worker);
    }

    private void Update()
    {
        // 코루틴(yield return null)은 Update 이후에 재개되므로 여기서 프레임 예산을 초기화
        frameTimer.Restart();

        if (false == initialized)
        {
            return;
        }

        // 카메라가 움직여서 로드 범위가 바뀌었을 때만 갱신
        RectInt range = CalculateLoadRange();
        if (false == hasLoadRange
            || range.xMin != loadRange.xMin || range.yMin != loadRange.yMin
            || range.width != loadRange.width || range.height != loadRange.height)
        {
            RefreshChunks(range);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (false == initialized)
        {
            return;
        }

        foreach (var pair in chunks)
        {
            Chunk chunk = pair.Value;
            if (false == chunk.wantVisible)
            {
                continue;
            }

            Vector2Int tileMin = ChunkToTileMin(chunk.coord);
            Vector3 min = TileToWorld(tileMin.x, tileMin.y) - new Vector3(cellSize.x, cellSize.y, 0f) * 0.5f;
            Vector3 size = new Vector3(chunkWidth * cellSize.x, chunkHeight * cellSize.y, 0f);
            Gizmos.color = chunk.shown ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(min + size * 0.5f, size);
        }
    }
}
