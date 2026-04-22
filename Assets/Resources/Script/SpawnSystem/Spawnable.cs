using System;
using UnityEngine;
using UnityEngine.Pool;

public class Spawnable
    : MonoBehaviour
{
    private IObjectPool<Spawnable> pool;

    public BlackBoardHandler settingData;

    public void SetPool(IObjectPool<Spawnable> _pool)
    {
        pool = _pool;
    }
}
