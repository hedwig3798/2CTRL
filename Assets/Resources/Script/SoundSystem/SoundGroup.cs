/// <summary>
/// 사운드 그룹 (docs/sound-design.md 4.1)
/// - 그룹별 음량(dB)은 SoundLibrary 에서 정한다. AudioMixer 대신 코드로 곱한다.
/// - BGM 그룹은 AudioChannel.BGM, 나머지는 AudioChannel.Effect 채널 음량을 받는다.
/// - SoundLibrary 에 int 로 직렬화되므로 순서를 바꾸지 말 것 (새 그룹은 끝에 추가)
/// </summary>
public enum SoundGroup
{
    Alert,
    Reward,
    UI,
    Weapon,
    Hit,
    Monster,
    BGM,
}

public static class SoundGroupExtensions
{
    /// <summary>
    /// 그룹이 받을 사용자 볼륨 채널 (BGM 그룹 = BGM, 나머지 = Effect)
    /// </summary>
    public static AudioChannel ToChannel(this SoundGroup _group)
    {
        return SoundGroup.BGM == _group ? AudioChannel.BGM : AudioChannel.Effect;
    }
}
