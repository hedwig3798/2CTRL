using UnityEngine;

/// <summary>
/// 설정 창 화면 크기 드롭다운의 선택지와 적용
/// - 0 ~ (창 크기 수 - 1) : 해당 크기의 창 모드 (창은 드래그로 자유롭게 늘리고 줄일 수 있다. PlayerSettings.resizableWindow)
/// - 마지막 : 전체 화면 (FullScreenWindow, 모니터 해상도. 16:9 가 아니면 Letterbox 가 검은 띠로 채운다)
/// - 에디터에서는 Screen.SetResolution 이 동작하지 않으므로 Game 뷰 크기를 그대로 쓴다.
///
/// 사용 예)
///   ScreenModes.Apply(GameSettings.ScreenModeIndex);
/// </summary>
public static class ScreenModes
{
    // 16:9 창 크기
    private static readonly Vector2Int[] WINDOW_SIZES =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
    };

    private const string FULLSCREEN_LABEL = "전체 화면";
    private const string WINDOW_LABEL_FORMAT = "{0} x {1}";

    public static int Count => WINDOW_SIZES.Length + 1;
    public static int FullScreenIndex => WINDOW_SIZES.Length;

    /// <summary>
    /// 저장값이 없을 때의 기본 선택 (전체 화면 = 창 크기 목록 다음 인덱스)
    /// </summary>
    public static readonly int DEFAULT_INDEX = WINDOW_SIZES.Length;

    // 창 모드에서 작업 영역(작업 표시줄 제외) 안에 제목 표시줄 · 테두리까지 들어가도록 남길 여유 (픽셀)
    private const int WINDOW_FRAME_MARGIN_X = 16;
    private const int WINDOW_FRAME_MARGIN_Y = 48;

    public static int Clamp(int _index)
    {
        return Mathf.Clamp(_index, 0, Count - 1);
    }

    public static bool IsFullScreen(int _index)
    {
        return FullScreenIndex == Clamp(_index);
    }

    /// <summary>
    /// 드롭다운에 표시할 이름
    /// </summary>
    public static string GetLabel(int _index)
    {
        int index = Clamp(_index);
        if (true == IsFullScreen(index))
        {
            return FULLSCREEN_LABEL;
        }

        Vector2Int size = WINDOW_SIZES[index];
        return string.Format(WINDOW_LABEL_FORMAT, size.x, size.y);
    }

    /// <summary>
    /// 창 모드 크기 (전체 화면이면 모니터 해상도)
    /// </summary>
    public static Vector2Int GetSize(int _index)
    {
        int index = Clamp(_index);
        if (true == IsFullScreen(index))
        {
            Display main = Display.main;
            return new Vector2Int(main.systemWidth, main.systemHeight);
        }
        return WINDOW_SIZES[index];
    }

    /// <summary>
    /// 화면 모드 적용
    /// </summary>
    public static void Apply(int _index)
    {
        int index = Clamp(_index);
        Vector2Int size = GetSize(index);

        if (true == IsFullScreen(index))
        {
            Screen.SetResolution(size.x, size.y, FullScreenMode.FullScreenWindow);
            return;
        }

        // 작업 영역보다 큰 창은 화면 밖으로 나가므로 비율을 유지한 채 작업 영역 안으로 줄인다
        Vector2Int available = GetWindowAvailableSize();
        if (0 < available.x && 0 < available.y
            && (available.x < size.x || available.y < size.y))
        {
            float scale = Mathf.Min((float)available.x / size.x, (float)available.y / size.y);
            size = new Vector2Int(Mathf.FloorToInt(size.x * scale), Mathf.FloorToInt(size.y * scale));
        }

        Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
    }

    /// <summary>
    /// 창의 클라이언트 영역으로 쓸 수 있는 최대 크기
    /// (현재 창이 있는 모니터의 작업 영역 - 제목 표시줄 / 테두리 여유. 얻지 못하면 모니터 해상도 기준)
    /// </summary>
    private static Vector2Int GetWindowAvailableSize()
    {
        RectInt workArea = Screen.mainWindowDisplayInfo.workArea;
        int width = workArea.width;
        int height = workArea.height;
        if (width <= 0 || height <= 0)
        {
            Display main = Display.main;
            width = main.systemWidth;
            height = main.systemHeight;
        }

        return new Vector2Int(Mathf.Max(0, width - WINDOW_FRAME_MARGIN_X), Mathf.Max(0, height - WINDOW_FRAME_MARGIN_Y));
    }
}
