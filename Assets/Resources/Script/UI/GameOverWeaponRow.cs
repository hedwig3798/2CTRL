using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 종료 화면의 무기별 데미지 한 칸 (GameOverUI 가 채운다)
/// - 아이콘이 있으면 아이콘 + 데미지 수치
/// - 아이콘이 없으면 아이콘을 숨기고 "이름 : 데미지" 로 대체 (글자를 칸 왼쪽 끝까지 넓힌다)
/// </summary>
[DisallowMultipleComponent]
public class GameOverWeaponRow
    : MonoBehaviour
{
    [Tooltip("무기 아이콘")]
    [SerializeField]
    private Image iconImage;

    [Tooltip("데미지 수치 (아이콘이 없으면 이름도 함께 표시)")]
    [SerializeField]
    private TMP_Text valueText;

    [Tooltip("아이콘이 있을 때 글자의 왼쪽 여백 (아이콘 자리)")]
    [SerializeField]
    private float textLeftWithIcon = 64f;

    public bool HasIcon => null != iconImage && true == iconImage.gameObject.activeSelf;
    public string Text => null != valueText ? valueText.text : string.Empty;
    public Sprite Icon => null != iconImage ? iconImage.sprite : null;

    /// <summary>
    /// 아이콘이 있으면 _valueOnly, 없으면 _withName 을 표시한다
    /// </summary>
    public void Set(Sprite _icon, string _valueOnly, string _withName)
    {
        bool hasIcon = null != _icon;

        if (null != iconImage)
        {
            iconImage.sprite = _icon;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(hasIcon);
        }

        if (null != valueText)
        {
            valueText.text = true == hasIcon ? _valueOnly : _withName;
            Vector2 offsetMin = valueText.rectTransform.offsetMin;
            offsetMin.x = true == hasIcon ? textLeftWithIcon : 0f;
            valueText.rectTransform.offsetMin = offsetMin;
        }
    }
}
