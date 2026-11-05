using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameSettings 값을 게임에 적용하는 상주 오브젝트 (첫 씬 로드 전에 자동 생성, DontDestroyOnLoad)
/// - 시작 시 저장된 화면 모드 / Master 볼륨 적용, 이후 변경 이벤트로 즉시 반영
///   Master : AudioListener.volume = GameSettings.GetEffectiveVolume(Master) (음소거면 0)
/// - 16:9 고정
///   · 배경 카메라 : 가장 먼저(depth 최저) 화면 전체를 검은색으로 지운다. (URP 에서 rect 밖이 이전 프레임 픽셀로 남지 않게)
///   · 씬이 로드될 때마다 모든 카메라에 LetterboxCamera, Overlay 루트 캔버스에 LetterboxCanvas 를 붙인다.
///     (targetTexture 로 그리는 카메라는 제외)
///   · 규약 : 씬 로드 뒤 런타임에 Instantiate 한 Overlay 루트 캔버스 / 카메라는 자동으로 붙지 않으므로
///     생성 직후 SettingsApplier.Instance.AttachLetterbox() 를 호출한다.
///   · 16:9 영역이 화면 전체면 배경 카메라는 꺼 둔다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class SettingsApplier
    : MonoBehaviour
{
    private const string OBJECT_NAME = "[SettingsApplier]";
    private const string BACKGROUND_CAMERA_NAME = "LetterboxBackgroundCamera";
    // 어떤 게임 카메라보다 먼저 그려지도록
    private const float BACKGROUND_CAMERA_DEPTH = -100f;

    private static SettingsApplier instance;

    private Camera backgroundCamera;

    public static SettingsApplier Instance => instance;
    public static bool HasInstance => null != instance;

    public Camera BackgroundCamera => backgroundCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (null != instance)
        {
            return;
        }

        GameObject go = new GameObject(OBJECT_NAME);
        DontDestroyOnLoad(go);
        go.AddComponent<SettingsApplier>();
    }

    /// <summary>
    /// 현재 씬의 카메라 / Overlay 캔버스에 레터박스 컴포넌트를 붙인다
    /// (씬 로드 뒤 런타임에 만든 카메라 / 캔버스가 있으면 직접 호출)
    /// </summary>
    public void AttachLetterbox()
    {
        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (cam == backgroundCamera || null != cam.targetTexture)
            {
                continue;
            }
            if (null == cam.GetComponent<LetterboxCamera>())
            {
                cam.gameObject.AddComponent<LetterboxCamera>();
            }
        }

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (false == canvas.isRootCanvas || RenderMode.ScreenSpaceOverlay != canvas.renderMode)
            {
                continue;
            }
            if (null == canvas.GetComponent<LetterboxCanvas>())
            {
                canvas.gameObject.AddComponent<LetterboxCanvas>();
            }
        }
    }

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        CreateBackgroundCamera();

        ScreenModes.Apply(GameSettings.ScreenModeIndex);
        ApplyMasterVolume();
    }

    private void OnEnable()
    {
        if (this != instance)
        {
            return;
        }

        GameSettings.OnScreenModeChanged += HandleScreenModeChanged;
        GameSettings.OnVolumeChanged += HandleVolumeChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        GameSettings.OnScreenModeChanged -= HandleScreenModeChanged;
        GameSettings.OnVolumeChanged -= HandleVolumeChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    private void CreateBackgroundCamera()
    {
        GameObject go = new GameObject(BACKGROUND_CAMERA_NAME, typeof(Camera));
        go.transform.SetParent(transform, false);

        backgroundCamera = go.GetComponent<Camera>();
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = Color.black;
        backgroundCamera.cullingMask = 0;
        backgroundCamera.depth = BACKGROUND_CAMERA_DEPTH;
        backgroundCamera.orthographic = true;
        backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
        backgroundCamera.useOcclusionCulling = false;
        backgroundCamera.allowHDR = false;
        backgroundCamera.allowMSAA = false;
    }

    private void LateUpdate()
    {
        // 16:9 영역이 화면 전체면 지울 띠가 없으므로 배경 카메라를 끈다 (빈 카메라 렌더 비용 절약)
        if (null == backgroundCamera)
        {
            return;
        }
        Rect area = Letterbox.NormalizedRect;
        bool needsBars = area.width < 1f || area.height < 1f;
        if (backgroundCamera.enabled != needsBars)
        {
            backgroundCamera.enabled = needsBars;
        }
    }

    private void ApplyMasterVolume()
    {
        AudioListener.volume = GameSettings.GetEffectiveVolume(AudioChannel.Master);
    }

    private void HandleScreenModeChanged(int _index)
    {
        ScreenModes.Apply(_index);
    }

    private void HandleVolumeChanged(AudioChannel _channel)
    {
        if (AudioChannel.Master == _channel)
        {
            ApplyMasterVolume();
        }
    }

    private void HandleSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        AttachLetterbox();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
}
