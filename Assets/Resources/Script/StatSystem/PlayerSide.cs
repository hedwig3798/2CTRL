/// <summary>
/// 플레이어 위치 (화면 왼쪽 / 오른쪽)
/// - PlayerStats 와 SelectPanelGroup 을 이어 주는 값
///   - 왼쪽 그룹에서 고른 보상의 버프는 왼쪽, 디버프는 반대쪽(오른쪽) 플레이어에게 적용된다
/// </summary>
public enum PlayerSide
{
    Left,
    Right,
}

public static class PlayerSideExtensions
{
    /// <summary>
    /// 반대쪽 플레이어
    /// </summary>
    public static PlayerSide Opposite(this PlayerSide _side)
    {
        return PlayerSide.Left == _side ? PlayerSide.Right : PlayerSide.Left;
    }
}
