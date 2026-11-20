using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 "게임 종료" 확인 팝업
/// - 이 컴포넌트가 붙은 오브젝트 자체를 켜고 끈다. 씬에는 꺼진 상태로 두고 Open() 으로 연다 (PermanentUpgradePanel 과 같은 방식)
/// - 확인 버튼 : 게임 종료 (에디터에서는 플레이 모드 종료)
/// - 취소 버튼 / ESC(TitleMenu 가 처리) : 팝업 닫기
/// </summary>
public class QuitConfirmPanel
    : MonoBehaviour
{
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("팝업을 닫을 때 (취소 버튼 / ESC)")]
    [SerializeField] private SoundId closeSound = SoundId.UI_BACK;

    /// <summary>
    /// 확인을 눌러 종료를 요청했을 때 (테스트·연출용)
    /// </summary>
    public event Action OnQuitRequested;

    public bool IsOpen => gameObject.activeSelf;

    public void Open()
    {
        if (true == gameObject.activeSelf)
        {
            return;
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        if (false == gameObject.activeSelf)
        {
            return;
        }
        gameObject.SetActive(false);
        SoundManager.Play(closeSound);
    }

    private void Awake()
    {
        // 취소 버튼은 Close 의 closeSound 가 대신한다 (클릭음과 겹치지 않도록)
        UIButtonSound.Assign(cancelButton, SoundId.None);
    }

    private void OnEnable()
    {
        if (null != confirmButton)
        {
            confirmButton.onClick.AddListener(Quit);
        }
        if (null != cancelButton)
        {
            cancelButton.onClick.AddListener(Close);
        }
    }

    private void OnDisable()
    {
        if (null != confirmButton)
        {
            confirmButton.onClick.RemoveListener(Quit);
        }
        if (null != cancelButton)
        {
            cancelButton.onClick.RemoveListener(Close);
        }
    }

    public void Quit()
    {
        OnQuitRequested?.Invoke();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
