using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재사용 설정 창 (Assets/prefab/UI/SettingsPanel.prefab, SettingsPanelBuilder 가 생성)
/// - 언어 드롭다운 / 화면 크기 드롭다운 / 적 체력바 표시 / 데미지 표시 / 볼륨 3줄(VolumeSettingRow)
/// - 언어 드롭다운 항목은 문자열 테이블을 거치지 않는 각 언어 고유 표기 (LanguageInfo.GetNativeName)
/// - 언어가 바뀌면 화면 크기 드롭다운 옵션("전체 화면")을 다시 만든다 (고정 라벨은 LocalizedText 가 갱신)
/// - 이 컴포넌트가 붙은 오브젝트 자체를 켜고 끈다. 씬에는 꺼진 상태로 두고 Open() 으로 연다.
/// - 값은 바뀌는 즉시 GameSettings 에 반영되고, 닫을 때 GameSettings.Save() 로 디스크에 저장한다.
/// - ESC 처리는 여는 쪽(TitleMenu, PauseMenu)이 한다. (같은 프레임에 두 번 처리되지 않도록)
///
/// 사용 예)
///   settingsPanel.Open();
///   settingsPanel.OnClosed += HandleSettingsClosed;
/// </summary>
public class SettingsPanel
    : MonoBehaviour
{
    [Header("언어")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("화면")]
    [SerializeField] private TMP_Dropdown screenModeDropdown;

    [Header("정보 표시")]
    [SerializeField] private Toggle enemyHPBarToggle;
    [SerializeField] private Toggle damageTextToggle;

    [Header("소리")]
    [Tooltip("볼륨 줄 (비어있으면 자식에서 탐색)")]
    [SerializeField] private VolumeSettingRow[] volumeRows;

    [Header("닫기")]
    [SerializeField] private Button closeButton;

    // TMP_Dropdown 이 펼친 목록 오브젝트 이름 (템플릿의 부모 = 드롭다운 아래에 만들어진다)
    private const string DROPDOWN_LIST_NAME = "Dropdown List";

    private readonly List<string> optionBuffer = new List<string>();
    private readonly List<Toggle> hookedItems = new List<Toggle>();

    // 같은 항목 재선택 감지용 (TMP_Dropdown 은 값이 같으면 onValueChanged 를 부르지 않는다)
    private bool wasDropdownExpanded;
    // 리스너를 단 목록. 닫힘 페이드 중 다시 열면 같은 프레임에 목록이 새로 만들어지므로 파괴 여부로 재부착을 판단한다.
    private Transform hookedList;
    private int valueOnExpand;
    private bool reselectHandled;

    /// <summary>
    /// 창이 닫힐 때 호출
    /// </summary>
    public event Action OnClosed;

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

        if (null != screenModeDropdown && true == screenModeDropdown.IsExpanded)
        {
            screenModeDropdown.Hide();
        }
        if (null != languageDropdown && true == languageDropdown.IsExpanded)
        {
            languageDropdown.Hide();
        }

        gameObject.SetActive(false);
        GameSettings.Save();
        OnClosed?.Invoke();
    }

    private void Awake()
    {
        if (null == volumeRows || 0 == volumeRows.Length)
        {
            volumeRows = GetComponentsInChildren<VolumeSettingRow>(true);
        }
    }

    private void OnEnable()
    {
        Refresh();

        if (null != languageDropdown)
        {
            languageDropdown.onValueChanged.AddListener(HandleLanguageDropdownChanged);
        }
        if (null != screenModeDropdown)
        {
            screenModeDropdown.onValueChanged.AddListener(HandleScreenModeChanged);
        }
        if (null != enemyHPBarToggle)
        {
            enemyHPBarToggle.onValueChanged.AddListener(HandleEnemyHPBarChanged);
        }
        if (null != damageTextToggle)
        {
            damageTextToggle.onValueChanged.AddListener(HandleDamageTextChanged);
        }
        if (null != closeButton)
        {
            closeButton.onClick.AddListener(Close);
        }

        GameSettings.OnScreenModeChanged += HandleSettingsScreenModeChanged;
        GameSettings.OnShowEnemyHPBarChanged += HandleSettingsEnemyHPBarChanged;
        GameSettings.OnShowDamageTextChanged += HandleSettingsDamageTextChanged;
        GameSettings.OnLanguageChanged += HandleSettingsLanguageChanged;
    }

    private void OnDisable()
    {
        if (null != languageDropdown)
        {
            languageDropdown.onValueChanged.RemoveListener(HandleLanguageDropdownChanged);
        }
        if (null != screenModeDropdown)
        {
            screenModeDropdown.onValueChanged.RemoveListener(HandleScreenModeChanged);
        }
        if (null != enemyHPBarToggle)
        {
            enemyHPBarToggle.onValueChanged.RemoveListener(HandleEnemyHPBarChanged);
        }
        if (null != damageTextToggle)
        {
            damageTextToggle.onValueChanged.RemoveListener(HandleDamageTextChanged);
        }
        if (null != closeButton)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        GameSettings.OnScreenModeChanged -= HandleSettingsScreenModeChanged;
        GameSettings.OnShowEnemyHPBarChanged -= HandleSettingsEnemyHPBarChanged;
        GameSettings.OnShowDamageTextChanged -= HandleSettingsDamageTextChanged;
        GameSettings.OnLanguageChanged -= HandleSettingsLanguageChanged;

        UnhookDropdownItems();
        wasDropdownExpanded = false;
    }

    private void Update()
    {
        if (null == screenModeDropdown)
        {
            return;
        }

        bool expanded = screenModeDropdown.IsExpanded;
        if (true == expanded && (false == wasDropdownExpanded || null == hookedList))
        {
            HookDropdownItems();
        }
        else if (false == expanded && true == wasDropdownExpanded)
        {
            UnhookDropdownItems();
        }
        wasDropdownExpanded = expanded;
    }

    /// <summary>
    /// 펼쳐진 목록의 항목 토글에 리스너를 단다.
    /// 이미 선택된 항목을 다시 골라도 화면 모드를 다시 적용해, 드래그로 바뀐 창 크기를 되돌린다.
    /// </summary>
    private void HookDropdownItems()
    {
        UnhookDropdownItems();
        valueOnExpand = screenModeDropdown.value;
        reselectHandled = false;

        Transform list = screenModeDropdown.transform.Find(DROPDOWN_LIST_NAME);
        if (null == list)
        {
            return;
        }

        hookedList = list;
        list.GetComponentsInChildren(true, hookedItems);
        foreach (Toggle item in hookedItems)
        {
            item.onValueChanged.AddListener(HandleDropdownItemToggled);
        }
    }

    private void UnhookDropdownItems()
    {
        foreach (Toggle item in hookedItems)
        {
            if (null != item)
            {
                item.onValueChanged.RemoveListener(HandleDropdownItemToggled);
            }
        }
        hookedItems.Clear();
        hookedList = null;
    }

    private void HandleDropdownItemToggled(bool _isOn)
    {
        // TMP_Dropdown 의 선택 처리(먼저 등록됨)가 끝난 뒤 호출된다.
        // 값이 바뀌었으면 onValueChanged 쪽에서 이미 적용했으므로, 값이 같을 때만 강제로 다시 적용한다.
        // (선택된 항목을 누르면 토글이 꺼졌다 다시 켜지며 두 번 들어오므로 한 번만 처리)
        if (true == reselectHandled || null == screenModeDropdown)
        {
            return;
        }
        reselectHandled = true;

        if (screenModeDropdown.value == valueOnExpand)
        {
            GameSettings.SetScreenModeIndex(screenModeDropdown.value, true);
        }
    }

    /// <summary>
    /// GameSettings 값으로 모든 항목 표시 갱신 (이벤트 없이)
    /// </summary>
    public void Refresh()
    {
        RefreshLanguageOptions();
        RefreshScreenModeOptions();
        if (null != enemyHPBarToggle)
        {
            enemyHPBarToggle.SetIsOnWithoutNotify(GameSettings.ShowEnemyHPBar);
        }
        if (null != damageTextToggle)
        {
            damageTextToggle.SetIsOnWithoutNotify(GameSettings.ShowDamageText);
        }
        if (null != volumeRows)
        {
            foreach (VolumeSettingRow row in volumeRows)
            {
                if (null != row)
                {
                    row.Refresh();
                }
            }
        }
    }

    /// <summary>
    /// 언어 드롭다운 옵션 (각 언어 고유 표기, 언어와 무관하게 고정) 과 현재 언어
    /// </summary>
    private void RefreshLanguageOptions()
    {
        if (null == languageDropdown)
        {
            return;
        }

        optionBuffer.Clear();
        for (int i = 0; i < LanguageInfo.Count; ++i)
        {
            optionBuffer.Add(LanguageInfo.GetNativeName((Language)i));
        }
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(optionBuffer);
        languageDropdown.SetValueWithoutNotify((int)GameSettings.Language);
        languageDropdown.RefreshShownValue();
    }

    /// <summary>
    /// 화면 크기 드롭다운 옵션 (현재 언어의 "전체 화면" 라벨) 과 현재 화면 모드
    /// </summary>
    private void RefreshScreenModeOptions()
    {
        if (null == screenModeDropdown)
        {
            return;
        }

        optionBuffer.Clear();
        for (int i = 0; i < ScreenModes.Count; ++i)
        {
            optionBuffer.Add(ScreenModes.GetLabel(i));
        }
        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(optionBuffer);
        screenModeDropdown.SetValueWithoutNotify(GameSettings.ScreenModeIndex);
        screenModeDropdown.RefreshShownValue();
    }

    private void HandleLanguageDropdownChanged(int _index)
    {
        GameSettings.Language = LanguageInfo.Clamp(_index);
    }

    private void HandleSettingsLanguageChanged(Language _language)
    {
        if (null != languageDropdown)
        {
            languageDropdown.SetValueWithoutNotify((int)_language);
            languageDropdown.RefreshShownValue();
        }
        RefreshScreenModeOptions();
    }

    private void HandleScreenModeChanged(int _index)
    {
        GameSettings.ScreenModeIndex = _index;
    }

    private void HandleEnemyHPBarChanged(bool _isOn)
    {
        GameSettings.ShowEnemyHPBar = _isOn;
    }

    private void HandleDamageTextChanged(bool _isOn)
    {
        GameSettings.ShowDamageText = _isOn;
    }

    private void HandleSettingsScreenModeChanged(int _index)
    {
        if (null != screenModeDropdown)
        {
            screenModeDropdown.SetValueWithoutNotify(_index);
        }
    }

    private void HandleSettingsEnemyHPBarChanged(bool _show)
    {
        if (null != enemyHPBarToggle)
        {
            enemyHPBarToggle.SetIsOnWithoutNotify(_show);
        }
    }

    private void HandleSettingsDamageTextChanged(bool _show)
    {
        if (null != damageTextToggle)
        {
            damageTextToggle.SetIsOnWithoutNotify(_show);
        }
    }
}
