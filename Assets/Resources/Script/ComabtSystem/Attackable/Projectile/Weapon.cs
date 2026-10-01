using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 무기의 발사 방식
/// </summary>
public enum WEAPON_TYPE
{
    /// <summary>
    /// coolTime 마다 fireCount 개를 발사한다
    /// </summary>
    Repeat,
    /// <summary>
    /// fireCount 개를 사라지지 않게 유지한다. coolTime 은 발사체의 같은 대상 재타격 간격이 된다
    /// </summary>
    Persistent,
}

/// <summary>
/// 무기 스펙을 가지고 발사체를 풀에서 꺼내 발사한다
/// Repeat 은 쿨타임마다 발사하고, Persistent 는 발사체를 fireCount 개 유지한다
/// 타겟 탐색 / 공격 로직은 Projectile 이 담당한다
/// </summary>
public class Weapon
    : MonoBehaviour
{
    [Header("weapon")]
    public Transform owner;
    public WEAPON_TYPE weaponType;

    [Header("pool preload coutn")]
    public int preLoadCount;

    [Header("projectile")]
    public Projectile projectile;

    [Header("target layer")]
    public LayerMask targetLayer;

    [Header("weapon spec")]
    [Tooltip("Repeat: 한 번에 발사하는 발사체 수 / Persistent: 유지하는 발사체 수")]
    public int fireCount;
    [Tooltip("Repeat: 공격 간격 (초) / Persistent: 같은 대상 재타격 간격 (초)")]
    public float coolTime;
    [Tooltip("타겟 탐색 범위")]
    public float range;
    public float damageRate;
    public float speedRate;
    [Tooltip("발사체 하나가 타격할 적 수 (0 이하는 1). OneHitProjectile 이 사용")]
    public int hitCount = 1;

    [Header("chain spec")]
    [Tooltip("전이 횟수 (총 방문 수 = 1 + chainCount). ChainMovement 가 사용")]
    public int chainCount;
    [Tooltip("이전 타겟 기준 다음 타겟 탐색 범위. ChainMovement 가 사용")]
    public float chainRange;

    private IObjectPool<Projectile> projectilePool;

    private float timer = 0;

    private List<Projectile> persistentProjectiles = new List<Projectile>();

    public void Fire()
    {
        for (int i = 0; i < fireCount; ++i)
        {
            Projectile p = projectilePool.Get();
            if (null == p)
            {
                continue;
            }

            p.SetFormation(i, fireCount);
            WriteBlackBoard(p, i, fireCount);
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
        switch (weaponType)
        {
            case WEAPON_TYPE.Repeat:
                UpdateRepeat();
                break;
            case WEAPON_TYPE.Persistent:
                UpdatePersistent();
                break;
        }
    }

    private void OnDisable()
    {
        // 무기가 꺼지면 유지 중인 발사체도 풀로 돌려보낸다
        foreach (Projectile p in persistentProjectiles)
        {
            if (null != p && true == p.gameObject.activeSelf)
            {
                p.gameObject.SetActive(false);
            }
        }
        persistentProjectiles.Clear();
    }

    private void UpdateRepeat()
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

    /// <summary>
    /// 발사체 수를 fireCount 에 맞춘다
    /// 풀로 돌아간 발사체는 목록에서 빼고, 부족하면 새로 발사, 넘치면 풀로 반환한다
    /// 발사체 수가 바뀌면 남은 발사체들의 순서를 다시 매긴다
    /// </summary>
    private void UpdatePersistent()
    {
        bool changed = 0 < persistentProjectiles.RemoveAll(p => null == p || false == p.gameObject.activeSelf);

        while (persistentProjectiles.Count < fireCount)
        {
            Projectile p = projectilePool.Get();
            if (null == p)
            {
                break;
            }

            p.SetFormation(persistentProjectiles.Count, fireCount);
            WriteBlackBoard(p, persistentProjectiles.Count, fireCount);
            if (false == p.Launch(this))
            {
                projectilePool.Release(p);
                break;
            }

            persistentProjectiles.Add(p);
            changed = true;
        }

        while (persistentProjectiles.Count > Mathf.Max(0, fireCount))
        {
            int last = persistentProjectiles.Count - 1;
            Projectile p = persistentProjectiles[last];
            persistentProjectiles.RemoveAt(last);
            p.gameObject.SetActive(false);
            changed = true;
        }

        if (true == changed)
        {
            for (int i = 0; i < persistentProjectiles.Count; ++i)
            {
                Projectile p = persistentProjectiles[i];
                p.SetFormation(i, persistentProjectiles.Count);
                WriteBlackBoard(p, i, persistentProjectiles.Count);
                p.InitializeBlackBoard();
            }
        }
    }

    /// <summary>
    /// 발사체 BlackBoard 에 무기 공통 정보를 기록한다
    /// moveTarget 은 기본값(주인)이며, 발사체가 OnLaunch 에서 덮어쓸 수 있다
    /// </summary>
    private void WriteBlackBoard(Projectile _projectile, int _index, int _count)
    {
        BlackBoard b = _projectile.GetBlackBoard();
        if (null == b)
        {
            return;
        }

        b.SetTransform(DATA_TYPE.startPosition, transform);
        b.SetTransform(DATA_TYPE.moveTarget, null != owner ? owner : transform);
        b.SetFloat(DATA_TYPE.moveSpeedRate, speedRate);
        b.SetFloat(DATA_TYPE.damageRate, damageRate);
        b.SetFloat(DATA_TYPE.formationIndex, _index);
        b.SetFloat(DATA_TYPE.formationCount, _count);
        b.SetInt(DATA_TYPE.targetLayer, targetLayer.value);
        b.SetInt(DATA_TYPE.hitCount, hitCount);
        b.SetInt(DATA_TYPE.chainCount, chainCount);
        b.SetFloat(DATA_TYPE.chainRange, chainRange);
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
