using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SpawnData
{
    public Spawnable spawnObject;
    public Vector2Int count;
    public Vector2 spawnRange;
    public float spawnInterval;
}
