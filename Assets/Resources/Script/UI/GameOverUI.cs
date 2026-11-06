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
/// - 무기별 데미지는 무기 목록(leftWeaponList / rightWeaponList)이 연결되어 있으면 아이콘 + 수치 칸(GameOverWeaponRow)으로 표시
///   - 아이콘은 weaponRewards 중 통계 키와 맞는 보상(보상 이름 키 / 무기 프리팹 이름 / 프리팹 DescriptionHolder 이름 키)의 이미지
///   - 아이콘을 못 찾으면 "이름 : 수치" 로 대체. 목록이 연결되지 않았으면 예전처럼 통계 텍스트에 "이름 : 수치" 줄로 넣는다
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

    [Tooltip("{0} = 데미지 (아이콘 옆에 표시)")]
    [SerializeField]
    private string weaponValueFormatKey = "gameover.weapon_value";

    [Header("weapon icons")]
    [Tooltip("왼쪽 화면 무기별 데미지 칸을 담을 부모 (비우면 텍스트로 표시)")]
    [SerializeField]
    private RectTransform leftWeaponList;

    [Tooltip("오른쪽 화면 무기별 데미지 칸을 담을 부모 (비우면 텍스트로 표시)")]
    [SerializeField]
    private RectTransform rightWeaponList;

    [Tooltip("복제해서 쓸 무기 칸 (꺼 둔 템플릿)")]
    [SerializeField]
    private GameOverWeaponRow weaponRowTemplate;

    [Tooltip("아이콘을 찾을 무기 보상 목록")]
    [SerializeField]
    private RewardData[] weaponRewards;

    private readonly StringBuilder builder = new StringBuilder(256);

    // 언어가 바뀌었을 때 다시 채우기 위한 마지막 표시 내용
    private StatisticsManager lastStats;
    private bool lastVictory;
    private readonly List<KeyValuePair<string, float>> sortedWeapons = new List<KeyValuePair<string, float>>();

    // 통계 키 -> 아이콘 (처음 표시할 때 weaponRewards 로 만든다)
    private Dictionary<string, Sprite> iconByKey;
    private readonly List<GameOverWeaponRow> leftRows = new List<GameOverWeaponRow>();
    private readonly List<GameOverWeaponRow> rightRows = new List<GameOverWeaponRow>();

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

        SetSide(leftLabel, leftWeaponList, leftRows, StringTable.Get(leftHeaderKey), title, null != _stats ? _stats.Get(PlayerSide.Left) : null);
        SetSide(rightLabel, rightWeaponList, rightRows, StringTable.Get(rightHeaderKey), title, null != _stats ? _stats.Get(PlayerSide.Right) : null);
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

    private void SetSide(TMP_Text _label, RectTransform _list, List<GameOverWeaponRow> _rows, string _header, string _title, PlayerCombatStats _stats)
    {
        bool useRows = null != _list && null != weaponRowTemplate;

        sortedWeapons.Clear();
        if (null != _stats)
        {
            _stats.GetSortedWeaponDamage(sortedWeapons);
        }

        if (null != _label)
        {
            _label.text = BuildText(_header, _title, _stats, useRows);
        }

        if (true == useRows)
        {
            FillRows(_list, _rows);
        }
    }

    /// <summary>
    /// sortedWeapons 를 아이콘 칸으로 채우고 남는 칸은 끈다
    /// </summary>
    private void FillRows(RectTransform _list, List<GameOverWeaponRow> _rows)
    {
        string valueFormat = StringTable.Get(weaponValueFormatKey);
        string lineFormat = StringTable.Get(weaponFormatKey);

        for (int i = 0; i < sortedWeapons.Count; i++)
        {
            if (i >= _rows.Count)
            {
                GameOverWeaponRow row = Instantiate(weaponRowTemplate, _list, false);
                row.name = weaponRowTemplate.name + "_" + i;
                _rows.Add(row);
            }

            KeyValuePair<string, float> pair = sortedWeapons[i];
            string valueOnly = StringTable.FormatText(valueFormat, pair.Value);
            string withName = StringTable.FormatText(lineFormat, StringTable.Resolve(pair.Key), pair.Value).Trim();
            _rows[i].gameObject.SetActive(true);
            _rows[i].Set(FindIcon(pair.Key), valueOnly, withName);
        }

        for (int i = sortedWeapons.Count; i < _rows.Count; i++)
        {
            _rows[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 통계 키에 맞는 무기 아이콘 (없으면 null)
    /// </summary>
    public Sprite FindIcon(string _weaponKey)
    {
        if (true == string.IsNullOrEmpty(_weaponKey))
        {
            return null;
        }

        if (null == iconByKey)
        {
            iconByKey = new Dictionary<string, Sprite>();
            if (null != weaponRewards)
            {
                foreach (RewardData reward in weaponRewards)
                {
                    if (null == reward || false == reward.HasImage)
                    {
                        continue;
                    }

                    Sprite image = reward.GetImage();
                    AddIconKey(reward.GetNameKey(), image);
                    if (reward.GetEffect() is WeaponRewardEffect effect && null != effect.WeaponPrefab)
                    {
                        AddIconKey(effect.WeaponPrefab.name, image);
                        if (true == effect.WeaponPrefab.TryGetComponent(out DescriptionHolder holder))
                        {
                            AddIconKey(holder.GetNameKey(), image);
                        }
                    }
                }
            }
        }

        iconByKey.TryGetValue(_weaponKey, out Sprite icon);
        return icon;
    }

    private void AddIconKey(string _key, Sprite _icon)
    {
        if (false == string.IsNullOrEmpty(_key) && false == iconByKey.ContainsKey(_key))
        {
            iconByKey.Add(_key, _icon);
        }
    }

    /// <summary>
    /// 한 화면의 통계 문자열 (무기별 데미지는 큰 순서, sortedWeapons 를 미리 채워 둔다)
    /// - _useRows 면 무기 줄은 넣지 않는다 (아이콘 칸이 표시)
    /// </summary>
    private string BuildText(string _header, string _title, PlayerCombatStats _stats, bool _useRows)
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

        // 아이콘 칸을 쓰면 EXP 를 먼저 쓰고 무기 제목은 맨 아래 (그 아래에 칸이 놓인다)
        if (true == _useRows)
        {
            builder.AppendLine(StringTable.Format(expFormatKey, exp));
            builder.Append(StringTable.Get(weaponHeaderKey));
            if (0 == sortedWeapons.Count)
            {
                builder.AppendLine();
                builder.Append(StringTable.Get(noWeaponKey));
            }
            return builder.ToString();
        }

        builder.AppendLine(StringTable.Get(weaponHeaderKey));
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
