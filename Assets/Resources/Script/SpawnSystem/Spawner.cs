using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// spawable ��ü�� �����ϴ� ������
/// </summary>
public class Spawner
    : MonoBehaviour
{
    [SerializeField]
    [Header("Managers")]
    private DropManager dropManager;

    [SerializeField]
    [Header("Spawn Setting")]
    private Transform spawnLocation;

    [SerializeField]
    private WaveData[] waveArray;

    private Dictionary<Spawnable, IObjectPool<Spawnable>> poolDict
        = new Dictionary<Spawnable, IObjectPool<Spawnable>>();

    // 
    [Header("Spawn Data")]
    public Transform target;
    public float speedRate;
    public float HPRate;
    public float damagerate;

    private void Awake()
    {
        if (null == waveArray)
        {
            return;
        }

        foreach (var wd in waveArray)
        {
            if (null == wd || null == wd.spwanArray)
            {
                continue;
            }

            foreach (var sd in wd.spwanArray)
            {
                if (null == sd.spawnObject)
                {
                    continue;
                }

                if (false == poolDict.ContainsKey(sd.spawnObject))
                {
                    Spawnable prefab = sd.spawnObject;
                    poolDict[prefab] = new ObjectPool<Spawnable>
                        (
                            createFunc: () => CreateObject(prefab)
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
        if (null == waveArray || 0 == waveArray.Length)
        {
            Debug.LogError("[Spawner] WaveData�� �����ϴ�.");
            return;
        }

        StartCoroutine(Wave());
    }

    private Spawnable CreateObject(Spawnable _sa)
    {
        Spawnable sa = Instantiate(_sa);
        sa.SetPool(poolDict[_sa]);

        Transform[] transforms = sa.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            t.gameObject.layer = gameObject.layer;
        }

        if (null != sa.blackBoardHandler)
        {
            BlackBoard data = sa.blackBoardHandler.GetBlackBoard();
            data.dropManager = dropManager;
        }

        return sa;
    }

    private void OnSpawn(Spawnable _object)
    {
        _object.gameObject.SetActive(true);
    }

    private void OnRelease(Spawnable _object)
    {
        _object.MarkReturningToPool();
        if (_object.gameObject.activeSelf)
        {
            _object.gameObject.SetActive(false);
        }
    }

    private void OnDespawn(Spawnable _object)
    {
        Destroy(_object.gameObject);
    }

    IEnumerator Spawn(SpawnData _data)
    {
        if (null == _data.spawnObject || false == poolDict.ContainsKey(_data.spawnObject))
        {
            yield break;
        }

        Transform origin = null != spawnLocation ? spawnLocation : transform;
        WaitForSeconds flag = new WaitForSeconds(Mathf.Max(0.01f, _data.spawnInterval));
        while (true)
        {
            Spawnable sa = poolDict[_data.spawnObject].Get();
            if (null == sa)
            {
                yield return flag;
                continue;
            }

            GameObject go = sa.gameObject;

            float dis = Random.Range(_data.spawnRange.x, _data.spawnRange.y);
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }

            Vector2 spawnPos = origin.position;
            spawnPos += dir * dis;

            go.transform.position = spawnPos;

            if (null == sa.blackBoardHandler)
            {
                Debug.LogError("it has no BlackBoardHandler");
                poolDict[_data.spawnObject].Release(sa);
                yield return flag;
                continue;
            }

            BlackBoard data = sa.blackBoardHandler.GetBlackBoard();
            if (null == data)
            {
                Debug.LogError("BlackBoardHandler has no data");
                poolDict[_data.spawnObject].Release(sa);
                yield return flag;
                continue;
            }

            data.dropManager = dropManager;
            data.SetFloat(DATA_TYPE.HPRate, HPRate);
            data.SetFloat(DATA_TYPE.moveSpeedRate, speedRate);
            data.SetFloat(DATA_TYPE.damageRate, damagerate);
            data.SetTransform(DATA_TYPE.moveTarget, target);
            sa.blackBoardHandler.Initialize();

            yield return flag;
        }
    }

    IEnumerator Wave()
    {
        List<Coroutine> spawCoroutine = new List<Coroutine>();

        for (int i = 0; i < waveArray.Length; i++)
        {
            if (null == waveArray[i] || null == waveArray[i].spwanArray)
            {
                continue;
            }

            foreach (Coroutine c in spawCoroutine)
            {
                if (null != c)
                {
                    StopCoroutine(c);
                }
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
