using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Select Panel 묶음. 그룹 안에서는 하나만 선택된다.
/// - 클릭된 패널만 선택하고 나머지는 해제하며, 같은 패널을 다시 눌러도 해제되지 않는다.
/// - 활성화되거나 보상을 새로 뽑을 때마다 보이는 첫 번째 패널(1번 선택지)이 선택된 상태로 시작한다.
/// - 키보드로도 고를 수 있다: Left 쪽은 W / S, Right 쪽은 ↑ / ↓ (보이는 패널만, 끝에서 멈춤)
/// - 레벨업 시 RewardManager 에서 패널마다 보상을 중복 없이 랜덤으로 뽑아 채운다.
///   - 첫 번째 데이터 : Weapon / PassiveBuff / WeaponUpgrade 풀 중 하나
///   - 두 번째 데이터 : 첫 번째 데이터가 PassiveBuff 풀에서 뽑혔을 때만 PassiveNerf(디버프) 풀 중 하나
///     (Weapon / WeaponUpgrade 패널은 디버프 없음 → SelectPanel 이 디버프 영역을 숨긴다)
///   뽑힌 보상이 모자라면 해당 데이터는 비워 두고, 두 데이터가 모두 없는 패널은 숨긴다.
/// - 고른 보상의 첫 번째 데이터(버프) 는 side 쪽, 두 번째 데이터(디버프) 는 반대쪽 플레이어에게 적용된다 (LevelUpUI 가 재개할 때 적용)
/// - 비활성 상태에서도 레벨업을 받아야 하므로 OnEnable 이 아닌 Initialize(Awake) / OnDestroy 에서 구독한다.
///   LevelUpUI 가 자식을 끄기 전에 Initialize 를 호출해 준다.
/// </summary>
public class SelectPanelGroup
    : MonoBehaviour
{
    [Tooltip("이 그룹의 선택지 패널 (비어 있으면 자식에서 자동으로 찾음)")]
    [SerializeField]
    private List<SelectPanel> selectPanels = new List<SelectPanel>();

    [Tooltip("이 그룹에서 고른 버프를 받을 플레이어 쪽 (디버프는 반대쪽이 받음)")]
    [SerializeField]
    private PlayerSide side = PlayerSide.Left;

    public PlayerSide Side => side;

    // 현재 선택된 패널 (없으면 null)
    public SelectPanel SelectedPanel { get; private set; }

    // 선택이 바뀌거나 해제될 때 호출 (해제 시 null)
    public event Action<SelectPanel> OnSelectionChanged;

    /// <summary>
    /// 보이는 패널이 하나라도 있는지 (보상이 없어 모두 숨겨졌으면 false)
    /// </summary>
    public bool HasSelectablePanel
    {
        get
        {
            foreach (SelectPanel selectPanel in selectPanels)
            {
                if (null != selectPanel && true == selectPanel.gameObject.activeSelf)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private bool isInitialized = false;

    /// <summary>
    /// 레벨업 이벤트를 받으면 스스로 보상을 다시 뽑을지 (기본 true)
    /// LevelUpUI 가 보상 순서(일반 레벨업 / 보스 보상)를 관리할 때는 끄고 직접 RefreshRewards / RefreshBossRewards 를 부른다
    /// </summary>
    public bool AutoRefreshOnLevelUp { get; set; } = true;

    // 패널의 첫 번째 데이터를 뽑을 풀
    private static readonly RewardPoolType[] MainPoolTypes =
    {
        RewardPoolType.Weapon,
        RewardPoolType.PassiveBuff,
        RewardPoolType.WeaponUpgrade,
    };

    // 패널의 두 번째 데이터를 뽑을 풀
    private static readonly RewardPoolType[] DebuffPoolTypes =
    {
        RewardPoolType.PassiveNerf,
    };

    // 첫 번째 데이터가 이 풀에서 뽑혔을 때만 두 번째 데이터(디버프)를 채운다 (무기 / 무기 업그레이드 패널은 디버프 없음)
    private static readonly RewardPoolType[] DebuffPairedPoolTypes =
    {
        RewardPoolType.PassiveBuff,
    };

    // 뽑은 보상을 받는 버퍼
    private readonly List<RewardData> mainBuffer = new List<RewardData>();
    private readonly List<RewardData> debuffBuffer = new List<RewardData>();

    // 무기 보유 상한일 때 Weapon 을 뺀 첫 번째 데이터 풀 목록
    private readonly List<RewardPoolType> mainTypeBuffer = new List<RewardPoolType>();

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        // 종료 중 매니져가 새로 생성되지 않도록 HasInstance 로 확인
        if (true == isInitialized && true == EventManager.HasInstance)
        {
            EventManager.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    /// <summary>
    /// 패널 수집 및 레벨업 이벤트 구독 (여러 번 호출해도 한 번만 실행)
    /// </summary>
    public void Initialize()
    {
        if (true == isInitialized)
        {
            return;
        }
        isInitialized = true;

        CollectPanels();

        EventManager.Instance.OnLevelUp -= HandleLevelUp;
        EventManager.Instance.OnLevelUp += HandleLevelUp;
    }

    private void HandleLevelUp(GameObject _owner, int _amount)
    {
        if (_amount <= 0 || false == AutoRefreshOnLevelUp)
        {
            return;
        }

        RefreshRewards();
    }

    // 보스 보상 풀 목록 버퍼
    private readonly List<RewardPoolType> bossTypeBuffer = new List<RewardPoolType>();

    /// <summary>
    /// 보스 보상: Weapon / WeaponUpgrade 풀에서만 뽑고 디버프는 없다
    /// 보스 보상 풀이 비어 있으면 일반 레벨업 보상(RefreshRewards)으로 대신 채운다. 보스 보상으로 채웠으면 true
    /// </summary>
    public bool RefreshBossRewards()
    {
        PlayerStats player = PlayerStats.Find(side);
        BossRewardRules.GetPoolTypes(null != player && true == player.IsWeaponFull, bossTypeBuffer);

        RefreshRewards(bossTypeBuffer, System.Array.Empty<RewardPoolType>());

        if (true == BossRewardRules.ShouldFallback(CountVisiblePanels()))
        {
            RefreshRewards();
            return false;
        }
        return true;
    }

    /// <summary>
    /// 보이는 패널 수
    /// </summary>
    public int CountVisiblePanels()
    {
        int count = 0;
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel && true == selectPanel.gameObject.activeSelf)
            {
                ++count;
            }
        }
        return count;
    }

    /// <summary>
    /// 패널마다 첫 번째 데이터는 Weapon / PassiveBuff / WeaponUpgrade 풀에서, 두 번째 데이터는 PassiveNerf 풀에서 하나씩 뽑아 채운다
    /// 없는 데이터는 비워 두고, 두 데이터가 모두 없는 패널은 숨긴다
    /// </summary>
    public void RefreshRewards()
    {
        RefreshRewards(GetMainPoolTypes(), DebuffPoolTypes);
    }

    /// <summary>
    /// 첫 번째 데이터를 뽑을 풀. side 플레이어가 무기 보유 상한이면 Weapon 풀을 뺀다
    /// (RewardManager 의 풀 막기와 별개로, 상한 도달 직후 남은 레벨업에서 무기가 다시 나오지 않도록 한 번 더 막는다)
    /// </summary>
    private IReadOnlyList<RewardPoolType> GetMainPoolTypes()
    {
        PlayerStats player = PlayerStats.Find(side);
        if (null == player || false == player.IsWeaponFull)
        {
            return MainPoolTypes;
        }

        mainTypeBuffer.Clear();
        foreach (RewardPoolType type in MainPoolTypes)
        {
            if (RewardPoolType.Weapon != type)
            {
                mainTypeBuffer.Add(type);
            }
        }
        return mainTypeBuffer;
    }

    /// <summary>
    /// 패널마다 첫 번째 데이터는 _mainTypes 풀에서, 두 번째 데이터는 _debuffTypes 풀에서 하나씩 뽑아 채운다
    /// 없는 데이터는 비워 두고, 두 데이터가 모두 없는 패널은 숨긴다
    /// </summary>
    private void RefreshRewards(IReadOnlyList<RewardPoolType> _mainTypes, IReadOnlyList<RewardPoolType> _debuffTypes)
    {
        int mainCount = 0;
        int debuffCount = 0;
        RewardManager rewardManager = RewardManager.Instance;
        if (null != rewardManager)
        {
            mainCount = rewardManager.GetRandomUnique(selectPanels.Count, _mainTypes, side, mainBuffer);

            // 디버프는 패시브 패널 수만큼만 뽑는다
            int pairedCount = 0;
            for (int i = 0; i < mainCount; ++i)
            {
                if (true == IsDebuffPaired(rewardManager, mainBuffer[i]))
                {
                    ++pairedCount;
                }
            }
            debuffCount = rewardManager.GetRandomUnique(pairedCount, _debuffTypes, side, debuffBuffer);
        }
        else
        {
            mainBuffer.Clear();
            debuffBuffer.Clear();
            Debug.LogWarning($"[SelectPanelGroup] RewardManager 가 없어 보상을 채우지 못했습니다. ({name})", this);
        }

        int index = 0;
        int debuffIndex = 0;
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null == selectPanel)
            {
                continue;
            }

            RewardData main = index < mainCount ? mainBuffer[index] : null;
            RewardData debuff = null;
            if (null != main && debuffIndex < debuffCount && true == IsDebuffPaired(rewardManager, main))
            {
                debuff = debuffBuffer[debuffIndex];
                debuffIndex++;
            }
            selectPanel.SetData(main, debuff);
            selectPanel.gameObject.SetActive(null != main || null != debuff);
            index++;
        }

        mainBuffer.Clear();
        debuffBuffer.Clear();
        SelectFirst();
    }

    /// <summary>
    /// _main 이 디버프와 짝지어지는 풀(PassiveBuff) 에서 뽑혔는지
    /// </summary>
    private bool IsDebuffPaired(RewardManager _rewardManager, RewardData _main)
    {
        if (null == _rewardManager || null == _main)
        {
            return false;
        }

        foreach (RewardPoolType type in DebuffPairedPoolTypes)
        {
            if (true == _rewardManager.IsInPoolFor(side, type, _main))
            {
                return true;
            }
        }
        return false;
    }

    private void CollectPanels()
    {
        if (0 == selectPanels.Count)
        {
            // 하위에 다른 그룹이 있으면 그 그룹의 패널은 제외
            foreach (SelectPanel selectPanel in GetComponentsInChildren<SelectPanel>(true))
            {
                if (this == selectPanel.GetComponentInParent<SelectPanelGroup>(true))
                {
                    selectPanels.Add(selectPanel);
                }
            }
        }
    }

    private void OnEnable()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel)
            {
                selectPanel.OnClicked += HandlePanelClicked;
            }
        }

        SelectFirst();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null == keyboard)
        {
            return;
        }

        bool isLeft = PlayerSide.Left == side;
        KeyControl upKey = isLeft ? keyboard.wKey : keyboard.upArrowKey;
        KeyControl downKey = isLeft ? keyboard.sKey : keyboard.downArrowKey;

        if (true == upKey.wasPressedThisFrame)
        {
            MoveSelection(-1);
        }
        else if (true == downKey.wasPressedThisFrame)
        {
            MoveSelection(1);
        }
    }

    /// <summary>
    /// 보이는 패널 중에서 _step 방향의 다음 패널을 클릭한 것처럼 선택한다 (끝이면 그대로)
    /// </summary>
    private void MoveSelection(int _step)
    {
        int current = null != SelectedPanel ? selectPanels.IndexOf(SelectedPanel) : -1;
        if (current < 0)
        {
            SelectPanel first = FindFirstVisible();
            if (null != first)
            {
                first.Click();
            }
            return;
        }

        for (int i = current + _step; i >= 0 && i < selectPanels.Count; i += _step)
        {
            SelectPanel candidate = selectPanels[i];
            if (null != candidate && true == candidate.gameObject.activeSelf)
            {
                candidate.Click();
                return;
            }
        }
    }

    private SelectPanel FindFirstVisible()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel && true == selectPanel.gameObject.activeSelf)
            {
                return selectPanel;
            }
        }
        return null;
    }

    /// <summary>
    /// 보이는 첫 번째 패널을 선택한다 (클릭음 없이). 보이는 패널이 없으면 선택 해제
    /// </summary>
    public void SelectFirst()
    {
        SelectPanel first = FindFirstVisible();
        if (null == first)
        {
            ClearSelection();
            return;
        }
        HandlePanelClicked(first);
    }

    private void OnDisable()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel)
            {
                selectPanel.OnClicked -= HandlePanelClicked;
            }
        }
    }

    private void HandlePanelClicked(SelectPanel _clicked)
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null == selectPanel)
            {
                continue;
            }

            selectPanel.IsSelected = (selectPanel == _clicked);
        }

        SelectedPanel = _clicked;
        OnSelectionChanged?.Invoke(_clicked);
    }

    public void ClearSelection()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel)
            {
                selectPanel.IsSelected = false;
            }
        }

        SelectedPanel = null;
        OnSelectionChanged?.Invoke(null);
    }
}
