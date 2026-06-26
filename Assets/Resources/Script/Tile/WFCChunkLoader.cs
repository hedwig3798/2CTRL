using System;
using System.Collections.Generic;
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
    public TileData[] tileDatas;

    private Dictionary<Vector2Int, CellState> states = new();
    private Dictionary<TileData, int> tileIndex = new();

    // 인접할 수 있는 목록 비트마스킹
    // i 타일, j 방향에 올 수 있는 타일 목록
    private int[,] adjustMatrix;

    private TileWeightPair[] tilePair;

    // 청크 로드를 위한 변수들
    private PriorityQueue<Vector2Int, CellState> pq = new();
    private Dictionary<Vector2Int, bool> isVisit = new();

    private int[] dx = { -1, 1, 0, 0 };
    private int[] dy = { 0, 0, 1, -1 };

    // 반복문 기반 탐색을 위한 상태 저장 클래스
    private class SearchState
    {
        public Vector2Int index;
        public CellState currCell;
        public List<int> tilesToTry;
        public int currentTryIndex;
        public int[] oldBitmask = new int[4];
    }

    private void InitializeMatrix()
    {
        tilePair = new TileWeightPair[tileDatas.Length];

        // 인접 행렬 초기화
        adjustMatrix = new int[tileDatas.Length, (int)DIRECTION.END];

        // 인덱스 캐싱
        for (int i = 0; i < tileDatas.Length; ++i)
        {
            tileIndex[tileDatas[i]] = i;
        }

        // 인접할 수 있는지 여부 초기화
        for (int i = 0; i < tileDatas.Length; ++i)
        {
            adjustMatrix[i, (int)DIRECTION.UP] = 0;
            for (int j = 0; j < tileDatas[i].validUp.Length; ++j)
            {
                adjustMatrix[i, (int)DIRECTION.UP] |= 1 << tileIndex[tileDatas[i].validUp[j]];
            }

            for (int j = 0; j < tileDatas[i].validDown.Length; ++j)
            {
                adjustMatrix[i, (int)DIRECTION.DOWN] |= 1 << tileIndex[tileDatas[i].validDown[j]];
            }

            for (int j = 0; j < tileDatas[i].validLeft.Length; ++j)
            {
                adjustMatrix[i, (int)DIRECTION.LEFT] |= 1 << tileIndex[tileDatas[i].validLeft[j]];
            }

            for (int j = 0; j < tileDatas[i].validRight.Length; ++j)
            {
                adjustMatrix[i, (int)DIRECTION.RIGHT] |= 1 << tileIndex[tileDatas[i].validRight[j]];
            }
        }
    }

    // 가중치 기반으로 랜덤하게 섞은 타일 인덱스 리스트 반환
    private List<int> GetWeightedRandomTiles(CellState currCell)
    {
        List<TileWeightPair> availablePairs = new List<TileWeightPair>();
        float totalWeight = 0;

        for (int i = 0; i < tileDatas.Length; ++i)
        {
            int bit = 1 << i;
            if ((currCell.bitMask & bit) > 0)
            {
                availablePairs.Add(new TileWeightPair { index = i, weight = tileDatas[i].weight });
                totalWeight += tileDatas[i].weight;
            }
        }

        List<int> shuffledIndices = new List<int>();

        while (availablePairs.Count > 0)
        {
            float randnum = UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < availablePairs.Count; ++i)
            {
                randnum -= availablePairs[i].weight;
                if (randnum <= 0 || i == availablePairs.Count - 1)
                {
                    shuffledIndices.Add(availablePairs[i].index);
                    totalWeight -= availablePairs[i].weight;
                    availablePairs.RemoveAt(i);
                    break;
                }
            }
        }

        return shuffledIndices;
    }

    // 큐에서 다음으로 탐색할 셀 정보를 가져와 탐색 상태 객체 생성
    private SearchState GetNextSearchState(Vector2Int _LT, Vector2Int _RB)
    {
        while (pq.TryDequeue(out Vector2Int index, out CellState currCell))
        {
            currCell = states[index];
            if (currCell.isFixed)
            {
                continue;
            }

            if (index.x < _LT.x || index.x > _RB.x || index.y < _LT.y || index.y > _RB.y)
            {
                continue;
            }

            List<int> validTiles = GetWeightedRandomTiles(currCell);

            return new SearchState
            {
                index = index,
                currCell = currCell,
                tilesToTry = validTiles,
                currentTryIndex = 0,
                oldBitmask = new int[4] { -1, -1, -1, -1 }
            };
        }

        return null;
    }

    // 반복문 구조의 타일 확정 메인 로직
    private bool CollapseTile(Vector2Int _LT, Vector2Int _RB)
    {
        Stack<SearchState> stack = new Stack<SearchState>();

        SearchState firstState = GetNextSearchState(_LT, _RB);
        if (firstState == null) return true;
        if (firstState.tilesToTry.Count > 0) stack.Push(firstState);

        while (stack.Count > 0)
        {
            SearchState state = stack.Peek();

            // 백트래킹 발생 시 이전 상태 복구
            if (state.currentTryIndex > 0)
            {
                for (int d = 0; d < 4; ++d)
                {
                    if (state.oldBitmask[d] != -1)
                    {
                        Vector2Int nextIndex = new(state.index.x + dx[d], state.index.y + dy[d]);
                        CellState restoreCell = states[nextIndex];
                        restoreCell.bitMask = state.oldBitmask[d];

                        restoreCell.entropy = 0;
                        for (int j = 0; j < tileDatas.Length; ++j)
                        {
                            if ((restoreCell.bitMask & (1 << j)) > 0) ++restoreCell.entropy;
                        }

                        states[nextIndex] = restoreCell;
                        pq.Enqueue(nextIndex, restoreCell);
                    }
                }
            }

            // 남은 후보 타일 시도
            if (state.currentTryIndex < state.tilesToTry.Count)
            {
                int tileIndex = state.tilesToTry[state.currentTryIndex];
                state.currentTryIndex++;

                state.currCell.bitMask ^= 1 << tileIndex;
                state.currCell.entropy--;

                for (int d = 0; d < 4; ++d)
                {
                    Vector2Int nextIndex = new(state.index.x + dx[d], state.index.y + dy[d]);
                    if (states.TryGetValue(nextIndex, out CellState nextCell))
                    {
                        state.oldBitmask[d] = nextCell.bitMask;
                    }
                    else
                    {
                        state.oldBitmask[d] = -1;
                        nextCell.bitMask = -1;
                        nextCell.entropy = tileDatas.Length;
                        states.Add(nextIndex, nextCell);
                    }

                    nextCell.bitMask &= adjustMatrix[tileIndex, d];
                    nextCell.entropy = 0;
                    for (int j = 0; j < tileDatas.Length; ++j)
                    {
                        if ((nextCell.bitMask & (1 << j)) > 0)
                        {
                            ++nextCell.entropy;
                        }
                    }

                    pq.Enqueue(nextIndex, nextCell);
                    states[nextIndex] = nextCell;
                }

                SearchState nextState = GetNextSearchState(_LT, _RB);

                if (nextState == null)
                {
                    // 모든 셀 확정 완료 시, 스택에 담긴 경로 최종 반영
                    foreach (var s in stack)
                    {
                        int finalTileIndex = s.tilesToTry[s.currentTryIndex - 1];
                        s.currCell.bitMask = 1 << finalTileIndex;
                        s.currCell.isFixed = true;
                        s.currCell.entropy = 1;
                        states[s.index] = s.currCell;
                    }

                    return true;
                }

                if (nextState.tilesToTry.Count > 0)
                {
                    stack.Push(nextState);
                }
            }
            else
            {
                // 현재 셀에서 가능한 타일이 없으므로 이전 셀로 백트래킹
                stack.Pop();
            }
        }

        return false;
    }

    public void LoadChunk(Vector2Int _LT, Vector2Int _RB)
    {
        pq.Clear();
        isVisit.Clear();

        Vector2Int start = new Vector2Int((_LT.x + _RB.x) / 2, (_LT.y + _RB.y) / 2);

        Vector2Int index = new();

        for (int i = _LT.x; i < _RB.x; ++i)
        {
            for (int j = _LT.y; j < _RB.y; ++j)
            {
                index.x = i;
                index.y = j;
                if (true == states.TryGetValue(index, out var value))
                {
                    if (false == value.isFixed)
                    {
                        pq.Enqueue(index, value);
                    }
                }
                else
                {
                    CellState newCell = new CellState();
                    newCell.bitMask = -1;
                    newCell.isFixed = false;
                    newCell.entropy = tileDatas.Length;
                    states[index] = newCell;

                    pq.Enqueue(index, newCell);
                }
            }
        }

        CollapseTile(_LT, _RB);
    }

    private void Start()
    {
        InitializeMatrix();
    }
}