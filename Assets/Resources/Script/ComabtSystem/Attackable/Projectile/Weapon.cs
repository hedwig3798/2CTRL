using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 무기 스펙을 가지고 쿨타임마다 발사체를 풀에서 꺼내 발사한다
/// 타겟 탐색 / 공격 로직은 Projectile 이 담당한다
/// </summary>
public class Weapon
    : MonoBehaviour
{
    [Header("weapon")]
    public Transform owner;

    [Header("pool preload coutn")]
    public int preLoadCount;

    [Header("projectile")]
    public Projectile projectile;

    [Header("target layer")]
    public LayerMask targetLayer;

    [Header("weapon spec")]
    [Tooltip("한 번에 발사하는 발사체 수")]
    public int fireCount;
    [Tooltip("공격 간격 (초)")]
    public float coolTime;
    [Tooltip("타겟 탐색 범위")]
    public float range;
    public float damageRate;
    public float speedRate;

    private IObjectPool<Projectile> projectilePool;

    private float timer = 0;

    public void Fire()
    {
        for (int i = 0; i < fireCount; ++i)
        {
            Projectile p = projectilePool.Get();
            if (null == p)
            {
                continue;
            }

            if (false == p.Launch(this))
            {
                // 타겟이 없으면 이번 발사는 전부 취소
                projectilePool.Release(p);
                return;
            }
        }
    }

    private void Awake()
    {
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
            Fire();
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
