using System;
using UnityEngine;

/// <summary>
/// 게임 종료 처리 (씬에 하나 배치)
/// - 한 명이 양쪽을 플레이하는 싱글 게임: 어느 쪽이든 죽으면 즉시 게임 오버, TriggerVictory 로 승리
/// - EventManager.OnPlayerDeath 를 구독하고, 게임 오버·승리 중 처음 한 번만 처리한다
/// - 모든 Spawner 의 웨이브가 끝나면(Spawner.OnAllWavesFinished) 승리
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

    [Tooltip("모두 웨이브를 끝내면 승리할 Spawner 목록 (비우면 씬의 모든 Spawner)")]
    [SerializeField]
    private Spawner[] spawners;

    /// <summary>
    /// 게임 종료(게임 오버·승리) 처리가 끝난 뒤 호출
    /// </summary>
    public Action OnGameOver;

    [Header("sound")]
    [SerializeField]
    private SoundId winSound = SoundId.RESULT_WIN;
    [SerializeField]
    private SoundId loseSound = SoundId.RESULT_LOSE;

    private bool isGameOver = false;
    private bool isVictory = false;

    // 먼저 죽은 쪽 (승리로 끝났으면 의미 없음)
    private PlayerSide deadSide;

    /// <summary>
    /// 게임이 끝났는지 (게임 오버·승리 모두 포함)
    /// </summary>
    public bool IsGameOver => isGameOver;

    public bool IsVictory => isVictory;

    public PlayerSide DeadSide => deadSide;

    /// <summary>
    /// 게임 오버를 처리한다 (이미 끝났으면 무시)
    /// </summary>
    public void TriggerGameOver(PlayerSide _deadSide)
    {
        if (true == isGameOver)
        {
            return;
        }

        deadSide = _deadSide;
        EndGame(false);
    }

    /// <summary>
    /// 승리를 처리한다 (이미 끝났으면 무시)
    /// </summary>
    public void TriggerVictory()
    {
        if (true == isGameOver)
        {
            return;
        }

        EndGame(true);
    }

    private void EndGame(bool _victory)
    {
        isGameOver = true;
        isVictory = _victory;

        GamePause.Pause(this);
        SoundManager.Play(true == _victory ? winSound : loseSound);

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
            gameOverUI.Show(stats, isVictory);
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

    private void HandleWavesFinished(Spawner _spawner)
    {
        foreach (Spawner spawner in spawners)
        {
            if (null != spawner && false == spawner.IsFinished)
            {
                return;
            }
        }

        TriggerVictory();
    }

    private void Awake()
    {
        if (null == spawners || 0 == spawners.Length)
        {
            spawners = FindObjectsByType<Spawner>();
        }
    }

    private void OnEnable()
    {
        foreach (Spawner spawner in spawners)
        {
            if (null != spawner)
            {
                spawner.OnAllWavesFinished -= HandleWavesFinished;
                spawner.OnAllWavesFinished += HandleWavesFinished;
            }
        }

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
        foreach (Spawner spawner in spawners)
        {
            if (null != spawner)
            {
                spawner.OnAllWavesFinished -= HandleWavesFinished;
            }
        }

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
