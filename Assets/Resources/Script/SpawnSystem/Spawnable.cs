using System;
using UnityEngine;
using UnityEngine.Pool;

public class Spawnable
    : MonoBehaviour
{
    private IObjectPool<Spawnable> pool;

    public BlackBoardHandler blackBoardHandler;

    public void SetPool(IObjectPool<Spawnable> _pool)
    {
        pool = _pool;
    }

    private void Awake()
    {
        blackBoardHandler = gameObject.GetComponent<BlackBoardHandler>();
    }
}
