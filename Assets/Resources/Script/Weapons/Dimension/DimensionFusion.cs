using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 무기 합성 판정
/// - 왼쪽 / 오른쪽 플레이어가 합성법의 재료를 하나씩 (어느 쪽이 어느 재료든) 최대 단계로 보유하면 후보
/// - 어느 한쪽이라도 같은 차원 무기를 이미 보유하면 제외
/// - 후보가 여럿이면 무작위 1개
/// - 재료의 최대 단계 = 씬 DimensionApplier 차원의 무기 보상(WeaponRewardEffect.UpgradeTable.StepCount),
///   그 플레이어 진영 applier(차원) 를 먼저 보고, 없으면 다른 진영. 못 찾으면 DimensionWeaponData.FallbackRequiredLevel
/// - 중보스 보상 라운드에서는 부르지 않는다 (호출하는 쪽 책임)
/// </summary>
public static class DimensionFusion
{
    // FindCandidate 용 재사용 목록 (메인 스레드 전용)
    private static readonly List<DimensionWeaponData> candidateBuffer = new List<DimensionWeaponData>();

    /// <summary>
    /// 지금 합성 가능한 합성법 중 무작위 1개 (없거나 플레이어가 없으면 null)
    /// </summary>
    public static DimensionWeaponData FindCandidate(IList<DimensionWeaponData> _recipes)
    {
        candidateBuffer.Clear();
        FindCandidates(_recipes, candidateBuffer);
        DimensionWeaponData picked = PickRandom(candidateBuffer, UnityEngine.Random.value);
        candidateBuffer.Clear();
        return picked;
    }

    /// <summary>
    /// 지금 합성 가능한 합성법을 모두 _result 에 담는다 (_result 는 비우지 않는다)
    /// </summary>
    /// <returns>담은 수</returns>
    public static int FindCandidates(IList<DimensionWeaponData> _recipes, List<DimensionWeaponData> _result)
    {
        if (null == _recipes || null == _result)
        {
            return 0;
        }

        PlayerStats left = PlayerStats.Find(PlayerSide.Left);
        PlayerStats right = PlayerStats.Find(PlayerSide.Right);
        if (null == left || null == right)
        {
            return 0;
        }

        DimensionApplier[] appliers = UnityEngine.Object.FindObjectsByType<DimensionApplier>();

        int added = 0;
        foreach (DimensionWeaponData recipe in _recipes)
        {
            if (null == recipe)
            {
                continue;
            }

            int fallback = recipe.FallbackRequiredLevel;
            bool owned = true == left.HasDimensionWeapon(recipe) || true == right.HasDimensionWeapon(recipe);
            bool candidate = IsCandidate(
                recipe,
                prefab => IsMaxLevel(left.GetWeapon(prefab), GetRequiredLevel(prefab, appliers, fallback, PlayerSide.Left)),
                prefab => IsMaxLevel(right.GetWeapon(prefab), GetRequiredLevel(prefab, appliers, fallback, PlayerSide.Right)),
                owned);

            if (true == candidate)
            {
                _result.Add(recipe);
                ++added;
            }
        }
        return added;
    }

    /// <summary>
    /// _recipe 가 합성 후보인지 (순수 판정)
    /// - 합성법이 유효하고, 이미 보유하지 않았고, (왼쪽 A + 오른쪽 B) 또는 (왼쪽 B + 오른쪽 A) 가 최대 단계
    /// </summary>
    /// <param name="_leftMaxed">왼쪽 플레이어가 그 재료 프리팹을 최대 단계로 보유하는지</param>
    /// <param name="_rightMaxed">오른쪽 플레이어가 그 재료 프리팹을 최대 단계로 보유하는지</param>
    /// <param name="_alreadyOwned">어느 한쪽이라도 이 차원 무기를 이미 보유하는지</param>
    public static bool IsCandidate(DimensionWeaponData _recipe, Func<Weapon, bool> _leftMaxed, Func<Weapon, bool> _rightMaxed, bool _alreadyOwned)
    {
        if (null == _recipe || false == _recipe.IsValid || true == _alreadyOwned || null == _leftMaxed || null == _rightMaxed)
        {
            return false;
        }

        Weapon a = _recipe.MaterialA;
        Weapon b = _recipe.MaterialB;
        return (true == _leftMaxed(a) && true == _rightMaxed(b))
            || (true == _leftMaxed(b) && true == _rightMaxed(a));
    }

    /// <summary>
    /// _candidates 중 _random01(0~1) 로 하나를 고른다 (비었으면 null)
    /// </summary>
    public static DimensionWeaponData PickRandom(IList<DimensionWeaponData> _candidates, float _random01)
    {
        if (null == _candidates || 0 == _candidates.Count)
        {
            return null;
        }
        int index = Mathf.Clamp(Mathf.FloorToInt(_random01 * _candidates.Count), 0, _candidates.Count - 1);
        return _candidates[index];
    }

    /// <summary>
    /// _weapon 이 _maxLevel 단계 이상 업그레이드됐는지 (무기가 없거나 _maxLevel 이 0 이하면 false)
    /// </summary>
    public static bool IsMaxLevel(Weapon _weapon, int _maxLevel)
    {
        return null != _weapon && _maxLevel > 0 && _weapon.UpgradeLevel >= _maxLevel;
    }

    /// <summary>
    /// _weapon(생성된 무기) 이 최대 단계인지. 최대 단계는 _prefab(획득 프리팹, PlayerStats.AcquiredWeaponPrefabs) 의
    /// 업그레이드 테이블에서 찾고, 못 찾으면 _fallbackLevel
    /// </summary>
    public static bool IsMaxLevel(Weapon _weapon, Weapon _prefab, int _fallbackLevel)
    {
        if (null == _weapon)
        {
            return false;
        }
        DimensionApplier[] appliers = UnityEngine.Object.FindObjectsByType<DimensionApplier>();
        return IsMaxLevel(_weapon, GetRequiredLevel(null != _prefab ? _prefab : _weapon, appliers, _fallbackLevel));
    }

    /// <summary>
    /// _prefab 무기의 최대 업그레이드 단계 (차원의 무기 보상 업그레이드 테이블 StepCount, 못 찾으면 _fallbackLevel)
    /// </summary>
    public static int GetRequiredLevel(Weapon _prefab, IList<DimensionApplier> _appliers, int _fallbackLevel)
    {
        WeaponUpgradeTable table = FindUpgradeTable(_prefab, _appliers);
        return null != table && table.StepCount > 0 ? table.StepCount : _fallbackLevel;
    }

    /// <summary>
    /// _side 플레이어 기준 _prefab 무기의 최대 업그레이드 단계 (_side 진영 차원을 먼저 본다, 못 찾으면 _fallbackLevel)
    /// </summary>
    public static int GetRequiredLevel(Weapon _prefab, IList<DimensionApplier> _appliers, int _fallbackLevel, PlayerSide _side)
    {
        WeaponUpgradeTable table = FindUpgradeTable(_prefab, _appliers, _side);
        return null != table && table.StepCount > 0 ? table.StepCount : _fallbackLevel;
    }

    /// <summary>
    /// _appliers 차원의 무기 보상 중 _prefab 을 주는 보상의 업그레이드 테이블 (없으면 null)
    /// </summary>
    public static WeaponUpgradeTable FindUpgradeTable(Weapon _prefab, IList<DimensionApplier> _appliers)
    {
        if (null == _prefab || null == _appliers)
        {
            return null;
        }

        foreach (DimensionApplier applier in _appliers)
        {
            WeaponUpgradeTable table = FindUpgradeTable(_prefab, applier);
            if (null != table)
            {
                return table;
            }
        }
        return null;
    }

    /// <summary>
    /// _side 진영 applier(차원) 에서 먼저 찾고, 없으면 나머지 applier 에서 찾는다 (없으면 null)
    /// - 진영마다 차원이 달라 같은 무기라도 업그레이드 테이블이 다를 수 있다
    /// </summary>
    public static WeaponUpgradeTable FindUpgradeTable(Weapon _prefab, IList<DimensionApplier> _appliers, PlayerSide _side)
    {
        if (null == _prefab || null == _appliers)
        {
            return null;
        }

        foreach (DimensionApplier applier in _appliers)
        {
            if (null != applier && _side == applier.Side)
            {
                WeaponUpgradeTable table = FindUpgradeTable(_prefab, applier);
                if (null != table)
                {
                    return table;
                }
            }
        }
        return FindUpgradeTable(_prefab, _appliers);
    }

    /// <summary>
    /// _applier 차원의 무기 보상 중 _prefab 을 주는 보상의 업그레이드 테이블 (없으면 null)
    /// </summary>
    private static WeaponUpgradeTable FindUpgradeTable(Weapon _prefab, DimensionApplier _applier)
    {
        if (null == _applier || null == _applier.Dimension || null == _applier.Dimension.WeaponRewards)
        {
            return null;
        }

        foreach (RewardData reward in _applier.Dimension.WeaponRewards)
        {
            if (null != reward && reward.GetEffect() is WeaponRewardEffect effect && _prefab == effect.WeaponPrefab && null != effect.UpgradeTable)
            {
                return effect.UpgradeTable;
            }
        }
        return null;
    }
}
