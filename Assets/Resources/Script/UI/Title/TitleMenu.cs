using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬 메뉴
/// - 게임 시작 버튼 : 차원 선택 씬(DimensionSelectScene) 으로 이동
/// - 설정 버튼 : 설정 창(SettingsPanel) 열기. ESC 로 닫는다.
/// 씬 이름은 Build Settings 에 등록되어 있어야 한다.
/// </summary>
public class TitleMenu
    : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string gameSceneName = "DimensionSelectScene";

    [Header("설정")]
    [SerializeField] private Button settingsButton;
    [Tooltip("설정 창 (꺼진 상태로 두기)")]
    [SerializeField] private SettingsPanel settingsPanel;

    private void Awake()
    {
        if (null != startButton)
        {
            startButton.onClick.AddListener(OnClickStart);
        }
        if (null != settingsButton)
        {
            settingsButton.onClick.AddListener(OnClickSettings);
        }
        if (null != settingsPanel)
        {
            settingsPanel.gameObject.SetActive(false);
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
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null == keyboard || false == keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (null != settingsPanel && true == settingsPanel.IsOpen)
        {
            settingsPanel.Close();
        }
    }

    public void OnClickStart()
    {
        if (null != settingsPanel && true == settingsPanel.IsOpen)
        {
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnClickSettings()
    {
        if (null != settingsPanel)
        {
            settingsPanel.Open();
        }
    }
}
