using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 무기의 발사 방식
/// </summary>
public enum WEAPON_TYPE
{
    /// <summary>
    /// 공격 간격(1 / FinalAttackSpeed) 마다 fireCount 개를 발사한다
    /// </summary>
    Repeat,
    /// <summary>
    /// fireCount 개를 사라지지 않게 유지한다. 공격 속도는 발사체의 같은 대상 재타격 간격이 된다
    /// </summary>
    Persistent,
}

/// <summary>
/// 무기 스펙을 가지고 발사체를 풀에서 꺼내 발사한다
/// Repeat 은 쿨타임마다 발사하고, Persistent 는 발사체를 fireCount 개 유지한다
/// 타겟 탐색 / 공격 로직은 Projectile 이 담당한다
/// - 무기 스텟(damage / attackSpeed / range) 은 플레이어 스텟과 따로 있으며, 실제 공격에는 둘을 더한 Final* 값을 쓴다
///   (주인 PlayerStats 가 없으면 무기 스텟 그대로)
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

    [Header("weapon stat (플레이어 스텟이 더해진다)")]
    [Tooltip("발사체 하나의 기본 데미지. 실제 데미지 = damage + 플레이어 Damage")]
    public float damage = 10f;
    [Tooltip("Repeat: 초당 공격 횟수 / Persistent: 같은 대상 초당 재타격 횟수. 실제 값 = attackSpeed + 플레이어 AttackSpeed")]
    public float attackSpeed = 1f;
    [Tooltip("타겟 탐색 범위. 실제 값 = range + 플레이어 Range")]
    public float range;

    [Header("weapon spec")]
    [Tooltip("Repeat: 한 번에 발사하는 발사체 수 / Persistent: 유지하는 발사체 수")]
    public int fireCount;
    public float speedRate;
    [Tooltip("발사체 하나가 타격할 적 수 (0 이하는 1). OneHitProjectile 이 사용")]
    public int hitCount = 1;
    [Tooltip("Repeat: 한 번에 여러 개를 발사할 때 각 발사 사이의 딜레이 (초). 0 이면 동시에 발사. 공격 속도에 비례해 줄어든다")]
    public float fireDelay;
    [Tooltip("방향성 공격의 랜덤 보정 범위. 타겟을 중심으로 반지름 spreadRange 인 원 안의 랜덤한 지점을 향해 산탄처럼 나간다. 0 이면 타겟 정중앙. Straight 가 사용")]
    [Min(0f)]
    public float spreadRange;

    [Header("chain spec")]
    [Tooltip("전이 횟수 (총 방문 수 = 1 + chainCount). ChainMovement 가 사용")]
    public int chainCount;
    [Tooltip("이전 타겟 기준 다음 타겟 탐색 범위. ChainMovement 가 사용")]
    public float chainRange;

    // 플레이어 스텟이 줄어도 공격이 완전히 멈추지 않도록 하는 최소 공격 속도
    private const float MinAttackSpeed = 0.1f;

    private IObjectPool<Projectile> projectilePool;

    private float timer = 0;

    // fireDelay 로 나눠 발사 중인 코루틴 (발사 중에는 다음 공격을 시작하지 않는다)
    private Coroutine fireRoutine;

    private List<Projectile> persistentProjectiles = new List<Projectile>();

    // 무기 스텟에 더할 주인 스텟 (없으면 무기 스텟만 사용)
    private PlayerStats ownerStats;

    #region Final Stat
    /// <summary>
    /// 무기 damage + 플레이어 Damage (0 미만은 0)
    /// </summary>
    public float FinalDamage => Mathf.Max(0f, damage + GetOwnerStat(ValueType.Damage));

    /// <summary>
    /// 무기 attackSpeed + 플레이어 AttackSpeed
    /// 무기 attackSpeed 가 0 이하면 공격하지 않는 무기로 보고 0, 아니면 최소 MinAttackSpeed
    /// </summary>
    public float FinalAttackSpeed
    {
        get
        {
            if (attackSpeed <= 0f)
            {
                return 0f;
            }
            return Mathf.Max(MinAttackSpeed, attackSpeed + GetOwnerStat(ValueType.AttackSpeed));
        }
    }

    /// <summary>
    /// Repeat: 공격 간격 (초) / Persistent: 같은 대상 재타격 간격 (초). 공격하지 않으면 0
    /// </summary>
    public float FinalCoolTime
    {
        get
        {
            float speed = FinalAttackSpeed;
            return speed > 0f ? 1f / speed : 0f;
        }
    }

    /// <summary>
    /// 무기 range + 플레이어 Range (0 미만은 0)
    /// </summary>
    public float FinalRange => Mathf.Max(0f, range + GetOwnerStat(ValueType.Range));

    /// <summary>
    /// 발사당 딜레이 (초). 무기 기본 공격 속도 대비 최종 공격 속도 배율만큼 줄어든다
    /// 한 번의 공격(fireCount 발)이 공격 간격 안에 끝나도록 FinalCoolTime / fireCount 를 넘지 않는다
    /// </summary>
    public float FinalFireDelay
    {
        get
        {
            float speed = FinalAttackSpeed;
            if (fireDelay <= 0f || speed <= 0f)
            {
                return 0f;
            }

            float delay = fireDelay * (attackSpeed / speed);
            return Mathf.Min(delay, FinalCoolTime / Mathf.Max(1, fireCount));
        }
    }

    private float GetOwnerStat(ValueType _type)
    {
        return null != ownerStats ? ownerStats.Get(_type) : 0f;
    }
    #endregion

    /// <summary>
    /// 무기 스텟에 더할 주인 스텟을 지정한다 (Awake 에서 부모의 PlayerStats 를 자동으로 찾는다)
    /// </summary>
    public void SetOwnerStats(PlayerStats _stats)
    {
        ownerStats = _stats;
    }

    /// <summary>
    /// fireCount 개를 발사한다. 발사당 딜레이가 있으면 딜레이 간격으로 나눠서 발사한다
    /// </summary>
    public void Fire()
    {
        if (null != fireRoutine)
        {
            return;
        }

        float delay = FinalFireDelay;
        if (delay <= 0f || fireCount <= 1)
        {
            for (int i = 0; i < fireCount; ++i)
            {
                if (false == FireOne(i, fireCount))
                {
                    // 타겟이 없으면 이번 발사는 전부 취소
                    return;
                }
            }
            return;
        }

        fireRoutine = StartCoroutine(FireRoutine(delay));
    }

    private IEnumerator FireRoutine(float _delay)
    {
        int count = fireCount;
        WaitForSeconds wait = new WaitForSeconds(_delay);
        for (int i = 0; i < count; ++i)
        {
            if (0 < i)
            {
                yield return wait;
            }

            if (false == FireOne(i, count))
            {
                // 타겟이 없으면 남은 발사는 취소
                break;
            }
        }
        fireRoutine = null;
    }

    /// <summary>
    /// 발사체 하나를 발사한다. 타겟이 없어 발사하지 못하면 false
    /// </summary>
    private bool FireOne(int _index, int _count)
    {
        Projectile p = projectilePool.Get();
        if (null == p)
        {
            return true;
        }

        p.SetFormation(_index, _count);
        WriteBlackBoard(p, _index, _count);
        if (false == p.Launch(this))
        {
            projectilePool.Release(p);
            return false;
        }
        return true;
    }

    private void Awake()
    {
        ownerStats = GetComponentInParent<PlayerStats>();

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
        // 꺼지면 코루틴이 멈추므로 발사 중 상태를 풀어준다
        fireRoutine = null;

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
        float coolTime = FinalCoolTime;
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
