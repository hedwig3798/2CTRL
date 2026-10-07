using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨업 보상 풀 매니져 (씬에 하나 배치)
/// - 풀은 4개: 신규 무기 / 무기 강화 / 패시브 버프 / 패시브 너프
/// - 시작 시 initial* 목록이 각 풀에 들어간다. 무기 강화 풀은 비어 있고, 무기를 선택하면 그 무기의 강화가 추가된다.
/// - GetChoices 는 신규 무기 / 무기 강화 / 패시브 버프 중에서 뽑고, 강화(무기 강화, 패시브 버프)에는 패시브 너프를 하나 조합한다.
/// - 게임 중 Add / Remove 로 어느 풀이든 추가, 삭제할 수 있다.
/// - 실제 무기 장착 / 스탯 적용은 OnRewardSelected 를 구독하는 쪽이 담당한다.
/// </summary>
[DisallowMultipleComponent]
public class RewardPoolManager
    : MonoBehaviour
{
    private static RewardPoolManager instance;

    public static RewardPoolManager Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindFirstObjectByType<RewardPoolManager>();
            }
            return instance;
        }
    }

    public static bool HasInstance => null != instance;

    [Header("initial pool")]
    [Tooltip("게임 시작 시 신규 무기 풀에 들어갈 무기")]
    [SerializeField]
    private List<WeaponRewardData> initialWeapons = new List<WeaponRewardData>();
    [Tooltip("게임 시작 시 패시브 버프 풀에 들어갈 버프")]
    [SerializeField]
    private List<PassiveBuffRewardData> initialPassiveBuffs = new List<PassiveBuffRewardData>();
    [Tooltip("게임 시작 시 패시브 너프 풀에 들어갈 너프")]
    [SerializeField]
    private List<PassiveNerfRewardData> initialPassiveNerfs = new List<PassiveNerfRewardData>();

    private readonly RewardPool<WeaponRewardData> weaponPool = new RewardPool<WeaponRewardData>();
    private readonly RewardPool<WeaponUpgradeRewardData> upgradePool = new RewardPool<WeaponUpgradeRewardData>();
    private readonly RewardPool<PassiveBuffRewardData> buffPool = new RewardPool<PassiveBuffRewardData>();
    private readonly RewardPool<PassiveNerfRewardData> nerfPool = new RewardPool<PassiveNerfRewardData>();

    /// <summary>
    /// 선택지가 선택되었을 때 (선택된 선택지)
    /// </summary>
    public Action<RewardChoice> OnRewardSelected;

    /// <summary>
    /// 풀 내용이 바뀌었을 때
    /// </summary>
    public Action OnPoolChanged;

    public IReadOnlyList<WeaponRewardData> WeaponPool => weaponPool.Items;
    public IReadOnlyList<WeaponUpgradeRewardData> UpgradePool => upgradePool.Items;
    public IReadOnlyList<PassiveBuffRewardData> BuffPool => buffPool.Items;
    public IReadOnlyList<PassiveNerfRewardData> NerfPool => nerfPool.Items;

    /// <summary>
    /// 최대 _count 개의 선택지를 만든다
    /// main 은 신규 무기 / 무기 강화 / 패시브 버프를 합쳐 가중치 랜덤으로 중복 없이 뽑고,
    /// 강화 main 에는 패시브 너프를 가중치 랜덤으로 조합한다 (가능하면 선택지끼리 너프가 겹치지 않게)
    /// </summary>
    public List<RewardChoice> GetChoices(int _count)
    {
        List<RewardData> candidates = new List<RewardData>(weaponPool.Count + upgradePool.Count + buffPool.Count);
        candidates.AddRange(weaponPool.Items);
        candidates.AddRange(upgradePool.Items);
        candidates.AddRange(buffPool.Items);

        List<RewardData> mains = new List<RewardData>(_count);
        RewardRandom.PickWeighted(candidates, _count, mains);

        int nerfNeed = 0;
        foreach (RewardData main in mains)
        {
            if (true == IsUpgrade(main))
            {
                nerfNeed++;
            }
        }

        // 서로 다른 너프를 먼저 뽑고, 부족하면 겹치는 것을 허용해 채운다
        List<PassiveNerfRewardData> nerfs = new List<PassiveNerfRewardData>(nerfNeed);
        RewardRandom.PickWeighted(nerfPool.Items, nerfNeed, nerfs);
        while (0 < nerfs.Count && nerfs.Count < nerfNeed)
        {
            RewardRandom.PickWeighted(nerfPool.Items, 1, nerfs);
        }

        List<RewardChoice> result = new List<RewardChoice>(mains.Count);
        int nerfIndex = 0;
        foreach (RewardData main in mains)
        {
            PassiveNerfRewardData nerf = null;
            if (true == IsUpgrade(main) && nerfIndex < nerfs.Count)
            {
                nerf = nerfs[nerfIndex++];
            }
            result.Add(new RewardChoice(main, nerf));
        }
        return result;
    }

    /// <summary>
    /// 선택지 선택 처리
    /// 신규 무기: 신규 무기 풀에서 제거 후 강화 선택지를 무기 강화 풀에 추가
    /// 무기 강화 / 패시브 버프 / 패시브 너프: 선택 횟수 기록 (maxPickCount 도달 시 제거)
    /// </summary>
    public void Select(RewardChoice _choice)
    {
        if (null == _choice || null == _choice.main)
        {
            return;
        }

        switch (_choice.main)
        {
            case WeaponRewardData weapon:
                weaponPool.MarkPicked(weapon);
                weaponPool.Remove(weapon);
                foreach (WeaponUpgradeRewardData upgrade in weapon.upgrades)
                {
                    upgradePool.Add(upgrade);
                }
                break;

            case WeaponUpgradeRewardData upgrade:
                upgradePool.MarkPicked(upgrade);
                break;

            case PassiveBuffRewardData buff:
                buffPool.MarkPicked(buff);
                break;

            default:
                Debug.LogWarning($"[RewardPoolManager] 선택지 main 으로 쓸 수 없는 보상 타입입니다. ({_choice.main.name})", this);
                return;
        }

        if (true == _choice.HasNerf)
        {
            nerfPool.MarkPicked(_choice.nerf);
        }

        OnRewardSelected?.Invoke(_choice);
        OnPoolChanged?.Invoke();
    }

    /// <summary>
    /// 보상을 타입에 맞는 풀에 추가한다
    /// </summary>
    public bool Add(RewardData _reward)
    {
        bool added = false;
        switch (_reward)
        {
            case WeaponRewardData weapon:
                added = weaponPool.Add(weapon);
                break;
            case WeaponUpgradeRewardData upgrade:
                added = upgradePool.Add(upgrade);
                break;
            case PassiveBuffRewardData buff:
                added = buffPool.Add(buff);
                break;
            case PassiveNerfRewardData nerf:
                added = nerfPool.Add(nerf);
                break;
        }

        if (true == added)
        {
            OnPoolChanged?.Invoke();
        }
        return added;
    }

    /// <summary>
    /// 보상을 타입에 맞는 풀에서 제거한다
    /// </summary>
    public bool Remove(RewardData _reward)
    {
        bool removed = false;
        switch (_reward)
        {
            case WeaponRewardData weapon:
                removed = weaponPool.Remove(weapon);
                break;
            case WeaponUpgradeRewardData upgrade:
                removed = upgradePool.Remove(upgrade);
                break;
            case PassiveBuffRewardData buff:
                removed = buffPool.Remove(buff);
                break;
            case PassiveNerfRewardData nerf:
                removed = nerfPool.Remove(nerf);
                break;
        }

        if (true == removed)
        {
            OnPoolChanged?.Invoke();
        }
        return removed;
    }

    public bool Contains(RewardData _reward)
    {
        switch (_reward)
        {
            case WeaponRewardData weapon:
                return weaponPool.Contains(weapon);
            case WeaponUpgradeRewardData upgrade:
                return upgradePool.Contains(upgrade);
            case PassiveBuffRewardData buff:
                return buffPool.Contains(buff);
            case PassiveNerfRewardData nerf:
                return nerfPool.Contains(nerf);
        }
        return false;
    }

    /// <summary>
    /// 풀을 초기 상태(initial* 목록만 있는 상태)로 되돌린다
    /// </summary>
    public void ResetPool()
    {
        weaponPool.Clear();
        upgradePool.Clear();
        buffPool.Clear();
        nerfPool.Clear();

        foreach (WeaponRewardData weapon in initialWeapons)
        {
            weaponPool.Add(weapon);
        }
        foreach (PassiveBuffRewardData buff in initialPassiveBuffs)
        {
            buffPool.Add(buff);
        }
        foreach (PassiveNerfRewardData nerf in initialPassiveNerfs)
        {
            nerfPool.Add(nerf);
        }

        OnPoolChanged?.Invoke();
    }

    /// <summary>
    /// 너프가 조합되는 강화 보상인지
    /// </summary>
    private static bool IsUpgrade(RewardData _reward)
    {
        return _reward is WeaponUpgradeRewardData || _reward is PassiveBuffRewardData;
    }

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogWarning($"[RewardPoolManager] RewardPoolManager 가 여러 개입니다. ({instance.name}, {name}) 먼저 등록된 것을 사용합니다.");
            Destroy(this);
            return;
        }

        instance = this;
        ResetPool();
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
}
