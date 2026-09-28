using UnityEngine;
using UnityEngine.Pool;

public class Projectile
    : MonoBehaviour
    , Initializable
{
    public BlackBoardHandler blackBoardHandler;

    public ProjectileWeapon owner;

    public float baseDamage;

    private DamageMassage damageMassage;
    private IObjectPool<Projectile> pool;
    private bool returningToPool;

    public void SetPool(IObjectPool<Projectile> _pool)
    {
        pool = _pool;
    }

    public void MarkReturningToPool()
    {
        returningToPool = true;
    }

    public void Initialize(BlackBoard _data)
    {
        float rate = _data.GetFloat(DATA_TYPE.damageRate);
        if (rate <= 0f)
        {
            rate = 1f;
        }
        damageMassage.damage = rate * baseDamage;
        damageMassage.isReflected = false;
        damageMassage.attacker = null;
    }

    private void Awake()
    {
        if (null == blackBoardHandler)
        {
            blackBoardHandler = GetComponent<BlackBoardHandler>();
        }
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == gameObject.layer)
        {
            return;
        }

        if (other.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            DamageMassage duplicateMassage = damageMassage;
            dp.ProcessDamage(ref duplicateMassage);
            gameObject.SetActive(false);
        }
    }
}
