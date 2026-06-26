using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WFCCell
{
    public bool isCollapsed;
    public int entropy;
    public List<int> possibleTiles;

    public WFCCell(int _totalTiles)
    {
        isCollapsed = false;
        entropy = _totalTiles;
        possibleTiles = new List<int>();

        for (int i = 0; i < _totalTiles; i++)
        {
            possibleTiles.Add(i);
        }
    }

    public void Collapse(int _selectedTileId)
    {
        isCollapsed = true;
        entropy = 1;
        possibleTiles.Clear();
        possibleTiles.Add(_selectedTileId);
    }
}

public class WFCChunkGenerator 
    : MonoBehaviour
{
    public int chunkWidth;
    public int chunkHeight;
    public List<TileData> tileDatabase;

    private WFCCell[,] grid;
    private Stack<Vector2Int> propagationStack;

    public void GenerateChunk(int _chunkX, int _chunkY)
    {
        InitializeGrid();
        ApplyBorderConstraints(_chunkX, _chunkY);

        while (false == IsAllCollapsed())
        {
            Vector2Int minEntropyCell = GetMinEntropyCell();
            CollapseCell(minEntropyCell);
            Propagate();
        }
    }

    private void InitializeGrid()
    {
        grid = new WFCCell[chunkWidth, chunkHeight];
        propagationStack = new Stack<Vector2Int>();

        for (int x = 0; x < chunkWidth; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                grid[x, y] = new WFCCell(tileDatabase.Count);
            }
        }
    }

    private void ApplyBorderConstraints(int _chunkX, int _chunkY)
    {
        // 인접한 기존 청크가 있다면 가장자리 셀의 후보를 미리 제한하고 스택에 추가합니다.
    }

    private bool IsAllCollapsed()
    {
        for (int x = 0; x < chunkWidth; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                if (false == grid[x, y].isCollapsed)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private Vector2Int GetMinEntropyCell()
    {
        // 가장 엔트로피가 낮은 셀의 좌표 반환 (동점일 경우 가중치 기반 랜덤을 적용합니다)
        return new Vector2Int(0, 0);
    }

    private void CollapseCell(Vector2Int _pos)
    {
        WFCCell cell = grid[_pos.x, _pos.y];
        if (true == cell.isCollapsed)
        {
            return;
        }

        // 가중치를 기반으로 하나의 타일 선택 후 Collapse 호출
        int selectedId = cell.possibleTiles[0]; // 예시용
        cell.Collapse(selectedId);

        propagationStack.Push(_pos);
    }

    private void Propagate()
    {
        while (0 < propagationStack.Count)
        {
            Vector2Int current = propagationStack.Pop();

            // 1. 현재 셀의 확정된 타일 또는 남은 후보들을 가져옵니다.
            // 2. 상하좌우 이웃 셀의 후보(m_possibleTiles) 중 인접 규칙에 맞지 않는 타일을 제거합니다.
            // 3. 이웃 셀의 후보가 하나라도 제거되었다면 해당 이웃을 m_propagationStack에 Push 합니다.
        }
    }
}
