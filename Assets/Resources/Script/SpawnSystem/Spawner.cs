using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Pool;

public class Spawner
    : MonoBehaviour
{
    public SpawnableInitData settingData;

    [SerializeField]
    private Transform spawnLocation;

    [SerializeField]
    private WaveData[] waveArray;

    private Dictionary<Spawnable, IObjectPool<Spawnable>> poolDict = new Dictionary<Spawnable, IObjectPool<Spawnable>>();

    private void Awake()
    {
        foreach (var wd in waveArray)
        {
            foreach (var sd in wd.spwanArray)
            {
                if (false == poolDict.ContainsKey(sd.spawnObject))
                {
                    sd.spawnObject.gameObject.SetActive(false);

                    poolDict[sd.spawnObject] = new ObjectPool<Spawnable>
                        (
                            createFunc: () => CreateObject(sd.spawnObject)
                            , OnSpawn
                            , OnRelease
                            , OnDespawn
                            , true
                            , 100
                            , 200
                        );
                }
            }
        }
    }

    private void Start()
    {
        StartCoroutine(Wave());
    }

    private Spawnable CreateObject(Spawnable _sa)
    {
        Spawnable sa = Instantiate(_sa);
        sa.gameObject.SetActive(false);
        sa.SetPool(poolDict[_sa]);
        sa.settingData = settingData;
        sa.gameObject.tag = gameObject.tag;

        return sa;
    }

    private void OnSpawn(Spawnable _object)
    {
        _object.gameObject.SetActive(true);
    }

    private void OnRelease(Spawnable _object)
    {
        _object.gameObject.SetActive(false);
    }

    private void OnDespawn(Spawnable _object)
    {
        Destroy(_object.gameObject);
    }

    IEnumerator Spawn(SpawnData _data)
    {
        WaitForSeconds flag = new WaitForSeconds(_data.spawnInterval);
        while (true)
        {
            Spawnable sa = poolDict[_data.spawnObject].Get();

            GameObject go = sa.gameObject;

            float dis = Random.Range(_data.spawnRange.x, _data.spawnRange.y);
            Vector2 dir = Random.insideUnitCircle.normalized;

            Vector2 spawnPos = spawnLocation.position;
            spawnPos += dir * dis;

            go.transform.position = spawnPos;

            sa.OnSpawn(settingData);

            go.SetActive(true);

            yield return flag;
        }
    }

    IEnumerator Wave()
    {
        List<Coroutine> spawCoroutine = new List<Coroutine>();

        for (int i = 0; i < waveArray.Length; i++)
        {
            foreach (Coroutine c in spawCoroutine)
            {
                StopCoroutine(c);
            }
            spawCoroutine.Clear();

            for (int j = 0; j < waveArray[i].spwanArray.Length; j++)
            {
                spawCoroutine.Add(StartCoroutine(Spawn(waveArray[i].spwanArray[j])));
            }

            yield return new WaitForSeconds(waveArray[i].time);

        }
    }
}
