using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 차원 선택 씬 로직
/// - 진영마다 커서로 차원을 넘기고 확정한다. 확정 키를 다시 누르면 확정을 취소한다
///   - 왼쪽 : A / D 로 넘기고 Space 로 확정
///   - 오른쪽 : ← / → 로 넘기고 Enter 로 확정
/// - 확정한 동안은 커서를 넘길 수 없다
/// - 상대가 확정한 차원은 확정할 수 없고, 넘길 때 건너뛴다
/// - 둘 다 확정하면 loadDelay 뒤 DimensionSelection 을 세팅하고 gameSceneName 을 로드한다 (대기 중 취소하면 로드하지 않음)
/// - 표시는 DimensionSelectView 또는 이벤트로 한다
/// </summary>
public class DimensionSelectController
    : MonoBehaviour
{
    [Tooltip("고를 수 있는 차원 (2개 이상)")]
    [SerializeField]
    private DimensionData[] dimensions;

    [Tooltip("둘 다 확정하면 로드할 씬 (Build Settings 에 등록되어 있어야 함)")]
    [SerializeField]
    private string gameSceneName = "GameScene";

    [Tooltip("둘 다 확정한 뒤 씬을 로드할 때까지 기다리는 시간 (실제 시간, 초)")]
    [SerializeField]
    private float loadDelay = 0.5f;

    [Header("Start Cursor")]
    [SerializeField]
    private int leftStartIndex = 0;

    [SerializeField]
    private int rightStartIndex = 1;

    [Header("Left Keys")]
    [SerializeField]
    private Key leftPrevKey = Key.A;

    [SerializeField]
    private Key leftNextKey = Key.D;

    [SerializeField]
    private Key leftConfirmKey = Key.Space;

    [Header("Right Keys")]
    [SerializeField]
    private Key rightPrevKey = Key.LeftArrow;

    [SerializeField]
    private Key rightNextKey = Key.RightArrow;

    [SerializeField]
    private Key rightConfirmKey = Key.Enter;

    [Tooltip("오른쪽 확정 보조 키 (None 이면 사용 안 함)")]
    [SerializeField]
    private Key rightConfirmAltKey = Key.NumpadEnter;

    [Header("View")]
    [SerializeField]
    private DimensionSelectView leftView;

    [SerializeField]
    private DimensionSelectView rightView;

    /// <summary>
    /// 커서가 바뀌었을 때 (진영, 새 인덱스)
    /// </summary>
    public event Action<PlayerSide, int> OnCursorChanged;

    /// <summary>
    /// 확정 / 취소했을 때 (진영, 확정 여부)
    /// </summary>
    public event Action<PlayerSide, bool> OnConfirmChanged;

    /// <summary>
    /// 둘 다 확정했을 때 (씬 로드 대기 시작)
    /// </summary>
    public event Action OnBothConfirmed;

    private readonly int[] cursors = new int[2];
    private readonly bool[] confirmed = new bool[2];

    private Coroutine loadRoutine;

    public IReadOnlyList<DimensionData> Dimensions => dimensions;
    public bool IsBothConfirmed => true == confirmed[0] && true == confirmed[1];

    private void Awake()
    {
        if (null == dimensions || dimensions.Length < 2)
        {
            Debug.LogError($"[DimensionSelectController] 차원이 2개 이상 필요합니다. ({name})", this);
        }

        int count = null != dimensions ? dimensions.Length : 0;
        cursors[(int)PlayerSide.Left] = 0 < count ? Mathf.Clamp(leftStartIndex, 0, count - 1) : 0;
        cursors[(int)PlayerSide.Right] = 0 < count ? Mathf.Clamp(rightStartIndex, 0, count - 1) : 0;
    }

    private void Start()
    {
        RefreshViews();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null == keyboard)
        {
            return;
        }

        HandleInput(keyboard, PlayerSide.Left, leftPrevKey, leftNextKey, leftConfirmKey, Key.None);
        HandleInput(keyboard, PlayerSide.Right, rightPrevKey, rightNextKey, rightConfirmKey, rightConfirmAltKey);
    }

    private static bool WasPressed(Keyboard _keyboard, Key _key)
    {
        return Key.None != _key && _keyboard[_key].wasPressedThisFrame;
    }

    private void HandleInput(Keyboard _keyboard, PlayerSide _side, Key _prev, Key _next, Key _confirm, Key _confirmAlt)
    {
        if (true == WasPressed(_keyboard, _prev))
        {
            MoveCursor(_side, -1);
        }

        if (true == WasPressed(_keyboard, _next))
        {
            MoveCursor(_side, 1);
        }

        if (true == WasPressed(_keyboard, _confirm) || true == WasPressed(_keyboard, _confirmAlt))
        {
            ToggleConfirm(_side);
        }
    }

    /// <summary>
    /// _side 커서가 가리키는 인덱스
    /// </summary>
    public int GetCursor(PlayerSide _side)
    {
        return cursors[(int)_side];
    }

    /// <summary>
    /// _side 커서가 가리키는 차원 (없으면 null)
    /// </summary>
    public DimensionData GetCurrent(PlayerSide _side)
    {
        int index = cursors[(int)_side];
        return null != dimensions && 0 <= index && index < dimensions.Length ? dimensions[index] : null;
    }

    public bool IsConfirmed(PlayerSide _side)
    {
        return confirmed[(int)_side];
    }

    /// <summary>
    /// _side 가 _index 차원을 확정할 수 있는지 (비어 있지 않고 상대가 확정하지 않은 차원)
    /// </summary>
    public bool CanConfirm(PlayerSide _side, int _index)
    {
        if (null == dimensions || _index < 0 || dimensions.Length <= _index || null == dimensions[_index])
        {
            return false;
        }

        int other = (int)_side.Opposite();
        return false == confirmed[other] || cursors[other] != _index;
    }

    /// <summary>
    /// _side 커서를 _direction(-1 / +1) 방향으로 넘긴다. 상대가 확정한 차원은 건너뛴다
    /// 확정한 동안은 넘기지 않는다
    /// </summary>
    /// <returns>커서가 바뀌었으면 true</returns>
    public bool MoveCursor(PlayerSide _side, int _direction)
    {
        int i = (int)_side;
        if (true == confirmed[i] || 0 == _direction || null == dimensions || 0 == dimensions.Length)
        {
            return false;
        }

        int count = dimensions.Length;
        int step = 0 < _direction ? 1 : -1;
        int index = cursors[i];
        for (int n = 0; n < count - 1; ++n)
        {
            index = (index + step + count) % count;
            if (true == CanConfirm(_side, index))
            {
                cursors[i] = index;
                OnCursorChanged?.Invoke(_side, index);
                RefreshViews();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// _side 의 확정을 토글한다. 확정할 수 없는 차원이면 무시한다
    /// </summary>
    /// <returns>상태가 바뀌었으면 true</returns>
    public bool ToggleConfirm(PlayerSide _side)
    {
        int i = (int)_side;
        if (false == confirmed[i] && false == CanConfirm(_side, cursors[i]))
        {
            return false;
        }

        confirmed[i] = !confirmed[i];
        OnConfirmChanged?.Invoke(_side, confirmed[i]);
        RefreshViews();

        // 로드 대기 중 취소하면 대기를 멈춘다
        if (false == IsBothConfirmed && null != loadRoutine)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        if (true == IsBothConfirmed)
        {
            OnBothConfirmed?.Invoke();
            if (loadDelay <= 0f)
            {
                LoadGameSceneNow();
            }
            else if (null == loadRoutine)
            {
                loadRoutine = StartCoroutine(LoadGameScene());
            }
        }
        return true;
    }

    private void OnDisable()
    {
        if (null != loadRoutine)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }
    }

    private IEnumerator LoadGameScene()
    {
        yield return new WaitForSecondsRealtime(loadDelay);
        loadRoutine = null;

        // 기다리는 동안 취소했으면 로드하지 않는다
        if (false == IsBothConfirmed)
        {
            yield break;
        }

        LoadGameSceneNow();
    }

    private void LoadGameSceneNow()
    {
        DimensionSelection.Set(GetCurrent(PlayerSide.Left), GetCurrent(PlayerSide.Right));
        SceneManager.LoadScene(gameSceneName);
    }

    private void RefreshViews()
    {
        RefreshView(leftView, PlayerSide.Left);
        RefreshView(rightView, PlayerSide.Right);
    }

    private void RefreshView(DimensionSelectView _view, PlayerSide _side)
    {
        if (null == _view)
        {
            return;
        }

        bool isConfirmed = confirmed[(int)_side];
        bool isLocked = false == isConfirmed && false == CanConfirm(_side, cursors[(int)_side]);
        _view.Show(GetCurrent(_side), isConfirmed, isLocked);
    }
}
