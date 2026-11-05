using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 차원 선택 씬에서 한 진영이 보고 있는 카드 표시 (이름, 미리보기, 확정 / 잠김 표시)
/// - DimensionSelectController 가 커서나 확정 상태가 바뀔 때 Show 를 호출한다
/// - 비어 있는 참조는 무시한다
/// - 언어가 바뀌면 보고 있는 차원 이름을 다시 표시한다
/// </summary>
public class DimensionSelectView
    : MonoBehaviour
{
    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private Image previewImage;

    [Tooltip("차원의 themeColor 를 입힐 그래픽 (비워도 됨)")]
    [SerializeField]
    private Graphic themeGraphic;

    [Tooltip("확정했을 때 켜는 오브젝트")]
    [SerializeField]
    private GameObject confirmedMark;

    [Tooltip("상대가 이미 확정한 차원이라 고를 수 없을 때 켜는 오브젝트")]
    [SerializeField]
    private GameObject lockedMark;

    private DimensionData shownData;

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += HandleLanguageChanged;
        // 꺼져 있는 동안 언어가 바뀌었을 수 있으므로 다시 표시
        HandleLanguageChanged(StringTable.CurrentLanguage);
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(Language _language)
    {
        ApplyName();
    }

    private void ApplyName()
    {
        if (null != nameText)
        {
            nameText.text = null != shownData ? shownData.DisplayName : string.Empty;
        }
    }

    /// <param name="_data">보고 있는 차원 (null 이면 비움)</param>
    /// <param name="_confirmed">이 진영이 확정했는지</param>
    /// <param name="_locked">상대가 확정해서 고를 수 없는지</param>
    public void Show(DimensionData _data, bool _confirmed, bool _locked)
    {
        shownData = _data;
        ApplyName();

        if (null != previewImage)
        {
            previewImage.sprite = null != _data ? _data.PreviewSprite : null;
            previewImage.enabled = null != previewImage.sprite;
        }

        if (null != themeGraphic && null != _data)
        {
            themeGraphic.color = _data.ThemeColor;
        }

        if (null != confirmedMark)
        {
            confirmedMark.SetActive(_confirmed);
        }

        if (null != lockedMark)
        {
            lockedMark.SetActive(_locked);
        }
    }
}
