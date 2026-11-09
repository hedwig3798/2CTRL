using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 차원 선택 격자의 한 칸 표시 (차원 카드, 진영별 커서 프레임)
/// - DimensionSelectController 가 커서나 확정 상태가 바뀔 때 Show 를 호출한다
/// - 프레임은 그 진영의 커서가 이 칸에 있을 때만 켜고, 확정하면 불투명하게 표시한다
/// - 누군가 이 칸을 확정하면 카드의 잠김 표시를 켠다
/// - 비어 있는 참조는 무시한다
/// </summary>
public class DimensionGridCell
    : MonoBehaviour
{
    [SerializeField]
    private DimensionSelectView view;

    [Tooltip("왼쪽 진영 커서 프레임")]
    [SerializeField]
    private Graphic leftFrame;

    [Tooltip("오른쪽 진영 커서 프레임")]
    [SerializeField]
    private Graphic rightFrame;

    [Tooltip("커서만 있고 확정하지 않았을 때의 프레임 알파")]
    [SerializeField]
    private float idleFrameAlpha = 0.55f;

    /// <param name="_data">이 칸의 차원 (null 이면 비움)</param>
    /// <param name="_leftHere">왼쪽 커서가 이 칸에 있는지</param>
    /// <param name="_leftConfirmed">왼쪽이 확정했는지 (커서가 이 칸에 있을 때만 이 칸의 확정으로 본다)</param>
    /// <param name="_rightHere">오른쪽 커서가 이 칸에 있는지</param>
    /// <param name="_rightConfirmed">오른쪽이 확정했는지 (커서가 이 칸에 있을 때만 이 칸의 확정으로 본다)</param>
    public void Show(DimensionData _data, bool _leftHere, bool _leftConfirmed, bool _rightHere, bool _rightConfirmed)
    {
        bool leftLocked = true == _leftHere && true == _leftConfirmed;
        bool rightLocked = true == _rightHere && true == _rightConfirmed;

        if (null != view)
        {
            view.Show(_data, false, true == leftLocked || true == rightLocked);
        }

        ApplyFrame(leftFrame, _leftHere, leftLocked);
        ApplyFrame(rightFrame, _rightHere, rightLocked);
    }

    private void ApplyFrame(Graphic _frame, bool _here, bool _locked)
    {
        if (null == _frame)
        {
            return;
        }

        _frame.gameObject.SetActive(_here);

        Color color = _frame.color;
        color.a = true == _locked ? 1f : idleFrameAlpha;
        _frame.color = color;
    }
}
