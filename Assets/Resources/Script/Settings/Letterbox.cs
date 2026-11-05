using UnityEngine;

/// <summary>
/// 게임 화면을 16:9 로 고정하기 위한 화면 영역 계산
/// - 창 비율이 16:9 보다 넓으면 좌우(필러박스), 좁으면 위아래(레터박스)에 검은 띠가 생긴다.
/// - 영역은 0 ~ 1 정규화 좌표 (Camera.rect, 앵커와 같은 기준)
/// - 같은 프레임에는 한 번만 다시 계산한다.
///
/// 사용 예)
///   Rect area = Letterbox.NormalizedRect;
///   camera.rect = Letterbox.Map(originalRect);
/// </summary>
public static class Letterbox
{
    public const float TARGET_WIDTH = 16f;
    public const float TARGET_HEIGHT = 9f;
    public const float TARGET_ASPECT = TARGET_WIDTH / TARGET_HEIGHT;

    private static int cachedFrame = -1;
    private static int cachedWidth;
    private static int cachedHeight;
    private static Rect cachedRect = new Rect(0f, 0f, 1f, 1f);

    /// <summary>
    /// 16:9 게임 영역 (정규화)
    /// </summary>
    public static Rect NormalizedRect
    {
        get
        {
            Refresh();
            return cachedRect;
        }
    }

    /// <summary>
    /// 16:9 게임 영역 (픽셀)
    /// </summary>
    public static Rect PixelRect
    {
        get
        {
            Refresh();
            return new Rect(cachedRect.x * cachedWidth, cachedRect.y * cachedHeight,
                cachedRect.width * cachedWidth, cachedRect.height * cachedHeight);
        }
    }

    /// <summary>
    /// 전체 화면 기준 정규화 영역(_rect)을 16:9 게임 영역 안의 영역으로 변환
    /// (좌우 분할 카메라처럼 원래 rect 가 있는 경우 그 비율을 유지)
    /// </summary>
    public static Rect Map(Rect _rect)
    {
        Rect area = NormalizedRect;
        return new Rect(
            area.x + _rect.x * area.width,
            area.y + _rect.y * area.height,
            _rect.width * area.width,
            _rect.height * area.height);
    }

    /// <summary>
    /// 주어진 화면 크기에서의 16:9 영역 계산 (테스트용으로 공개)
    /// </summary>
    public static Rect Calculate(int _screenWidth, int _screenHeight)
    {
        if (_screenWidth <= 0 || _screenHeight <= 0)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        float screenAspect = (float)_screenWidth / _screenHeight;
        if (screenAspect > TARGET_ASPECT)
        {
            // 옆이 남음 : 좌우 검은 띠
            float w = TARGET_ASPECT / screenAspect;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        if (screenAspect < TARGET_ASPECT)
        {
            // 위아래가 남음 : 위아래 검은 띠
            float h = screenAspect / TARGET_ASPECT;
            return new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        return new Rect(0f, 0f, 1f, 1f);
    }

    private static void Refresh()
    {
        if (cachedFrame == Time.frameCount)
        {
            return;
        }
        cachedFrame = Time.frameCount;

        int width = Screen.width;
        int height = Screen.height;
        if (width == cachedWidth && height == cachedHeight)
        {
            return;
        }

        cachedWidth = width;
        cachedHeight = height;
        cachedRect = Calculate(width, height);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cachedFrame = -1;
        cachedWidth = 0;
        cachedHeight = 0;
        cachedRect = new Rect(0f, 0f, 1f, 1f);
    }
}
