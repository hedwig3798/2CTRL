using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 시 게임을 일시정지하고 레벨업 패널을 표시하는 UI
/// - EventManager.OnLevelUp 을 구독하므로, 항상 활성 상태인 Canvas 루트에 붙이고 자식 전체를 On/Off 한다.
/// - 한 번에 여러 레벨이 오르면 오른 레벨 수만큼 버튼을 눌러야 재개된다.
/// - 자식 SelectPanelGroup 에서 선택지를 고르기 전에는 재개 버튼이 비활성화된다.
/// </summary>
public class LevelUpUI
    : MonoBehaviour
{
    [Tooltip("누르면 게임을 재개하는 버튼")]
    [SerializeField]
    private Button resumeButton;

    // 아직 처리하지 않은 레벨업 횟수
    private int pendingCount = 0;

    private bool isOpen = false;

    public bool IsOpen => isOpen;

    // 선택해야 재개할 수 있는 선택지 그룹
    private SelectPanelGroup[] selectPanelGroups;

    private void Awake()
    {
        ValidateSetup();

        // 자식이 꺼지면 Awake 가 호출되지 않으므로, 끄기 전에 레벨업 구독을 위한 초기화를 해 둠
        selectPanelGroups = GetComponentsInChildren<SelectPanelGroup>(true);
        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            group.Initialize();
        }

        SetChildrenActive(false);
    }

    /// <summary>
    /// 인스펙터 연결이 잘못된 경우 경고
    /// </summary>
    private void ValidateSetup()
    {
        if (null == resumeButton)
        {
            Debug.LogWarning($"[LevelUpUI] resumeButton 이 연결되지 않았습니다. ({name})", this);
        }
        else if (resumeButton.transform == transform || false == resumeButton.transform.IsChildOf(transform))
        {
            Debug.LogWarning($"[LevelUpUI] resumeButton 이 이 오브젝트의 자식이 아니어서 UI 를 닫아도 버튼이 남습니다. ({name})", this);
        }

        if (null != resumeButton)
        {
            Canvas canvas = resumeButton.GetComponentInParent<Canvas>(true);
            if (null != canvas && null == canvas.rootCanvas.GetComponent<GraphicRaycaster>())
            {
                Debug.LogWarning($"[LevelUpUI] resumeButton 이 있는 Canvas({canvas.rootCanvas.name}) 에 GraphicRaycaster 가 없어 버튼이 클릭되지 않습니다.", this);
            }
        }
    }

    private void OnEnable()
    {
        EventManager.Instance.OnLevelUp -= HandleLevelUp;
        EventManager.Instance.OnLevelUp += HandleLevelUp;

        if (null != resumeButton)
        {
            resumeButton.onClick.AddListener(HandleResume);
        }

        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            group.OnSelectionChanged += HandleSelectionChanged;
        }
        UpdateResumeButton();
    }

    private void OnDisable()
    {
        // 종료 중 매니져가 새로 생성되지 않도록 HasInstance 로 확인
        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnLevelUp -= HandleLevelUp;
        }

        if (null != resumeButton)
        {
            resumeButton.onClick.RemoveListener(HandleResume);
        }

        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            if (null != group)
            {
                group.OnSelectionChanged -= HandleSelectionChanged;
            }
        }

        // 열린 채로 비활성화되면 게임이 멈춘 상태로 남지 않도록 닫음
        if (true == IsOpen)
        {
            Close();
        }
    }

    private void HandleLevelUp(GameObject _owner, int _amount)
    {
        if (_amount <= 0)
        {
            return;
        }

        pendingCount += _amount;

        if (false == IsOpen)
        {
            Open();
        }
    }

    private void HandleResume()
    {
        // 선택지를 고르지 않으면 재개하지 않음
        if (false == CanResume())
        {
            return;
        }

        ApplySelectedRewards();

        pendingCount--;

        if (pendingCount <= 0)
        {
            Close();
        }
        else
        {
            // 남은 레벨업은 새 선택지를 뽑아 다시 선택해야 재개된다
            foreach (SelectPanelGroup group in selectPanelGroups)
            {
                group.RefreshRewards();
            }
            UpdateResumeButton();
        }
    }

    /// <summary>
    /// 그룹마다 선택된 패널의 보상을 적용한다
    /// - 첫 번째 데이터(버프) 는 그 그룹 쪽 플레이어, 두 번째 데이터(디버프) 는 반대쪽 플레이어에게 적용한다
    /// - 비어 있는 데이터는 무시한다
    /// </summary>
    private void ApplySelectedRewards()
    {
        RewardManager rewardManager = RewardManager.Instance;
        if (null == rewardManager)
        {
            Debug.LogWarning($"[LevelUpUI] RewardManager 가 없어 보상을 적용하지 못했습니다. ({name})", this);
            return;
        }

        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            SelectPanel selected = group.SelectedPanel;
            if (null == selected)
            {
                continue;
            }

            rewardManager.ApplyReward(selected.Data1, group.Side);
            rewardManager.ApplyReward(selected.Data2, group.Side.Opposite());
        }
    }

    private void HandleSelectionChanged(SelectPanel _selected)
    {
        UpdateResumeButton();
    }

    /// <summary>
    /// 보이는 패널이 있는 그룹은 모두 선택되어 있어야 재개 가능
    /// </summary>
    private bool CanResume()
    {
        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            if (true == group.HasSelectablePanel && null == group.SelectedPanel)
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateResumeButton()
    {
        if (null != resumeButton)
        {
            resumeButton.interactable = CanResume();
        }
    }

    /// <summary>
    /// 루트는 이벤트 구독을 위해 켜둔 채, 직계 자식 전체를 On/Off
    /// </summary>
    private void SetChildrenActive(bool _active)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(_active);
        }
    }

    private void Open()
    {
        SetChildrenActive(true);
        isOpen = true;
        GamePause.Pause(this);
        UpdateResumeButton();
    }

    private void Close()
    {
        pendingCount = 0;

        SetChildrenActive(false);
        isOpen = false;
        GamePause.Resume(this);
    }
}
