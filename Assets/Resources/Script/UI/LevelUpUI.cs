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

    [Header("dimension fusion")]
    [Tooltip("차원 무기 합성법 목록 (비우면 합성 카드를 띄우지 않음)")]
    [SerializeField]
    private DimensionFusionRegistry fusionRegistry;

    [Tooltip("중앙 합성 카드 (비우면 합성 카드를 띄우지 않음)")]
    [SerializeField]
    private DimensionFusionCard fusionCard;

    [Tooltip("합성 카드가 있는 라운드에서 양쪽 그룹을 바깥쪽으로 미는 거리 (px)")]
    [Min(0f)]
    [SerializeField]
    private float fusionGroupOffset = 200f;

    [Tooltip("왼쪽 그룹 RectTransform (비우면 Left 진영 SelectPanelGroup)")]
    [SerializeField]
    private RectTransform leftGroupRect;

    [Tooltip("오른쪽 그룹 RectTransform (비우면 Right 진영 SelectPanelGroup)")]
    [SerializeField]
    private RectTransform rightGroupRect;

    // 중앙 합성 카드 커서 상태
    private readonly DimensionFusionCursor fusionCursor = new DimensionFusionCursor();

    // 이번 라운드 합성 후보 (없으면 null)
    private DimensionWeaponData fusionCandidate;

    // 카드로 옮기기 전에 그룹에서 고르고 있던 패널 (복귀할 때 되돌림)
    private SelectPanel leftRememberedPanel;
    private SelectPanel rightRememberedPanel;

    // 밀어낸 그룹과 원래 위치
    private bool isGroupShifted;
    private RectTransform shiftedLeftRect;
    private RectTransform shiftedRightRect;
    private Vector2 leftGroupOrigin;
    private Vector2 rightGroupOrigin;

    /// <summary>
    /// 지금 라운드에 중앙 합성 카드가 있는지
    /// </summary>
    public bool HasFusionCard => fusionCursor.HasCard;

    /// <summary>
    /// 지금 라운드의 합성 후보 (없으면 null)
    /// </summary>
    public DimensionWeaponData FusionCandidate => fusionCandidate;

    /// <summary>
    /// _side 커서가 중앙 합성 카드에 있는지
    /// </summary>
    public bool IsFusionSelected(PlayerSide _side) => fusionCursor.IsOnCard(_side);

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
        if (null != fusionCard)
        {
            fusionCard.OnClicked += HandleFusionCardClicked;
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
        if (null != fusionCard)
        {
            fusionCard.OnClicked -= HandleFusionCardClicked;
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

        // 커서 상태를 먼저 새 라운드로 돌린다 (RefreshRewards 의 선택 변경 콜백이 이전 라운드 커서를 보지 않도록)
        SetupFusion(round.isBoss);

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
        if (null == keyboard)
        {
            return;
        }

        // 중앙 합성 카드 커서: 왼쪽 D / A, 오른쪽 ← / →
        if (true == fusionCursor.HasCard)
        {
            HandleFusionKey(PlayerSide.Left, keyboard.dKey.wasPressedThisFrame, keyboard.aKey.wasPressedThisFrame);
            HandleFusionKey(PlayerSide.Right, keyboard.leftArrowKey.wasPressedThisFrame, keyboard.rightArrowKey.wasPressedThisFrame);
        }

        if (false == keyboard.spaceKey.wasPressedThisFrame)
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

        // 어느 한쪽이라도 중앙 합성 카드를 골랐으면 합성만 적용 (다른 선택 무시)
        if (true == fusionCursor.ShouldApplyFusion)
        {
            // 합성에 실패하면 카드를 거두고 라운드를 유지한다 (일반 보상을 다시 고르게)
            if (false == ApplyFusionReward())
            {
                AbandonFusion();
                return;
            }
        }
        else
        {
            ApplySelectedRewards();
        }
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
        // 카드에 있던 쪽이 그룹 패널을 다시 고르면(클릭 / W·S / ↑·↓) 커서를 그룹으로 되돌린다
        if (true == fusionCursor.AnyOnCard)
        {
            bool changed = false;
            foreach (SelectPanelGroup group in selectPanelGroups)
            {
                if (null != group && null != group.SelectedPanel && true == fusionCursor.MoveToGroup(group.Side))
                {
                    SetRememberedPanel(group.Side, null);
                    changed = true;
                }
            }
            if (true == changed)
            {
                ApplyFusionCursor();
            }
        }

        UpdateResumeButton();
    }

    /// <summary>
    /// 일반 라운드면 합성 후보를 찾아 중앙 카드를 띄우고 양쪽 그룹을 바깥으로 민다. 보스 라운드 / 후보 없음이면 카드를 숨기고 원위치
    /// </summary>
    private void SetupFusion(bool _isBoss)
    {
        fusionCandidate = null;
        if (false == _isBoss && null != fusionRegistry && null != fusionCard)
        {
            fusionCandidate = fusionRegistry.FindCandidate();
        }

        fusionCursor.Begin(null != fusionCandidate);
        leftRememberedPanel = null;
        rightRememberedPanel = null;

        if (null != fusionCandidate)
        {
            fusionCard.Show(fusionCandidate);
            ShiftGroups(true);
        }
        else
        {
            if (null != fusionCard)
            {
                fusionCard.Hide();
            }
            ShiftGroups(false);
        }
    }

    /// <summary>
    /// 합성 카드를 숨기고 그룹을 원위치 (UI 를 닫을 때)
    /// </summary>
    private void EndFusion()
    {
        fusionCandidate = null;
        fusionCursor.End();
        leftRememberedPanel = null;
        rightRememberedPanel = null;
        if (null != fusionCard)
        {
            fusionCard.Hide();
        }
        ShiftGroups(false);
    }

    /// <summary>
    /// 키 입력 판정은 DimensionFusionCursor.HandleInput 에 맡기고, 상태가 바뀌었을 때 UI 후처리만 한다
    /// </summary>
    private void HandleFusionKey(PlayerSide _side, bool _towardCenter, bool _towardOutside)
    {
        if (false == fusionCursor.HandleInput(_side, _towardCenter, _towardOutside))
        {
            return;
        }

        if (true == fusionCursor.IsOnCard(_side))
        {
            OnMovedToCard(_side);
        }
        else
        {
            OnMovedToGroup(_side);
        }
    }

    private void HandleFusionCardClicked(PlayerSide _side)
    {
        SelectFusionCard(_side);
    }

    /// <summary>
    /// _side 커서를 중앙 합성 카드로 옮기고 그 쪽 그룹 선택을 해제한다 (카드가 없으면 무시)
    /// </summary>
    public void SelectFusionCard(PlayerSide _side)
    {
        if (false == fusionCursor.MoveToCard(_side))
        {
            return;
        }
        OnMovedToCard(_side);
    }

    /// <summary>
    /// 커서가 카드로 옮겨진 뒤: 그 쪽 그룹 선택을 기억하고 해제
    /// </summary>
    private void OnMovedToCard(PlayerSide _side)
    {
        SelectPanelGroup group = FindVisibleGroup(_side);
        if (null != group)
        {
            SetRememberedPanel(_side, group.SelectedPanel);
            group.ClearSelection();
        }

        ApplyFusionCursor();
        UpdateResumeButton();
    }

    /// <summary>
    /// _side 커서를 그룹으로 되돌린다. 카드로 옮기기 전 패널을 다시 고르고, 없으면 첫 번째 패널
    /// </summary>
    public void ReturnToGroup(PlayerSide _side)
    {
        if (false == fusionCursor.MoveToGroup(_side))
        {
            return;
        }
        OnMovedToGroup(_side);
    }

    /// <summary>
    /// 커서가 그룹으로 돌아온 뒤: 기억한 패널(없으면 첫 패널) 을 다시 고른다
    /// </summary>
    private void OnMovedToGroup(PlayerSide _side)
    {
        ApplyFusionCursor();

        SelectPanel remembered = PlayerSide.Left == _side ? leftRememberedPanel : rightRememberedPanel;
        SetRememberedPanel(_side, null);

        SelectPanelGroup group = FindVisibleGroup(_side);
        if (null != group)
        {
            if (null != remembered && true == remembered.gameObject.activeSelf && group == remembered.GetComponentInParent<SelectPanelGroup>(true))
            {
                remembered.Click();
            }
            else
            {
                group.SelectFirst();
            }
        }

        UpdateResumeButton();
    }

    private void SetRememberedPanel(PlayerSide _side, SelectPanel _panel)
    {
        if (PlayerSide.Left == _side)
        {
            leftRememberedPanel = _panel;
        }
        else
        {
            rightRememberedPanel = _panel;
        }
    }

    private void ApplyFusionCursor()
    {
        if (null != fusionCard)
        {
            fusionCard.SetCursor(fusionCursor.IsOnCard(PlayerSide.Left), fusionCursor.IsOnCard(PlayerSide.Right));
        }
    }

    private SelectPanelGroup FindVisibleGroup(PlayerSide _side)
    {
        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            if (null != group && _side == group.Side && true == group.gameObject.activeInHierarchy)
            {
                return group;
            }
        }
        return null;
    }

    /// <summary>
    /// 양쪽 그룹을 바깥쪽으로 fusionGroupOffset 만큼 밀거나(true) 원위치(false)
    /// </summary>
    private void ShiftGroups(bool _shift)
    {
        if (_shift == isGroupShifted)
        {
            return;
        }

        if (true == _shift)
        {
            shiftedLeftRect = ResolveGroupRect(PlayerSide.Left, leftGroupRect);
            shiftedRightRect = ResolveGroupRect(PlayerSide.Right, rightGroupRect);
            if (null != shiftedLeftRect)
            {
                leftGroupOrigin = shiftedLeftRect.anchoredPosition;
                shiftedLeftRect.anchoredPosition = leftGroupOrigin + Vector2.left * fusionGroupOffset;
            }
            if (null != shiftedRightRect)
            {
                rightGroupOrigin = shiftedRightRect.anchoredPosition;
                shiftedRightRect.anchoredPosition = rightGroupOrigin + Vector2.right * fusionGroupOffset;
            }
        }
        else
        {
            if (null != shiftedLeftRect)
            {
                shiftedLeftRect.anchoredPosition = leftGroupOrigin;
            }
            if (null != shiftedRightRect)
            {
                shiftedRightRect.anchoredPosition = rightGroupOrigin;
            }
            shiftedLeftRect = null;
            shiftedRightRect = null;
        }
        isGroupShifted = _shift;
    }

    private RectTransform ResolveGroupRect(PlayerSide _side, RectTransform _assigned)
    {
        if (null != _assigned)
        {
            return _assigned;
        }

        foreach (SelectPanelGroup group in selectPanelGroups)
        {
            if (null != group && _side == group.Side)
            {
                return group.transform as RectTransform;
            }
        }
        return null;
    }

    /// <summary>
    /// 중앙 합성 카드 보상만 적용한다 (합성 효과는 고른 쪽과 무관하게 양쪽 플레이어를 처리)
    /// - 보상 데이터는 적용할 때 만들고 바로 지운다
    /// </summary>
    /// <returns>실제로 합성했으면 true (RewardManager 없음, 합성법 무효, 재료 부족 등이면 false)</returns>
    private bool ApplyFusionReward()
    {
        RewardManager rewardManager = RewardManager.Instance;
        if (null == rewardManager)
        {
            Debug.LogWarning($"[LevelUpUI] RewardManager 가 없어 합성 보상을 적용하지 못했습니다. ({name})", this);
            return false;
        }

        RewardData data = DimensionFusionEffect.CreateReward(fusionCandidate);
        if (null == data)
        {
            Debug.LogWarning($"[LevelUpUI] 합성법이 비어 있거나 유효하지 않아 합성하지 못했습니다. ({name})", this);
            return false;
        }

        PlayerSide side = fusionCursor.FusionContextSide;
        bool fused = DimensionFusionEffect.TryApply(rewardManager, data, side);
        if (true == fused)
        {
            RewardSound.PlayConfirm(data, null, side);
        }
        else
        {
            Debug.LogWarning($"[LevelUpUI] 합성에 실패해 합성 카드를 거둡니다. 일반 보상을 고르세요. ({name})", this);
        }

        RewardEffect effect = data.GetEffect();
        if (null != effect)
        {
            Destroy(effect);
        }
        Destroy(data);
        return fused;
    }

    /// <summary>
    /// 합성 실패 시: 카드에 있던 커서를 그룹으로 되돌리고(기억한 패널 또는 첫 패널) 카드를 숨긴 뒤 그룹을 원위치. 라운드는 유지
    /// </summary>
    private void AbandonFusion()
    {
        ReturnToGroup(PlayerSide.Left);
        ReturnToGroup(PlayerSide.Right);

        fusionCandidate = null;
        fusionCursor.Begin(false);
        leftRememberedPanel = null;
        rightRememberedPanel = null;
        if (null != fusionCard)
        {
            fusionCard.Hide();
        }
        ShiftGroups(false);
        UpdateResumeButton();
    }

    /// <summary>
    /// 재개 가능 여부: 어느 한쪽이라도 중앙 합성 카드를 골랐거나, 보이는 그룹이 모두 선택됨
    /// </summary>
    private bool CanResume()
    {
        return fusionCursor.CanResume(AreGroupsReady());
    }

    /// <summary>
    /// 보이는 패널이 있는 그룹은 모두 선택되어 있어야 재개 가능
    /// </summary>
    private bool AreGroupsReady()
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
        EndFusion();

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
