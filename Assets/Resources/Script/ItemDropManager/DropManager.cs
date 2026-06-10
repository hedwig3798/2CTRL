using UnityEngine;
using UnityEngine.Pool;

public class DropManager
    : MonoBehaviour
{
    public GameObject dropItem;

    private IObjectPool<GameObject> dropItemPool;

    public void DropItem(GameObject _deadObject)
    {
        GameObject item = dropItemPool.Get();
        item.layer = gameObject.layer;
        item.transform.position = _deadObject.transform.position;
    }

    private void Awake()
    {
        if (null == dropItem)
        {
            Debug.LogError("DropManager has no item");
            return;
        }

        dropItemPool = new ObjectPool<GameObject>
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

    private GameObject CreateObject(GameObject _object)
    {
        return Instantiate(_object);
    }

    private void OnSpawn(GameObject _object)
    {
        _object.SetActive(true);
    }

    private void OnRelease(GameObject _object)
    {
        _object.SetActive(false);
    }

    private void OnDespawn(GameObject _object)
    {
        Destroy(_object.gameObject);
    }
}
