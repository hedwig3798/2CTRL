using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 씬 메뉴
/// - 게임 시작 버튼 : 차원 선택 씬(DimensionSelectScene) 으로 이동
/// 씬 이름은 Build Settings 에 등록되어 있어야 한다.
/// </summary>
public class TitleMenu
    : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private string gameSceneName = "DimensionSelectScene";

    private void Awake()
    {
        if (null != startButton)
        {
            startButton.onClick.AddListener(OnClickStart);
        }
    }

    private void OnDestroy()
    {
        if (null != startButton)
        {
            startButton.onClick.RemoveListener(OnClickStart);
        }
    }

    public void OnClickStart()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}
