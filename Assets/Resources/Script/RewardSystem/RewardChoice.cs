/// <summary>
/// 레벨업 선택지 하나
/// - main: 신규 무기 / 무기 강화 / 패시브 버프 중 하나
/// - nerf: main 이 강화(무기 강화, 패시브 버프)일 때 조합되는 패시브 너프. 신규 무기거나 너프 풀이 비었으면 null
/// </summary>
public class RewardChoice
{
    public readonly RewardData main;
    public readonly PassiveNerfRewardData nerf;

    public RewardChoice(RewardData _main, PassiveNerfRewardData _nerf)
    {
        main = _main;
        nerf = _nerf;
    }

    public bool HasNerf => null != nerf;
}
