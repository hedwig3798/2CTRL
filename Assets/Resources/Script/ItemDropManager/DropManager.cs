using UnityEngine;
using UnityEngine.Pool;

public class DropManager
    : MonoBehaviour
{
    public Spawnable dropItem;

    private IObjectPool<Spawnable> dropItemPool;

    public void DropItem(GameObject _deadObject)
    {
        Spawnable item = dropItemPool.Get();
        item.gameObject.layer = gameObject.layer;
        item.transform.position = _deadObject.transform.position;
    }

    private void Awake()
    {
        if (null == dropItem)
        {
            Debug.LogError("DropManager has no item");
            return;
        }

        dropItemPool = new ObjectPool<Spawnable>
        (
            createFunc: () => CreateObject(dropItem)
            , OnSpawn
            , OnRelease
            , OnDespawn
            , true
            , 100
            , 200
        );
    }

    private Spawnable CreateObject(Spawnable _object)
    {
        Spawnable sa = Instantiate(_object);
        sa.SetPool(dropItemPool);


        return sa;
    }

    private void OnSpawn(Spawnable _object)
    {
        _object.blackBoardHandler.GetBlackBoard().SetFloat(DATA_TYPE.expRate, 1.0f);
        _object.blackBoardHandler.Initialize();
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
}
