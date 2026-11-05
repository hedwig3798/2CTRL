using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 스텟 (Player 루트에 하나 배치)
/// - ValueType 마다 base / ΣAdd / ΣMultiply 를 가지며, 최종값 = (base + ΣAdd) × 배율 (0 미만은 0)
///   배율 = max(minMultiplier, 1 + ΣMultiply) (상대에게 너프를 몰아 받아도 일정 이하로 떨어지지 않게)
/// - base 는 MoveSpeed = ArrowMovemnt.speed, HP = HealthSystem.maxHP 에서 가져오고, 나머지(Damage / AttackSpeed / Range) 는 0
/// - 무기 스텟과는 별개이며, Weapon 이 (무기 스텟 + ΣAdd) × 배율 로 사용한다 (Damage / AttackSpeed / Range 의 배율은 모든 무기에 곱해진다)
/// - 무기 보유 상한(maxWeaponCount, 시작 무기 포함)에 도달하면 AddWeapon 이 더 이상 무기를 주지 않는다
/// - 스텟이 바뀌면 OnStatChanged 로 알리고, ArrowMovemnt / HealthSystem 이 구독해서 반영한다
/// - side 로 등록되어 Find 로 찾을 수 있다 (왼쪽 보상 → 왼쪽 플레이어)
/// </summary>
[DisallowMultipleComponent]
public class PlayerStats
    : MonoBehaviour
{
    private static readonly Dictionary<PlayerSide, PlayerStats> registry = new Dictionary<PlayerSide, PlayerStats>();

    [Tooltip("화면 위치. 같은 쪽 SelectPanelGroup 에서 고른 보상을 받는다")]
    [SerializeField]
    private PlayerSide side = PlayerSide.Left;

    [Tooltip("보상으로 얻은 무기를 생성할 부모 (비어 있으면 이 오브젝트)")]
    [SerializeField]
    private Transform weaponHolder;

    [Tooltip("배율(1 + ΣMultiply) 의 하한. 너프를 몰아 받아도 이 배율 아래로 내려가지 않는다")]
    [Min(0f)]
    [SerializeField]
    private float minMultiplier = 0.5f;

    [Tooltip("보유할 수 있는 무기 수 (시작 무기 포함). 0 이하면 상한 없음")]
    [SerializeField]
    private int maxWeaponCount = 4;

    private static readonly int StatCount = Enum.GetValues(typeof(ValueType)).Length;

    private readonly float[] baseValues = new float[StatCount];
    private readonly float[] addValues = new float[StatCount];
    private readonly float[] mulValues = new float[StatCount];

    // AddWeapon 으로 얻은 무기 (프리팹 → 생성된 무기), 같은 무기를 두 번 얻지 않도록 사용
    private readonly Dictionary<Weapon, Weapon> acquiredWeapons = new Dictionary<Weapon, Weapon>();

    // 획득 순서 (Dictionary 는 순서를 보장하지 않으므로 따로 기록, 같은 인덱스끼리 프리팹 / 생성된 무기)
    private readonly List<Weapon> acquiredPrefabOrder = new List<Weapon>();
    private readonly List<Weapon> acquiredWeaponOrder = new List<Weapon>();

    /// <summary>
    /// 스텟이 바뀔 때 호출
    /// </summary>
    public event Action<PlayerStats> OnStatChanged;

    /// <summary>
    /// AddWeapon 으로 무기를 새로 얻었을 때 호출 (플레이어, 프리팹, 생성된 무기)
    /// </summary>
    public event Action<PlayerStats, Weapon, Weapon> OnWeaponAdded;

    public PlayerSide Side => side;

    /// <summary>
    /// side 에 등록된 플레이어 (없으면 null)
    /// </summary>
    public static PlayerStats Find(PlayerSide _side)
    {
        registry.TryGetValue(_side, out PlayerStats stats);
        return stats;
    }

    #region Stat
    /// <summary>
    /// 최종 수치 = (base + ΣAdd) × GetMultiplier, 0 미만은 0
    /// - Damage / AttackSpeed / Range 는 base 가 0 이므로 무기에서는 GetAdd / GetMultiplier 를 따로 쓴다
    /// </summary>
    public float Get(ValueType _type)
    {
        EnsureBaseValues();
        int i = (int)_type;
        return Mathf.Max(0f, (baseValues[i] + addValues[i]) * GetMultiplier(_type));
    }

    /// <summary>
    /// 최종 배율 = max(minMultiplier, 1 + ΣMultiply)
    /// </summary>
    public float GetMultiplier(ValueType _type)
    {
        return Mathf.Max(minMultiplier, 1f + mulValues[(int)_type]);
    }

    public float GetBase(ValueType _type)
    {
        EnsureBaseValues();
        return baseValues[(int)_type];
    }

    /// <summary>
    /// 보상으로 누적된 고정값 합 (ΣAdd)
    /// </summary>
    public float GetAdd(ValueType _type)
    {
        return addValues[(int)_type];
    }

    /// <summary>
    /// 보상으로 누적된 비율 합 (ΣMultiply)
    /// </summary>
    public float GetMultiply(ValueType _type)
    {
        return mulValues[(int)_type];
    }

    public void AddModifier(ValueType _type, StatModifierMode _mode, float _value)
    {
        EnsureBaseValues();
        int i = (int)_type;
        switch (_mode)
        {
            case StatModifierMode.Add:
                addValues[i] += _value;
                break;
            case StatModifierMode.Multiply:
                mulValues[i] += _value;
                break;
        }

        OnStatChanged?.Invoke(this);
    }
    #endregion

    #region Weapon
    /// <summary>
    /// AddWeapon 으로 얻은 무기 수 (시작 무기 포함)
    /// </summary>
    public int WeaponCount => acquiredWeapons.Count;

    /// <summary>
    /// 보유할 수 있는 무기 수 (0 이하면 상한 없음)
    /// </summary>
    public int MaxWeaponCount => maxWeaponCount;

    /// <summary>
    /// 무기 보유 상한에 도달했는지
    /// </summary>
    public bool IsWeaponFull => maxWeaponCount > 0 && acquiredWeapons.Count >= maxWeaponCount;

    /// <summary>
    /// 무기 프리팹을 weaponHolder 아래에 생성한다
    /// - 이미 같은 프리팹으로 얻은 무기가 있거나 보유 상한에 도달했으면 생성하지 않는다
    /// - 레이어와 targetLayer 는 이미 가진 무기를 따라간다 (플레이어마다 다른 레이어를 쓰므로)
    /// </summary>
    /// <returns>새로 생성했으면 true</returns>
    public bool AddWeapon(Weapon _prefab)
    {
        if (null == _prefab || true == acquiredWeapons.ContainsKey(_prefab))
        {
            return false;
        }

        if (true == IsWeaponFull)
        {
            Debug.LogWarning($"[PlayerStats] 무기 보유 상한({maxWeaponCount}) 에 도달해 {_prefab.name} 을(를) 얻지 못했습니다. ({name})", this);
            return false;
        }

        Weapon reference = GetComponentInChildren<Weapon>(true);

        Transform parent = null != weaponHolder ? weaponHolder : transform;
        Weapon weapon = Instantiate(_prefab, parent);
        weapon.name = _prefab.name;
        weapon.owner = transform;
        weapon.SetOwnerStats(this);

        if (null != reference)
        {
            weapon.targetLayer = reference.targetLayer;
            LayerUtils.SetLayer(weapon.gameObject, reference.gameObject.layer);
        }
        else
        {
            // 가진 무기가 없으면 side 로 레이어를 정한다 (무기 프리팹은 Default / targetLayer 0 이라 그대로 두면 공격하지 않는다)
            int attackLayer = LayerMask.NameToLayer(PlayerSide.Left == side ? "LAttack" : "RAttack");
            int monsterLayer = LayerMask.NameToLayer(PlayerSide.Left == side ? "LMonster" : "RMonster");
            if (attackLayer < 0 || monsterLayer < 0)
            {
                Debug.LogWarning($"[PlayerStats] {side} 무기 레이어를 찾지 못했습니다. (Attack {attackLayer}, Monster {monsterLayer})", this);
            }
            else
            {
                weapon.targetLayer = 1 << monsterLayer;
                LayerUtils.SetLayer(weapon.gameObject, attackLayer);
            }
        }

        acquiredWeapons.Add(_prefab, weapon);
        acquiredPrefabOrder.Add(_prefab);
        acquiredWeaponOrder.Add(weapon);
        OnWeaponAdded?.Invoke(this, _prefab, weapon);
        return true;
    }

    /// <summary>
    /// AddWeapon 으로 얻은 무기 프리팹 (획득 순서, 시작 무기 포함)
    /// </summary>
    public IReadOnlyList<Weapon> AcquiredWeaponPrefabs => acquiredPrefabOrder;

    /// <summary>
    /// AddWeapon 으로 생성한 무기 (획득 순서, AcquiredWeaponPrefabs 와 같은 인덱스. 파괴됐으면 null 로 비교된다)
    /// </summary>
    public IReadOnlyList<Weapon> AcquiredWeapons => acquiredWeaponOrder;

    public bool HasWeapon(Weapon _prefab)
    {
        return null != _prefab && acquiredWeapons.ContainsKey(_prefab);
    }

    /// <summary>
    /// AddWeapon 으로 _prefab 에서 생성한 무기 (없거나 파괴됐으면 null)
    /// </summary>
    public Weapon GetWeapon(Weapon _prefab)
    {
        if (null == _prefab || false == acquiredWeapons.TryGetValue(_prefab, out Weapon weapon))
        {
            return null;
        }
        return null != weapon ? weapon : null;
    }
    #endregion

    private bool isBaseInitialized = false;

    private void Awake()
    {
        EnsureBaseValues();
    }

    /// <summary>
    /// base 값을 한 번만 읽는다
    /// 같은 오브젝트의 ArrowMovemnt 가 이 컴포넌트의 Awake 보다 먼저 OnEnable 에서 Get 을 부를 수 있으므로 지연 초기화한다
    /// </summary>
    private void EnsureBaseValues()
    {
        if (true == isBaseInitialized)
        {
            return;
        }
        isBaseInitialized = true;

        ArrowMovemnt movement = GetComponent<ArrowMovemnt>();
        if (null != movement)
        {
            baseValues[(int)ValueType.MoveSpeed] = movement.speed;
        }

        HealthSystem health = GetComponentInChildren<HealthSystem>(true);
        if (null != health)
        {
            baseValues[(int)ValueType.HP] = health.maxHP;
        }
    }

    private void OnEnable()
    {
        if (registry.TryGetValue(side, out PlayerStats other) && null != other && this != other)
        {
            Debug.LogWarning($"[PlayerStats] {side} 플레이어가 여러 명입니다. ({other.name}, {name}) 나중에 활성화된 것을 사용합니다.", this);
        }
        registry[side] = this;
    }

    private void OnDisable()
    {
        if (registry.TryGetValue(side, out PlayerStats registered) && this == registered)
        {
            registry.Remove(side);
        }
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registry.Clear();
    }
}
