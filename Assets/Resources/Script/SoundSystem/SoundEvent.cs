using UnityEngine;

/// <summary>
/// 효과음 / BGM 하나의 재생 설정
/// - clips 중 하나를 랜덤으로 고른다 (직전과 다른 클립 우선)
/// - 실제 음량 = 채널 음량(GameSettings.GetChannelVolume) × volume × 그룹 음량(SoundLibrary)
/// - minInterval / maxVoices 는 (ID, 진영) 별로 따로 센다 (SoundThrottle)
/// - SoundLibrary 의 events 목록에 넣어야 재생된다
/// </summary>
[CreateAssetMenu(fileName = "SoundEvent", menuName = "2CTRL/Sound/SoundEvent")]
public class SoundEvent
    : ScriptableObject
{
    [Tooltip("사운드 ID (라이브러리 안에서 중복되면 안 됨)")]
    [SerializeField]
    private SoundId id = SoundId.None;

    [Tooltip("재생할 클립 변형 (랜덤으로 하나, 직전과 다른 것 우선)")]
    [SerializeField]
    private AudioClip[] clips = new AudioClip[0];

    [Tooltip("믹스 그룹 (그룹 음량과 볼륨 채널을 정한다)")]
    [SerializeField]
    private SoundGroup group = SoundGroup.Weapon;

    [Tooltip("이벤트 음량 (0 ~ 1)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float volume = 1f;

    [Tooltip("피치 랜덤 범위 최솟값")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float pitchMin = 1f;

    [Tooltip("피치 랜덤 범위 최댓값")]
    [Range(0.1f, 3f)]
    [SerializeField]
    private float pitchMax = 1f;

    [Tooltip("같은 (ID, 진영) 의 최소 재생 간격 (초, 실제 시간). 0 이면 제한 없음")]
    [Min(0f)]
    [SerializeField]
    private float minInterval = 0f;

    [Tooltip("같은 (ID, 진영) 의 동시 재생 수. 0 이하면 제한 없음")]
    [SerializeField]
    private int maxVoices = 0;

    [Tooltip("일시정지 중에도 재생 (UI, BGM, LVL_*, RESULT_*)")]
    [SerializeField]
    private bool ignorePause = false;

    [Tooltip("AudioSource.priority (0 이 가장 중요, 256 이 가장 낮음). 원샷 소스가 모자라면 낮은 쪽을 교체한다")]
    [Range(0, 256)]
    [SerializeField]
    private int priority = 128;

    [Tooltip("루프 재생 (StartLoop / StopLoop 로만 재생)")]
    [SerializeField]
    private bool loop = false;

    public SoundId Id => id;
    public AudioClip[] Clips => clips;
    public SoundGroup Group => group;
    public float Volume => volume;
    public float PitchMin => Mathf.Min(pitchMin, pitchMax);
    public float PitchMax => Mathf.Max(pitchMin, pitchMax);
    public float MinInterval => minInterval;
    public int MaxVoices => maxVoices;
    public bool IgnorePause => ignorePause;
    public int Priority => priority;
    public bool Loop => loop;

    /// <summary>
    /// 재생할 수 있는 클립이 하나라도 있는지
    /// </summary>
    public bool HasClip
    {
        get
        {
            if (null == clips)
            {
                return false;
            }
            for (int i = 0; i < clips.Length; ++i)
            {
                if (null != clips[i])
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 클립 하나를 랜덤으로 고른다. 직전 인덱스(_lastIndex)와 다른 클립을 우선한다. 클립이 없으면 -1
    /// </summary>
    public int PickClipIndex(int _lastIndex)
    {
        if (null == clips || 0 == clips.Length)
        {
            return -1;
        }

        int count = clips.Length;
        if (1 == count)
        {
            return null != clips[0] ? 0 : -1;
        }

        // 직전 것을 뺀 나머지에서 먼저 고르고, 비어 있으면 앞에서부터 찾는다
        int start = Random.Range(0, count);
        for (int n = 0; n < count; ++n)
        {
            int i = (start + n) % count;
            if (i != _lastIndex && null != clips[i])
            {
                return i;
            }
        }
        return 0 <= _lastIndex && _lastIndex < count && null != clips[_lastIndex] ? _lastIndex : -1;
    }

    private void OnValidate()
    {
        if (pitchMin <= 0f)
        {
            pitchMin = 0.1f;
        }
        if (pitchMax <= 0f)
        {
            pitchMax = 0.1f;
        }
    }
}
