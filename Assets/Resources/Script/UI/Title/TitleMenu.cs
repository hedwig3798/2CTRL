using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬 메뉴
/// - 게임 시작 버튼 : 차원 선택 씬(DimensionSelectScene) 으로 이동
/// - 설정 버튼 : 설정 창(SettingsPanel) 열기. ESC 로 닫는다.
/// - 차원 강화 버튼 : 영구 강화 창(PermanentUpgradePanel) 열기. ESC 로 닫는다.
/// - 창이 하나라도 열려 있으면 시작 버튼과 다른 창 열기를 막는다.
/// 씬 이름은 Build Settings 에 등록되어 있어야 한다.
/// </summary>
public class TitleMenu
    : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string gameSceneName = "DimensionSelectScene";
    [Tooltip("시작 버튼 소리")]
    [SerializeField] private SoundId startSound = SoundId.UI_START;

    [Header("설정")]
    [SerializeField] private Button settingsButton;
    [Tooltip("설정 창 (꺼진 상태로 두기)")]
    [SerializeField] private SettingsPanel settingsPanel;

    [Header("차원 강화")]
    [SerializeField] private Button upgradeButton;
    [Tooltip("영구 강화 창 (꺼진 상태로 두기)")]
    [SerializeField] private PermanentUpgradePanel upgradePanel;

    private bool IsSettingsOpen => null != settingsPanel && true == settingsPanel.IsOpen;
    private bool IsUpgradeOpen => null != upgradePanel && true == upgradePanel.IsOpen;

    private void Awake()
    {
        if (null != startButton)
        {
            startButton.onClick.AddListener(OnClickStart);
            // 자동 부착 클릭음(UI_CLICK) 대신 시작음
            UIButtonSound.Assign(startButton, startSound);
        }
        if (null != settingsButton)
        {
            settingsButton.onClick.AddListener(OnClickSettings);
        }
        if (null != settingsPanel)
        {
            settingsPanel.gameObject.SetActive(false);
        }
        if (null != upgradeButton)
        {
            upgradeButton.onClick.AddListener(OnClickUpgrade);
        }
        if (null != upgradePanel)
        {
            upgradePanel.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (null != startButton)
        {
            startButton.onClick.RemoveListener(OnClickStart);
        }
        if (null != settingsButton)
        {
            settingsButton.onClick.RemoveListener(OnClickSettings);
        }
        if (null != upgradeButton)
        {
            upgradeButton.onClick.RemoveListener(OnClickUpgrade);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null == keyboard || false == keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (true == IsSettingsOpen)
        {
            settingsPanel.Close();
        }
        else if (true == IsUpgradeOpen)
        {
            upgradePanel.Close();
        }
    }

    public void OnClickStart()
    {
        if (true == IsSettingsOpen || true == IsUpgradeOpen)
        {
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnClickSettings()
    {
        if (null != settingsPanel && false == IsUpgradeOpen)
        {
            settingsPanel.Open();
        }
    }

    public void OnClickUpgrade()
    {
        if (null != upgradePanel && false == IsSettingsOpen)
        {
            upgradePanel.Open();
        }
    }
}
