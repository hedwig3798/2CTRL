using UnityEngine;

[System.Serializable]
public class Wave
{
    public float spawnInterval;
    public int minSpawnCount;
    public int maxSpawnCount;
    public string monster;
    public float minSpawnRange;
    public float maxSpawnRange;
}

[System.Serializable]
public class WaveData
{
    public float totalDuration;
    public Wave[] waves;
}
