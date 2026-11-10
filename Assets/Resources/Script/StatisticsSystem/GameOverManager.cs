using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 종료 처리 (씬에 하나 배치)
/// - 한 명이 양쪽을 플레이하는 싱글 게임: 어느 쪽이든 죽으면 즉시 게임 오버, TriggerVictory 로 승리
/// - EventManager.OnPlayerDeath 를 구독하고, 게임 오버·승리 중 처음 한 번만 처리한다
/// - 모든 Spawner 의 웨이브가 끝나면(Spawner.OnAllWavesFinished) 승리
/// - 승리 순서: 일시정지(GamePause) → 통계 확정(StatisticsManager.Flush/Lock) → 결과 패널 표시(GameOverUI) → OnGameOver
/// - 게임 오버 순서: 일시정지 + 효과음·BGM 즉시 정지 + 통계 확정 → freezeSeconds 대기
///   → 죽은 쪽 카메라 영역을 dimSeconds 동안 어둡게(런타임 오버레이 캔버스) → holdSeconds 대기 → 패배음 + 결과 패널 → OnGameOver
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

    [Header("defeat sequence (실시간 초)")]
    [Tooltip("죽은 직후 화면을 멈춘 채로 두는 시간")]
    [SerializeField]
    private float freezeSeconds = 0.6f;
    [Tooltip("죽은 쪽 화면이 어두워지는 시간")]
    [SerializeField]
    private float dimSeconds = 0.5f;
    [Tooltip("어두워진 뒤 결과 화면까지 기다리는 시간")]
    [SerializeField]
    private float holdSeconds = 0.8f;
    [Tooltip("어둡게 할 색 (알파 = 최종 어두움 정도)")]
    [SerializeField]
    private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("HUD(10) · 일시정지(90) 위, 결과 패널(100) 아래")]
    [SerializeField]
    private int dimSortingOrder = 95;

    private bool isGameOver = false;
    private bool isVictory = false;

    private bool statsLocked = false;
    private StatisticsManager resultStats;
    private Camera deadSideCamera;
    private Rect dimRect;

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

        if (true == _victory)
        {
            SoundManager.Play(winSound);
            ShowResult();
            return;
        }

        // 게임 오버 연출 : 정지 + 모든 소리 즉시 종료 → 죽은 쪽 화면 어둡게 → 결과 화면
        SoundManager.StopAll();
        BgmDirector.StopImmediately();
        // 결과를 먼저 확정해 연출 중 들어오는 기록을 막는다
        LockStatistics();
        StartCoroutine(DefeatSequence());
    }

    private IEnumerator DefeatSequence()
    {
        yield return new WaitForSecondsRealtime(freezeSeconds);

        Image dim = CreateDimOverlay();
        float elapsed = 0f;
        while (elapsed < dimSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetDim(dim, Mathf.Clamp01(elapsed / dimSeconds));
            yield return null;
        }
        SetDim(dim, 1f);

        float hold = 0f;
        while (hold < holdSeconds)
        {
            hold += Time.unscaledDeltaTime;
            SetDim(dim, 1f);
            yield return null;
        }

        SoundManager.Play(loseSound);
        ShowResult();
    }

    private void SetDim(Image _dim, float _t)
    {
        if (null == _dim)
        {
            return;
        }
        Color c = dimColor;
        c.a = dimColor.a * _t;
        _dim.color = c;
        FitToDeadSide(_dim.rectTransform);
    }

    /// <summary>
    /// 결과 패널 아래, HUD · 일시정지 메뉴 위에 그리는 어둡게 하기 전용 캔버스
    /// </summary>
    private Image CreateDimOverlay()
    {
        GameObject root = new GameObject("GameOverDim", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        root.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = dimSortingOrder;

        GameObject imageObject = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(root.transform, false);
        imageObject.layer = root.layer;
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.clear;
        RectTransform rt = image.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        dimRect = default;
        FitToDeadSide(rt);
        return image;
    }

    /// <summary>
    /// 죽은 쪽 카메라가 그리는 화면 영역(레터박스 반영)에 맞춘다
    /// </summary>
    private void FitToDeadSide(RectTransform _rt)
    {
        Rect normalized = GetDeadSideViewport();
        if (normalized == dimRect)
        {
            return;
        }
        dimRect = normalized;
        _rt.anchorMin = normalized.min;
        _rt.anchorMax = normalized.max;
    }

    private Rect GetDeadSideViewport()
    {
        Camera cam = FindSideCamera(deadSide);
        if (null != cam && 0 < Screen.width && 0 < Screen.height)
        {
            Rect px = cam.pixelRect;
            return new Rect(px.x / Screen.width, px.y / Screen.height, px.width / Screen.width, px.height / Screen.height);
        }
        // 카메라를 못 찾으면 화면 절반
        return PlayerSide.Left == deadSide ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0.5f, 0f, 0.5f, 1f);
    }

    private Camera FindSideCamera(PlayerSide _side)
    {
        if (null != deadSideCamera)
        {
            return deadSideCamera;
        }
        DimensionApplier[] appliers = FindObjectsByType<DimensionApplier>();
        foreach (DimensionApplier applier in appliers)
        {
            if (_side == applier.Side && null != applier.SideCamera)
            {
                deadSideCamera = applier.SideCamera;
                break;
            }
        }
        return deadSideCamera;
    }

    private void LockStatistics()
    {
        if (true == statsLocked)
        {
            return;
        }
        statsLocked = true;

        resultStats = null != statisticsManager ? statisticsManager : StatisticsManager.Instance;
        if (null != resultStats)
        {
            resultStats.Flush();
            // 결과를 확정한다 (일시정지 중 늦게 들어오는 기록 차단)
            resultStats.Lock();
        }
        else
        {
            Debug.LogWarning("[GameOverManager] 씬에 StatisticsManager 가 없습니다.", this);
        }
    }

    private void ShowResult()
    {
        LockStatistics();

        if (null != gameOverUI)
        {
            gameOverUI.Show(resultStats, isVictory);
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
