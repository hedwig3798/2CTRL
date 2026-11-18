using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영구 강화 레벨(MetaProgress) 을 양쪽 플레이어 PlayerStats 영구 계층에 적용한다 (GameScene 에 하나)
/// - 강화 효과 = valuePerLevel × 레벨, 같은 스텟·방식끼리 더해서 SetPermanentModifiers 로 한 번에 넣는다
/// - 모든 Awake / OnEnable(PlayerStats 등록, HealthSystem 등 OnStatChanged 구독) 뒤,
///   DimensionApplier(-100) 의 Start(시작 무기 지급) 보다 먼저 적용되도록 실행 순서를 앞당긴 Start 에서 적용한다
/// - Start 의 첫 적용 때만 살아 있는 플레이어의 현재 HP 를 늘어난 최대 HP 로 채운다 (Apply 재호출은 회복하지 않음)
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class PermanentUpgradeApplier
    : MonoBehaviour
{
    [Tooltip("적용할 강화 목록")]
    [SerializeField]
    private PermanentUpgradeRegistry registry;

    [Tooltip("적용할 플레이어 (비우면 PlayerStats.Find 로 Left / Right). 모두 같은 값을 받는다 (차원 무기가 양쪽 영구 계층을 같다고 보고 한쪽 값만 쓰므로 양쪽 플레이어를 모두 넣을 것)")]
    [SerializeField]
    private PlayerStats[] players;

    private static readonly int StatCount = Enum.GetValues(typeof(ValueType)).Length;

    private readonly float[] addBuffer = new float[StatCount];
    private readonly float[] mulBuffer = new float[StatCount];
    private bool isApplied;

    public PermanentUpgradeRegistry Registry => registry;

    /// <summary>
    /// 적용을 마쳤는지 (registry 가 없어 적용하지 못했으면 false)
    /// </summary>
    public bool IsApplied => isApplied;

    private void Start()
    {
        ApplyInternal(true);
    }

    /// <summary>
    /// 현재 MetaProgress 레벨로 다시 적용한다 (영구 계층은 덮어쓰기라 여러 번 불러도 누적되지 않음)
    /// </summary>
    public void Apply()
    {
        ApplyInternal(false);
    }

    private void ApplyInternal(bool _fillHP)
    {
        if (null == registry)
        {
            Debug.LogWarning("[PermanentUpgradeApplier] registry 가 연결되지 않아 영구 강화를 적용하지 않습니다.", this);
            return;
        }

        BuildModifiers(registry.Upgrades, MetaProgress.GetLevel, addBuffer, mulBuffer);

        if (null != players && players.Length > 0)
        {
            foreach (PlayerStats player in players)
            {
                ApplyTo(player, _fillHP);
            }
        }
        else
        {
            ApplyTo(PlayerStats.Find(PlayerSide.Left), _fillHP);
            ApplyTo(PlayerStats.Find(PlayerSide.Right), _fillHP);
        }
        isApplied = true;
    }

    private void ApplyTo(PlayerStats _player, bool _fillHP)
    {
        if (null == _player)
        {
            return;
        }

        _player.SetPermanentModifiers(addBuffer, mulBuffer);

        if (false == _fillHP)
        {
            return;
        }

        // 게임 시작 시 늘어난 최대 HP 로 가득 채운다 (HealthSystem 은 늘어난 만큼만 더하므로 시작 HP 가 모자랐을 때 대비)
        HealthSystem health = _player.GetComponentInChildren<HealthSystem>(true);
        if (null != health && health.CurrHP > 0f)
        {
            health.CurrHP = health.MaxHP;
        }
    }

    /// <summary>
    /// 강화 목록과 레벨로 ValueType 별 영구 Add / Mul 합을 만든다 (순수 함수, _add / _mul 은 0 으로 비운 뒤 채운다)
    /// - _add / _mul 길이는 ValueType 개수 이상이어야 한다 (넘는 스텟은 무시)
    /// </summary>
    public static void BuildModifiers(IReadOnlyList<PermanentUpgradeData> _upgrades, Func<PermanentUpgradeData, int> _getLevel, float[] _add, float[] _mul)
    {
        if (null != _add)
        {
            Array.Clear(_add, 0, _add.Length);
        }
        if (null != _mul)
        {
            Array.Clear(_mul, 0, _mul.Length);
        }
        if (null == _upgrades || null == _getLevel)
        {
            return;
        }

        foreach (PermanentUpgradeData data in _upgrades)
        {
            if (null == data)
            {
                continue;
            }

            float value = data.GetTotalValue(_getLevel(data));
            if (0f == value)
            {
                continue;
            }

            int i = (int)data.Stat;
            float[] target = StatModifierMode.Add == data.Mode ? _add : _mul;
            if (null != target && i >= 0 && i < target.Length)
            {
                target[i] += value;
            }
        }
    }
}
