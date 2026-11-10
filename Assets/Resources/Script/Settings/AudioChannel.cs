/// <summary>
/// 볼륨 설정 채널
/// - Master : 전체 음량 (AudioListener.volume 에 바로 적용)
/// - Effect / BGM : 실제 재생 측에서 GameSettings.GetChannelVolume 으로 읽어 AudioSource.volume 에 쓴다
///   (Master 는 AudioListener.volume 이 이미 곱하므로 GetEffectiveVolume 을 쓰면 두 번 곱해진다)
/// </summary>
public enum AudioChannel
{
    Master,
    Effect,
    BGM,
}
