using System;
using UnityEngine;

/// <summary>
/// 게임 설정값을 PlayerPrefs 에 저장 / 로드하는 static 클래스
/// - 처음 접근할 때 PlayerPrefs 에서 읽는다. 값을 바꾸면 PlayerPrefs 에 바로 쓰고, 디스크 저장은 Save() 에서 한다.
/// - 값이 실제로 바뀔 때만 변경 이벤트가 호출된다.
/// - 이벤트는 OnEnable 에서 구독, OnDisable 에서 해제할 것 (비활성 오브젝트는 다시 켜질 때 현재 값을 직접 읽어 반영)
///
/// 사용 예)
///   GameSettings.ShowDamageText = false;
///   float sfx = GameSettings.GetEffectiveVolume(AudioChannel.Effect);
///   GameSettings.OnShowEnemyHPBarChanged += HandleChanged;
///
/// 사운드 재생 쪽 규약 (실제 오디오 추가 시)
/// - Master 는 SettingsApplier 가 AudioListener.volume 으로 전체 음량에 이미 적용한다.
/// - 그래서 AudioSource.volume = GetChannelVolume(채널) (Effect / BGM, Master 미포함) 을 쓴다.
///   GetEffectiveVolume 을 AudioSource 에 쓰면 Master 가 두 번 곱해지므로 쓰지 말 것 (표시·계산용).
/// - AudioSource 를 가진 쪽은 OnEnable 에서 OnVolumeChanged 를 구독, OnDisable 에서 해제한다.
///   바뀐 채널이 자기 채널이면 다시 계산한다. 켜질 때도 한 번 계산.
///
/// 레터박스 규약
/// - 씬 로드 뒤 런타임에 Instantiate 한 Screen Space Overlay 루트 캔버스는 자동으로 16:9 에 맞춰지지 않는다.
///   생성 직후 SettingsApplier.Instance.AttachLetterbox() 를 호출하거나 LetterboxCanvas 를 직접 붙일 것. (카메라도 같음)
/// </summary>
public static class GameSettings
{
    private const string KEY_PREFIX = "Settings.";
    private const string KEY_SCREEN_MODE = KEY_PREFIX + "ScreenMode";
    private const string KEY_SHOW_ENEMY_HP_BAR = KEY_PREFIX + "ShowEnemyHPBar";
    private const string KEY_SHOW_DAMAGE_TEXT = KEY_PREFIX + "ShowDamageText";
    private const string KEY_VOLUME = KEY_PREFIX + "Volume.";
    private const string KEY_MUTE = KEY_PREFIX + "Mute.";
    // 채널별 키 (매번 문자열을 만들지 않도록 캐시)
    private static readonly string[] VOLUME_KEYS = BuildChannelKeys(KEY_VOLUME);
    private static readonly string[] MUTE_KEYS = BuildChannelKeys(KEY_MUTE);

    public const bool DEFAULT_SHOW_ENEMY_HP_BAR = true;
    public const bool DEFAULT_SHOW_DAMAGE_TEXT = true;
    public const float DEFAULT_VOLUME = 1f;
    public const bool DEFAULT_MUTE = false;

    private static readonly int CHANNEL_COUNT = Enum.GetValues(typeof(AudioChannel)).Length;

    private static bool isLoaded;
    private static int screenModeIndex;
    private static bool showEnemyHPBar;
    private static bool showDamageText;
    private static readonly float[] volumes = new float[CHANNEL_COUNT];
    private static readonly bool[] mutes = new bool[CHANNEL_COUNT];

    /// <summary>
    /// 화면 모드 인덱스가 바뀔 때 (새 인덱스, ScreenModes 기준)
    /// </summary>
    public static event Action<int> OnScreenModeChanged;
    /// <summary>
    /// 적 체력바 표시 여부가 바뀔 때
    /// </summary>
    public static event Action<bool> OnShowEnemyHPBarChanged;
    /// <summary>
    /// 데미지 텍스트 표시 여부가 바뀔 때
    /// </summary>
    public static event Action<bool> OnShowDamageTextChanged;
    /// <summary>
    /// 채널 볼륨 또는 음소거가 바뀔 때 (바뀐 채널. Master 가 바뀌면 다른 채널의 실제 음량도 바뀐다)
    /// </summary>
    public static event Action<AudioChannel> OnVolumeChanged;

    #region Public API

    /// <summary>
    /// 화면 모드 인덱스 (ScreenModes.Count 범위로 보정)
    /// </summary>
    public static int ScreenModeIndex
    {
        get
        {
            EnsureLoaded();
            return screenModeIndex;
        }
        set => SetScreenModeIndex(value, false);
    }

    /// <summary>
    /// 화면 모드 설정
    /// </summary>
    /// <param name="_forceNotify">true 면 같은 인덱스여도 OnScreenModeChanged 를 호출 (드래그로 바뀐 창 크기를 다시 맞출 때)</param>
    public static void SetScreenModeIndex(int _index, bool _forceNotify)
    {
        EnsureLoaded();
        int index = ScreenModes.Clamp(_index);
        if (index == screenModeIndex && false == _forceNotify)
        {
            return;
        }

        if (index != screenModeIndex)
        {
            screenModeIndex = index;
            PlayerPrefs.SetInt(KEY_SCREEN_MODE, index);
        }
        OnScreenModeChanged?.Invoke(index);
    }

    /// <summary>
    /// 적(몬스터) 머리 위 체력바 표시 여부. 플레이어 체력바는 영향 없음
    /// </summary>
    public static bool ShowEnemyHPBar
    {
        get
        {
            EnsureLoaded();
            return showEnemyHPBar;
        }
        set
        {
            EnsureLoaded();
            if (value == showEnemyHPBar)
            {
                return;
            }

            showEnemyHPBar = value;
            PlayerPrefs.SetInt(KEY_SHOW_ENEMY_HP_BAR, value ? 1 : 0);
            OnShowEnemyHPBarChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// 떠오르는 데미지 숫자 표시 여부
    /// </summary>
    public static bool ShowDamageText
    {
        get
        {
            EnsureLoaded();
            return showDamageText;
        }
        set
        {
            EnsureLoaded();
            if (value == showDamageText)
            {
                return;
            }

            showDamageText = value;
            PlayerPrefs.SetInt(KEY_SHOW_DAMAGE_TEXT, value ? 1 : 0);
            OnShowDamageTextChanged?.Invoke(value);
        }
    }

    /// <summary>
    /// 채널 볼륨 슬라이더 값 (0 ~ 1, 음소거와 무관한 원래 값)
    /// </summary>
    public static float GetVolume(AudioChannel _channel)
    {
        EnsureLoaded();
        return volumes[(int)_channel];
    }

    public static void SetVolume(AudioChannel _channel, float _volume)
    {
        EnsureLoaded();
        float volume = Mathf.Clamp01(_volume);
        int i = (int)_channel;
        if (Mathf.Approximately(volume, volumes[i]))
        {
            return;
        }

        volumes[i] = volume;
        PlayerPrefs.SetFloat(VOLUME_KEYS[i], volume);
        OnVolumeChanged?.Invoke(_channel);
    }

    public static bool IsMuted(AudioChannel _channel)
    {
        EnsureLoaded();
        return mutes[(int)_channel];
    }

    public static void SetMuted(AudioChannel _channel, bool _muted)
    {
        EnsureLoaded();
        int i = (int)_channel;
        if (_muted == mutes[i])
        {
            return;
        }

        mutes[i] = _muted;
        PlayerPrefs.SetInt(MUTE_KEYS[i], _muted ? 1 : 0);
        OnVolumeChanged?.Invoke(_channel);
    }

    /// <summary>
    /// 실제로 써야 할 음량 (0 ~ 1)
    /// - Master : 음소거면 0, 아니면 Master 볼륨
    /// - Effect / BGM : Master 또는 자기 채널이 음소거면 0, 아니면 Master * 채널 볼륨
    /// </summary>
    public static float GetEffectiveVolume(AudioChannel _channel)
    {
        EnsureLoaded();
        int master = (int)AudioChannel.Master;
        if (true == mutes[master])
        {
            return 0f;
        }
        if (AudioChannel.Master == _channel)
        {
            return volumes[master];
        }

        int i = (int)_channel;
        return true == mutes[i] ? 0f : volumes[master] * volumes[i];
    }

    /// <summary>
    /// AudioSource.volume 에 넣을 채널 음량 (0 ~ 1, Master 미포함)
    /// - Master 는 AudioListener.volume 이 맡으므로 여기서는 곱하지 않는다.
    /// - 자기 채널이 음소거면 0. Master 채널을 넘기면 1 (AudioListener 가 처리).
    /// </summary>
    public static float GetChannelVolume(AudioChannel _channel)
    {
        EnsureLoaded();
        if (AudioChannel.Master == _channel)
        {
            return 1f;
        }

        int i = (int)_channel;
        return true == mutes[i] ? 0f : volumes[i];
    }

    /// <summary>
    /// PlayerPrefs 를 디스크에 저장 (설정 창을 닫을 때 호출)
    /// </summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 저장값을 다시 읽는다 (바뀐 항목은 이벤트 호출)
    /// </summary>
    public static void Reload()
    {
        int prevScreen = screenModeIndex;
        bool prevHP = showEnemyHPBar;
        bool prevDamage = showDamageText;
        float[] prevVolumes = (float[])volumes.Clone();
        bool[] prevMutes = (bool[])mutes.Clone();
        bool wasLoaded = isLoaded;

        Load();

        if (false == wasLoaded)
        {
            return;
        }
        if (prevScreen != screenModeIndex)
        {
            OnScreenModeChanged?.Invoke(screenModeIndex);
        }
        if (prevHP != showEnemyHPBar)
        {
            OnShowEnemyHPBarChanged?.Invoke(showEnemyHPBar);
        }
        if (prevDamage != showDamageText)
        {
            OnShowDamageTextChanged?.Invoke(showDamageText);
        }
        for (int i = 0; i < CHANNEL_COUNT; ++i)
        {
            if (false == Mathf.Approximately(prevVolumes[i], volumes[i]) || prevMutes[i] != mutes[i])
            {
                OnVolumeChanged?.Invoke((AudioChannel)i);
            }
        }
    }

    #endregion

    private static void EnsureLoaded()
    {
        if (false == isLoaded)
        {
            Load();
        }
    }

    private static void Load()
    {
        isLoaded = true;
        screenModeIndex = ScreenModes.Clamp(PlayerPrefs.GetInt(KEY_SCREEN_MODE, ScreenModes.DEFAULT_INDEX));
        showEnemyHPBar = 0 != PlayerPrefs.GetInt(KEY_SHOW_ENEMY_HP_BAR, DEFAULT_SHOW_ENEMY_HP_BAR ? 1 : 0);
        showDamageText = 0 != PlayerPrefs.GetInt(KEY_SHOW_DAMAGE_TEXT, DEFAULT_SHOW_DAMAGE_TEXT ? 1 : 0);

        for (int i = 0; i < CHANNEL_COUNT; ++i)
        {
            volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(VOLUME_KEYS[i], DEFAULT_VOLUME));
            mutes[i] = 0 != PlayerPrefs.GetInt(MUTE_KEYS[i], DEFAULT_MUTE ? 1 : 0);
        }
    }

    private static string[] BuildChannelKeys(string _prefix)
    {
        Array values = Enum.GetValues(typeof(AudioChannel));
        var keys = new string[values.Length];
        for (int i = 0; i < values.Length; ++i)
        {
            keys[i] = _prefix + ((AudioChannel)i).ToString();
        }
        return keys;
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        isLoaded = false;
        OnScreenModeChanged = null;
        OnShowEnemyHPBarChanged = null;
        OnShowDamageTextChanged = null;
        OnVolumeChanged = null;
    }
}
