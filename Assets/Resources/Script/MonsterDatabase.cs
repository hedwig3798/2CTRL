using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Scriptable Objects/MonsterDatabase")]
public class MonsterDatabase : ScriptableObject
{
    public MonsterData[] monsterDatas;

    [HideInInspector]
    public Dictionary<string, MonsterData> monsterMap;

    void OnEnable()
    {
        monsterMap = new Dictionary<string, MonsterData>();

        foreach (var monsterData in monsterDatas)
        {
            monsterMap[monsterData.name] = monsterData;
        }
    }
}
