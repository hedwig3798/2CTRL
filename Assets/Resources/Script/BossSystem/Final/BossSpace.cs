using UnityEngine;

/// <summary>
/// 최종 보스 공간 (보스 월드 ↔ 진영 월드 좌표 변환)
/// - 보스는 전용 레이어("Boss")의 별도 월드에 산다. 진영마다 URP Overlay 카메라(진영 카메라 스택)가 그 진영 뷰포트 안에 보스 레이어만 그린다
/// - 왼쪽 오버레이는 보스 월드 왼쪽 절반 중심, 오른쪽 오버레이는 오른쪽 절반 중심에 둔다 (가운데 구분선 폭만큼 틈)
/// - 오버레이와 진영 카메라의 ortho·aspect·뷰포트가 같으므로 변환은 평행 이동뿐이다: 진영 월드 = 진영 카메라 위치 + (p - 진영 오버레이 위치)
/// - 배치 계산(OverlayCenters)과 매핑(Translate)은 순수 함수
/// </summary>
public class BossSpace
{
    private readonly Camera leftOverlay;
    private readonly Camera rightOverlay;
    private readonly Camera left;
    private readonly Camera right;

    public BossSpace(Camera _leftOverlay, Camera _rightOverlay, Camera _left, Camera _right)
    {
        leftOverlay = _leftOverlay;
        rightOverlay = _rightOverlay;
        left = _left;
        right = _right;
    }

    public Camera GetCamera(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? left : right;
    }

    public Camera GetOverlay(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? leftOverlay : rightOverlay;
    }

    #region 순수 계산
    /// <summary>
    /// 정사영 카메라가 비추는 월드 사각형 (중심, orthographicSize, aspect)
    /// </summary>
    public static Rect OrthoWorldRect(Vector2 _center, float _orthoSize, float _aspect)
    {
        float halfH = Mathf.Max(0f, _orthoSize);
        float halfW = halfH * Mathf.Max(0f, _aspect);
        return new Rect(_center.x - halfW, _center.y - halfH, halfW * 2f, halfH * 2f);
    }

    /// <summary>
    /// _from 사각형 안의 상대 위치를 _to 사각형의 같은 상대 위치로 옮긴다 (밖이면 클램프 없이 외삽)
    /// _from 의 크기가 0 인 축은 _to 의 중심으로
    /// </summary>
    public static Vector2 MapPoint(Vector2 _p, Rect _from, Rect _to)
    {
        float x = Mathf.Approximately(0f, _from.width)
            ? _to.center.x
            : _to.xMin + (_p.x - _from.xMin) / _from.width * _to.width;
        float y = Mathf.Approximately(0f, _from.height)
            ? _to.center.y
            : _to.yMin + (_p.y - _from.yMin) / _from.height * _to.height;
        return new Vector2(x, y);
    }

    /// <summary>
    /// _r 사각형을 _from → _to 매핑으로 옮긴다
    /// </summary>
    public static Rect MapRect(Rect _r, Rect _from, Rect _to)
    {
        Vector2 min = MapPoint(_r.min, _from, _to);
        Vector2 max = MapPoint(_r.max, _from, _to);
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    /// <summary>
    /// 두 사각형을 모두 덮는 사각형
    /// </summary>
    public static Rect Union(Rect _a, Rect _b)
    {
        return Rect.MinMaxRect(
            Mathf.Min(_a.xMin, _b.xMin), Mathf.Min(_a.yMin, _b.yMin),
            Mathf.Max(_a.xMax, _b.xMax), Mathf.Max(_a.yMax, _b.yMax));
    }

    /// <summary>
    /// 원이 사각형과 겹치는지 (경계 접촉 포함)
    /// </summary>
    public static bool CircleOverlapsRect(Vector2 _center, float _radius, Rect _rect)
    {
        float cx = Mathf.Clamp(_center.x, _rect.xMin, _rect.xMax);
        float cy = Mathf.Clamp(_center.y, _rect.yMin, _rect.yMax);
        float dx = _center.x - cx;
        float dy = _center.y - cy;
        return dx * dx + dy * dy <= _radius * _radius;
    }

    /// <summary>
    /// 사각형을 _inset 만큼 안쪽으로 줄인다 (음수 크기면 중심점)
    /// </summary>
    public static Rect Inset(Rect _rect, float _inset)
    {
        float halfW = Mathf.Max(0f, _rect.width * 0.5f - _inset);
        float halfH = Mathf.Max(0f, _rect.height * 0.5f - _inset);
        Vector2 c = _rect.center;
        return new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);
    }

    /// <summary>
    /// 경계 포함 사각형 판정 (Rect.Contains 는 최대 경계를 빼서 크기 0 사각형을 늘 거짓으로 본다)
    /// </summary>
    public static bool ContainsInclusive(Rect _rect, Vector2 _p)
    {
        return _p.x >= _rect.xMin && _p.x <= _rect.xMax && _p.y >= _rect.yMin && _p.y <= _rect.yMax;
    }

    /// <summary>
    /// 평행 이동: _fromOrigin 기준 상대 위치를 _toOrigin 기준으로 옮긴다
    /// </summary>
    public static Vector2 Translate(Vector2 _p, Vector2 _fromOrigin, Vector2 _toOrigin)
    {
        return _toOrigin + (_p - _fromOrigin);
    }

    /// <summary>
    /// 화면 픽셀 간격을 월드 길이로 (정사영: 세로 픽셀 _pixelHeight 가 월드 2 × _orthoSize)
    /// </summary>
    public static float PixelsToWorld(float _pixels, float _orthoSize, float _pixelHeight)
    {
        if (_pixelHeight <= 0f)
        {
            return 0f;
        }
        return Mathf.Max(0f, _pixels) * 2f * Mathf.Max(0f, _orthoSize) / _pixelHeight;
    }

    /// <summary>
    /// 보스 월드 배치: 무대 중심 _origin 에 좌우 뷰(폭 _leftWidth, _rightWidth)를 틈 _gap 을 두고 붙였을 때 각 오버레이 중심
    /// </summary>
    public static void OverlayCenters(Vector2 _origin, float _leftWidth, float _rightWidth, float _gap,
        out Vector2 _leftCenter, out Vector2 _rightCenter)
    {
        float total = Mathf.Max(0f, _leftWidth) + Mathf.Max(0f, _gap) + Mathf.Max(0f, _rightWidth);
        float xMin = _origin.x - total * 0.5f;
        _leftCenter = new Vector2(xMin + Mathf.Max(0f, _leftWidth) * 0.5f, _origin.y);
        _rightCenter = new Vector2(xMin + total - Mathf.Max(0f, _rightWidth) * 0.5f, _origin.y);
    }
    #endregion

    #region 카메라 사각형
    private static Rect WorldRect(Camera _camera)
    {
        return OrthoWorldRect(_camera.transform.position, _camera.orthographicSize, _camera.aspect);
    }

    /// <summary>
    /// 진영 카메라의 월드 뷰 사각형
    /// </summary>
    public Rect SideViewRect(PlayerSide _side)
    {
        return WorldRect(GetCamera(_side));
    }
    #endregion

    #region 변환
    /// <summary>
    /// 보스 월드 점이 들어 있는 진영(오버레이 뷰)을 찾고 그 진영 월드 좌표를 돌려준다 (가운데 틈이나 무대 밖이면 false)
    /// </summary>
    public bool TryToSideWorld(Vector2 _bossPos, out PlayerSide _side, out Vector2 _world)
    {
        if (true == ContainsInclusive(SideRect(PlayerSide.Left), _bossPos))
        {
            _side = PlayerSide.Left;
            _world = ToSideWorld(_side, _bossPos);
            return true;
        }
        if (true == ContainsInclusive(SideRect(PlayerSide.Right), _bossPos))
        {
            _side = PlayerSide.Right;
            _world = ToSideWorld(_side, _bossPos);
            return true;
        }

        _side = PlayerSide.Left;
        _world = Vector2.zero;
        return false;
    }

    /// <summary>
    /// 보스 월드 → _side 진영 월드 (그 진영 뷰 밖이어도 그대로 평행 이동)
    /// </summary>
    public Vector2 ToSideWorld(PlayerSide _side, Vector2 _bossPos)
    {
        return Translate(_bossPos, GetOverlay(_side).transform.position, GetCamera(_side).transform.position);
    }

    /// <summary>
    /// _side 진영 월드 → 보스 월드
    /// </summary>
    public Vector2 FromSideWorld(PlayerSide _side, Vector2 _world)
    {
        return Translate(_world, GetCamera(_side).transform.position, GetOverlay(_side).transform.position);
    }

    /// <summary>
    /// _side 진영 오버레이가 비추는 보스 월드 사각형 (크기는 진영 카메라 뷰와 같다)
    /// </summary>
    public Rect SideRect(PlayerSide _side)
    {
        Camera cam = GetCamera(_side);
        return OrthoWorldRect(GetOverlay(_side).transform.position, cam.orthographicSize, cam.aspect);
    }

    /// <summary>
    /// 보스 월드 기준 전체 무대 사각형 (좌우 오버레이 뷰의 합집합, 가운데 틈 포함)
    /// </summary>
    public Rect StageRect => Union(SideRect(PlayerSide.Left), SideRect(PlayerSide.Right));

    /// <summary>
    /// 보스 월드 길이 1 이 진영 월드에서 몇인지 (평행 이동이라 항상 1)
    /// </summary>
    public float SideScale(PlayerSide _side)
    {
        return 1f;
    }
    #endregion
}
