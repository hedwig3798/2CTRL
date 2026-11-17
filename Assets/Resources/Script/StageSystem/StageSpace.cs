using UnityEngine;

/// <summary>
/// 화면 공간 (Stage Space, 씬에 하나 배치)
/// - 최종 보스용 보스 월드(오버레이 카메라 2대가 보는 공간)를 게임 시작부터 화면 공간으로 쓴다
/// - Start 에서 오버레이 두 대를 stageWorldOrigin 중심으로 배치하고 BossSpace 를 만든다. FinalBossDirector 는 이 Space 를 재사용한다
/// - 오버레이 culling mask = Boss | Stage (Stage 레이어가 없으면 1회 경고 후 Boss 만)
/// - 진영 월드 ↔ 화면 공간 변환은 평행 이동(BossSpace). 진영 카메라가 플레이어를 따라 움직이므로 매번 변환한다
/// - 진영 카메라의 뷰포트·ortho·aspect 가 바뀌면(해상도 변경 등) 다시 배치한다
/// - 매 프레임 무기 Update 보다 먼저(DefaultExecutionOrder 음수) 화면 쿼드트리(Tree)를 재구성한다
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class StageSpace
    : MonoBehaviour
{
    private const string STAGE_LAYER_NAME = "Stage";

    [Header("카메라")]
    [SerializeField]
    private Camera leftCamera;

    [SerializeField]
    private Camera rightCamera;

    [Tooltip("왼쪽 진영 카메라 스택의 URP Overlay 카메라")]
    [SerializeField]
    private Camera leftOverlayCamera;

    [Tooltip("오른쪽 진영 카메라 스택의 URP Overlay 카메라")]
    [SerializeField]
    private Camera rightOverlayCamera;

    [Tooltip("화면 공간 중심 (진영 월드와 겹치지 않는 먼 곳). FinalBossDirector 의 bossWorldOrigin 과 같은 값")]
    [SerializeField]
    private Vector2 stageWorldOrigin = new Vector2(0f, 500f);

    [Header("쿼드트리")]
    [SerializeField]
    [Min(1)]
    private int nodeCapacity = 8;

    [SerializeField]
    [Min(0)]
    private int maxDepth = 6;

    [Tooltip("원소 수가 이보다 적으면 쿼리를 선형 탐색으로 처리")]
    [SerializeField]
    [Min(0)]
    private int linearThreshold = 32;

    [Header("대상 수집")]
    [SerializeField]
    private string leftMonsterLayerName = "LMonster";

    [SerializeField]
    private string rightMonsterLayerName = "RMonster";

    [Tooltip("콜라이더가 없거나 꺼져 크기를 못 잴 때 쓰는 반지름")]
    [SerializeField]
    [Min(0f)]
    private float defaultTargetRadius = 0.3f;

    private static StageSpace instance;
    private static int stageLayer = int.MinValue;
    private static bool stageLayerWarned;

    private BossSpace space;
    private StageQuadTree tree;
    private StageTargets targets;
    private bool isReady;

    private Rect lastLeftPixel;
    private Rect lastRightPixel;
    private float lastLeftOrtho;
    private float lastRightOrtho;

    /// <summary>
    /// 씬의 화면 공간 (없으면 null)
    /// </summary>
    public static StageSpace Instance => instance;

    /// <summary>
    /// 화면 쿼드트리 (StageSpace 가 없거나 준비 전이면 null)
    /// </summary>
    public static StageQuadTree Targets => null != instance && true == instance.isReady ? instance.tree : null;

    /// <summary>
    /// "Stage" 레이어 번호 (없으면 -1)
    /// </summary>
    public static int StageLayer
    {
        get
        {
            if (int.MinValue == stageLayer)
            {
                stageLayer = LayerMask.NameToLayer(STAGE_LAYER_NAME);
            }
            return stageLayer;
        }
    }

    public bool IsReady => isReady;
    public BossSpace Space => space;
    public StageQuadTree Tree => tree;
    public Vector2 StageWorldOrigin => stageWorldOrigin;

    /// <summary>
    /// 화면 공간 전체 사각형 (좌우 화면 + 가운데 구분선)
    /// </summary>
    public Rect StageRect => null != space ? space.StageRect : new Rect(stageWorldOrigin, Vector2.zero);

    /// <summary>
    /// 화면 중앙 (좌우 화면 사이 구분선)
    /// </summary>
    public Vector2 StageCenter => StageRect.center;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        instance = null;
        stageLayer = int.MinValue;
        stageLayerWarned = false;
    }

    #region Unity
    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogWarning("[StageSpace] 씬에 StageSpace 가 둘 이상입니다. 나중 것을 끕니다.");
            enabled = false;
            return;
        }
        instance = this;
        tree = new StageQuadTree(nodeCapacity, maxDepth, linearThreshold);
        targets = new StageTargets(leftMonsterLayerName, rightMonsterLayerName, defaultTargetRadius);
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    private void Start()
    {
        TryLayout();
    }

    private void Update()
    {
        if (false == isReady)
        {
            TryLayout();
            if (false == isReady)
            {
                return;
            }
        }
        else if (true == CamerasChanged())
        {
            Layout();
        }

        // 일시정지(레벨업 UI 등, timeScale 0) 중에는 적이 움직이지 않으므로 재구성을 생략한다
        // 재개 직후 첫 프레임은 실행 순서에 따라 무기가 정지 전 트리를 볼 수 있다 (1프레임 지연 허용)
        if (Time.timeScale <= 0f)
        {
            return;
        }

        targets.Rebuild(tree, space);
    }
    #endregion

    #region 배치
    private bool HasCameras => null != leftCamera && null != rightCamera && null != leftOverlayCamera && null != rightOverlayCamera;

    private void TryLayout()
    {
        if (false == HasCameras)
        {
            return;
        }
        Layout();
        space = new BossSpace(leftOverlayCamera, rightOverlayCamera, leftCamera, rightCamera);
        isReady = true;
    }

    /// <summary>
    /// 오버레이 두 대를 화면 공간에 배치한다: 중심 stageWorldOrigin, 좌우 뷰 폭 + 가운데 구분선(화면 픽셀 틈을 월드로 환산)
    /// ortho 는 진영 카메라와 같게 (카메라 스택 등록은 씬에서)
    /// </summary>
    private void Layout()
    {
        float leftWidth = leftCamera.orthographicSize * leftCamera.aspect * 2f;
        float rightWidth = rightCamera.orthographicSize * rightCamera.aspect * 2f;
        float gapPixels = rightCamera.pixelRect.xMin - leftCamera.pixelRect.xMax;
        float gap = BossSpace.PixelsToWorld(gapPixels, leftCamera.orthographicSize, leftCamera.pixelRect.height);

        BossSpace.OverlayCenters(stageWorldOrigin, leftWidth, rightWidth, gap, out Vector2 leftCenter, out Vector2 rightCenter);
        int mask = ResolveCullingMask();
        PlaceOverlay(leftOverlayCamera, leftCamera, leftCenter, mask);
        PlaceOverlay(rightOverlayCamera, rightCamera, rightCenter, mask);

        lastLeftPixel = leftCamera.pixelRect;
        lastRightPixel = rightCamera.pixelRect;
        lastLeftOrtho = leftCamera.orthographicSize;
        lastRightOrtho = rightCamera.orthographicSize;
    }

    private static void PlaceOverlay(Camera _overlay, Camera _side, Vector2 _center, int _mask)
    {
        _overlay.orthographic = true;
        _overlay.orthographicSize = _side.orthographicSize;
        _overlay.cullingMask = _mask;
        _overlay.transform.position = new Vector3(_center.x, _center.y, ResolveOverlayZ(_overlay, _side));
    }

    private const float DEFAULT_OVERLAY_Z = -10f;

    /// <summary>
    /// 오버레이 카메라 z. 무대 오브젝트는 z=0 이라 카메라는 반드시 음수 z 여야 보인다
    /// - 진영 카메라가 아직 배치 전(z=0)일 수 있으므로 음수가 아니면 오버레이 자신의 z, 그것도 아니면 -10
    /// </summary>
    private static float ResolveOverlayZ(Camera _overlay, Camera _side)
    {
        float z = _side.transform.position.z;
        if (z < 0f)
        {
            return z;
        }
        z = _overlay.transform.position.z;
        return z < 0f ? z : DEFAULT_OVERLAY_Z;
    }

    private static int ResolveCullingMask()
    {
        int mask = 1 << FinalBossDirector.ResolveBossLayer();
        int stage = StageLayer;
        if (stage < 0)
        {
            if (false == stageLayerWarned)
            {
                stageLayerWarned = true;
                Debug.LogWarning("[StageSpace] \"" + STAGE_LAYER_NAME + "\" 레이어가 없습니다. 오버레이는 보스 레이어만 그립니다.");
            }
            return mask;
        }
        return mask | (1 << stage);
    }

    private bool CamerasChanged()
    {
        if (false == HasCameras)
        {
            return false;
        }
        return lastLeftPixel != leftCamera.pixelRect || lastRightPixel != rightCamera.pixelRect
            || false == Mathf.Approximately(lastLeftOrtho, leftCamera.orthographicSize)
            || false == Mathf.Approximately(lastRightOrtho, rightCamera.orthographicSize);
    }
    #endregion

    #region 변환
    /// <summary>
    /// _side 진영 월드 → 화면 공간
    /// </summary>
    public Vector2 WorldToStage(PlayerSide _side, Vector2 _world)
    {
        return null != space ? space.FromSideWorld(_side, _world) : _world;
    }

    /// <summary>
    /// 화면 공간 → _side 진영 월드
    /// </summary>
    public Vector2 StageToWorld(PlayerSide _side, Vector2 _stage)
    {
        return null != space ? space.ToSideWorld(_side, _stage) : _stage;
    }

    /// <summary>
    /// _side 진영 월드 점이 그 진영 화면(카메라 뷰) 안에 있는지 (경계 포함)
    /// </summary>
    public bool IsOnScreen(PlayerSide _side, Vector2 _world)
    {
        return null != space && true == BossSpace.ContainsInclusive(space.SideViewRect(_side), _world);
    }
    #endregion
}
