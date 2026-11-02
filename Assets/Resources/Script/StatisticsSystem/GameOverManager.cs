using System;
using UnityEngine;

/// <summary>
/// 플레이어가 한 명이라도 죽으면 즉시 게임 오버 처리 (씬에 하나 배치)
/// - EventManager.OnPlayerDeath 를 구독하고 한 번만 처리한다
/// - 순서: 일시정지(GamePause) → 통계 큐 동기 처리(StatisticsManager.Flush) → 결과 패널 표시(GameOverUI) → OnGameOver
/// </summary>
[DisallowMultipleComponent]
public class GameOverManager
    : MonoBehaviour
{
    [Tooltip("비우면 StatisticsManager.Instance 를 사용한다")]
    [SerializeField]
    private StatisticsManager statisticsManager;

    [Tooltip("게임 오버 시 통계를 보여 줄 UI")]
    [SerializeField]
    private GameOverUI gameOverUI;

    /// <summary>
    /// 게임 오버 처리가 끝난 뒤 호출
    /// </summary>
    public Action OnGameOver;

    private bool isGameOver = false;

    // 먼저 죽은 플레이어
    private PlayerSide deadSide;

    public bool IsGameOver => isGameOver;

    public PlayerSide DeadSide => deadSide;

    /// <summary>
    /// 게임 오버를 처리한다 (이미 처리했으면 무시)
    /// </summary>
    public void TriggerGameOver(PlayerSide _deadSide)
    {
        if (true == isGameOver)
        {
            return;
        }

        isGameOver = true;
        deadSide = _deadSide;

        GamePause.Pause(this);

        StatisticsManager stats = null != statisticsManager ? statisticsManager : StatisticsManager.Instance;
        if (null != stats)
        {
            stats.Flush();
            // 결과를 확정한다 (일시정지 중 늦게 들어오는 기록 차단)
            stats.Lock();
        }
        else
        {
            Debug.LogWarning("[GameOverManager] 씬에 StatisticsManager 가 없습니다.", this);
        }

        if (null != gameOverUI)
        {
            gameOverUI.Show(stats);
        }
        else
        {
            Debug.LogWarning("[GameOverManager] gameOverUI 가 연결되지 않았습니다.", this);
        }

        OnGameOver?.Invoke();
    }

    private void HandlePlayerDeath(PlayerSide _side)
    {
        TriggerGameOver(_side);
    }

    private void OnEnable()
    {
        EventManager events = EventManager.Instance;
        if (null == events)
        {
            return;
        }

        events.OnPlayerDeath -= HandlePlayerDeath;
        events.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnPlayerDeath -= HandlePlayerDeath;
        }
    }

    private void OnDestroy()
    {
        // 씬을 벗어나면 일시정지 요청을 풀어 다음 씬이 멈춘 채 시작하지 않도록 한다
        GamePause.Resume(this);
    }
}
