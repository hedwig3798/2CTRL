using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Animations;
using UnityEngine;

public class MonsterGenerator
    : MonoBehaviour
{
    [SerializeField]
    private Transform target;

    [SerializeField]
    private AnimatorOverrideController animatorController;

    [SerializeField]
    private MonsterDatabase monsterDatabase;

    [SerializeField]
    private WaveDataList waveDataList;

    public Monster defaultMonster;

    public string objectLayer;
    public string renderOrder;
    public int preLoadCount;
    public int sortingOder;

    private float time = 0;
    private List<float> interval = new();
    private int waveNumber = 0;

    public Queue<Monster> monsterPool = new();

    public LayerMask layermask;
    private void Awake()
    {
        PreloadMonster(preLoadCount);

        while (interval.Count() <= 5)
        {
            interval.Add(0);  
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        float dt = Time.deltaTime;
        // 웨이브 시간과 스폰 타임 설정
        time += dt;

        Wave[] waves = waveDataList.waveList[waveNumber].waves;
        for (int i = 0; i < waveDataList.waveList[waveNumber].waves.Count(); i++)
        {
            interval[i] += dt;
            // spawn check
            if (interval[i] >= waves[i].spawnInterval)
            {
                interval[i] -= waves[i].spawnInterval;

                // Spaw Monster
                int spawnCount = UnityEngine.Random.Range(waves[i].minSpawnCount, waves[i].maxSpawnCount);

                // 만일 몬스터 풀의 남은게 적다면 추가 생산 
                if (spawnCount > monsterPool.Count)
                {
                    Debug.LogWarning($"Too Many Monster In Field. Load {spawnCount} Monster Object");
                    break;
                }

                // 몬스터 생성
                if (false == monsterDatabase.monsterMap.ContainsKey(waves[i].monster))
                {
                    Debug.LogError($"No Monster Exist : {waves[i].monster}");
                    break;
                }

                MonsterData monsterData = monsterDatabase.monsterMap[waves[i].monster];

                for (int j = 0; j < spawnCount; j++)
                {
                    // 큐에서 컴포넌트 가져와서 활성화 후 데이터 등록
                    Monster monster = monsterPool.Dequeue();
                    monster.SetTartgetTransform(target);
                    monster.id = monsterData.name;
                    monster.SetStats(monsterData.stats);
                    monster.SetExcuteAction(() => monsterData.ai.Excute(monster));
                    monster.SetReadyAction(() => monsterData.ai.Ready(monster));
                    monster.SetLayerMask(layermask);
                    // monsterData.ai.CaclulateDirection(monster);

                    if (null != monsterData.runClip)
                    {
                        monster.SetAnimationClip("Run", monsterData.runClip);
                    }

                    Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                    monster.gameObject.transform.position = (Vector2)target.position + (dir * UnityEngine.Random.Range(waves[i].minSpawnRange, waves[i].maxSpawnRange));
                    monster.gameObject.layer = gameObject.layer;
                    monster.gameObject.SetActive(true);

                    monster.monsterGenerator = this;
                }
            }
        }

      
        
        if (time > waveDataList.waveList[waveNumber].totalDuration)
        {
            Debug.Log("Wave Process");
        
            waveNumber++;
        }
    }

    private void PreloadMonster(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Monster m = Instantiate(defaultMonster);
            monsterPool.Enqueue(m);

            m.gameObject.SetActive(false);
            m.gameObject.transform.SetParent(transform, false);
        }
    }

    public void ReturnMonster(Monster _monster)
    {
        monsterPool.Enqueue(_monster);
        _monster.gameObject.SetActive(false);
    }
}
