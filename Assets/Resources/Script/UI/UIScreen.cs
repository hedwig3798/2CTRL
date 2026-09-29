/// <summary>
/// UI 가 그려질 화면 영역
/// </summary>
public enum UI_SCREEN
{
    /// <summary>
    /// 원래 부모(owner)의 레이어를 보고 자동 결정 (판단할 수 없으면 Full)
    /// </summary>
    Auto,
    /// <summary>
    /// 왼쪽 카메라 화면에만 그림
    /// </summary>
    Left,
    /// <summary>
    /// 오른쪽 카메라 화면에만 그림
    /// </summary>
    Right,
    /// <summary>
    /// 화면 전체에 그림
    /// </summary>
    Full,
}
