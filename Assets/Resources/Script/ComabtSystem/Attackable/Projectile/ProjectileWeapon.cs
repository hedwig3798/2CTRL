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

    private Collider2D[] candinate = new Collider2D[20];
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
            if (p != null)
            {
                BlackBoard b = p.blackBoardHandler.GetBlackBoard();
                b.SetTransform(DATA_TYPE.moveTarget, FindNerest());
                b.SetFloat(DATA_TYPE.moveSpeedRate, speedRate);
                b.SetFloat(DATA_TYPE.damageRate, damageRate);
            }
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

        if (0 == hitCount)
        {
            return result;
        }

        float minDistance = Mathf.Infinity;
        result = candinate[0].transform;
        for (int i = 1; i < hitCount; ++i)
        {
            Transform curr = candinate[i].transform;

            float currDistance = (result.position - transform.position).sqrMagnitude;
            if (currDistance < minDistance)
            {
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
        timer += Time.deltaTime;

        if (timer >= coolTime)
        {
            timer -= coolTime;

            Attack(FindNerest());
        }
    }

    private Projectile CreateObject(Projectile _projectile)
    {
        Projectile projectile = Instantiate(_projectile);
        Transform[] transforms = projectile.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            t.gameObject.layer = gameObject.layer;
        }
        return projectile;
    }

    private void OnSpawn(Projectile _object)
    {
        _object.gameObject.SetActive(true);
    }

    private void OnRelease(Projectile _object)
    {
        _object.gameObject.SetActive(false);
    }

    private void OnDespawn(Projectile _object)
    {
        Destroy(_object.gameObject);
    }
}
