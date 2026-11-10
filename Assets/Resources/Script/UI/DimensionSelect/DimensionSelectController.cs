using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 차원 선택 씬 로직
/// - 차원을 columns 열 격자에 왼쪽 위부터 순서대로 놓고, 진영마다 커서를 4방향으로 옮겨 확정한다. 확정 키를 다시 누르면 확정을 취소한다
///   - 왼쪽 : W / S / A / D 로 이동하고 Space 로 확정
///   - 오른쪽 : ↑ / ↓ / ← / → 로 이동하고 Enter 로 확정
/// - 가로는 현재 줄 안에서, 세로는 줄 수 안에서 순환한다. 세로로 옮긴 곳이 빈칸이면 그 줄의 마지막 칸으로 간다
/// - 확정한 동안은 커서를 옮길 수 없다
/// - 커서는 상대가 확정한 칸을 포함해 어느 칸이든 이동·겹침 가능하다
/// - 상대가 확정한 차원은 확정할 수 없다. 커서가 겹치면 먼저 확정 키를 누른 쪽이 차지한다 (같은 프레임이면 왼쪽이 먼저)
/// - 둘 다 확정하면 loadDelay 뒤 DimensionSelection 을 세팅하고 gameSceneName 을 로드한다 (대기 중 취소하면 로드하지 않음)
/// - 표시는 DimensionGridCell(격자), DimensionSelectView(좌우 큰 카드) 또는 이벤트로 한다
/// </summary>
public class DimensionSelectController
    : MonoBehaviour
{
    [Tooltip("고를 수 있는 차원 (2개 이상)")]
    [SerializeField]
    private DimensionData[] dimensions;

    [Tooltip("격자의 열 수 (1 ~ 차원 수로 보정, 차원은 왼쪽 위부터 채운다)")]
    [SerializeField]
    private int columns = 3;

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
    private Key leftUpKey = Key.W;

    [SerializeField]
    private Key leftDownKey = Key.S;

    [SerializeField]
    private Key leftLeftKey = Key.A;

    [SerializeField]
    private Key leftRightKey = Key.D;

    [SerializeField]
    private Key leftConfirmKey = Key.Space;

    [Header("Right Keys")]
    [SerializeField]
    private Key rightUpKey = Key.UpArrow;

    [SerializeField]
    private Key rightDownKey = Key.DownArrow;

    [SerializeField]
    private Key rightLeftKey = Key.LeftArrow;

    [SerializeField]
    private Key rightRightKey = Key.RightArrow;

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

    [Tooltip("격자 칸 (dimensions 와 같은 순서, 칸 수가 달라도 안전)")]
    [SerializeField]
    private DimensionGridCell[] gridCells;

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

    /// <summary>
    /// 확정할 수 없는 차원을 확정하려 했을 때 (진영)
    /// </summary>
    public event Action<PlayerSide> OnConfirmDenied;

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

        HandleInput(keyboard, PlayerSide.Left, leftUpKey, leftDownKey, leftLeftKey, leftRightKey, leftConfirmKey, Key.None);
        HandleInput(keyboard, PlayerSide.Right, rightUpKey, rightDownKey, rightLeftKey, rightRightKey, rightConfirmKey, rightConfirmAltKey);
    }

    private static bool WasPressed(Keyboard _keyboard, Key _key)
    {
        return Key.None != _key && _keyboard[_key].wasPressedThisFrame;
    }

    private void HandleInput(Keyboard _keyboard, PlayerSide _side, Key _up, Key _down, Key _left, Key _right, Key _confirm, Key _confirmAlt)
    {
        if (true == WasPressed(_keyboard, _up))
        {
            MoveCursor(_side, 0, -1);
        }

        if (true == WasPressed(_keyboard, _down))
        {
            MoveCursor(_side, 0, 1);
        }

        if (true == WasPressed(_keyboard, _left))
        {
            MoveCursor(_side, -1, 0);
        }

        if (true == WasPressed(_keyboard, _right))
        {
            MoveCursor(_side, 1, 0);
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
    /// _side 커서를 격자에서 (_dx, _dy) 만큼 옮긴다. _dy 는 아래쪽이 + 이다
    /// - 가로는 현재 줄의 실제 길이 안에서, 세로는 줄 수 안에서 순환한다
    /// - 세로로 옮긴 곳이 빈칸이면 그 줄의 마지막 칸으로 간다
    /// - 상대가 확정한 칸도 건너뛰지 않는다. 확정한 동안은 옮기지 않는다
    /// </summary>
    /// <returns>커서가 바뀌었으면 true</returns>
    public bool MoveCursor(PlayerSide _side, int _dx, int _dy)
    {
        int i = (int)_side;
        if (true == confirmed[i] || (0 == _dx && 0 == _dy) || null == dimensions || 0 == dimensions.Length)
        {
            return false;
        }

        int count = dimensions.Length;
        int gridColumns = Mathf.Clamp(columns, 1, count);
        int rowCount = (count + gridColumns - 1) / gridColumns;

        int row = cursors[i] / gridColumns;
        int column = Wrap(cursors[i] % gridColumns + _dx, GetRowLength(row, rowCount, gridColumns, count));

        row = Wrap(row + _dy, rowCount);
        column = Mathf.Min(column, GetRowLength(row, rowCount, gridColumns, count) - 1);

        int index = row * gridColumns + column;
        if (index == cursors[i])
        {
            return false;
        }

        cursors[i] = index;
        OnCursorChanged?.Invoke(_side, index);
        RefreshViews();
        return true;
    }

    private static int Wrap(int _value, int _length)
    {
        return (_value % _length + _length) % _length;
    }

    /// <summary>
    /// _row 줄의 실제 칸 수 (마지막 줄만 짧을 수 있다)
    /// </summary>
    private static int GetRowLength(int _row, int _rowCount, int _columns, int _count)
    {
        return _row < _rowCount - 1 ? _columns : _count - _columns * (_rowCount - 1);
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
            OnConfirmDenied?.Invoke(_side);
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
        RefreshGridCells();
    }

    private void RefreshGridCells()
    {
        if (null == gridCells)
        {
            return;
        }

        int count = null != dimensions ? dimensions.Length : 0;
        int left = (int)PlayerSide.Left;
        int right = (int)PlayerSide.Right;

        for (int i = 0; i < gridCells.Length; ++i)
        {
            DimensionGridCell cell = gridCells[i];
            if (null == cell)
            {
                continue;
            }

            bool hasDimension = i < count;
            cell.Show(
                true == hasDimension ? dimensions[i] : null,
                true == hasDimension && cursors[left] == i,
                confirmed[left],
                true == hasDimension && cursors[right] == i,
                confirmed[right]);
        }
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
