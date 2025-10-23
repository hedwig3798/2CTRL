using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MonsterSpawnData
{
    public string id;
    public float spawn_rate;
    public int[] spawn_count;
}

[Serializable]
public class WaveData
{
    public int id;
    public float duration;
    public float spawn_interval;
    public List<MonsterSpawnData> monsters;
}

[Serializable]
public class WaveList
{
    public List<WaveData> wave;
}

public class WavedataLoader : MonoBehaviour
{
    public static WavedataLoader instance;
    public WaveList waves;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Data/MonsterWave");
        if (jsonFile == null)
        {
            Debug.LogError("MonsterWave.json not found!");
            return;
        }
        Debug.Log(jsonFile.text);

        waves = JsonUtility.FromJson<WaveList>(jsonFile.text);

        Debug.Log($"Loaded {waves.wave.Count} wave.");
    }
}
