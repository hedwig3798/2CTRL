#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무기 테스트 씬의 패시브 스택 한 줄 (이름 / 스택 / [-] [+])
/// - WeaponTestController 가 템플릿 행(비활성)을 복제해서 쓴다. 이 컴포넌트는 참조만 묶는다
/// </summary>
public class WeaponTestPassiveRow
    : MonoBehaviour
{
    [Tooltip("패시브 이름 + 1스택 변화량")]
    [SerializeField]
    private TMP_Text nameText;

    [Tooltip("현재 스택 표시")]
    [SerializeField]
    private TMP_Text stackText;

    [SerializeField]
    private Button minusButton;

    [SerializeField]
    private Button plusButton;

    public Button MinusButton => minusButton;
    public Button PlusButton => plusButton;

    public void SetLabel(string _label)
    {
        if (null != nameText)
        {
            nameText.text = _label;
        }
    }

    /// <summary>
    /// 스택 표시와 버튼 활성 상태를 갱신한다
    /// </summary>
    public void SetStack(string _stackLabel, bool _canMinus, bool _canPlus)
    {
        if (null != stackText)
        {
            stackText.text = _stackLabel;
        }
        if (null != minusButton)
        {
            minusButton.interactable = _canMinus;
        }
        if (null != plusButton)
        {
            plusButton.interactable = _canPlus;
        }
    }
}
#endif
