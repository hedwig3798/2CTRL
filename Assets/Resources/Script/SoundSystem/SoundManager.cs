using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 효과음 / 루프 재생을 맡는 상주 오브젝트 (첫 씬 로드 전에 자동 생성, DontDestroyOnLoad)
/// - AudioListener 를 이 오브젝트가 가진다. 씬의 다른 리스너는 씬 로드 때 끈다 (경고 1회)
/// - SoundLibrary(Resources/Data/Sound/SoundLibrary) 가 없으면 경고 1회 후 무음으로 동작한다.
/// - 음량 : source.volume = GameSettings.GetChannelVolume(채널) × event.volume × 그룹 음량(dB → 선형)
///   (Master 는 SettingsApplier 가 AudioListener.volume 으로 처리하므로 곱하지 않는다)
/// - 패닝 : Left LEFT_PAN / Right RIGHT_PAN / 공유 CENTER_PAN, spatialBlend 0
/// - 일시정지 : GamePause.OnPauseChanged → AudioListener.pause. ignorePause 이벤트는 ignoreListenerPause
/// - 제한 : SoundThrottle 로 (ID, 진영) 별 최소 간격 / 동시 재생 수. 시각은 Time.unscaledTime
/// - 씬 로드 때 버튼에 UIButtonSound 를 자동으로 붙인다 (SelectPanel 은 LVL_SELECT)
/// - 전투 이벤트 연결은 같은 오브젝트의 CombatSoundBinder, 웨이브 / 차원 선택은 FlowSoundBinder 가 한다.
/// - 위치 컬링 : Play(id, side, worldPos) 는 진영 카메라 화면 밖이면 재생하지 않는다 (몬스터 행동음 / 연출음)
///
/// 사용 예)
///   SoundManager.Play(SoundId.UI_CLICK);
///   SoundManager.Play(SoundId.PLR_HIT, PlayerSide.Left);
///   int loop = SoundManager.StartLoop(SoundId.WPN_FLAME_LOOP, PlayerSide.Right);
///   SoundManager.StopLoop(loop);
/// </summary>
[DisallowMultipleComponent]
public class SoundManager
    : MonoBehaviour
{
    private const string OBJECT_NAME = "[SoundManager]";

    public const float LEFT_PAN = -0.45f;
    public const float RIGHT_PAN = 0.45f;
    public const float CENTER_PAN = 0f;

    /// <summary>
    /// StartLoop 실패 시 돌려주는 핸들
    /// </summary>
    public const int INVALID_HANDLE = 0;

    private static SoundManager instance;
    private static bool hasWarnedMissingLibrary;

    /// <summary>
    /// AudioSource 하나와 그 재생 상태
    /// </summary>
    private class Voice
    {
        public AudioSource source;
        public SoundEvent soundEvent;
        public int throttleKey;
        // event.volume × 그룹 음량 (채널 음량은 곱하지 않은 값)
        public float baseVolume;
        // 원샷이 끝나는 시각 (ignorePause 면 unscaledTime, 아니면 pausableTime 기준)
        public float endTime;
        public bool ignorePause;
        public bool isActive;
        // 루프 핸들 (원샷은 INVALID_HANDLE)
        public int handle;
    }

    private SoundLibrary library;
    private AudioListener listener;
    private readonly SoundThrottle throttle = new SoundThrottle();
    private Voice[] oneShots = new Voice[0];
    private Voice[] loops = new Voice[0];
    private int nextLoopHandle = 1;

    // AudioListener.pause 동안 멈추는 시계 (일시정지되는 원샷의 끝 시각 계산용)
    private float pausableTime;

    // 이벤트별 직전 클립 인덱스 (key = (int)SoundId)
    private readonly Dictionary<int, int> lastClipIndex = new Dictionary<int, int>();
    // 이벤트별 재생 기록 (key = (int)SoundId). 에디터 / Development Build 에서만 채운다 (릴리스는 항상 비어 있음)
    private readonly Dictionary<int, SoundStats> stats = new Dictionary<int, SoundStats>();
    // Play / StartLoop 방식이 맞지 않아 경고한 이벤트 (key = (int)SoundId)
    private readonly HashSet<int> warnedModeMismatch = new HashSet<int>();

    public static SoundManager Instance => instance;
    public static bool HasInstance => null != instance;

    /// <summary>
    /// 읽어 온 라이브러리 (없으면 null, 무음)
    /// </summary>
    public SoundLibrary Library => library;

    /// <summary>
    /// 지금 재생 중인 원샷 수
    /// </summary>
    public int ActiveOneShotCount => CountActive(oneShots);

    /// <summary>
    /// 지금 재생 중인 루프 수
    /// </summary>
    public int ActiveLoopCount => CountActive(loops);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (null != instance)
        {
            return;
        }

        GameObject go = new GameObject(OBJECT_NAME);
        DontDestroyOnLoad(go);
        go.AddComponent<SoundManager>();
    }

    #region Public API

    /// <summary>
    /// 원샷 재생 (_side 가 null 이면 가운데, 제한은 양쪽 공유)
    /// </summary>
    public static void Play(SoundId _id, PlayerSide? _side = null)
    {
        Play(_id, _side, 1f);
    }

    /// <summary>
    /// 원샷 재생. 랜덤 피치에 _pitchScale 을 곱한다 (경험치 콤보 등)
    /// </summary>
    public static void Play(SoundId _id, PlayerSide? _side, float _pitchScale)
    {
        if (null == instance)
        {
            return;
        }
        instance.PlayInternal(_id, _side, _pitchScale);
    }

    /// <summary>
    /// 위치가 있는 원샷 재생 (몬스터 행동음 / 연출음용)
    /// - _side 진영 카메라 화면(뷰포트 + SoundLibrary.CullViewportMargin) 밖이면 재생하지 않는다 (통계 culled)
    /// - _side 가 null 이거나 진영 카메라를 못 찾으면 컬링하지 않고 재생한다
    /// </summary>
    public static void Play(SoundId _id, PlayerSide? _side, Vector3 _worldPos)
    {
        if (null == instance || SoundId.None == _id)
        {
            return;
        }
        if (false == instance.IsOnScreen(_side, _worldPos))
        {
            instance.AddStat(_id, StatKind.Culled);
            return;
        }
        instance.PlayInternal(_id, _side, 1f);
    }

    /// <summary>
    /// 루프 재생 시작. 실패하면(이벤트 없음 / 제한 / 소스 부족) INVALID_HANDLE
    /// 씬이 Single 로 바뀌면 모든 루프가 멈추므로 핸들을 버린다.
    /// </summary>
    public static int StartLoop(SoundId _id, PlayerSide? _side = null)
    {
        if (null == instance)
        {
            return INVALID_HANDLE;
        }
        return instance.StartLoopInternal(_id, _side);
    }

    /// <summary>
    /// 루프 정지 (이미 멈췄거나 잘못된 핸들이면 아무것도 하지 않음)
    /// </summary>
    public static void StopLoop(int _handle)
    {
        if (null == instance || INVALID_HANDLE == _handle)
        {
            return;
        }
        instance.StopLoopInternal(_handle);
    }

    /// <summary>
    /// 모든 루프 정지
    /// </summary>
    public static void StopAllLoops()
    {
        if (null == instance)
        {
            return;
        }
        Voice[] voices = instance.loops;
        for (int i = 0; i < voices.Length; ++i)
        {
            if (true == voices[i].isActive)
            {
                instance.ReleaseVoice(voices[i]);
            }
        }
    }

    /// <summary>
    /// 이벤트 하나의 재생 기록 (없으면 0)
    /// </summary>
    public static SoundStats GetStats(SoundId _id)
    {
        if (null == instance)
        {
            return default;
        }
        instance.stats.TryGetValue((int)_id, out SoundStats s);
        return s;
    }

    /// <summary>
    /// 기록이 있는 모든 이벤트를 _result 에 채운다 (ID 순)
    /// </summary>
    public static void GetAllStats(List<KeyValuePair<SoundId, SoundStats>> _result)
    {
        if (null == _result)
        {
            return;
        }
        _result.Clear();
        if (null == instance)
        {
            return;
        }
        foreach (KeyValuePair<int, SoundStats> pair in instance.stats)
        {
            _result.Add(new KeyValuePair<SoundId, SoundStats>((SoundId)pair.Key, pair.Value));
        }
        _result.Sort((a, b) => ((int)a.Key).CompareTo((int)b.Key));
    }

    /// <summary>
    /// 재생 기록 초기화
    /// </summary>
    public static void ResetStats()
    {
        if (null == instance)
        {
            return;
        }
        instance.stats.Clear();
    }

    /// <summary>
    /// 재생 기록을 표 형태 문자열로 (개발용, unity command script eval 로 읽기)
    /// </summary>
    public static string BuildStatsReport()
    {
        if (null == instance)
        {
            return "[SoundManager] 없음";
        }

        var list = new List<KeyValuePair<SoundId, SoundStats>>();
        GetAllStats(list);

        var sb = new StringBuilder();
        sb.Append("library=").Append(null != instance.library ? instance.library.name : "(없음)")
          .Append(" oneShot=").Append(instance.ActiveOneShotCount).Append('/').Append(instance.oneShots.Length)
          .Append(" loop=").Append(instance.ActiveLoopCount).Append('/').Append(instance.loops.Length)
          .Append(" paused=").Append(AudioListener.pause)
          .AppendLine();
        sb.AppendLine("id\tplayed\tthrottled\tnoVoice\tmissing\tculled");
        foreach (KeyValuePair<SoundId, SoundStats> pair in list)
        {
            sb.Append(pair.Key).Append('\t')
              .Append(pair.Value.played).Append('\t')
              .Append(pair.Value.throttled).Append('\t')
              .Append(pair.Value.noVoice).Append('\t')
              .Append(pair.Value.missing).Append('\t')
              .Append(pair.Value.culled).AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// 진영의 패닝 값 (null 이면 가운데)
    /// </summary>
    public static float GetPan(PlayerSide? _side)
    {
        if (false == _side.HasValue)
        {
            return CENTER_PAN;
        }
        return PlayerSide.Left == _side.Value ? LEFT_PAN : RIGHT_PAN;
    }

    #endregion

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // DontDestroyOnLoad 는 루트 오브젝트만 가능
        transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);

        listener = GetComponent<AudioListener>();
        if (null == listener)
        {
            listener = gameObject.AddComponent<AudioListener>();
        }

        library = Resources.Load<SoundLibrary>(SoundLibrary.RESOURCE_PATH);
        if (null != library)
        {
            // 도메인 리로드를 끈 경우 이전 플레이의 캐시가 남아 있을 수 있다
            library.InvalidateCache();
        }
        else if (false == hasWarnedMissingLibrary)
        {
            hasWarnedMissingLibrary = true;
            Debug.LogWarning($"[SoundManager] SoundLibrary 를 찾지 못했습니다. (Resources/{SoundLibrary.RESOURCE_PATH}) 소리 없이 동작합니다.");
        }

        CreateVoices();
        AudioListener.pause = GamePause.IsPaused;

        if (null == GetComponent<CombatSoundBinder>())
        {
            gameObject.AddComponent<CombatSoundBinder>();
        }
        if (null == GetComponent<FlowSoundBinder>())
        {
            gameObject.AddComponent<FlowSoundBinder>();
        }
        if (null == GetComponent<BgmDirector>())
        {
            gameObject.AddComponent<BgmDirector>();
        }
    }

    private void OnEnable()
    {
        if (this != instance)
        {
            return;
        }

        GamePause.OnPauseChanged += HandlePauseChanged;
        GameSettings.OnVolumeChanged += HandleVolumeChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        GamePause.OnPauseChanged -= HandlePauseChanged;
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

    private void Update()
    {
        if (false == AudioListener.pause)
        {
            pausableTime += Time.unscaledDeltaTime;
        }

        float unscaledNow = Time.unscaledTime;
        for (int i = 0; i < oneShots.Length; ++i)
        {
            Voice v = oneShots[i];
            if (false == v.isActive)
            {
                continue;
            }
            float now = true == v.ignorePause ? unscaledNow : pausableTime;
            if (now >= v.endTime)
            {
                ReleaseVoice(v);
            }
        }
    }

    #region Voices

    private void CreateVoices()
    {
        int oneShotCount = null != library ? library.OneShotVoices : 0;
        int loopCount = null != library ? library.LoopVoices : 0;

        oneShots = new Voice[oneShotCount];
        for (int i = 0; i < oneShotCount; ++i)
        {
            oneShots[i] = CreateVoice();
        }

        loops = new Voice[loopCount];
        for (int i = 0; i < loopCount; ++i)
        {
            loops[i] = CreateVoice();
        }
    }

    private Voice CreateVoice()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = false;
        source.dopplerLevel = 0f;
        return new Voice { source = source, handle = INVALID_HANDLE };
    }

    /// <summary>
    /// 빈 원샷 소스. 없으면 우선순위가 같거나 낮은(숫자가 큰) 소리 중 가장 먼저 끝나는 것을 멈추고 쓴다. 그것도 없으면 null
    /// </summary>
    private Voice AcquireOneShot(int _priority)
    {
        Voice steal = null;
        float stealRemaining = 0f;
        float unscaledNow = Time.unscaledTime;
        for (int i = 0; i < oneShots.Length; ++i)
        {
            Voice v = oneShots[i];
            if (false == v.isActive)
            {
                return v;
            }
            if (v.soundEvent.Priority < _priority)
            {
                continue;
            }
            // 두 시계(unscaled / pausable) 가 섞여 있으므로 남은 시간으로 비교한다
            float remaining = v.endTime - (true == v.ignorePause ? unscaledNow : pausableTime);
            if (null == steal || remaining < stealRemaining)
            {
                steal = v;
                stealRemaining = remaining;
            }
        }

        if (null != steal)
        {
            ReleaseVoice(steal);
        }
        return steal;
    }

    private Voice AcquireLoop()
    {
        for (int i = 0; i < loops.Length; ++i)
        {
            if (false == loops[i].isActive)
            {
                return loops[i];
            }
        }
        return null;
    }

    /// <summary>
    /// 소스에 이벤트를 넣고 재생한다. 클립이 없으면 false
    /// </summary>
    private bool StartVoice(Voice _voice, SoundEvent _event, int _key, PlayerSide? _side, float _pitchScale, bool _loop)
    {
        int id = (int)_event.Id;
        int last = true == lastClipIndex.TryGetValue(id, out int found) ? found : -1;
        int clipIndex = _event.PickClipIndex(last);
        if (clipIndex < 0)
        {
            return false;
        }
        lastClipIndex[id] = clipIndex;

        AudioClip clip = _event.Clips[clipIndex];
        float pitch = Random.Range(_event.PitchMin, _event.PitchMax) * Mathf.Max(0.01f, _pitchScale);

        AudioSource source = _voice.source;
        source.clip = clip;
        source.loop = _loop;
        source.pitch = pitch;
        source.panStereo = GetPan(_side);
        source.priority = _event.Priority;
        source.ignoreListenerPause = _event.IgnorePause;

        _voice.soundEvent = _event;
        _voice.throttleKey = _key;
        _voice.ignorePause = _event.IgnorePause;
        _voice.baseVolume = _event.Volume * library.GetGroupGainLinear(_event.Group);
        _voice.isActive = true;
        ApplyVolume(_voice);

        float now = true == _voice.ignorePause ? Time.unscaledTime : pausableTime;
        _voice.endTime = now + clip.length / Mathf.Abs(pitch);

        source.Play();
        return true;
    }

    private void ReleaseVoice(Voice _voice)
    {
        if (false == _voice.isActive)
        {
            return;
        }
        _voice.isActive = false;
        _voice.handle = INVALID_HANDLE;
        throttle.Release(_voice.throttleKey);
        if (null != _voice.source)
        {
            _voice.source.Stop();
            _voice.source.clip = null;
        }
        _voice.soundEvent = null;
    }

    private static void ApplyVolume(Voice _voice)
    {
        AudioChannel channel = _voice.soundEvent.Group.ToChannel();
        _voice.source.volume = GameSettings.GetChannelVolume(channel) * _voice.baseVolume;
    }

    private static int CountActive(Voice[] _voices)
    {
        int count = 0;
        for (int i = 0; i < _voices.Length; ++i)
        {
            if (true == _voices[i].isActive)
            {
                ++count;
            }
        }
        return count;
    }

    #endregion

    #region Play

    private void PlayInternal(SoundId _id, PlayerSide? _side, float _pitchScale)
    {
        if (null == library || SoundId.None == _id)
        {
            return;
        }

        if (false == library.TryGetEvent(_id, out SoundEvent e) || false == e.HasClip)
        {
            AddStat(_id, StatKind.Missing);
            return;
        }

        // 루프 이벤트는 StartLoop 로만 재생한다
        if (true == e.Loop)
        {
            WarnModeMismatch(e, true);
            AddStat(_id, StatKind.Missing);
            return;
        }

        int key = SoundThrottle.MakeKey(_id, _side);
        if (false == throttle.TryAcquire(key, Time.unscaledTime, e.MinInterval, e.MaxVoices))
        {
            AddStat(_id, StatKind.Throttled);
            return;
        }

        Voice voice = AcquireOneShot(e.Priority);
        if (null == voice)
        {
            throttle.Cancel(key);
            AddStat(_id, StatKind.NoVoice);
            return;
        }

        if (false == StartVoice(voice, e, key, _side, _pitchScale, false))
        {
            throttle.Cancel(key);
            AddStat(_id, StatKind.Missing);
            return;
        }
        AddStat(_id, StatKind.Played);
    }

    private int StartLoopInternal(SoundId _id, PlayerSide? _side)
    {
        if (null == library || SoundId.None == _id)
        {
            return INVALID_HANDLE;
        }

        if (false == library.TryGetEvent(_id, out SoundEvent e) || false == e.HasClip)
        {
            AddStat(_id, StatKind.Missing);
            return INVALID_HANDLE;
        }

        // 원샷 이벤트는 Play 로만 재생한다
        if (false == e.Loop)
        {
            WarnModeMismatch(e, false);
            AddStat(_id, StatKind.Missing);
            return INVALID_HANDLE;
        }

        int key = SoundThrottle.MakeKey(_id, _side);
        if (false == throttle.TryAcquire(key, Time.unscaledTime, e.MinInterval, e.MaxVoices))
        {
            AddStat(_id, StatKind.Throttled);
            return INVALID_HANDLE;
        }

        Voice voice = AcquireLoop();
        if (null == voice)
        {
            throttle.Cancel(key);
            AddStat(_id, StatKind.NoVoice);
            return INVALID_HANDLE;
        }

        if (false == StartVoice(voice, e, key, _side, 1f, true))
        {
            throttle.Cancel(key);
            AddStat(_id, StatKind.Missing);
            return INVALID_HANDLE;
        }

        int handle = nextLoopHandle;
        nextLoopHandle = int.MaxValue == nextLoopHandle ? 1 : nextLoopHandle + 1;
        voice.handle = handle;
        AddStat(_id, StatKind.Played);
        return handle;
    }

    private void StopLoopInternal(int _handle)
    {
        for (int i = 0; i < loops.Length; ++i)
        {
            Voice v = loops[i];
            if (true == v.isActive && _handle == v.handle)
            {
                ReleaseVoice(v);
                return;
            }
        }
    }

    private enum StatKind
    {
        Played,
        Throttled,
        NoVoice,
        Missing,
        Culled,
    }

    /// <summary>
    /// Play 에 루프 이벤트 / StartLoop 에 원샷 이벤트가 왔을 때 이벤트별로 1회 경고
    /// </summary>
    private void WarnModeMismatch(SoundEvent _event, bool _isLoopEvent)
    {
        if (false == warnedModeMismatch.Add((int)_event.Id))
        {
            return;
        }
        string message = true == _isLoopEvent
            ? "루프 이벤트입니다. Play 대신 StartLoop 를 쓰세요."
            : "원샷 이벤트입니다. StartLoop 대신 Play 를 쓰세요.";
        Debug.LogWarning($"[SoundManager] {_event.Id} ({_event.name}) 는 {message}", _event);
    }

    /// <summary>
    /// 재생 기록 (개발용. 에디터 / Development Build 에서만 센다)
    /// </summary>
    private void AddStat(SoundId _id, StatKind _kind)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int key = (int)_id;
        stats.TryGetValue(key, out SoundStats s);
        switch (_kind)
        {
            case StatKind.Played:
                ++s.played;
                break;
            case StatKind.Throttled:
                ++s.throttled;
                break;
            case StatKind.NoVoice:
                ++s.noVoice;
                break;
            case StatKind.Missing:
                ++s.missing;
                break;
            case StatKind.Culled:
                ++s.culled;
                break;
        }
        stats[key] = s;
#endif
    }

    #endregion

    #region Culling

    private const int SIDE_COUNT = 2;

    // 진영별 카메라 (cullingMask 에 그 진영 몬스터 레이어가 들어간 카메라). 씬 로드 때 비우고 다시 찾는다
    private readonly Camera[] sideCameras = new Camera[SIDE_COUNT];
    private Camera[] cameraBuffer = new Camera[8];
    private float nextCameraSearchTime;

    /// <summary>
    /// _worldPos 가 _side 진영 카메라 화면 안(여유 포함)인지. 판정할 수 없으면 true
    /// </summary>
    private bool IsOnScreen(PlayerSide? _side, Vector3 _worldPos)
    {
        if (false == _side.HasValue || null == library)
        {
            return true;
        }

        Camera cam = GetSideCamera(_side.Value);
        if (null == cam)
        {
            return true;
        }

        Vector3 vp = cam.WorldToViewportPoint(_worldPos);
        float m = library.CullViewportMargin;
        return vp.x >= -m && vp.x <= 1f + m && vp.y >= -m && vp.y <= 1f + m;
    }

    private Camera GetSideCamera(PlayerSide _side)
    {
        int i = (int)_side;
        Camera cam = sideCameras[i];
        if (null != cam && true == cam.isActiveAndEnabled)
        {
            return cam;
        }

        // Camera.allCameras 는 배열을 할당하므로 버퍼에 받고, 못 찾으면 일정 간격으로만 다시 찾는다
        float now = Time.unscaledTime;
        if (now < nextCameraSearchTime)
        {
            return null;
        }
        nextCameraSearchTime = now + (null != library ? library.CameraRetryInterval : 1f);
        FindSideCameras();
        return sideCameras[i];
    }

    /// <summary>
    /// 진영 몬스터 레이어를 그리는 카메라를 찾는다. 그 진영만 그리는 카메라를 먼저, 없으면 양쪽을 다 그리는 카메라
    /// </summary>
    private void FindSideCameras()
    {
        int count = Camera.allCamerasCount;
        if (cameraBuffer.Length < count)
        {
            cameraBuffer = new Camera[Mathf.NextPowerOfTwo(count)];
        }
        count = Camera.GetAllCameras(cameraBuffer);

        for (int s = 0; s < SIDE_COUNT; ++s)
        {
            sideCameras[s] = null;
            int own = SoundLayerSide.GetMonsterLayer((PlayerSide)s);
            int other = SoundLayerSide.GetMonsterLayer(((PlayerSide)s).Opposite());
            if (own < 0)
            {
                continue;
            }

            Camera shared = null;
            for (int c = 0; c < count; ++c)
            {
                Camera cam = cameraBuffer[c];
                if (null == cam || 0 == (cam.cullingMask & (1 << own)))
                {
                    continue;
                }
                if (other < 0 || 0 == (cam.cullingMask & (1 << other)))
                {
                    sideCameras[s] = cam;
                    break;
                }
                if (null == shared)
                {
                    shared = cam;
                }
            }
            if (null == sideCameras[s])
            {
                sideCameras[s] = shared;
            }
        }

        // 파괴된 카메라를 붙잡고 있지 않도록 버퍼를 비운다
        for (int c = 0; c < count; ++c)
        {
            cameraBuffer[c] = null;
        }
    }

    private void ClearSideCameras()
    {
        for (int s = 0; s < SIDE_COUNT; ++s)
        {
            sideCameras[s] = null;
        }
        nextCameraSearchTime = 0f;
    }

    #endregion

    #region Handlers

    private void HandlePauseChanged(bool _paused)
    {
        AudioListener.pause = _paused;
    }

    private void HandleVolumeChanged(AudioChannel _channel)
    {
        // Master 는 AudioListener.volume 이 처리한다
        if (AudioChannel.Master == _channel)
        {
            return;
        }
        RefreshVolumes(oneShots, _channel);
        RefreshVolumes(loops, _channel);
    }

    private static void RefreshVolumes(Voice[] _voices, AudioChannel _channel)
    {
        for (int i = 0; i < _voices.Length; ++i)
        {
            Voice v = _voices[i];
            if (true == v.isActive && _channel == v.soundEvent.Group.ToChannel())
            {
                ApplyVolume(v);
            }
        }
    }

    private void HandleSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        AudioListener.pause = GamePause.IsPaused;
        ClearSideCameras();

        // 씬이 통째로 바뀌면 루프 주인(발사체 등)이 정지를 못 부르고 사라지므로 모두 멈춘다
        // 일시정지로 멈춰 있던 전투 원샷도 다음 씬에서 이어 나오지 않도록 멈춘다 (UI 등 ignorePause 는 그대로)
        if (LoadSceneMode.Single == _mode)
        {
            StopAllLoops();
            StopPausableOneShots();
        }

        DisableOtherListeners();
        AttachButtonSounds();
    }

    /// <summary>
    /// ignorePause 가 아닌 원샷을 모두 멈춘다
    /// </summary>
    private void StopPausableOneShots()
    {
        for (int i = 0; i < oneShots.Length; ++i)
        {
            Voice v = oneShots[i];
            if (true == v.isActive && false == v.ignorePause)
            {
                ReleaseVoice(v);
            }
        }
    }

    /// <summary>
    /// 씬 카메라 등에 남은 AudioListener 를 끈다 (리스너가 둘이면 Unity 경고가 계속 나온다)
    /// </summary>
    private void DisableOtherListeners()
    {
        StringBuilder names = null;
        foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
        {
            if (l == listener || false == l.enabled)
            {
                continue;
            }
            l.enabled = false;
            names ??= new StringBuilder();
            names.Append(0 < names.Length ? ", " : string.Empty).Append(l.name);
        }

        if (null != names)
        {
            Debug.LogWarning($"[SoundManager] 씬의 AudioListener 를 껐습니다. ({names}) 리스너는 SoundManager 가 가지므로 씬에서 제거하세요.");
        }
    }

    /// <summary>
    /// 아직 UIButtonSound 가 없는 버튼 / SelectPanel 에 붙인다 (SelectPanel 은 LVL_SELECT)
    /// </summary>
    private static void AttachButtonSounds()
    {
        foreach (SelectPanel panel in FindObjectsByType<SelectPanel>(FindObjectsInactive.Include))
        {
            if (null == panel.GetComponent<UIButtonSound>())
            {
                panel.gameObject.AddComponent<UIButtonSound>().SetSoundId(SoundId.LVL_SELECT);
            }
        }

        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (null == button.GetComponent<UIButtonSound>())
            {
                button.gameObject.AddComponent<UIButtonSound>();
            }
        }
    }

    #endregion

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        hasWarnedMissingLibrary = false;
    }
}
