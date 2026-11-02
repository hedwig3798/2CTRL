using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 몬스터가 죽으면 EXP 아이템을 떨어뜨린다
/// - 아이템의 expRate = 죽은 몬스터의 MonsterData.expValue (MonsterData 가 없으면 defaultExpRate)
///   expValue 는 Spawner 가 스폰할 때 BlackBoard 에 넣고 HealthSystem 이 저장해 둔 값을 읽는다 (죽을 때 계층 탐색을 하지 않음)
/// - 꺼낸 아이템은 BlackBoard 에 expRate 를 넣은 뒤 Initialize 한다 (Get → expRate 기록 → Initialize)
/// </summary>
public class DropManager
    : MonoBehaviour
{
    public Spawnable dropItem;

    [Tooltip("MonsterData 가 없는 몬스터(박쥐 등) 의 EXP 배율")]
    [Min(0f)]
    [SerializeField]
    private float defaultExpRate = 1f;

    private IObjectPool<Spawnable> dropItemPool;

    public void DropItem(GameObject _deadObject)
    {
        if (null == dropItemPool || null == _deadObject)
        {
            return;
        }

        Spawnable item = dropItemPool.Get();
        if (null == item)
        {
            return;
        }

        LayerUtils.SetLayer(item.gameObject, gameObject.layer);
        item.transform.position = _deadObject.transform.position;

        if (null != item.blackBoardHandler)
        {
            item.blackBoardHandler.GetBlackBoard().SetFloat(DATA_TYPE.expRate, GetExpRate(_deadObject));
            item.blackBoardHandler.Initialize();
        }
    }

    /// <summary>
    /// 죽은 몬스터의 EXP 배율 (계층 탐색 없이 같은 오브젝트의 컴포넌트만 본다)
    /// 1. HealthSystem 이 스폰 때 저장한 값 (Spawner 가 MonsterData.expValue 로 넣음)
    /// 2. 같은 오브젝트의 MonsterBrain.Data.expValue (Spawner 를 거치지 않은 몬스터)
    /// 3. 없거나 0 이하면 defaultExpRate
    /// </summary>
    private float GetExpRate(GameObject _deadObject)
    {
        float rate = 0f;
        if (true == _deadObject.TryGetComponent(out HealthSystem health))
        {
            rate = health.ExpRate;
        }

        if (rate <= 0f && true == _deadObject.TryGetComponent(out MonsterBrain brain) && null != brain.Data)
        {
            rate = brain.Data.expValue;
        }

        return rate > 0f ? rate : defaultExpRate;
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

        LayerUtils.SetLayer(sa.gameObject, gameObject.layer);

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
}
