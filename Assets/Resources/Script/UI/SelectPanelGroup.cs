using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Select Panel 묶음. 그룹 안에서는 하나만 선택된다.
/// - 클릭된 패널만 선택하고 나머지는 해제하며, 같은 패널을 다시 눌러도 해제되지 않는다.
/// - 활성화될 때마다 선택을 초기화하므로 UI 를 열 때마다 선택 없는 상태로 시작한다.
/// - 레벨업 시 RewardManager 의 모든 풀에서 패널당 2개씩 중복 없이 랜덤으로 보상을 뽑아 패널에 채운다.
///   뽑힌 보상이 모자라면 마지막 패널은 1개만 채우고, 남는 패널은 숨긴다.
/// - 비활성 상태에서도 레벨업을 받아야 하므로 OnEnable 이 아닌 Initialize(Awake) / OnDestroy 에서 구독한다.
///   LevelUpUI 가 자식을 끄기 전에 Initialize 를 호출해 준다.
/// </summary>
public class SelectPanelGroup
    : MonoBehaviour
{
    [Tooltip("이 그룹의 선택지 패널 (비어 있으면 자식에서 자동으로 찾음)")]
    [SerializeField]
    private List<SelectPanel> selectPanels = new List<SelectPanel>();

    // 현재 선택된 패널 (없으면 null)
    public SelectPanel SelectedPanel { get; private set; }

    public event Action<SelectPanel> OnSelectionChanged;

    private bool isInitialized = false;

    // 패널 하나에 들어가는 보상 수
    private const int RewardsPerPanel = 2;

    // 뽑은 보상을 받는 버퍼
    private readonly List<RewardData> rewardBuffer = new List<RewardData>();

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
    /// 모든 풀에서 패널당 2개씩 보상을 뽑아 채우고, 데이터가 없는 패널은 숨긴다
    /// </summary>
    public void RefreshRewards()
    {
        int count = 0;
        RewardManager rewardManager = RewardManager.Instance;
        if (null != rewardManager)
        {
            count = rewardManager.GetRandomUnique(selectPanels.Count * RewardsPerPanel, rewardBuffer);
        }
        else
        {
            rewardBuffer.Clear();
            Debug.LogWarning($"[SelectPanelGroup] RewardManager 가 없어 보상을 채우지 못했습니다. ({name})", this);
        }

        int index = 0;
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null == selectPanel)
            {
                continue;
            }

            int first = index * RewardsPerPanel;
            int second = first + 1;
            bool hasData = first < count;
            selectPanel.SetData(hasData ? rewardBuffer[first] : null, second < count ? rewardBuffer[second] : null);
            selectPanel.gameObject.SetActive(hasData);
            index++;
        }

        rewardBuffer.Clear();
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
    }
}
