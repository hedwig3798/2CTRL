using UnityEngine;
using UnityEngine.Pool;

public class Item 
    : MonoBehaviour
{
    private IObjectPool<Item> pool;

    public void SetPool(IObjectPool<Item> _pool)
    {
        pool = _pool;
    }

    private void OnDisable()
    {
        pool.Release(this);
    }
}
