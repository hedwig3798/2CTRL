using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Select Panel 묶음. 그룹 안에서는 하나만 선택된다.
/// - 클릭된 패널만 선택하고 나머지는 해제하며, 같은 패널을 다시 눌러도 해제되지 않는다.
/// - 활성화될 때마다 선택을 초기화하므로 UI 를 열 때마다 선택 없는 상태로 시작한다.
/// - 레벨업 시 RewardManager 에서 패널마다 보상을 중복 없이 랜덤으로 뽑아 채운다.
///   - 첫 번째 데이터 : Weapon / PassiveBuff / WeaponUpgrade 풀 중 하나
///   - 두 번째 데이터 : PassiveNerf(디버프) 풀 중 하나
///   뽑힌 보상이 모자라면 해당 데이터는 비워 두고, 두 데이터가 모두 없는 패널은 숨긴다.
/// - 게임 시작 보상(RefreshStartRewards) 은 첫 번째 데이터를 Weapon 풀에서만 뽑고, 두 번째 데이터는 비워 둔다.
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

    // 게임 시작 보상에서 첫 번째 데이터를 뽑을 풀 (신규 무기만)
    private static readonly RewardPoolType[] StartPoolTypes =
    {
        RewardPoolType.Weapon,
    };

    // 게임 시작 보상에는 디버프를 붙이지 않는다
    private static readonly RewardPoolType[] NoPoolTypes = { };

    // 뽑은 보상을 받는 버퍼
    private readonly List<RewardData> mainBuffer = new List<RewardData>();
    private readonly List<RewardData> debuffBuffer = new List<RewardData>();

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
        if (_amount <= 0)
        {
            return;
        }

        RefreshRewards();
    }

    /// <summary>
    /// 패널마다 첫 번째 데이터는 Weapon / PassiveBuff / WeaponUpgrade 풀에서, 두 번째 데이터는 PassiveNerf 풀에서 하나씩 뽑아 채운다
    /// 없는 데이터는 비워 두고, 두 데이터가 모두 없는 패널은 숨긴다
    /// </summary>
    public void RefreshRewards()
    {
        RefreshRewards(MainPoolTypes, DebuffPoolTypes);
    }

    /// <summary>
    /// 게임 시작 보상용으로 패널마다 Weapon 풀에서만 하나씩 뽑아 채운다 (디버프 없음)
    /// </summary>
    public void RefreshStartRewards()
    {
        RefreshRewards(StartPoolTypes, NoPoolTypes);
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
            mainCount = rewardManager.GetRandomUnique(selectPanels.Count, _mainTypes, mainBuffer);
            debuffCount = rewardManager.GetRandomUnique(selectPanels.Count, _debuffTypes, debuffBuffer);
        }
        else
        {
            mainBuffer.Clear();
            debuffBuffer.Clear();
            Debug.LogWarning($"[SelectPanelGroup] RewardManager 가 없어 보상을 채우지 못했습니다. ({name})", this);
        }

        int index = 0;
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null == selectPanel)
            {
                continue;
            }

            RewardData main = index < mainCount ? mainBuffer[index] : null;
            RewardData debuff = index < debuffCount ? debuffBuffer[index] : null;
            selectPanel.SetData(main, debuff);
            selectPanel.gameObject.SetActive(null != main || null != debuff);
            index++;
        }

        mainBuffer.Clear();
        debuffBuffer.Clear();
        ClearSelection();
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

        ClearSelection();
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
