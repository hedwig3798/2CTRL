/// <summary>
/// 레벨업 보상 확정 소리 (LevelUpUI 가 보상을 적용할 때 호출)
/// - 보상 효과 타입으로 판정 (RewardEffect 공용 베이스는 건드리지 않는다)
///   WeaponRewardEffect → LVL_WEAPON / WeaponUpgradeRewardEffect → LVL_UPGRADE (★ 단계면 LVL_STAR)
///   StatRewardEffect → 고른 쪽 LVL_BUFF + (디버프가 있으면) 반대쪽 LVL_DEBUFF / 그 외(SpawnerRewardEffect 등) → LVL_UPGRADE
/// </summary>
public static class RewardSound
{
    /// <summary>
    /// _side 가 고른 보상(_buff = 패널의 첫 번째 데이터, _debuff = 두 번째 데이터)의 확정 소리를 낸다
    /// - LVL_DEBUFF 는 실제 디버프(_debuff 의 효과)가 있을 때만 반대쪽에서 낸다
    /// </summary>
    public static void PlayConfirm(RewardData _buff, RewardData _debuff, PlayerSide _side)
    {
        if (null == _buff || false == SoundManager.HasInstance)
        {
            return;
        }

        RewardEffect effect = _buff.GetEffect();
        switch (effect)
        {
            case null:
                break;
            case WeaponRewardEffect _:
                SoundManager.Play(SoundId.LVL_WEAPON, _side);
                break;
            case WeaponUpgradeRewardEffect upgrade:
                SoundLibrary library = SoundManager.Instance.Library;
                bool star = null != library && true == library.IsStarUpgradeLevel(upgrade.Level);
                SoundManager.Play(true == star ? SoundId.LVL_STAR : SoundId.LVL_UPGRADE, _side);
                break;
            case StatRewardEffect _:
                SoundManager.Play(SoundId.LVL_BUFF, _side);
                if (null != _debuff && true == _debuff.HasEffect)
                {
                    SoundManager.Play(SoundId.LVL_DEBUFF, _side.Opposite());
                }
                break;
            default:
                SoundManager.Play(SoundId.LVL_UPGRADE, _side);
                break;
        }
    }
}
