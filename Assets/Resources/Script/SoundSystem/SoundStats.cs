/// <summary>
/// 이벤트 하나의 재생 기록 (개발용, 플레이 검증에 쓴다)
/// </summary>
public struct SoundStats
{
    /// <summary>
    /// 실제로 재생한 횟수
    /// </summary>
    public int played;
    /// <summary>
    /// 최소 간격 / 동시 재생 수 제한(SoundThrottle)으로 버린 횟수
    /// </summary>
    public int throttled;
    /// <summary>
    /// 빈 AudioSource 가 없어 버린 횟수 (교체할 수 있는 낮은 우선순위 소리도 없을 때)
    /// </summary>
    public int noVoice;
    /// <summary>
    /// 라이브러리에 이벤트가 없거나 클립이 비어 있어 재생하지 못한 횟수
    /// </summary>
    public int missing;
    /// <summary>
    /// 위치가 진영 카메라 화면 밖이라 재생하지 않은 횟수 (SoundManager.Play 위치 오버로드)
    /// </summary>
    public int culled;
}
