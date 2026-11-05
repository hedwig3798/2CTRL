using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameScene 일시정지 메뉴 (ESC)
/// - 계속하기 / 설정 / 타이틀로
/// - 열면 GamePause.Pause(this), 닫으면 Resume(this). 꺼지거나 파괴될 때도 Resume 해서 timeScale 이 0 으로 남지 않게 한다.
/// - 다른 요청자(레벨업 선택, 게임 오버 등)가 이미 일시정지 중이거나 게임 오버 화면이 떠 있으면 열리지 않는다.
/// - 설정 창이 열려 있을 때 ESC 는 설정 창만 닫는다.
/// - 이 컴포넌트는 항상 켜진 오브젝트에 두고, panel 만 켜고 끈다. (Update 는 timeScale 0 에서도 돈다)
/// </summary>
public class PauseMenu
    : MonoBehaviour
{
    [Tooltip("켜고 끌 메뉴 패널 (이 오브젝트의 자식 권장)")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button titleButton;

    [Tooltip("설정 창 (꺼진 상태로 두기)")]
    [SerializeField] private SettingsPanel settingsPanel;

    [Tooltip("게임 오버 화면이 떠 있으면 일시정지 메뉴를 열지 않는다 (비워도 됨)")]
    [SerializeField] private GameOverUI gameOverUI;

    [Tooltip("Build Settings 에 등록된 타이틀 씬 이름")]
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (null != panel)
        {
            panel.SetActive(false);
        }
        if (null != settingsPanel)
        {
            settingsPanel.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (null != resumeButton)
        {
            resumeButton.onClick.AddListener(Close);
        }
        if (null != settingsButton)
        {
            settingsButton.onClick.AddListener(OpenSettings);
        }
        if (null != titleButton)
        {
            titleButton.onClick.AddListener(GoToTitle);
        }
    }

    private void OnDisable()
    {
        if (null != resumeButton)
        {
            resumeButton.onClick.RemoveListener(Close);
        }
        if (null != settingsButton)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
        }
        if (null != titleButton)
        {
            titleButton.onClick.RemoveListener(GoToTitle);
        }

        // 메뉴가 열린 채 꺼지면(씬 전환 등) 일시정지만 해제
        // (비활성화 도중에는 계층 활성 상태를 바꾸지 않는다)
        if (true == isOpen)
        {
            isOpen = false;
            GamePause.Resume(this);
        }
    }

    private void OnDestroy()
    {
        GamePause.Resume(this);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null == keyboard || false == keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        // 꺼진 동안 일시정지가 풀렸다면 패널 상태를 다시 맞춘다
        if (false == isOpen && null != panel && true == panel.activeSelf)
        {
            panel.SetActive(false);
        }

        if (true == isOpen)
        {
            if (null != settingsPanel && true == settingsPanel.IsOpen)
            {
                settingsPanel.Close();
            }
            else
            {
                Close();
            }
            return;
        }

        Open();
    }

    /// <summary>
    /// 일시정지 메뉴 열기 (게임 오버 중이거나 다른 일시정지 중이면 무시)
    /// </summary>
    public void Open()
    {
        if (true == isOpen || false == CanOpen())
        {
            return;
        }

        isOpen = true;
        GamePause.Pause(this);
        if (null != panel)
        {
            panel.SetActive(true);
        }
    }

    /// <summary>
    /// 메뉴(와 설정 창)를 닫고 재개
    /// </summary>
    public void Close()
    {
        if (false == isOpen)
        {
            return;
        }

        isOpen = false;
        if (null != settingsPanel && true == settingsPanel.IsOpen)
        {
            settingsPanel.Close();
        }
        if (null != panel)
        {
            panel.SetActive(false);
        }
        GamePause.Resume(this);
    }

    public void OpenSettings()
    {
        if (false == isOpen || null == settingsPanel)
        {
            return;
        }
        settingsPanel.Open();
    }

    public void GoToTitle()
    {
        Close();
        SceneManager.LoadScene(titleSceneName);
    }

    private bool CanOpen()
    {
        if (null != gameOverUI && true == gameOverUI.IsShown)
        {
            return false;
        }
        // 레벨업 선택 / 게임 오버 등 다른 곳이 이미 멈춘 상태면 겹쳐 열지 않는다
        return false == GamePause.IsPaused;
    }
}
