using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 종료(게임 오버 / 승리) 시 양쪽 화면 통계와 재시작·타이틀 버튼을 보여 주는 패널
/// - 한 명이 양쪽을 동시에 플레이하는 싱글 게임이므로 좌우 승패는 표시하지 않는다
/// - GameOverManager 가 Show 를 호출한다
/// - 재시작 : 현재 씬을 다시 로드 (DimensionSelection 이 static 이라 같은 차원 설정 유지)
/// - 타이틀 : titleSceneName 씬으로 이동
/// - 시작 시 panel 을 끈다. panel 은 이 컴포넌트가 붙은 오브젝트의 자식으로 두는 것을 권장
/// </summary>
public class GameOverUI
    : MonoBehaviour
{
    [Tooltip("켜고 끌 패널 루트")]
    [SerializeField]
    private GameObject panel;

    [Tooltip("제목 텍스트 (비우면 각 화면 텍스트 맨 위에 제목을 넣는다)")]
    [SerializeField]
    private TMP_Text titleLabel;

    [Tooltip("왼쪽 화면 통계 텍스트")]
    [SerializeField]
    private TMP_Text leftLabel;

    [Tooltip("오른쪽 화면 통계 텍스트")]
    [SerializeField]
    private TMP_Text rightLabel;

    [Tooltip("같은 설정으로 다시 시작")]
    [SerializeField]
    private Button restartButton;

    [Tooltip("타이틀 씬으로 이동")]
    [SerializeField]
    private Button titleButton;

    [Tooltip("Build Settings 에 등록된 타이틀 씬 이름")]
    [SerializeField]
    private string titleSceneName = "TitleScene";

    [Header("text")]
    [SerializeField]
    private string titleText = "게임 오버";
    [SerializeField]
    private string victoryText = "승리";
    [SerializeField]
    private string leftHeader = "왼쪽 화면";
    [SerializeField]
    private string rightHeader = "오른쪽 화면";
    [SerializeField]
    private string killFormat = "킬 : {0}";
    [SerializeField]
    private string damageTakenFormat = "받은 데미지 : {0:0}";
    [SerializeField]
    private string weaponHeader = "무기별 데미지";
    [SerializeField]
    private string weaponFormat = "  {0} : {1:0}";
    [SerializeField]
    private string noWeaponText = "  없음";
    [SerializeField]
    private string expFormat = "획득 EXP : {0:0}";

    private readonly StringBuilder builder = new StringBuilder(256);
    private readonly List<KeyValuePair<string, float>> sortedWeapons = new List<KeyValuePair<string, float>>();

    public bool IsShown => null != panel && true == panel.activeSelf;

    private void Awake()
    {
        if (null == panel)
        {
            Debug.LogWarning("[GameOverUI] panel 이 연결되지 않았습니다.", this);
            return;
        }

        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (null != restartButton)
        {
            restartButton.onClick.AddListener(Restart);
        }
        if (null != titleButton)
        {
            titleButton.onClick.AddListener(GoToTitle);
        }
    }

    private void OnDisable()
    {
        if (null != restartButton)
        {
            restartButton.onClick.RemoveListener(Restart);
        }
        if (null != titleButton)
        {
            titleButton.onClick.RemoveListener(GoToTitle);
        }
    }

    /// <summary>
    /// 통계로 텍스트를 채우고 패널을 켠다 (_stats 가 null 이면 빈 통계로 표시)
    /// - _victory 가 true 면 제목을 승리, 아니면 게임 오버로 표시
    /// </summary>
    public void Show(StatisticsManager _stats, bool _victory = false)
    {
        string title = true == _victory ? victoryText : titleText;

        if (null != titleLabel)
        {
            titleLabel.text = title;
        }

        SetSideText(leftLabel, leftHeader, title, null != _stats ? _stats.Get(PlayerSide.Left) : null);
        SetSideText(rightLabel, rightHeader, title, null != _stats ? _stats.Get(PlayerSide.Right) : null);

        if (null != panel)
        {
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (null != panel)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 같은 설정(선택한 차원)으로 현재 씬을 다시 시작
    /// </summary>
    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// 타이틀 씬으로 이동
    /// </summary>
    public void GoToTitle()
    {
        SceneManager.LoadScene(titleSceneName);
    }

    private void SetSideText(TMP_Text _label, string _header, string _title, PlayerCombatStats _stats)
    {
        if (null == _label)
        {
            return;
        }

        _label.text = BuildText(_header, _title, _stats);
    }

    /// <summary>
    /// 한 화면의 통계 문자열 (무기별 데미지는 큰 순서)
    /// </summary>
    private string BuildText(string _header, string _title, PlayerCombatStats _stats)
    {
        builder.Clear();

        if (null == titleLabel)
        {
            builder.AppendLine(_title);
        }
        builder.AppendLine(_header);

        int kills = null != _stats ? _stats.Kills : 0;
        float damageTaken = null != _stats ? _stats.DamageTaken : 0f;
        float exp = null != _stats ? _stats.Exp : 0f;

        builder.AppendFormat(killFormat, kills).AppendLine();
        builder.AppendFormat(damageTakenFormat, damageTaken).AppendLine();

        builder.AppendLine(weaponHeader);
        sortedWeapons.Clear();
        if (null != _stats)
        {
            _stats.GetSortedWeaponDamage(sortedWeapons);
        }

        if (0 == sortedWeapons.Count)
        {
            builder.AppendLine(noWeaponText);
        }
        else
        {
            foreach (KeyValuePair<string, float> pair in sortedWeapons)
            {
                builder.AppendFormat(weaponFormat, pair.Key, pair.Value).AppendLine();
            }
        }

        builder.AppendFormat(expFormat, exp);
        return builder.ToString();
    }
}
