using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 레벨업 시 게임을 일시정지하고 레벨업 패널을 표시하는 UI
/// - EventManager.OnLevelUp 을 구독하므로, 항상 활성 상태인 Canvas 루트에 붙이고 자식 전체를 On/Off 한다.
/// - 한 번에 여러 레벨이 오르면 오른 레벨 수만큼 버튼을 눌러야 재개된다.
/// - 자식 SelectPanelGroup 에서 선택지를 고르기 전에는 재개 버튼이 비활성화된다.
/// - 스페이스바로도 재개 버튼을 누를 수 있다.
/// - 재개 버튼 글자는 resumeLabelKey 로 채운다 (프리팹에 박힌 글자 대신. 언어가 바뀌면 다시 채움)
/// - 중간 보스 처치(EventManager.OnBossDefeated) 시 그 진영 그룹만 보스 보상(무기·무기 업그레이드, 디버프 없음)을 연다
///   일반 레벨업과 보스 보상은 들어온 순서대로 큐에서 하나씩 처리한다
/// </summary>
public class LevelUpUI
    : MonoBehaviour
{
    [Tooltip("누르면 게임을 재개하는 버튼")]
    [SerializeField]
    private Button resumeButton;

    [Tooltip("재개 버튼 글자 StringTable 키 (비우면 프리팹 글자 그대로)")]
    [SerializeField]
    private string resumeLabelKey = "ui.levelup.resume";

    [Tooltip("재개 버튼 글자 (비우면 버튼 자식에서 찾는다. LocalizedText 가 붙은 라벨은 건드리지 않음)")]
    [SerializeField]
    private TMP_Text resumeLabel;

    /// <summary>
    /// 보상 선택 한 번 (일반 레벨업: 양쪽 그룹, 보스 보상: side 그룹만)
    /// </summary>
    private struct RewardRound
    {
        public bool isBoss;
        public PlayerSide side;
    }

    // 아직 처리하지 않은 보상 선택 (맨 앞이 지금 보이는 것)
    private readonly Queue<RewardRound> rounds = new Queue<RewardRound>();

    /// <summary>
    /// 지금 보이는 선택이 보스 보상인지
    /// </summary>
    public bool IsBossRound => 0 < rounds.Count && true == rounds.Peek().isBoss;

    /// <summary>
    /// 지금 보이는 보스 보상의 진영 (IsBossRound 일 때만 의미 있음)
    /// </summary>
    public PlayerSide BossRoundSide => 0 < rounds.Count ? rounds.Peek().side : PlayerSide.Left;

    /// <summary>
    /// 남은 선택 수 (지금 보이는 것 포함)
    /// </summary>
    public int PendingRoundCount => rounds.Count;

    /// <summary>
    /// 새 선택이 보일 때 (보스 보상인지, 보스 보상 진영). 보스 보상 제목 등 연출용
    /// </summary>
    public event System.Action<bool, PlayerSide> OnRoundShown;

    private bool isOpen = false;

    public bool IsOpen => isOpen;

    // 선택해야 재개할 수 있는 선택지 그룹
    private SelectPanelGroup[] selectPanelGroups;

    // 열고 닫을 패널들 (Awake 시점의 직계 자식)
    // - LetterboxCanvas 가 나중에 자식을 LetterboxRoot 아래로 옮겨도 같은 오브젝트를 켜고 끄도록 미리 저장한다
    private GameObject[] contentObjects;

    private void Awake()
    {
        ValidateSetup();

        contentObjects = new GameObject[transform.childCount];
        for (int i = 0; i < contentObjects.Length; ++i)
        {
            contentObjects[i] = transform.GetChild(i).gameObject;
        }

        // 자식이 꺼지면 Awake 가 호출되지 않으므로, 끄기 전에 레벨업 구독을 위한 초기화를 해 둠
        selectPanelGroups = GetComponentsInChildren<SelectPanelGroup>(true);
        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            group.Initialize();
            // 보상 순서(일반 / 보스)는 이 UI 가 관리한다
            group.AutoRefreshOnLevelUp = false;
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
        EventManager.Instance.OnBossDefeated -= HandleBossDefeated;
        EventManager.Instance.OnBossDefeated += HandleBossDefeated;

        if (null != resumeButton)
        {
            resumeButton.onClick.AddListener(HandleResume);
        }

        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            group.OnSelectionChanged += HandleSelectionChanged;
        }
        UpdateResumeButton();

        StringTable.OnLanguageChanged += HandleLanguageChanged;
        ApplyResumeLabel();
    }

    private void HandleLanguageChanged(Language _language)
    {
        ApplyResumeLabel();
    }

    private void ApplyResumeLabel()
    {
        if (null == resumeButton || true == string.IsNullOrEmpty(resumeLabelKey))
        {
            return;
        }
        if (null == resumeLabel)
        {
            TMP_Text found = resumeButton.GetComponentInChildren<TMP_Text>(true);
            // LocalizedText 가 관리하는 라벨이면 덮어쓰지 않는다
            if (null != found && null != found.GetComponent<LocalizedText>())
            {
                return;
            }
            resumeLabel = found;
        }
        if (null != resumeLabel)
        {
            resumeLabel.text = StringTable.Get(resumeLabelKey);
        }
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= HandleLanguageChanged;

        // 열린 채로 씬이 바뀌면(재시작·타이틀) 다음 씬이 멈춘 채 시작하지 않도록 일시정지 요청을 푼다
        if (true == isOpen)
        {
            GamePause.Resume(this);
        }

        // 종료 중 매니져가 새로 생성되지 않도록 HasInstance 로 확인
        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnLevelUp -= HandleLevelUp;
            EventManager.Instance.OnBossDefeated -= HandleBossDefeated;
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

        for (int i = 0; i < _amount; ++i)
        {
            EnqueueRound(false, PlayerSide.Left);
        }
    }

    private void HandleBossDefeated(PlayerSide _side, MidBoss _boss)
    {
        OpenBossReward(_side);
    }

    /// <summary>
    /// _side 진영에 보스 보상 선택 1회를 연다 (이미 열려 있으면 큐 뒤에 붙는다)
    /// 그 진영 그룹만 보이고 Weapon / WeaponUpgrade 풀에서 디버프 없이 뽑는다. 풀이 비면 그 진영 일반 레벨업 보상으로 대신한다
    /// </summary>
    public void OpenBossReward(PlayerSide _side)
    {
        EnqueueRound(true, _side);
    }

    private void EnqueueRound(bool _isBoss, PlayerSide _side)
    {
        RewardRound round = new RewardRound();
        round.isBoss = _isBoss;
        round.side = _side;
        rounds.Enqueue(round);

        if (false == IsOpen)
        {
            Open();
        }
    }

    /// <summary>
    /// 큐 맨 앞 선택을 그룹에 채운다. 보스 보상이면 다른 진영 그룹은 숨겨 재개 조건과 보상 적용에서 빠진다
    /// </summary>
    private void ShowCurrentRound()
    {
        if (0 == rounds.Count)
        {
            return;
        }

        RewardRound round = rounds.Peek();
        bool hasGroup = false;
        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            bool visible = false == round.isBoss || round.side == group.Side;
            group.gameObject.SetActive(visible);
            if (false == visible)
            {
                continue;
            }

            hasGroup = true;
            if (true == round.isBoss)
            {
                group.RefreshBossRewards();
            }
            else
            {
                group.RefreshRewards();
            }
        }

        if (true == round.isBoss && false == hasGroup)
        {
            Debug.LogWarning($"[LevelUpUI] {round.side} 진영 SelectPanelGroup 이 없어 보스 보상을 건너뜁니다. ({name})", this);
        }

        UpdateResumeButton();
        OnRoundShown?.Invoke(round.isBoss, round.side);
    }

    private void Update()
    {
        if (false == isOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (null == keyboard || false == keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        // 버튼 클릭과 똑같이 처리 (클릭음 포함). 버튼이 비활성이면 무시
        if (null == resumeButton)
        {
            HandleResume();
        }
        else if (true == resumeButton.interactable)
        {
            resumeButton.onClick.Invoke();
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
        if (0 < rounds.Count)
        {
            rounds.Dequeue();
        }

        if (0 == rounds.Count)
        {
            Close();
        }
        else
        {
            // 남은 선택은 새 선택지를 뽑아 다시 선택해야 재개된다
            ShowCurrentRound();
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
            // 보스 보상 중 숨긴 그룹은 이전 선택이 남아 있으므로 건너뛴다
            if (false == group.gameObject.activeInHierarchy)
            {
                continue;
            }

            SelectPanel selected = group.SelectedPanel;
            if (null == selected)
            {
                continue;
            }

            rewardManager.ApplyReward(selected.Data1, group.Side);
            RewardSound.PlayConfirm(selected.Data1, selected.Data2, group.Side);

            // 무기 / 무기 업그레이드 패널은 디버프가 없다
            if (null != selected.Data2)
            {
                rewardManager.ApplyReward(selected.Data2, group.Side.Opposite());
            }
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
            // 숨긴 그룹(보스 보상의 다른 진영)은 선택하지 않아도 된다
            if (false == group.gameObject.activeInHierarchy)
            {
                continue;
            }

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
    /// 루트는 이벤트 구독을 위해 켜둔 채, Awake 시점의 직계 자식 전체를 On/Off
    /// </summary>
    private void SetChildrenActive(bool _active)
    {
        foreach (GameObject content in contentObjects)
        {
            if (null != content)
            {
                content.SetActive(_active);
            }
        }
    }

    private void Open()
    {
        SetChildrenActive(true);
        isOpen = true;
        GamePause.Pause(this);
        ShowCurrentRound();
    }

    private void Close()
    {
        rounds.Clear();

        // 보스 보상으로 숨긴 그룹을 다음에 열 때를 위해 되돌린다
        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            if (null != group)
            {
                group.gameObject.SetActive(true);
            }
        }

        SetChildrenActive(false);
        isOpen = false;
        GamePause.Resume(this);
    }
}
