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
/// - 표시 문자열은 모두 StringTable 키. 표시 중에 언어가 바뀌면 마지막 통계로 다시 채운다
/// - 무기 이름은 통계 키(무기 이름 StringTable 키 또는 오브젝트 이름)를 번역해서 표시 (키가 없으면 그대로)
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

    [Header("text (StringTable 키)")]
    [SerializeField]
    private string titleKey = "gameover.title";
    [SerializeField]
    private string victoryKey = "gameover.victory";
    [SerializeField]
    private string leftHeaderKey = "gameover.left_header";
    [SerializeField]
    private string rightHeaderKey = "gameover.right_header";
    [Tooltip("{0} = 킬 수")]
    [SerializeField]
    private string killFormatKey = "gameover.kill";
    [Tooltip("{0} = 받은 데미지")]
    [SerializeField]
    private string damageTakenFormatKey = "gameover.damage_taken";
    [SerializeField]
    private string weaponHeaderKey = "gameover.weapon_header";
    [Tooltip("{0} = 무기 이름, {1} = 데미지")]
    [SerializeField]
    private string weaponFormatKey = "gameover.weapon_line";
    [SerializeField]
    private string noWeaponKey = "gameover.no_weapon";
    [Tooltip("{0} = 획득 EXP")]
    [SerializeField]
    private string expFormatKey = "gameover.exp";

    private readonly StringBuilder builder = new StringBuilder(256);

    // 언어가 바뀌었을 때 다시 채우기 위한 마지막 표시 내용
    private StatisticsManager lastStats;
    private bool lastVictory;
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
        StringTable.OnLanguageChanged += HandleLanguageChanged;
        // 꺼져 있는 동안 언어가 바뀌었을 수 있으므로 다시 표시 (표시 중일 때만 채움)
        HandleLanguageChanged(StringTable.CurrentLanguage);
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
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(Language _language)
    {
        if (true == IsShown)
        {
            Fill(lastStats, lastVictory);
        }
    }

    /// <summary>
    /// 통계로 텍스트를 채우고 패널을 켠다 (_stats 가 null 이면 빈 통계로 표시)
    /// - _victory 가 true 면 제목을 승리, 아니면 게임 오버로 표시
    /// </summary>
    public void Show(StatisticsManager _stats, bool _victory = false)
    {
        lastStats = _stats;
        lastVictory = _victory;
        Fill(_stats, _victory);

        if (null != panel)
        {
            panel.SetActive(true);
        }
    }

    private void Fill(StatisticsManager _stats, bool _victory)
    {
        string title = StringTable.Get(true == _victory ? victoryKey : titleKey);

        if (null != titleLabel)
        {
            titleLabel.text = title;
        }

        SetSideText(leftLabel, StringTable.Get(leftHeaderKey), title, null != _stats ? _stats.Get(PlayerSide.Left) : null);
        SetSideText(rightLabel, StringTable.Get(rightHeaderKey), title, null != _stats ? _stats.Get(PlayerSide.Right) : null);
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

        builder.AppendLine(StringTable.Format(killFormatKey, kills));
        builder.AppendLine(StringTable.Format(damageTakenFormatKey, damageTaken));

        builder.AppendLine(StringTable.Get(weaponHeaderKey));
        sortedWeapons.Clear();
        if (null != _stats)
        {
            _stats.GetSortedWeaponDamage(sortedWeapons);
        }

        if (0 == sortedWeapons.Count)
        {
            builder.AppendLine(StringTable.Get(noWeaponKey));
        }
        else
        {
            string weaponFormat = StringTable.Get(weaponFormatKey);
            foreach (KeyValuePair<string, float> pair in sortedWeapons)
            {
                builder.AppendLine(StringTable.FormatText(weaponFormat, StringTable.Resolve(pair.Key), pair.Value));
            }
        }

        builder.Append(StringTable.Format(expFormatKey, exp));
        return builder.ToString();
    }
}
