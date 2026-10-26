/// <summary>
/// 보상 효과가 적용될 대상 묶음 (RewardManager.ApplyReward 에서 만든다)
/// - 새 보상 종류가 다른 대상을 필요로 하면 여기에 필드를 추가한다
/// </summary>
public class RewardContext
{
    /// <summary>
    /// 보상을 고른 SelectPanelGroup 쪽 플레이어 (없으면 null)
    /// </summary>
    public PlayerStats Player;

    /// <summary>
    /// 몬스터 비율을 바꿀 스포너 (없으면 null)
    /// </summary>
    public Spawner Spawner;
}
