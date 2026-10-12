using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 시 게임을 일시정지하고 레벨업 패널을 표시하는 UI
/// - EventManager.OnLevelUp 을 구독하므로, 항상 활성 상태인 Canvas 루트에 붙이고 자식 전체를 On/Off 한다.
/// - 한 번에 여러 레벨이 오르면 오른 레벨 수만큼 버튼을 눌러야 재개된다.
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

    private void Awake()
    {
        ValidateSetup();

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
        pendingCount--;

        if (pendingCount <= 0)
        {
            Close();
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
    }

    private void Close()
    {
        pendingCount = 0;

        SetChildrenActive(false);
        isOpen = false;
        GamePause.Resume(this);
    }
}
