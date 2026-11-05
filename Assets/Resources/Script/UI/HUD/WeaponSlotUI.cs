using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 무기 슬롯 하나 (WeaponHUD 가 채운다)
/// - 무기 아이콘 + 아이콘 좌하단 레벨 숫자 (Weapon.UpgradeLevel, 최초 습득 0)
/// - 아이콘이 없으면 아이콘을 숨기고 nameText 에 무기 이름을 표시한다
/// - 레벨은 RefreshLevel 에서 이전 값과 비교해서 바뀔 때만 글자를 바꾼다
/// </summary>
[DisallowMultipleComponent]
public class WeaponSlotUI
    : MonoBehaviour
{
    [Tooltip("무기 아이콘")]
    [SerializeField]
    private Image iconImage;

    [Tooltip("레벨 숫자 (아이콘 좌하단)")]
    [SerializeField]
    private TMP_Text levelText;

    [Tooltip("아이콘이 없을 때 대신 표시할 무기 이름 (비워도 됨)")]
    [SerializeField]
    private TMP_Text nameText;

    [Tooltip("무기가 없을 때 숨길 오브젝트 (비어 있으면 이 오브젝트)")]
    [SerializeField]
    private GameObject contentRoot;

    [Tooltip("레벨 표시 포멧 ({0} = 레벨)")]
    [SerializeField]
    private string levelFormat = "{0}";

    private Weapon weapon;

    // 마지막으로 표시한 레벨 (-1 이면 아직 표시 전)
    private int displayedLevel = -1;

    /// <summary>
    /// 이 슬롯에 표시 중인 무기 (비어 있으면 null)
    /// </summary>
    public Weapon Weapon => weapon;

    /// <summary>
    /// 무기가 들어 있는지
    /// </summary>
    public bool HasWeapon => null != weapon;

    /// <summary>
    /// 지금 표시 중인 레벨 (-1 이면 표시 전)
    /// </summary>
    public int DisplayedLevel => displayedLevel;

    /// <summary>
    /// 무기를 표시한다
    /// - _icon 이 null 이면 아이콘을 숨기고 _fallbackName 을 nameText 에 표시한다
    /// </summary>
    public void SetWeapon(Weapon _weapon, Sprite _icon, string _fallbackName)
    {
        weapon = _weapon;
        SetContentActive(true);

        bool hasIcon = null != _icon;
        if (null != iconImage)
        {
            iconImage.sprite = _icon;
            iconImage.enabled = hasIcon;
        }

        if (null != nameText)
        {
            nameText.text = hasIcon ? string.Empty : (null != _fallbackName ? _fallbackName : string.Empty);
            nameText.gameObject.SetActive(false == hasIcon);
        }

        displayedLevel = -1;
        RefreshLevel();
    }

    /// <summary>
    /// 슬롯을 비운다 (contentRoot 를 숨김)
    /// </summary>
    public void Clear()
    {
        weapon = null;
        displayedLevel = -1;

        if (null != iconImage)
        {
            iconImage.sprite = null;
        }

        SetContentActive(false);
    }

    /// <summary>
    /// 무기의 UpgradeLevel 이 바뀌었으면 레벨 글자를 갱신한다
    /// </summary>
    public void RefreshLevel()
    {
        if (null == weapon || null == levelText)
        {
            return;
        }

        int level = weapon.UpgradeLevel;
        if (level == displayedLevel)
        {
            return;
        }
        displayedLevel = level;
        levelText.text = string.Format(levelFormat, level);
    }

    private void SetContentActive(bool _active)
    {
        GameObject root = null != contentRoot ? contentRoot : gameObject;
        if (root.activeSelf != _active)
        {
            root.SetActive(_active);
        }
    }
}
