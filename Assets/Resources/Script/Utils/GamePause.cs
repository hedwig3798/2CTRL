using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 일시정지 요청을 관리하는 static 클래스
/// - 여러 곳(레벨업 UI, 일시정지 메뉴 등)이 동시에 멈춰도 서로 덮어쓰지 않도록 요청자 단위로 관리한다.
/// - 첫 요청 시 Time.timeScale = 0, 마지막 요청이 해제되면 1 로 복구한다.
///
/// 사용 예)
///   GamePause.Pause(this);
///   GamePause.Resume(this);
/// </summary>
public static class GamePause
{
    private static readonly HashSet<object> requesters = new HashSet<object>();

    /// <summary>
    /// 일시정지 상태가 바뀔 때 호출 (일시정지 여부)
    /// </summary>
    public static Action<bool> OnPauseChanged;

    public static bool IsPaused => 0 < requesters.Count;

    /// <summary>
    /// 일시정지 요청 (같은 요청자가 여러 번 호출해도 한 번으로 취급)
    /// </summary>
    public static void Pause(object _requester)
    {
        if (null == _requester)
        {
            return;
        }

        bool wasPaused = IsPaused;
        requesters.Add(_requester);

        if (false == wasPaused)
        {
            Time.timeScale = 0f;
            OnPauseChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// 일시정지 요청 해제 (모든 요청이 해제되면 재개)
    /// </summary>
    public static void Resume(object _requester)
    {
        if (null == _requester)
        {
            return;
        }

        if (false == requesters.Remove(_requester))
        {
            return;
        }

        if (false == IsPaused)
        {
            Time.timeScale = 1f;
            OnPauseChanged?.Invoke(false);
        }
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        requesters.Clear();
        OnPauseChanged = null;
        Time.timeScale = 1f;
    }
}
