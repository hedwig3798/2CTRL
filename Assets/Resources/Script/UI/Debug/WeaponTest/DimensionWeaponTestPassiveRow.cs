#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 차원 무기 테스트 씬의 패시브 스택 한 줄 (이름 / 좌 [-] 스택 [+] / 우 [-] 스택 [+])
/// - DimensionWeaponTestController 가 템플릿 행(비활성)을 복제해서 쓴다. 이 컴포넌트는 참조만 묶는다
/// - 좌 / 우 플레이어 스택을 따로 표시한다
/// </summary>
public class DimensionWeaponTestPassiveRow
    : MonoBehaviour
{
    [Tooltip("패시브 이름 + 1스택 변화량")]
    [SerializeField]
    private TMP_Text nameText;

    [Header("왼쪽 플레이어")]
    [SerializeField]
    private TMP_Text leftStackText;

    [SerializeField]
    private Button leftMinusButton;

    [SerializeField]
    private Button leftPlusButton;

    [Header("오른쪽 플레이어")]
    [SerializeField]
    private TMP_Text rightStackText;

    [SerializeField]
    private Button rightMinusButton;

    [SerializeField]
    private Button rightPlusButton;

    public Button LeftMinusButton => leftMinusButton;
    public Button LeftPlusButton => leftPlusButton;
    public Button RightMinusButton => rightMinusButton;
    public Button RightPlusButton => rightPlusButton;

    /// <summary>
    /// _side 쪽 [-] 버튼
    /// </summary>
    public Button GetMinusButton(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? leftMinusButton : rightMinusButton;
    }

    /// <summary>
    /// _side 쪽 [+] 버튼
    /// </summary>
    public Button GetPlusButton(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? leftPlusButton : rightPlusButton;
    }

    public void SetLabel(string _label)
    {
        if (null != nameText)
        {
            nameText.text = _label;
        }
    }

    /// <summary>
    /// _side 쪽 스택 표시와 버튼 활성 상태를 갱신한다
    /// </summary>
    public void SetStack(PlayerSide _side, string _stackLabel, bool _canMinus, bool _canPlus)
    {
        bool isLeft = PlayerSide.Left == _side;
        TMP_Text stackText = (true == isLeft) ? leftStackText : rightStackText;
        Button minus = (true == isLeft) ? leftMinusButton : rightMinusButton;
        Button plus = (true == isLeft) ? leftPlusButton : rightPlusButton;

        if (null != stackText)
        {
            stackText.text = _stackLabel;
        }
        if (null != minus)
        {
            minus.interactable = _canMinus;
        }
        if (null != plus)
        {
            plus.interactable = _canPlus;
        }
    }
}
#endif
