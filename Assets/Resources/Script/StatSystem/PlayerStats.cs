using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 스텟 (Player 루트에 하나 배치)
/// - ValueType 마다 base / ΣAdd / ΣMultiply 를 가지며, 최종값 = (base + ΣAdd) × (1 + ΣMultiply) (0 미만은 0)
/// - base 는 MoveSpeed = ArrowMovemnt.speed, HP = HealthSystem.maxHP 에서 가져오고, 나머지(Damage / AttackSpeed / Range) 는 0
/// - 무기 스텟과는 별개이며, Weapon 이 자신의 스텟에 플레이어 스텟을 더해서 사용한다
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

    private static readonly int StatCount = Enum.GetValues(typeof(ValueType)).Length;

    private readonly float[] baseValues = new float[StatCount];
    private readonly float[] addValues = new float[StatCount];
    private readonly float[] mulValues = new float[StatCount];

    // AddWeapon 으로 얻은 무기 (프리팹 → 생성된 무기), 같은 무기를 두 번 얻지 않도록 사용
    private readonly Dictionary<Weapon, Weapon> acquiredWeapons = new Dictionary<Weapon, Weapon>();

    /// <summary>
    /// 스텟이 바뀔 때 호출
    /// </summary>
    public event Action<PlayerStats> OnStatChanged;

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
    /// 최종 수치 = (base + ΣAdd) × (1 + ΣMultiply), 0 미만은 0
    /// </summary>
    public float Get(ValueType _type)
    {
        EnsureBaseValues();
        int i = (int)_type;
        return Mathf.Max(0f, (baseValues[i] + addValues[i]) * (1f + mulValues[i]));
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

        Debug.Log($"[PlayerStats] {name}({side}) {_type.ToDisplayName()} {_mode} {_value:+0.##;-0.##} → {Get(_type):0.##}", this);
        OnStatChanged?.Invoke(this);
    }
    #endregion

    #region Weapon
    /// <summary>
    /// 무기 프리팹을 weaponHolder 아래에 생성한다
    /// - 이미 같은 프리팹으로 얻은 무기가 있으면 생성하지 않는다
    /// - 레이어와 targetLayer 는 이미 가진 무기를 따라간다 (플레이어마다 다른 레이어를 쓰므로)
    /// </summary>
    /// <returns>새로 생성했으면 true</returns>
    public bool AddWeapon(Weapon _prefab)
    {
        if (null == _prefab || true == acquiredWeapons.ContainsKey(_prefab))
        {
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
            foreach (Transform t in weapon.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = reference.gameObject.layer;
            }
        }

        acquiredWeapons.Add(_prefab, weapon);
        Debug.Log($"[PlayerStats] {name}({side}) 무기 획득 : {_prefab.name}", this);
        return true;
    }

    public bool HasWeapon(Weapon _prefab)
    {
        return null != _prefab && acquiredWeapons.ContainsKey(_prefab);
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
