using TMPro;
using UnityEngine;

/// <summary>
/// 남은 웨이브 시간 표시 (인게임 HUD, 화면 중앙 위)
/// - 스포너들의 RemainingTime 중 가장 큰 값을 "분:초"(mm:ss) 로 표시한다 (스포너가 시작 전이면 총 시간)
/// - 초는 올림으로 표시해서 마지막 웨이브가 끝나는 순간 00:00 이 되고, 이후 00:00 으로 고정된다
/// - Spawner 가 Time.time(스케일 시간) 기준이라 일시정지 / 레벨업(timeScale 0) 동안 멈춘다
/// - 표시할 초가 바뀔 때만 글자를 바꾼다 (char 버퍼를 다시 써서 GC 없음)
/// </summary>
[DisallowMultipleComponent]
public class WaveTimerUI
    : MonoBehaviour
{
    [Tooltip("남은 시간을 표시할 텍스트")]
    [SerializeField]
    private TMP_Text timerText;

    [Tooltip("남은 시간을 볼 스포너 (비어 있으면 씬의 모든 스포너)")]
    [SerializeField]
    private Spawner[] spawners;

    // mm:ss (분이 100 이상이면 자릿수가 늘어난다)
    private readonly char[] buffer = new char[16];

    // 마지막으로 표시한 초 (-1 이면 아직 표시 전)
    private int displayedSeconds = -1;

    private bool hasWarnedNoSpawner = false;

    /// <summary>
    /// 지금 표시 중인 남은 시간 (초, 올림)
    /// </summary>
    public int DisplayedSeconds => displayedSeconds;

    private void Start()
    {
        if (null == timerText)
        {
            timerText = GetComponent<TMP_Text>();
        }
        if (null == timerText)
        {
            Debug.LogWarning($"[WaveTimerUI] 표시할 TMP_Text 가 없습니다. ({name})", this);
        }

        if (null == spawners || 0 == spawners.Length)
        {
            spawners = FindObjectsByType<Spawner>();
        }

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    /// <summary>
    /// 스포너들의 남은 시간 중 최댓값 (초). 스포너가 없으면 0
    /// </summary>
    public float GetRemainingTime()
    {
        float remaining = 0f;
        bool hasSpawner = false;
        if (null != spawners)
        {
            foreach (Spawner spawner in spawners)
            {
                if (null == spawner)
                {
                    continue;
                }
                hasSpawner = true;
                remaining = Mathf.Max(remaining, spawner.RemainingTime);
            }
        }

        if (false == hasSpawner && false == hasWarnedNoSpawner)
        {
            hasWarnedNoSpawner = true;
            Debug.LogWarning($"[WaveTimerUI] 남은 시간을 볼 스포너가 없어 00:00 으로 표시합니다. ({name})", this);
        }

        return remaining;
    }

    private void Refresh()
    {
        if (null == timerText)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(GetRemainingTime()));
        if (seconds == displayedSeconds)
        {
            return;
        }
        displayedSeconds = seconds;

        int length = Format(seconds, buffer);
        timerText.SetCharArray(buffer, 0, length);
    }

    /// <summary>
    /// 초를 mm:ss 로 _buffer 에 쓴다 (분은 최소 두 자리)
    /// </summary>
    /// <returns>쓴 글자 수</returns>
    private static int Format(int _seconds, char[] _buffer)
    {
        int minutes = _seconds / 60;
        int sec = _seconds % 60;

        // 분 자릿수 (최소 2)
        int digits = 2;
        for (int m = minutes / 100; m > 0; m /= 10)
        {
            digits++;
        }
        digits = Mathf.Min(digits, _buffer.Length - 3);

        int index = digits - 1;
        int value = minutes;
        for (int i = 0; i < digits; i++)
        {
            _buffer[index--] = (char)('0' + value % 10);
            value /= 10;
        }

        _buffer[digits] = ':';
        _buffer[digits + 1] = (char)('0' + sec / 10);
        _buffer[digits + 2] = (char)('0' + sec % 10);
        return digits + 3;
    }
}
