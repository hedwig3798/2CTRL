using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BGM 재생 (docs/sound-design.md 5장)
/// - SoundManager 가 자동으로 붙인다. 자식 오브젝트의 자체 AudioSource 를 쓴다 (SoundManager 원샷/루프 풀과 별개)
/// - 씬 이름 → 모드 / 클립 : SoundLibrary.TryGetBgmScene. 없는 씬은 None (페이드아웃)
///   Single : 같은 클립이면 유지, 다르면 sceneCrossfadeSeconds 크로스페이드
///   Combat : 베이스 4종(LOW / MID / HIGH / RUSH) + 진영별 차원 레이어 2개를 같은 dspTime 에 PlayScheduled (모두 loop)
///            현재 강도 베이스만 들리고 나머지는 볼륨 0 (샘플 단위 동기 유지)
/// - 강도 : Spawner.OnWaveChanged (진영마다 있으므로 같은 웨이브 번호는 한 번만) → SoundLibrary.GetBgmBaseForWave
///   다음 마디 경계(재생 시작 dsp 시각 기준, 4/4)에서 BgmCrossfadeBars 마디 동안 크로스페이드
/// - 일시정지 : ignoreListenerPause 로 계속 재생, BgmPauseDb + 로우패스 BgmPauseCutoff 로 pauseTransitionSeconds(unscaled) 전환
/// - 게임 종료(GameOverManager.OnGameOver) : gameOverFadeSeconds 페이드아웃 후 정지
/// - 음량 = GetChannelVolume(BGM) × event.volume × 그룹 음량 × 페이드(강도 가중 포함) × 일시정지 감쇠 (Master 미포함)
/// </summary>
[DisallowMultipleComponent]
public class BgmDirector
    : MonoBehaviour
{
    private const float OPEN_CUTOFF = 22000f;

    private static readonly SoundId[] BASE_IDS =
    {
        SoundId.BGM_BASE_LOW,
        SoundId.BGM_BASE_MID,
        SoundId.BGM_BASE_HIGH,
        SoundId.BGM_RUSH,
    };

    [Tooltip("씬 전환 시 BGM 크로스페이드 / 페이드아웃 (초)")]
    [Min(0f)]
    [SerializeField]
    private float sceneCrossfadeSeconds = 1f;

    [Tooltip("게임 종료 시 페이드아웃 (초)")]
    [Min(0f)]
    [SerializeField]
    private float gameOverFadeSeconds = 0.5f;

    [Tooltip("일시정지 감쇠 / 필터 전환 (초, unscaled)")]
    [Min(0f)]
    [SerializeField]
    private float pauseTransitionSeconds = 0.2f;

    [Tooltip("전투 BGM PlayScheduled 여유 시간 (초)")]
    [Min(0.01f)]
    [SerializeField]
    private float scheduleLeadSeconds = 0.2f;

    [Tooltip("전투 → 전투(재시작) 시 이전 전투 BGM 페이드아웃 (초, dsp)")]
    [Min(0f)]
    [SerializeField]
    private float combatRestartFadeSeconds = 0.3f;

    /// <summary>
    /// BGM 소스 하나 (가중치 포락선은 dspTime 기준)
    /// </summary>
    private class BgmVoice
    {
        public string label;
        public AudioSource source;
        public AudioLowPassFilter filter;
        public SoundId id = SoundId.None;
        public SoundEvent evt;
        public float from;
        public float to;
        public double start;
        public double duration;
        public bool stopWhenSilent;

        public bool IsActive => null != evt;

        public float Evaluate(double _time)
        {
            if (duration <= 0d || _time >= start + duration)
            {
                return to;
            }
            if (_time <= start)
            {
                return from;
            }
            // 등전력 곡선 (올라갈 때 sin, 내려갈 때 cos)
            float t = (float)((_time - start) / duration) * Mathf.PI * 0.5f;
            if (to >= from)
            {
                return from + (to - from) * Mathf.Sin(t);
            }
            return to + (from - to) * Mathf.Cos(t);
        }

        public void Set(float _weight)
        {
            from = _weight;
            to = _weight;
            start = 0d;
            duration = 0d;
        }

        public void FadeTo(float _target, double _start, double _duration, double _now)
        {
            from = Evaluate(_now);
            to = _target;
            start = _start;
            duration = Math.Max(0d, _duration);
        }
    }

    private static BgmDirector instance;

    private SoundLibrary library;
    private readonly BgmVoice[] singleVoices = new BgmVoice[2];
    private readonly BgmVoice[] baseVoices = new BgmVoice[4];
    // 0 : 왼쪽 진영, 1 : 오른쪽 진영
    private readonly BgmVoice[] layerVoices = new BgmVoice[2];
    private BgmVoice[] allVoices = new BgmVoice[0];

    private BgmMode mode = BgmMode.None;
    private SoundId singleId = SoundId.None;
    private int activeSingle = 0;

    private bool combatPending = false;
    private bool combatRunning = false;
    private double combatStartDsp = 0d;
    private int intensityIndex = 0;
    private bool layersResolved = false;
    private SoundId pendingLeftLayer = SoundId.None;
    private SoundId pendingRightLayer = SoundId.None;

    // 같은 씬을 sceneLoaded 와 Start 에서 두 번 적용하지 않도록
    private bool hasAppliedScene = false;
    private Scene appliedScene;

    private bool pauseTarget = false;
    private float pauseBlend = 0f;
    private float channelVolume = 1f;

    private Spawner[] spawners = new Spawner[0];
    private int lastWaveIndex = -1;
    private GameOverManager gameOver;

    public static BgmMode CurrentMode => null != instance ? instance.mode : BgmMode.None;

    /// <summary>
    /// 재생 중인 BGM 을 페이드 없이 즉시 멈춘다 (게임 오버 연출 시작 등)
    /// </summary>
    public static void StopImmediately()
    {
        if (null == instance)
        {
            return;
        }
        instance.StopAll(0f);
    }
    public static SoundId CurrentIntensity =>
        null != instance && BgmMode.Combat == instance.mode ? BASE_IDS[instance.intensityIndex] : SoundId.None;

    #region Unity

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        instance = null;
    }

    private void Start()
    {
        // 첫 씬의 sceneLoaded 를 놓친 경우 현재 씬을 한 번 적용한다 (이미 적용했으면 무시)
        if (this != instance)
        {
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        if (true == active.isLoaded)
        {
            ApplyScene(active);
        }
    }

    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Destroy(this);
            return;
        }
        instance = this;

        SoundManager manager = GetComponent<SoundManager>();
        library = null != manager ? manager.Library : null;
        CreateVoices();
        channelVolume = GameSettings.GetChannelVolume(AudioChannel.BGM);
        pauseTarget = GamePause.IsPaused;
        pauseBlend = true == pauseTarget ? 1f : 0f;
    }

    private void OnEnable()
    {
        if (this != instance)
        {
            return;
        }
        SceneManager.sceneLoaded += HandleSceneLoaded;
        GamePause.OnPauseChanged += HandlePauseChanged;
        GameSettings.OnVolumeChanged += HandleVolumeChanged;
        channelVolume = GameSettings.GetChannelVolume(AudioChannel.BGM);
        pauseTarget = GamePause.IsPaused;
        if (BgmMode.Combat == mode)
        {
            BindScene();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        GamePause.OnPauseChanged -= HandlePauseChanged;
        GameSettings.OnVolumeChanged -= HandleVolumeChanged;
        UnbindScene();
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
        float step = pauseTransitionSeconds <= 0f ? 1f : Time.unscaledDeltaTime / pauseTransitionSeconds;
        pauseBlend = Mathf.MoveTowards(pauseBlend, true == pauseTarget ? 1f : 0f, step);

        float pauseDb = null != library ? library.BgmPauseDb : 0f;
        float pauseCut = null != library ? library.BgmPauseCutoff : OPEN_CUTOFF;
        float pauseGain = Mathf.Lerp(1f, SoundLibrary.DbToLinear(pauseDb), pauseBlend);
        float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(OPEN_CUTOFF), Mathf.Log(pauseCut), pauseBlend));

        double now = AudioSettings.dspTime;
        for (int i = 0; i < allVoices.Length; ++i)
        {
            UpdateVoice(allVoices[i], now, pauseGain, cutoff);
        }

        // 씬의 Start(DimensionApplier 의 차원 확정)가 끝나고, 이전 전투 소스가 멈추고, 클립이 로드된 뒤 시작한다
        if (true == combatPending)
        {
            TryStartCombat();
        }
    }

    #endregion

    #region Voices

    private void CreateVoices()
    {
        singleVoices[0] = CreateVoice("BGM_Single_A");
        singleVoices[1] = CreateVoice("BGM_Single_B");
        for (int i = 0; i < baseVoices.Length; ++i)
        {
            baseVoices[i] = CreateVoice("BGM_" + BASE_IDS[i]);
        }
        layerVoices[0] = CreateVoice("BGM_Layer_Left");
        layerVoices[1] = CreateVoice("BGM_Layer_Right");

        allVoices = new BgmVoice[singleVoices.Length + baseVoices.Length + layerVoices.Length];
        int n = 0;
        foreach (BgmVoice v in singleVoices) { allVoices[n++] = v; }
        foreach (BgmVoice v in baseVoices) { allVoices[n++] = v; }
        foreach (BgmVoice v in layerVoices) { allVoices[n++] = v; }
    }

    private BgmVoice CreateVoice(string _label)
    {
        var go = new GameObject(_label);
        go.transform.SetParent(transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.priority = 0;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;

        AudioLowPassFilter filter = go.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = OPEN_CUTOFF;
        filter.enabled = false;

        return new BgmVoice { label = _label, source = source, filter = filter };
    }

    private void UpdateVoice(BgmVoice _voice, double _now, float _pauseGain, float _cutoff)
    {
        if (false == _voice.IsActive)
        {
            return;
        }

        float weight = _voice.Evaluate(_now);
        if (true == _voice.stopWhenSilent && _now >= _voice.start + _voice.duration && weight <= 0f)
        {
            StopVoice(_voice);
            return;
        }

        float group = null != library ? library.GetGroupGainLinear(_voice.evt.Group) : 1f;
        _voice.source.volume = Mathf.Clamp01(channelVolume * _voice.evt.Volume * group * weight * _pauseGain);

        bool filtered = 0f < pauseBlend;
        if (_voice.filter.enabled != filtered)
        {
            _voice.filter.enabled = filtered;
        }
        _voice.filter.cutoffFrequency = true == filtered ? _cutoff : OPEN_CUTOFF;
    }

    /// <summary>
    /// 소스에 이벤트 클립을 싣는다 (재생은 호출 측). 이벤트나 클립이 없으면 false
    /// </summary>
    private bool LoadVoice(BgmVoice _voice, SoundId _id, float _pan)
    {
        StopVoice(_voice);
        if (SoundId.None == _id || null == library || false == library.TryGetEvent(_id, out SoundEvent evt) || null == evt)
        {
            return false;
        }
        AudioClip clip = FirstClip(evt);
        if (null == clip)
        {
            return false;
        }

        _voice.id = _id;
        _voice.evt = evt;
        _voice.stopWhenSilent = false;
        _voice.Set(0f);
        _voice.source.clip = clip;
        _voice.source.loop = true;
        _voice.source.panStereo = _pan;
        _voice.source.volume = 0f;
        return true;
    }

    private static AudioClip FirstClip(SoundEvent _event)
    {
        AudioClip[] clips = _event.Clips;
        if (null == clips)
        {
            return null;
        }
        for (int i = 0; i < clips.Length; ++i)
        {
            if (null != clips[i])
            {
                return clips[i];
            }
        }
        return null;
    }

    private static void StopVoice(BgmVoice _voice)
    {
        _voice.source.Stop();
        _voice.source.clip = null;
        _voice.source.volume = 0f;
        _voice.id = SoundId.None;
        _voice.evt = null;
        _voice.stopWhenSilent = false;
        _voice.Set(0f);
    }

    private static void FadeOut(BgmVoice[] _voices, double _seconds)
    {
        double now = AudioSettings.dspTime;
        for (int i = 0; i < _voices.Length; ++i)
        {
            BgmVoice v = _voices[i];
            if (true == v.IsActive)
            {
                v.FadeTo(0f, now, _seconds, now);
                v.stopWhenSilent = true;
            }
        }
    }

    #endregion

    #region Mode

    private void HandleSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        if (LoadSceneMode.Single != _mode)
        {
            return;
        }
        ApplyScene(_scene);
    }

    private void ApplyScene(Scene _scene)
    {
        if (true == hasAppliedScene && _scene == appliedScene)
        {
            return;
        }
        hasAppliedScene = true;
        appliedScene = _scene;

        UnbindScene();
        lastWaveIndex = -1;
        pauseTarget = GamePause.IsPaused;

        BgmMode next = BgmMode.None;
        SoundId nextSingle = SoundId.None;
        if (null != library && true == library.TryGetBgmScene(_scene.name, out BgmSceneEntry entry))
        {
            next = entry.mode;
            nextSingle = entry.single;
        }

        switch (next)
        {
            case BgmMode.Single:
                PlaySingle(nextSingle);
                break;
            case BgmMode.Combat:
                RequestCombat();
                break;
            default:
                StopAll(sceneCrossfadeSeconds);
                break;
        }
    }

    private void PlaySingle(SoundId _id)
    {
        BgmVoice current = singleVoices[activeSingle];
        if (BgmMode.Single == mode && _id == singleId && true == current.IsActive && false == current.stopWhenSilent)
        {
            return;
        }

        combatPending = false;
        combatRunning = false;
        FadeOut(baseVoices, sceneCrossfadeSeconds);
        FadeOut(layerVoices, sceneCrossfadeSeconds);
        FadeOut(singleVoices, sceneCrossfadeSeconds);

        double now = AudioSettings.dspTime;

        // 같은 클립이 아직 페이드아웃 중이면 끊지 않고 현재 가중치에서 다시 올린다
        for (int i = 0; i < singleVoices.Length; ++i)
        {
            BgmVoice fading = singleVoices[i];
            if (true == fading.IsActive && _id == fading.id)
            {
                fading.FadeTo(1f, now, sceneCrossfadeSeconds, now);
                fading.stopWhenSilent = false;
                activeSingle = i;
                mode = BgmMode.Single;
                singleId = _id;
                return;
            }
        }

        // 빈 소스 우선, 둘 다 쓰는 중이면 더 작게 들리는 쪽을 쓴다
        int nextIndex = 1 - activeSingle;
        BgmVoice other = singleVoices[activeSingle];
        if (true == singleVoices[nextIndex].IsActive
            && (false == other.IsActive || other.Evaluate(now) < singleVoices[nextIndex].Evaluate(now)))
        {
            nextIndex = activeSingle;
        }
        BgmVoice voice = singleVoices[nextIndex];
        if (false == LoadVoice(voice, _id, 0f))
        {
            mode = BgmMode.None;
            singleId = SoundId.None;
            return;
        }

        voice.FadeTo(1f, now, sceneCrossfadeSeconds, now);
        voice.source.Play();
        activeSingle = nextIndex;
        mode = BgmMode.Single;
        singleId = _id;
    }

    private void RequestCombat()
    {
        FadeOut(singleVoices, sceneCrossfadeSeconds);
        // 전투 → 전투(재시작)는 이전 전투 소스를 짧게 페이드아웃하고, 멈춘 뒤 처음부터 다시 맞춘다
        FadeOut(baseVoices, combatRestartFadeSeconds);
        FadeOut(layerVoices, combatRestartFadeSeconds);

        mode = BgmMode.Combat;
        singleId = SoundId.None;
        combatRunning = false;
        combatPending = true;
        layersResolved = false;
        intensityIndex = 0;
        BindScene();
    }

    /// <summary>
    /// 시작 조건(이전 전투 소스 정지, 클립 로드)이 맞으면 전투 BGM 을 같은 dsp 시각에 예약한다. 아니면 다음 프레임에 다시 본다
    /// </summary>
    private void TryStartCombat()
    {
        if (null == library)
        {
            combatPending = false;
            return;
        }
        if (true == AnyActive(baseVoices) || true == AnyActive(layerVoices))
        {
            return;
        }
        if (false == layersResolved)
        {
            pendingLeftLayer = ResolveLayer(PlayerSide.Left);
            pendingRightLayer = ResolveLayer(PlayerSide.Right);
            layersResolved = true;
        }

        bool ready = true;
        for (int i = 0; i < BASE_IDS.Length; ++i)
        {
            ready &= IsClipReady(BASE_IDS[i]);
        }
        ready &= IsClipReady(pendingLeftLayer);
        ready &= IsClipReady(pendingRightLayer);
        if (false == ready)
        {
            return;
        }

        combatPending = false;
        double startDsp = AudioSettings.dspTime + scheduleLeadSeconds;
        combatStartDsp = startDsp;

        for (int i = 0; i < baseVoices.Length; ++i)
        {
            if (true == LoadVoice(baseVoices[i], BASE_IDS[i], 0f))
            {
                baseVoices[i].Set(i == intensityIndex ? 1f : 0f);
                baseVoices[i].source.PlayScheduled(startDsp);
            }
        }

        float pan = library.BgmLayerPan;
        StartLayer(layerVoices[0], pendingLeftLayer, -pan, startDsp);
        StartLayer(layerVoices[1], pendingRightLayer, pan, startDsp);
        combatRunning = true;
    }

    private static bool AnyActive(BgmVoice[] _voices)
    {
        for (int i = 0; i < _voices.Length; ++i)
        {
            if (true == _voices[i].IsActive)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 이벤트 클립이 재생 가능하게 로드됐는지. 안 됐으면 로드를 시작한다 (클립이 없거나 로드 실패면 기다리지 않는다)
    /// </summary>
    private bool IsClipReady(SoundId _id)
    {
        if (SoundId.None == _id || false == library.TryGetEvent(_id, out SoundEvent evt) || null == evt)
        {
            return true;
        }
        AudioClip clip = FirstClip(evt);
        if (null == clip)
        {
            return true;
        }
        switch (clip.loadState)
        {
            case AudioDataLoadState.Loaded:
            case AudioDataLoadState.Failed:
                return true;
            case AudioDataLoadState.Unloaded:
                clip.LoadAudioData();
                return false;
            default:
                return false;
        }
    }

    private void StartLayer(BgmVoice _voice, SoundId _id, float _pan, double _startDsp)
    {
        if (true == LoadVoice(_voice, _id, _pan))
        {
            _voice.Set(1f);
            _voice.source.PlayScheduled(_startDsp);
        }
    }

    /// <summary>
    /// 진영의 차원 → 레이어 ID (DimensionApplier 의 적용 차원 우선, 없으면 DimensionSelection). 못 찾으면 None
    /// </summary>
    private SoundId ResolveLayer(PlayerSide _side)
    {
        DimensionData data = null;
        DimensionApplier[] appliers = FindObjectsByType<DimensionApplier>(FindObjectsInactive.Include);
        for (int i = 0; i < appliers.Length; ++i)
        {
            if (_side == appliers[i].Side && null != appliers[i].Dimension)
            {
                data = appliers[i].Dimension;
                break;
            }
        }
        if (null == data)
        {
            data = DimensionSelection.Get(_side);
        }
        if (null == data || false == library.TryGetDimension(data.name, out DimensionSoundEntry entry))
        {
            return SoundId.None;
        }
        return entry.layer;
    }

    private void StopAll(float _seconds)
    {
        combatPending = false;
        combatRunning = false;
        FadeOut(allVoices, _seconds);
        mode = BgmMode.None;
        singleId = SoundId.None;
    }

    #endregion

    #region Intensity

    private void SetIntensity(int _index)
    {
        if (_index == intensityIndex)
        {
            return;
        }
        intensityIndex = _index;
        if (false == combatRunning || null == library)
        {
            // 시작 전이면 StartCombat 이 이 강도로 시작한다
            return;
        }

        double now = AudioSettings.dspTime;
        double bar = 240d / library.BgmBpm;
        double boundary = combatStartDsp;
        if (now > combatStartDsp)
        {
            boundary = combatStartDsp + Math.Ceiling((now - combatStartDsp) / bar) * bar;
        }
        double duration = library.BgmCrossfadeBars * bar;

        for (int i = 0; i < baseVoices.Length; ++i)
        {
            if (true == baseVoices[i].IsActive)
            {
                baseVoices[i].FadeTo(i == _index ? 1f : 0f, boundary, duration, now);
            }
        }
    }

    private static int IndexOfBase(SoundId _id)
    {
        int index = Array.IndexOf(BASE_IDS, _id);
        return index < 0 ? 0 : index;
    }

    #endregion

    #region Scene Binding

    private void BindScene()
    {
        UnbindScene();
        spawners = FindObjectsByType<Spawner>(FindObjectsInactive.Include);
        foreach (Spawner s in spawners)
        {
            s.OnWaveChanged -= HandleWaveChanged;
            s.OnWaveChanged += HandleWaveChanged;
        }
        gameOver = FindAnyObjectByType<GameOverManager>(FindObjectsInactive.Include);
        if (null != gameOver)
        {
            gameOver.OnGameOver -= HandleGameOver;
            gameOver.OnGameOver += HandleGameOver;
        }
    }

    private void UnbindScene()
    {
        foreach (Spawner s in spawners)
        {
            if (null != s)
            {
                s.OnWaveChanged -= HandleWaveChanged;
            }
        }
        spawners = new Spawner[0];
        if (null != gameOver)
        {
            gameOver.OnGameOver -= HandleGameOver;
        }
        gameOver = null;
    }

    private void HandleWaveChanged(Spawner _spawner, int _index)
    {
        if (_index <= lastWaveIndex)
        {
            return;
        }
        lastWaveIndex = _index;
        if (BgmMode.Combat != mode || null == library)
        {
            return;
        }
        SetIntensity(IndexOfBase(library.GetBgmBaseForWave(_index)));
    }

    private void HandleGameOver()
    {
        if (BgmMode.Combat == mode)
        {
            StopAll(gameOverFadeSeconds);
        }
    }

    private void HandlePauseChanged(bool _paused)
    {
        pauseTarget = _paused;
    }

    private void HandleVolumeChanged(AudioChannel _channel)
    {
        if (AudioChannel.BGM == _channel)
        {
            channelVolume = GameSettings.GetChannelVolume(AudioChannel.BGM);
        }
    }

    #endregion

    #region Debug

    /// <summary>
    /// 개발용 상태 문자열 (모드, 강도, 소스별 클립·볼륨·timeSamples·isPlaying·패닝)
    /// </summary>
    public static string BuildDebugState()
    {
        if (null == instance)
        {
            return "[BgmDirector] 없음";
        }
        BgmDirector d = instance;
        var sb = new StringBuilder(512);
        sb.Append("[BgmDirector] mode=").Append(d.mode)
          .Append(" single=").Append(d.singleId)
          .Append(" intensity=").Append(BASE_IDS[d.intensityIndex])
          .Append(" pending=").Append(d.combatPending)
          .Append(" running=").Append(d.combatRunning)
          .Append(" startDsp=").Append(d.combatStartDsp.ToString("F3"))
          .Append(" dsp=").Append(AudioSettings.dspTime.ToString("F3"))
          .Append(" pauseBlend=").Append(d.pauseBlend.ToString("F2"))
          .Append(" channel=").Append(d.channelVolume.ToString("F2"))
          .Append(" layerPan=").Append(null != d.library ? d.library.BgmLayerPan.ToString("F2") : "-")
          .AppendLine();
        for (int i = 0; i < d.allVoices.Length; ++i)
        {
            BgmVoice v = d.allVoices[i];
            AudioSource s = v.source;
            sb.Append("  ").Append(v.label)
              .Append(" id=").Append(v.id)
              .Append(" clip=").Append(null != s.clip ? s.clip.name : "-")
              .Append(" vol=").Append(s.volume.ToString("F3"))
              .Append(" samples=").Append(null != s.clip ? s.timeSamples.ToString() : "-")
              .Append(" playing=").Append(s.isPlaying)
              .Append(" pan=").Append(s.panStereo.ToString("F2"))
              .Append(" cutoff=").Append(v.filter.enabled ? v.filter.cutoffFrequency.ToString("F0") : "off")
              .AppendLine();
        }
        return sb.ToString();
    }

    #endregion
}
