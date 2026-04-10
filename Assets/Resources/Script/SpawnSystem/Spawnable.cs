using System;
using UnityEngine;
using UnityEngine.Pool;

public class Spawnable
    : MonoBehaviour
{
    private IObjectPool<Spawnable> pool;

    public SpawnableInitData settingData;

    public event Action OnSpawnEvent;

    public void SetPool(IObjectPool<Spawnable> _pool)
    {
        pool = _pool;
    }

    public void OnSpawn(SpawnableInitData _data)
    {
        settingData = _data;
        OnSpawnEvent?.Invoke();
    }

}
