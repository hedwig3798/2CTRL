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
    [Tooltip("발사체 수명 (초). 0 이면 발사체의 Lifetime 기본값. Lifetime 컴포넌트가 사용")]
    [Min(0f)]
    public float lifetime;
    [Tooltip("타격한 적을 밀어내는 힘 (밀려나는 초기 속도). 0 이면 넉백 없음. Knockback 컴포넌트가 사용")]
    [Min(0f)]
    public float knockbackForce;

    [Header("chain spec")]
    [Tooltip("전이 횟수 (총 방문 수 = 1 + chainCount). ChainMovement 가 사용")]
    public int chainCount;
    [Tooltip("이전 타겟 기준 다음 타겟 탐색 범위. ChainMovement 가 사용")]
    public float chainRange;

    [Header("area spec")]
    [Tooltip("범위 공격(폭발, 장판, 충격파 등)의 반지름. Projectile.ApplyAreaDamage / ScaleOverLifetime 등이 사용")]
    [Min(0f)]
    public float areaRadius;

    // 플레이어 스텟이 줄어도 공격이 완전히 멈추지 않도록 하는 최소 공격 속도
    private const float MinAttackSpeed = 0.1f;

    private IObjectPool<Projectile> projectilePool;

    // FireSub 로 발사하는 보조 발사체 풀 (프리팹별로 처음 쓸 때 만든다)
    private Dictionary<Projectile, IObjectPool<Projectile>> subPools = new Dictionary<Projectile, IObjectPool<Projectile>>();

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
    public PlayerStats OwnerStats => ownerStats;

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

        EnsureBaseValues();

        projectilePool = CreatePool(projectile);
    }

    #region Upgrade
    // 업그레이드 누적치 (WeaponParam 값을 인덱스로 쓴다)
    private readonly float[] paramAdd = new float[WeaponParamUtils.SlotCount];
    private readonly float[] paramMultiply = new float[WeaponParamUtils.SlotCount];

    // 프리팹 기본값 (업그레이드 계산 기준)
    private bool isBaseCached;
    private float baseDamage;
    private float baseAttackSpeed;
    private float baseRange;
    private int baseFireCount;
    private int baseHitCount;
    private float baseAreaRadius;
    private float baseLifetime;
    private float baseKnockbackForce;
    private float baseSpeedRate;
    private float baseFireDelay;
    private int baseChainCount;
    private float baseChainRange;

    private int upgradeLevel;
    private int upgradeVersion;

    // 유지형 발사체에 바뀐 값을 다시 기록해야 하는지
    private bool isUpgradeDirty;

    /// <summary>
    /// 적용한 업그레이드 단계 수
    /// </summary>
    public int UpgradeLevel => upgradeLevel;

    /// <summary>
    /// 업그레이드 누적치가 바뀔 때마다 1 씩 오른다 (발사체가 바뀐 값을 다시 읽을지 판단할 때 쓴다)
    /// </summary>
    public int UpgradeVersion => upgradeVersion;

    /// <summary>
    /// 업그레이드 한 단계를 적용한다 (수정 목록을 누적하고 UpgradeLevel 을 1 올린다)
    /// </summary>
    public void ApplyUpgrade(WeaponUpgradeStep _step)
    {
        if (null == _step)
        {
            return;
        }

        WeaponModifier[] modifiers = _step.Modifiers;
        if (null != modifiers)
        {
            foreach (WeaponModifier m in modifiers)
            {
                AccumulateModifier(m.param, m.mode, m.value);
            }
        }

        ++upgradeLevel;
        MarkUpgradeChanged();
    }

    /// <summary>
    /// 수정 하나를 누적한다
    /// Add 는 기본값에 그대로 더하고, Multiply 는 기본값 대비 비율을 더한다
    /// </summary>
    public void AddModifier(WeaponParam _param, StatModifierMode _mode, float _value)
    {
        if (false == AccumulateModifier(_param, _mode, _value))
        {
            return;
        }
        MarkUpgradeChanged();
    }

    /// <summary>
    /// 누적치를 _add / _multiply 로 덮어쓴다 (드론이 자식 무기에 값을 넘길 때 쓴다)
    /// 값이 같으면 아무것도 하지 않는다
    /// </summary>
    public void SetModifier(WeaponParam _param, float _add, float _multiply)
    {
        int i = (int)_param;
        if (false == IsValidSlot(i))
        {
            return;
        }

        if (paramAdd[i] == _add && paramMultiply[i] == _multiply)
        {
            return;
        }

        paramAdd[i] = _add;
        paramMultiply[i] = _multiply;
        RefreshParam(_param);
        MarkUpgradeChanged();
    }

    /// <summary>
    /// 모든 업그레이드를 지우고 공통값을 프리팹 기본값으로 되돌린다
    /// </summary>
    public void ResetUpgrades()
    {
        EnsureBaseValues();

        System.Array.Clear(paramAdd, 0, paramAdd.Length);
        System.Array.Clear(paramMultiply, 0, paramMultiply.Length);
        foreach (WeaponParam p in WeaponParamUtils.CommonParams)
        {
            RefreshParam(p);
        }

        upgradeLevel = 0;
        MarkUpgradeChanged();
    }

    /// <summary>
    /// Add 누적치
    /// </summary>
    public float GetParamAdd(WeaponParam _param)
    {
        int i = (int)_param;
        return IsValidSlot(i) ? paramAdd[i] : 0f;
    }

    /// <summary>
    /// Multiply 누적치 (기본값 대비 비율)
    /// </summary>
    public float GetParamMultiply(WeaponParam _param)
    {
        int i = (int)_param;
        return IsValidSlot(i) ? paramMultiply[i] : 0f;
    }

    /// <summary>
    /// 공통값은 현재 Weapon 필드 값, 그 외(발사체 고유값 / 플래그)는 기본값 0 기준의 값 (= Add 누적치)
    /// 발사체 고유값은 기본값을 넘기는 GetParam(_param, _baseValue) 를 쓴다
    /// </summary>
    public float GetParam(WeaponParam _param)
    {
        switch (_param)
        {
            case WeaponParam.Damage: return damage;
            case WeaponParam.AttackSpeed: return attackSpeed;
            case WeaponParam.Range: return range;
            case WeaponParam.FireCount: return fireCount;
            case WeaponParam.HitCount: return hitCount;
            case WeaponParam.AreaRadius: return areaRadius;
            case WeaponParam.Lifetime: return lifetime;
            case WeaponParam.Knockback: return knockbackForce;
            case WeaponParam.SpeedRate: return speedRate;
            case WeaponParam.FireDelay: return fireDelay;
            case WeaponParam.ChainCount: return chainCount;
            case WeaponParam.ChainRange: return chainRange;
        }
        return GetParam(_param, 0f);
    }

    /// <summary>
    /// _baseValue(발사체 프리팹 기본값) 에 누적치를 적용한 값 = 기본값 + 기본값 * ΣMultiply + ΣAdd
    /// </summary>
    public float GetParam(WeaponParam _param, float _baseValue)
    {
        int i = (int)_param;
        if (false == IsValidSlot(i))
        {
            return _baseValue;
        }
        return _baseValue + _baseValue * paramMultiply[i] + paramAdd[i];
    }

    /// <summary>
    /// GetParam(_param, _baseValue) 를 반올림한 정수 (조각 수 등)
    /// </summary>
    public int GetParamInt(WeaponParam _param, int _baseValue)
    {
        return Mathf.RoundToInt(GetParam(_param, (float)_baseValue));
    }

    /// <summary>
    /// ★ 플래그가 켜졌는지 (누적값이 0 보다 크면 켜진다)
    /// </summary>
    public bool HasFlag(WeaponParam _param)
    {
        int i = (int)_param;
        return IsValidSlot(i) && paramAdd[i] + paramMultiply[i] > 0f;
    }

    private bool HasModifier(int _slot)
    {
        return 0f != paramAdd[_slot] || 0f != paramMultiply[_slot];
    }

    private static bool IsValidSlot(int _slot)
    {
        return 0 <= _slot && _slot < WeaponParamUtils.SlotCount;
    }

    /// <returns>잘못된 값이면 false</returns>
    private bool AccumulateModifier(WeaponParam _param, StatModifierMode _mode, float _value)
    {
        int i = (int)_param;
        if (false == IsValidSlot(i))
        {
            return false;
        }

        if (StatModifierMode.Multiply == _mode)
        {
            paramMultiply[i] += _value;
        }
        else
        {
            paramAdd[i] += _value;
        }

        RefreshParam(_param);
        return true;
    }

    private void MarkUpgradeChanged()
    {
        ++upgradeVersion;
        isUpgradeDirty = true;
    }

    /// <summary>
    /// 프리팹 기본값을 한 번만 저장한다
    /// </summary>
    private void EnsureBaseValues()
    {
        if (true == isBaseCached)
        {
            return;
        }
        isBaseCached = true;

        baseDamage = damage;
        baseAttackSpeed = attackSpeed;
        baseRange = range;
        baseFireCount = fireCount;
        baseHitCount = hitCount;
        baseAreaRadius = areaRadius;
        baseLifetime = lifetime;
        baseKnockbackForce = knockbackForce;
        baseSpeedRate = speedRate;
        baseFireDelay = fireDelay;
        baseChainCount = chainCount;
        baseChainRange = chainRange;
    }

    /// <summary>
    /// 공통값이면 기본값 + 누적치로 Weapon 필드를 다시 계산한다 (발사체 고유값 / 플래그는 아무것도 하지 않는다)
    /// 수정이 없는 값은 기본값 그대로 둔다
    /// </summary>
    private void RefreshParam(WeaponParam _param)
    {
        if (false == _param.IsCommon())
        {
            return;
        }

        EnsureBaseValues();

        switch (_param)
        {
            case WeaponParam.Damage:
                damage = Mathf.Max(0f, CalcParam(_param, baseDamage));
                break;
            case WeaponParam.AttackSpeed:
                attackSpeed = Mathf.Max(0f, CalcParam(_param, baseAttackSpeed));
                break;
            case WeaponParam.Range:
                range = Mathf.Max(0f, CalcParam(_param, baseRange));
                break;
            case WeaponParam.FireCount:
                fireCount = Mathf.Max(0, Mathf.RoundToInt(CalcParam(_param, baseFireCount)));
                break;
            case WeaponParam.HitCount:
                hitCount = Mathf.Max(0, Mathf.RoundToInt(CalcParam(_param, baseHitCount)));
                break;
            case WeaponParam.AreaRadius:
                areaRadius = Mathf.Max(0f, CalcParam(_param, baseAreaRadius));
                break;
            case WeaponParam.Lifetime:
                lifetime = Mathf.Max(0f, CalcParam(_param, baseLifetime));
                break;
            case WeaponParam.Knockback:
                knockbackForce = Mathf.Max(0f, CalcParam(_param, baseKnockbackForce));
                break;
            case WeaponParam.SpeedRate:
                // 0 이하는 이동 컴포넌트가 1 로 보므로, 수정이 있으면 1 을 기준으로 계산한다
                float speedBase = baseSpeedRate <= 0f && true == HasModifier((int)_param) ? 1f : baseSpeedRate;
                speedRate = Mathf.Max(0f, CalcParam(_param, speedBase));
                break;
            case WeaponParam.FireDelay:
                fireDelay = Mathf.Max(0f, CalcParam(_param, baseFireDelay));
                break;
            case WeaponParam.ChainCount:
                chainCount = Mathf.Max(0, Mathf.RoundToInt(CalcParam(_param, baseChainCount)));
                break;
            case WeaponParam.ChainRange:
                chainRange = Mathf.Max(0f, CalcParam(_param, baseChainRange));
                break;
        }
    }

    private float CalcParam(WeaponParam _param, float _baseValue)
    {
        int i = (int)_param;
        return _baseValue + _baseValue * paramMultiply[i] + paramAdd[i];
    }
    #endregion

    private IObjectPool<Projectile> CreatePool(Projectile _prefab)
    {
        IObjectPool<Projectile> pool = null;
        pool = new ObjectPool<Projectile>
            (
                createFunc: () => CreateObject(_prefab, pool)
                , OnSpawn
                , OnRelease
                , OnDespawn
                , true
                , 100
                , 200
            );
        return pool;
    }

    /// <summary>
    /// 보조 발사체(분열탄 조각 등)를 _start 에서 _point 를 향해 발사한다
    /// 타겟 탐색 없이 위치만으로 발사하며 (Projectile.LaunchAt), 이 무기의 스텟과 BlackBoard 정보를 그대로 쓴다
    /// 데미지는 FinalDamage * _damageRate. 프리팹별로 따로 풀을 만들어 쓴다. 발사하지 못하면 null
    /// </summary>
    public Projectile FireSub(Projectile _prefab, Vector3 _start, Vector3 _point, float _damageRate = 1f, int _index = 0, int _count = 1)
    {
        if (null == _prefab)
        {
            return null;
        }

        if (false == subPools.TryGetValue(_prefab, out IObjectPool<Projectile> pool))
        {
            pool = CreatePool(_prefab);
            subPools.Add(_prefab, pool);
        }

        Projectile p = pool.Get();
        if (null == p)
        {
            return null;
        }

        p.SetFormation(_index, _count);
        WriteBlackBoard(p, _index, _count);
        if (false == p.LaunchAt(this, _start, _point, _damageRate))
        {
            pool.Release(p);
            return null;
        }
        return p;
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

        // 업그레이드로 값이 바뀌면 유지 중인 발사체에도 다시 기록한다
        if (true == isUpgradeDirty)
        {
            isUpgradeDirty = false;
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
        b.RemoveVector(DATA_TYPE.startPoint);
        b.SetTransform(DATA_TYPE.moveTarget, null != owner ? owner : transform);
        b.SetFloat(DATA_TYPE.moveSpeedRate, speedRate);
        b.SetFloat(DATA_TYPE.formationIndex, _index);
        b.SetFloat(DATA_TYPE.formationCount, _count);
        b.SetInt(DATA_TYPE.targetLayer, targetLayer.value);
        b.SetInt(DATA_TYPE.hitCount, hitCount);
        b.SetInt(DATA_TYPE.chainCount, chainCount);
        b.SetFloat(DATA_TYPE.chainRange, chainRange);
        b.SetFloat(DATA_TYPE.lifetime, lifetime);
        b.SetFloat(DATA_TYPE.areaRadius, areaRadius);
    }

    private Projectile CreateObject(Projectile _projectile, IObjectPool<Projectile> _pool)
    {
        Projectile created = Instantiate(_projectile);
        created.SetPool(_pool);
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
