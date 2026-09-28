using UnityEngine;
using UnityEngine.Pool;

public class Item
    : MonoBehaviour
{
    private IObjectPool<Item> pool;
    private bool returningToPool;

    public void SetPool(IObjectPool<Item> _pool)
    {
        pool = _pool;
    }

    public void MarkReturningToPool()
    {
        returningToPool = true;
    }

    private void OnEnable()
    {
        returningToPool = false;
    }

    private void OnDisable()
    {
        if (returningToPool || null == pool)
        {
            return;
        }

        returningToPool = true;
        pool.Release(this);
    }
}
