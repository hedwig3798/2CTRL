/// <summary>
/// 볼륨 설정 채널
/// - Master : 전체 음량 (AudioListener.volume 에 바로 적용)
/// - Effect / BGM : 실제 재생 측에서 GameSettings.GetEffectiveVolume 으로 읽어 쓴다
/// </summary>
public enum AudioChannel
{
    Master,
    Effect,
    BGM,
}
