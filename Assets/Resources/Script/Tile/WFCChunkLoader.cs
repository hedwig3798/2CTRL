using NUnit.Framework.Constraints;
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
    [Header("tile set")]
    public TileData[] tileDatas;

    [Header("tile scale")]
    public int tileScale;

    private Vector2 tileSize;

    private Dictionary<Vector2Int, CellState> states = new();
    private Dictionary<TileData, int> tileIndex = new();

    // 인접할 수 있는 목록 비트마스킹
    // i 타일, j 방향에 올 수 있는 타일 목록
    private int[,] adjustMatrix;

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

    private void SetTileScale()
    {
        tileSize = tileDatas[0].GetSpriteSize();
    }

    private void InitializeMatrix()
    {
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
            adjustMatrix[i, (int)DIRECTION.DOWN] = 0;
            adjustMatrix[i, (int)DIRECTION.LEFT] = 0;
            adjustMatrix[i, (int)DIRECTION.RIGHT] = 0;

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

    // 가중치 기반으로 랜덤하게 섞은 타일 인덱스 리스트 반환
    // 일단 구현만 함, 나중에 수정해야 할듯?
    private List<int> GetWeightedRandomTiles(CellState currCell)
    {
        // 뽑을 리스트
        List<TileWeightPair> availablePairs = new();
        float totalWeight = 0;

        for (int i = 0; i < tileDatas.Length; ++i)
        {
            int bit = 1 << i;
            if (0 < (currCell.bitMask & bit))
            {
                availablePairs.Add(new TileWeightPair { index = i, weight = tileDatas[i].weight });
                totalWeight += tileDatas[i].weight;
            }
        }

        // 섞은 리스트
        List<int> shuffledIndices = new();
        // 뽑을 수 있담면
        while (0 < availablePairs.Count)
        {
            // 가중치 기반 랜덤 가져오기
            float randnum = UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < availablePairs.Count; ++i)
            {
                randnum -= availablePairs[i].weight;
                if (0 >= randnum || i == availablePairs.Count - 1)
                {
                    shuffledIndices.Add(availablePairs[i].index);
                    totalWeight -= availablePairs[i].weight;

                    // 가져온 숫자는 제거
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
        // 다음 탐색 인덱스
        while (pq.TryDequeue(out Vector2Int index, out CellState currCell))
        {
            currCell = states[index];

            // 이미 정해진 타일 무시
            if (currCell.isFixed)
            {
                continue;
            }

            // 방문한 타일 무시
            if (true == isVisit.TryGetValue(index, out bool visited))
            {
                if (true == visited)
                {
                    continue;
                }
            }

            // 범위 밖의 타일 무시
            if (_LT.x > index.x || _RB.x <= index.x || _LT.y > index.y || _RB.y <= index.y)
            {
                continue;
            }

            // 랜덤 타일 가져오기
            List<int> validTiles = GetWeightedRandomTiles(currCell);
            isVisit[index] = true;
            // 검색 데이터 반환
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
        // 백트래킹을 위한 스택
        Stack<SearchState> stack = new Stack<SearchState>();

        // 범위 내의 셀 하나 가져오기
        SearchState firstState = GetNextSearchState(_LT, _RB);
        // 없으면 끝
        if (null == firstState)
        {
            return true;
        }

        // 가능한 타일이 있다면
        if (0 < firstState.tilesToTry.Count)
        {
            stack.Push(firstState);
        }

        // 스택의 값을 하나씩 가져오면서 시작
        while (0 < stack.Count)
        {
            SearchState state = stack.Peek();

            // 백트래킹 되어서 온 경우 이전 상태로 돌리기
            if (0 < state.currentTryIndex)
            {
                for (int d = 0; d < 4; ++d)
                {
                    Vector2Int nextIndex = new(state.index.x + dx[d], state.index.y + dy[d]);

                    if (true == states.TryGetValue(nextIndex, out CellState restoreCell))
                    {
                        if (-1 != state.oldBitmask[d])
                        {
                            restoreCell.bitMask = state.oldBitmask[d];
                        }
                        else
                        {
                            restoreCell.bitMask = -1;
                        }

                        restoreCell.entropy = 0;
                        for (int j = 0; j < tileDatas.Length; ++j)
                        {
                            if (0 < (restoreCell.bitMask & (1 << j)))
                            {
                                ++restoreCell.entropy;
                            }
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

                if (null == nextState)
                {
                    // 모든 셀 확정 완료 시, 스택에 담긴 경로 최종 반영
                    foreach (var s in stack)
                    {
                        int finalTileIndex = s.tilesToTry[s.currentTryIndex - 1];
                        s.currCell.bitMask = 1 << finalTileIndex;
                        s.currCell.isFixed = true;
                        s.currCell.entropy = 1;
                        states[s.index] = s.currCell;

                        // 셀 배치
                        GameObject temp = Instantiate(tileDatas[finalTileIndex]).gameObject;
                        temp.transform.position = new Vector3(s.index.x * tileSize.x * tileScale, s.index.y * tileSize.y * tileScale, 0);
                        temp.transform.rotation = Quaternion.identity;
                        temp.transform.localScale = new Vector3(tileScale, tileScale, tileScale);
                        temp.name = $"{s.index.x}_{s.index.y}";
                        temp.layer = gameObject.layer;
                    }

                    return true;
                }

                if (0 < nextState.tilesToTry.Count)
                {
                    stack.Push(nextState);
                }
                else
                {
                    // 다음에 탐색할 수 있는 셀이 아예 없다면 백트래킹
                    isVisit[nextState.index] = false;
                    pq.Enqueue(nextState.index, states[nextState.index]);
                }
            }
            else
            {
                // 현재 셀에서 가능한 타일이 없으므로 이전 셀로 백트래킹
                SearchState popped = stack.Pop();
                isVisit[popped.index] = false;
                pq.Enqueue(popped.index, states[popped.index]);
            }
        }

        return false;
    }

    public void LoadChunk(Vector2Int _LT, Vector2Int _RB)
    {
        // 데이터 초기화
        pq.Clear();
        isVisit.Clear();

        // 엔트로피가 같을때 WFC 시작점
        Vector2Int start = new Vector2Int((_LT.x + _RB.x) / 2, (_LT.y + _RB.y) / 2);

        Vector2Int index = new();

        // 범위를 전부 순회하며 힙에 데이터 넣기
        for (int i = _LT.x; i < _RB.x; ++i)
        {
            for (int j = _LT.y; j < _RB.y; ++j)
            {
                index.x = i;
                index.y = j;

                // 무언가 변한 스테이트가 있다면
                if (true == states.TryGetValue(index, out var value))
                {
                    if (false == value.isFixed)
                    {
                        pq.Enqueue(index, value);
                    }
                }
                // 새 스테이트 넣기
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

        // 붕괴 시작
        CollapseTile(_LT, _RB);
    }

    private void Awake()
    {
        if (0 == tileDatas.Length)
        {
            enabled = false;
        }

        InitializeMatrix();
    }

    private void Start()
    {
        SetTileScale();

        LoadChunk(new Vector2Int(-8, -8), new Vector2Int(8, 8));
    }
}