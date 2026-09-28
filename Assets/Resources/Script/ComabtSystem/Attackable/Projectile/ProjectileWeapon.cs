using UnityEngine;
using UnityEngine.Pool;

public class ProjectileWeapon
    : MonoBehaviour
    , IAttackable
{
    [Header("weapon")]
    public Transform owner;

    [Header("pool preload coutn")]
    public int preLoadCount;

    [Header("projectile")]
    public Projectile projectile;

    [Header("target layer")]
    public LayerMask targetLayer;

    [Header("weapon damage")]
    public int fireCount;
    public float coolTime;
    public float range;
    public float damageRate;
    public float speedRate;

    private IObjectPool<Projectile> projectilePool;

    private Collider2D[] candinate = new Collider2D[64];
    private ContactFilter2D filter = new ContactFilter2D();

    private float timer = 0;

    public void Attack(Transform _target)
    {
        if (null == _target)
        {
            return;
        }

        for (int i = 0; i < fireCount; ++i)
        {
            Projectile p = projectilePool.Get();
            if (null == p || null == p.blackBoardHandler)
            {
                continue;
            }

            BlackBoard b = p.blackBoardHandler.GetBlackBoard();
            b.SetTransform(DATA_TYPE.moveTarget, _target);
            b.SetTransform(DATA_TYPE.startPosition, transform);
            b.SetFloat(DATA_TYPE.moveSpeedRate, speedRate);
            b.SetFloat(DATA_TYPE.damageRate, damageRate);
            p.owner = this;
            p.blackBoardHandler.Initialize();
        }
    }

    private Transform FindNerest()
    {
        Transform result = null;

        int hitCount = Physics2D.OverlapCircle(
            transform.position
            , range
            , filter
            , candinate
        );

        if (0 >= hitCount)
        {
            return result;
        }

        float minDistance = float.MaxValue;
        for (int i = 0; i < hitCount; ++i)
        {
            Transform curr = candinate[i].transform;
            if (curr == transform || (null != owner && curr == owner))
            {
                continue;
            }

            float currDistance = (curr.position - transform.position).sqrMagnitude;
            if (currDistance < minDistance)
            {
                minDistance = currDistance;
                result = curr;
            }
        }

        return result;
    }

    private void Awake()
    {
        filter.useLayerMask = true;
        filter.useTriggers = true;
        filter.SetLayerMask(targetLayer);

        projectilePool = new ObjectPool<Projectile>
            (
                createFunc: () => CreateObject(projectile)
                , OnSpawn
                , OnRelease
                , OnDespawn
                , true
                , 100
                , 200
            );
    }

    private void Update()
    {
        if (coolTime <= 0f)
        {
            return;
        }

        timer += Time.deltaTime;

        if (timer >= coolTime)
        {
            timer -= coolTime;
            Attack(FindNerest());
        }
    }

    private Projectile CreateObject(Projectile _projectile)
    {
        Projectile created = Instantiate(_projectile);
        created.SetPool(projectilePool);
        Transform[] transforms = created.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            t.gameObject.layer = gameObject.layer;
        }
        return created;
    }

    private void OnSpawn(Projectile _object)
    {
        _object.gameObject.SetActive(true);
    }

    private void OnRelease(Projectile _object)
    {
        _object.MarkReturningToPool();
        if (_object.gameObject.activeSelf)
        {
            _object.gameObject.SetActive(false);
        }
    }

    private void OnDespawn(Projectile _object)
    {
        Destroy(_object.gameObject);
    }
}
