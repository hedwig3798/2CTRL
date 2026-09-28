using UnityEngine;
using UnityEngine.Pool;

public class Spawnable
    : MonoBehaviour
{
    private IObjectPool<Spawnable> pool;
    private bool returningToPool;

    public BlackBoardHandler blackBoardHandler;

    public void SetPool(IObjectPool<Spawnable> _pool)
    {
        pool = _pool;
    }

    public void MarkReturningToPool()
    {
        returningToPool = true;
    }

    private void Awake()
    {
        if (null == blackBoardHandler)
        {
            blackBoardHandler = gameObject.GetComponent<BlackBoardHandler>();
        }
    }

    private void OnEnable()
    {
        returningToPool = false;
    }

    private void OnDisable()
    {
        ReturnToPool();
    }

    public void ReturnToPool()
    {
        if (returningToPool || null == pool)
        {
            return;
        }

        returningToPool = true;
        pool.Release(this);
    }
}
