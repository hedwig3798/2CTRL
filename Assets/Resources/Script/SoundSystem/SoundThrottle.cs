using System.Collections.Generic;

/// <summary>
/// (SoundId, 진영) 키별 재생 제한 (docs/sound-design.md 4.4)
/// - 마지막 재생 시각과 재생 중 개수로 판정한다. 시간은 호출하는 쪽이 넘긴다 (SoundManager 는 Time.unscaledTime)
/// - 재생이 끝나면 Release 로 개수를 돌려줘야 한다.
/// - 순수 C# 클래스 (EditMode 테스트 가능), Update 경로에서 GC 없음
///
/// 사용 예)
///   int key = SoundThrottle.MakeKey(SoundId.HIT_ORGANIC, PlayerSide.Left);
///   if (true == throttle.TryAcquire(key, now, 0.045f, 3)) { 재생 ... 끝나면 throttle.Release(key); }
/// </summary>
public class SoundThrottle
{
    // 키의 진영 칸 (공유 / 왼쪽 / 오른쪽)
    private const int SIDE_SLOTS = 3;
    private const int SIDE_SHARED = 0;

    private struct Entry
    {
        public float lastTime;
        // 마지막 TryAcquire 직전의 lastTime (Cancel 로 되돌릴 때 쓴다)
        public float prevTime;
        public int voices;
    }

    private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();

    /// <summary>
    /// (ID, 진영) 키. _side 가 null 이면 양쪽 공유 키
    /// </summary>
    public static int MakeKey(SoundId _id, PlayerSide? _side)
    {
        int sideSlot = _side.HasValue ? 1 + (int)_side.Value : SIDE_SHARED;
        return (int)_id * SIDE_SLOTS + sideSlot;
    }

    /// <summary>
    /// 재생해도 되면 true 를 돌려주고 마지막 재생 시각 / 재생 중 개수를 갱신한다
    /// </summary>
    /// <param name="_now">현재 시각 (초)</param>
    /// <param name="_minInterval">최소 간격 (초). 0 이하면 제한 없음</param>
    /// <param name="_maxVoices">동시 재생 수. 0 이하면 제한 없음</param>
    public bool TryAcquire(int _key, float _now, float _minInterval, int _maxVoices)
    {
        bool exists = entries.TryGetValue(_key, out Entry entry);
        if (true == exists)
        {
            if (0f < _minInterval && _now - entry.lastTime < _minInterval)
            {
                return false;
            }
            if (0 < _maxVoices && entry.voices >= _maxVoices)
            {
                return false;
            }
        }

        entry.prevTime = true == exists ? entry.lastTime : float.NegativeInfinity;
        entry.lastTime = _now;
        entry.voices = (true == exists ? entry.voices : 0) + 1;
        entries[_key] = entry;
        return true;
    }

    /// <summary>
    /// 방금 성공한 TryAcquire 를 없던 일로 한다 (재생할 소스가 없어 실제로는 못 냈을 때)
    /// - 개수를 돌려주고 마지막 재생 시각도 이전 값으로 되돌려, 다음 재생이 minInterval 동안 막히지 않게 한다.
    /// </summary>
    public void Cancel(int _key)
    {
        if (false == entries.TryGetValue(_key, out Entry entry))
        {
            return;
        }
        if (0 < entry.voices)
        {
            --entry.voices;
        }
        entry.lastTime = entry.prevTime;
        entries[_key] = entry;
    }

    /// <summary>
    /// 재생이 끝난 소리 하나의 개수를 돌려준다
    /// </summary>
    public void Release(int _key)
    {
        if (false == entries.TryGetValue(_key, out Entry entry))
        {
            return;
        }
        if (0 < entry.voices)
        {
            --entry.voices;
            entries[_key] = entry;
        }
    }

    /// <summary>
    /// 키의 재생 중 개수 (없으면 0)
    /// </summary>
    public int GetVoices(int _key)
    {
        return true == entries.TryGetValue(_key, out Entry entry) ? entry.voices : 0;
    }

    /// <summary>
    /// 모든 기록을 지운다
    /// </summary>
    public void Clear()
    {
        entries.Clear();
    }
}
