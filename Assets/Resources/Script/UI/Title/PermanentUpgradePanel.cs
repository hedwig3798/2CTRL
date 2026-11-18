using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 "차원 강화" 창 (영구 강화 구매)
/// - 이 컴포넌트가 붙은 오브젝트 자체를 켜고 끈다. 씬에는 꺼진 상태로 두고 Open() 으로 연다 (SettingsPanel 과 같은 방식)
/// - 처음 켜질 때 rowTemplate(꺼 둔 템플릿) 을 registry 목록 순서대로 복제해 rowParent 아래에 만든다
/// - MetaProgress.OnChanged / 언어 변경 시 보유 차원력과 모든 줄을 다시 채운다
/// - ESC 처리는 여는 쪽(TitleMenu) 이 한다. 제목 등 고정 라벨은 LocalizedText 로
/// </summary>
public class PermanentUpgradePanel
    : MonoBehaviour
{
    [Tooltip("표시할 강화 목록")]
    [SerializeField] private PermanentUpgradeRegistry registry;

    [Tooltip("보유 차원력 텍스트")]
    [SerializeField] private TMP_Text currencyLabel;
    [Tooltip("{0} = 보유 차원력")]
    [SerializeField] private string currencyFormatKey = "meta.currency";

    [Header("목록")]
    [Tooltip("줄을 담을 부모 (비우면 템플릿의 부모)")]
    [SerializeField] private RectTransform rowParent;
    [Tooltip("복제해서 쓸 줄 (꺼 둔 템플릿)")]
    [SerializeField] private PermanentUpgradeRow rowTemplate;

    [Header("닫기")]
    [SerializeField] private Button closeButton;
    [Tooltip("창을 닫을 때 (닫기 버튼 / ESC)")]
    [SerializeField] private SoundId closeSound = SoundId.UI_BACK;

    private readonly List<PermanentUpgradeRow> rows = new List<PermanentUpgradeRow>();
    private bool isBuilt;

    /// <summary>
    /// 창이 닫힐 때 호출
    /// </summary>
    public event Action OnClosed;

    public bool IsOpen => gameObject.activeSelf;

    /// <summary>
    /// 만든 줄 (registry 순서)
    /// </summary>
    public IReadOnlyList<PermanentUpgradeRow> Rows => rows;

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
        OnClosed?.Invoke();
    }

    private void Awake()
    {
        // 닫기 버튼은 Close 의 closeSound 가 대신한다 (클릭음과 겹치지 않도록)
        UIButtonSound.Assign(closeButton, SoundId.None);

        if (null != rowTemplate)
        {
            rowTemplate.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        BuildRows();
        Refresh();

        if (null != closeButton)
        {
            closeButton.onClick.AddListener(Close);
        }
        MetaProgress.OnChanged += Refresh;
        StringTable.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        if (null != closeButton)
        {
            closeButton.onClick.RemoveListener(Close);
        }
        MetaProgress.OnChanged -= Refresh;
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(Language _language)
    {
        Refresh();
    }

    /// <summary>
    /// 보유 차원력과 모든 줄을 다시 채운다
    /// </summary>
    public void Refresh()
    {
        if (null != currencyLabel)
        {
            currencyLabel.text = StringTable.Format(currencyFormatKey, MetaProgress.Currency);
        }

        foreach (PermanentUpgradeRow row in rows)
        {
            if (null != row)
            {
                row.Refresh();
            }
        }
    }

    private void BuildRows()
    {
        if (true == isBuilt)
        {
            return;
        }
        isBuilt = true;

        if (null == registry || null == rowTemplate)
        {
            Debug.LogWarning("[PermanentUpgradePanel] registry 또는 rowTemplate 이 연결되지 않았습니다.", this);
            return;
        }

        Transform parent = null != rowParent ? rowParent : rowTemplate.transform.parent;
        IReadOnlyList<PermanentUpgradeData> upgrades = registry.Upgrades;
        for (int i = 0; i < upgrades.Count; i++)
        {
            PermanentUpgradeData data = upgrades[i];
            if (null == data)
            {
                continue;
            }

            PermanentUpgradeRow row = Instantiate(rowTemplate, parent, false);
            row.name = rowTemplate.name + "_" + (string.IsNullOrEmpty(data.Id) ? i.ToString() : data.Id);
            row.gameObject.SetActive(true);
            row.Bind(data);
            rows.Add(row);
        }
    }
}
