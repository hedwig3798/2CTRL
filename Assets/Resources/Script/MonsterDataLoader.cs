using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MonsterData
{
    public string id = "default";
    public string ai = "chasing";
    public int hp = 1;
    public int atk = 1;
    public int exp = 1;
    public string sprite = "None";
    public float speed = 1;
}

[Serializable]
public class MonsterJsonWrapper
{
    public MonsterData[] monsters;
}

[Serializable]
public class MonsterDataList
{
    public List<MonsterData> monsters;
}

public class MonsterDataLoader : MonoBehaviour
{
    public static MonsterDataLoader instance;
    public Dictionary<string, MonsterData> monsterMap;

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
        TextAsset jsonFile = Resources.Load<TextAsset>("Data/MonsterData");
        if (jsonFile == null)
        {
            Debug.LogError("MonsterData.json not found!");
            return;
        }
        Debug.Log(jsonFile.text);

        MonsterDataList dataList = JsonUtility.FromJson<MonsterDataList>(jsonFile.text);
        monsterMap = new Dictionary<string, MonsterData>();

        foreach (var monster in dataList.monsters)
        {
            monsterMap[monster.id] = monster;
        }

        Debug.Log($"Loaded {monsterMap.Count} monsters.");
    }
}
