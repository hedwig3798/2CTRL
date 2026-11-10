using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 하나의 소리 (무기 프리팹 이름 → 발사 / 타격 ID)
/// - weaponKey 는 무기 프리팹 이름. 인스턴스 이름의 "(Clone)" 을 뗀 값과 비교한다.
/// - None 이면 재생하지 않는다.
/// </summary>
[Serializable]
public struct WeaponSoundEntry
{
    [Tooltip("무기 프리팹 이름 (인스턴스 이름에서 \"(Clone)\" 을 뗀 값)")]
    public string weaponKey;
    [Tooltip("Weapon.Fired 때 재생 (공격 1회에 1번, fireDelay 로 나뉜 무기는 발마다)")]
    public SoundId fire;
    [Tooltip("OnWeaponDamage 때 재생 (보통 HIT_*)")]
    public SoundId hit;
    [Tooltip("이 무기의 발사체가 하나라도 살아 있는 동안 재생할 루프 (무기당 1개, 발사체 수와 무관)")]
    public SoundId loop;
}

/// <summary>
/// 몬스터 종류 하나의 소리 (MonsterData 에셋 이름 → 사망 ID). 없으면 SoundLibrary.DefaultMonsterDie
/// </summary>
[Serializable]
public struct MonsterSoundEntry
{
    [Tooltip("MonsterData 에셋 이름 (예: GolemData)")]
    public string monsterKey;
    [Tooltip("사망 때 재생 (MON_DIE_S / M / L)")]
    public SoundId die;
}

/// <summary>
/// 연출 하나의 소리 (PooledEffect 프리팹 이름 → ID). EffectPool.Spawn 때 위치 컬링으로 재생
/// </summary>
[Serializable]
public struct EffectSoundEntry
{
    [Tooltip("PooledEffect 프리팹 이름 (예: MineExplosion)")]
    public string effectKey;
    public SoundId sound;
}

/// <summary>
/// 차원 하나의 소리 (DimensionData 에셋 이름 → 확정 ID)
/// </summary>
[Serializable]
public struct DimensionSoundEntry
{
    [Tooltip("DimensionData 에셋 이름 (Forest / Factory / Storm)")]
    public string dimensionKey;
    [Tooltip("차원 선택에서 이 차원을 확정했을 때 (DIM_CONFIRM_*)")]
    public SoundId confirm;
    [Tooltip("전투 BGM 에서 이 차원을 고른 진영 쪽에 까는 레이어 (BGM_LAYER_*)")]
    public SoundId layer;
}

/// <summary>
/// BGM 재생 방식 (BgmDirector)
/// - None : 재생하지 않는다 (페이드아웃)
/// - Single : 클립 하나를 루프 (타이틀, 차원 선택)
/// - Combat : 베이스 4종 + 진영별 차원 레이어 2개를 같은 DSP 시각에 루프
/// </summary>
public enum BgmMode
{
    None = 0,
    Single = 1,
    Combat = 2,
}

/// <summary>
/// 씬 하나의 BGM (씬 이름 → 재생 방식 / 클립)
/// - 목록에 없는 씬은 None
/// </summary>
[Serializable]
public struct BgmSceneEntry
{
    [Tooltip("씬 이름 (Scene.name, 예: TitleScene)")]
    public string sceneName;
    public BgmMode mode;
    [Tooltip("mode 가 Single 일 때 재생할 BGM_*")]
    public SoundId single;
}

/// <summary>
/// 그룹 하나의 음량 (dB)
/// </summary>
[Serializable]
public struct SoundGroupGain
{
    public SoundGroup group;
    [Tooltip("그룹 음량 (dB, 0 이 원음). AudioMixer 대신 코드에서 선형 값으로 바꿔 곱한다")]
    public float gainDb;

    public SoundGroupGain(SoundGroup _group, float _gainDb)
    {
        group = _group;
        gainDb = _gainDb;
    }
}

/// <summary>
/// 모든 SoundEvent 와 믹스 설정을 모은 테이블
/// - SoundManager 가 Resources.Load(RESOURCE_PATH) 로 1회 읽는다. (Assets/Resources/Data/Sound/SoundLibrary.asset)
/// - id → SoundEvent, weaponKey → 항목 사전은 처음 찾을 때 만든다.
/// </summary>
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "2CTRL/Sound/SoundLibrary")]
public class SoundLibrary
    : ScriptableObject
{
    public const string RESOURCE_PATH = "Data/Sound/SoundLibrary";

    private static readonly int GROUP_COUNT = Enum.GetValues(typeof(SoundGroup)).Length;

    [Tooltip("재생할 수 있는 모든 이벤트")]
    [SerializeField]
    private List<SoundEvent> events = new List<SoundEvent>();

    [Tooltip("그룹별 음량 (dB). 없는 그룹은 0 dB")]
    [SerializeField]
    private SoundGroupGain[] groupGains =
    {
        new SoundGroupGain(SoundGroup.Alert, -6f),
        new SoundGroupGain(SoundGroup.Reward, -10f),
        new SoundGroupGain(SoundGroup.UI, -12f),
        new SoundGroupGain(SoundGroup.Weapon, -14f),
        new SoundGroupGain(SoundGroup.Hit, -22f),
        new SoundGroupGain(SoundGroup.Monster, -20f),
        new SoundGroupGain(SoundGroup.BGM, -16f),
    };

    [Tooltip("무기 프리팹 이름별 발사 / 타격 소리")]
    [SerializeField]
    private WeaponSoundEntry[] weapons = new WeaponSoundEntry[0];

    [Tooltip("MonsterData 에셋 이름별 사망 소리")]
    [SerializeField]
    private MonsterSoundEntry[] monsters = new MonsterSoundEntry[0];

    [Tooltip("monsters 에 없는 몬스터 (MonsterBrain 이 없거나 데이터가 없는 몬스터 포함) 의 사망 소리")]
    [SerializeField]
    private SoundId defaultMonsterDie = SoundId.MON_DIE_S;

    [Tooltip("PooledEffect 프리팹 이름별 소리")]
    [SerializeField]
    private EffectSoundEntry[] effects = new EffectSoundEntry[0];

    [Tooltip("DimensionData 에셋 이름별 확정 소리")]
    [SerializeField]
    private DimensionSoundEntry[] dimensions = new DimensionSoundEntry[0];

    [Header("BGM")]
    [Tooltip("씬 이름별 BGM. 없는 씬은 None")]
    [SerializeField]
    private BgmSceneEntry[] bgmScenes = new BgmSceneEntry[0];

    [Tooltip("전투 차원 레이어 패닝 (왼쪽 진영 -값, 오른쪽 진영 +값)")]
    [Range(0f, 1f)]
    [SerializeField]
    private float bgmLayerPan = 0.6f;

    [Tooltip("전투 BGM 템포 (4/4 기준, 강도 전환을 마디 경계에 맞춘다)")]
    [Min(1f)]
    [SerializeField]
    private float bgmBpm = 128f;

    [Tooltip("이 웨이브 번호(0 부터)부터 BGM_BASE_MID")]
    [Min(0)]
    [SerializeField]
    private int bgmMidFromWave = 3;

    [Tooltip("이 웨이브 번호(0 부터)부터 BGM_BASE_HIGH (러시 웨이브는 BGM_RUSH, 러시 이후는 HIGH)")]
    [Min(0)]
    [SerializeField]
    private int bgmHighFromWave = 6;

    [Tooltip("강도 전환 크로스페이드 길이 (마디)")]
    [Min(0f)]
    [SerializeField]
    private float bgmCrossfadeBars = 2f;

    [Tooltip("일시정지 중 BGM 로우패스 컷오프 (Hz)")]
    [Min(10f)]
    [SerializeField]
    private float bgmPauseCutoff = 900f;

    [Tooltip("일시정지 중 BGM 음량 (dB)")]
    [SerializeField]
    private float bgmPauseDb = -6f;

    [Header("Game Flow")]
    [Tooltip("WAVE_RUSH 를 낼 웨이브 번호 (0 부터, 20:00 러시)")]
    [Min(0)]
    [SerializeField]
    private int rushWaveIndex = 10;

    [Tooltip("플레이어 HP 비율이 이 값 미만이면 PLR_LOW_HP 루프")]
    [Range(0f, 1f)]
    [SerializeField]
    private float lowHpRatio = 0.25f;

    [Tooltip("무기 업그레이드 확정 때 LVL_STAR 를 낼 단계 (★)")]
    [SerializeField]
    private int[] starUpgradeLevels = { 5, 10 };

    [Header("Position Culling")]
    [Tooltip("진영 카메라 뷰포트 밖 여유 (0.1 = 화면 크기의 10%). 이보다 멀면 재생하지 않는다")]
    [Min(0f)]
    [SerializeField]
    private float cullViewportMargin = 0.1f;

    [Tooltip("진영 카메라를 못 찾았을 때 다시 찾는 간격 (초, 실제 시간)")]
    [Min(0.1f)]
    [SerializeField]
    private float cameraRetryInterval = 1f;

    [Header("Voices")]
    [Tooltip("원샷용 AudioSource 수 (docs 4.3 일반 원샷 16)")]
    [Min(1)]
    [SerializeField]
    private int oneShotVoices = 16;

    [Tooltip("루프용 AudioSource 수 (루프 효과음 6 + BGM 4)")]
    [Min(0)]
    [SerializeField]
    private int loopVoices = 10;

    [Header("EXP Combo")]
    [Tooltip("이 시간(초, 실제 시간) 안에 다시 획득하면 콤보로 보고 음높이를 올린다")]
    [Min(0f)]
    [SerializeField]
    private float expComboWindow = 0.4f;

    [Tooltip("콤보 1회당 올릴 반음 수")]
    [Min(0f)]
    [SerializeField]
    private float expComboStepSemitones = 1f;

    [Tooltip("콤보로 올릴 수 있는 최대 반음 수")]
    [Min(0f)]
    [SerializeField]
    private float expComboMaxSemitones = 12f;

    // 지연 생성 캐시
    private Dictionary<int, SoundEvent> eventMap;
    private Dictionary<string, int> weaponMap;
    private Dictionary<string, int> monsterMap;
    private Dictionary<string, int> effectMap;
    private Dictionary<string, int> dimensionMap;
    private Dictionary<string, int> bgmSceneMap;
    private float[] groupLinear;

    public IReadOnlyList<SoundEvent> Events => events;
    public IReadOnlyList<WeaponSoundEntry> Weapons => weapons;
    public IReadOnlyList<MonsterSoundEntry> Monsters => monsters;
    public IReadOnlyList<EffectSoundEntry> Effects => effects;
    public IReadOnlyList<DimensionSoundEntry> Dimensions => dimensions;
    public SoundId DefaultMonsterDie => defaultMonsterDie;
    public int RushWaveIndex => rushWaveIndex;
    public float LowHpRatio => lowHpRatio;
    public float CullViewportMargin => cullViewportMargin;
    public float CameraRetryInterval => cameraRetryInterval;
    public int OneShotVoices => Mathf.Max(1, oneShotVoices);
    public int LoopVoices => Mathf.Max(0, loopVoices);
    public float ExpComboWindow => expComboWindow;
    public float ExpComboStepSemitones => expComboStepSemitones;
    public float ExpComboMaxSemitones => expComboMaxSemitones;
    public IReadOnlyList<BgmSceneEntry> BgmScenes => bgmScenes;
    public float BgmLayerPan => Mathf.Clamp01(bgmLayerPan);
    public float BgmBpm => Mathf.Max(1f, bgmBpm);
    public int BgmMidFromWave => bgmMidFromWave;
    public int BgmHighFromWave => bgmHighFromWave;
    public float BgmCrossfadeBars => Mathf.Max(0f, bgmCrossfadeBars);
    public float BgmPauseCutoff => Mathf.Max(10f, bgmPauseCutoff);
    public float BgmPauseDb => bgmPauseDb;

    /// <summary>
    /// id 의 이벤트를 찾는다 (None 이거나 없으면 false)
    /// </summary>
    public bool TryGetEvent(SoundId _id, out SoundEvent _event)
    {
        _event = null;
        if (SoundId.None == _id)
        {
            return false;
        }
        EnsureEventMap();
        return eventMap.TryGetValue((int)_id, out _event) && null != _event;
    }

    /// <summary>
    /// 무기 키(프리팹 이름)의 항목을 찾는다
    /// </summary>
    public bool TryGetWeapon(string _weaponKey, out WeaponSoundEntry _entry)
    {
        _entry = default;
        if (string.IsNullOrEmpty(_weaponKey))
        {
            return false;
        }
        EnsureWeaponMap();
        if (false == weaponMap.TryGetValue(_weaponKey, out int index))
        {
            return false;
        }
        _entry = weapons[index];
        return true;
    }

    /// <summary>
    /// MonsterData 에셋 이름의 항목을 찾는다
    /// </summary>
    public bool TryGetMonster(string _monsterKey, out MonsterSoundEntry _entry)
    {
        _entry = default;
        monsterMap ??= BuildKeyMap(monsters, (in MonsterSoundEntry e) => e.monsterKey, "몬스터");
        if (false == TryFind(monsterMap, _monsterKey, out int index))
        {
            return false;
        }
        _entry = monsters[index];
        return true;
    }

    /// <summary>
    /// PooledEffect 프리팹 이름의 항목을 찾는다
    /// </summary>
    public bool TryGetEffect(string _effectKey, out EffectSoundEntry _entry)
    {
        _entry = default;
        effectMap ??= BuildKeyMap(effects, (in EffectSoundEntry e) => e.effectKey, "연출");
        if (false == TryFind(effectMap, _effectKey, out int index))
        {
            return false;
        }
        _entry = effects[index];
        return true;
    }

    /// <summary>
    /// DimensionData 에셋 이름의 항목을 찾는다
    /// </summary>
    public bool TryGetDimension(string _dimensionKey, out DimensionSoundEntry _entry)
    {
        _entry = default;
        dimensionMap ??= BuildKeyMap(dimensions, (in DimensionSoundEntry e) => e.dimensionKey, "차원");
        if (false == TryFind(dimensionMap, _dimensionKey, out int index))
        {
            return false;
        }
        _entry = dimensions[index];
        return true;
    }

    /// <summary>
    /// 씬 이름의 BGM 항목을 찾는다 (없으면 false → None)
    /// </summary>
    public bool TryGetBgmScene(string _sceneName, out BgmSceneEntry _entry)
    {
        _entry = default;
        bgmSceneMap ??= BuildKeyMap(bgmScenes, (in BgmSceneEntry e) => e.sceneName, "BGM 씬");
        if (false == TryFind(bgmSceneMap, _sceneName, out int index))
        {
            return false;
        }
        _entry = bgmScenes[index];
        return true;
    }

    /// <summary>
    /// 웨이브 번호(0 부터)의 BGM 강도 베이스 (LOW / MID / HIGH / RUSH)
    /// </summary>
    public SoundId GetBgmBaseForWave(int _waveIndex)
    {
        if (_waveIndex == rushWaveIndex)
        {
            return SoundId.BGM_RUSH;
        }
        if (_waveIndex > rushWaveIndex || _waveIndex >= bgmHighFromWave)
        {
            return SoundId.BGM_BASE_HIGH;
        }
        if (_waveIndex >= bgmMidFromWave)
        {
            return SoundId.BGM_BASE_MID;
        }
        return SoundId.BGM_BASE_LOW;
    }

    /// <summary>
    /// ★ 단계(starUpgradeLevels 에 있는 단계)인지
    /// </summary>
    public bool IsStarUpgradeLevel(int _level)
    {
        if (null == starUpgradeLevels)
        {
            return false;
        }
        for (int i = 0; i < starUpgradeLevels.Length; ++i)
        {
            if (_level == starUpgradeLevels[i])
            {
                return true;
            }
        }
        return false;
    }

    private delegate string KeySelector<T>(in T _entry);

    private static bool TryFind(Dictionary<string, int> _map, string _key, out int _index)
    {
        _index = -1;
        return false == string.IsNullOrEmpty(_key) && _map.TryGetValue(_key, out _index);
    }

    private Dictionary<string, int> BuildKeyMap<T>(T[] _entries, KeySelector<T> _selector, string _label)
    {
        var map = new Dictionary<string, int>(null != _entries ? _entries.Length : 0, StringComparer.Ordinal);
        if (null == _entries)
        {
            return map;
        }
        for (int i = 0; i < _entries.Length; ++i)
        {
            string key = _selector(in _entries[i]);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            if (true == map.ContainsKey(key))
            {
                Debug.LogWarning($"[SoundLibrary] 같은 {_label} 키가 여러 개입니다. ({key}) 먼저 등록된 것을 사용합니다.", this);
                continue;
            }
            map.Add(key, i);
        }
        return map;
    }

    /// <summary>
    /// 그룹 음량 (dB)
    /// </summary>
    public float GetGroupGainDb(SoundGroup _group)
    {
        if (null != groupGains)
        {
            for (int i = 0; i < groupGains.Length; ++i)
            {
                if (_group == groupGains[i].group)
                {
                    return groupGains[i].gainDb;
                }
            }
        }
        return 0f;
    }

    /// <summary>
    /// 그룹 음량 (선형 배율, 0 dB = 1)
    /// </summary>
    public float GetGroupGainLinear(SoundGroup _group)
    {
        if (null == groupLinear)
        {
            groupLinear = new float[GROUP_COUNT];
            for (int i = 0; i < GROUP_COUNT; ++i)
            {
                groupLinear[i] = DbToLinear(GetGroupGainDb((SoundGroup)i));
            }
        }

        int index = (int)_group;
        return 0 <= index && index < groupLinear.Length ? groupLinear[index] : 1f;
    }

    /// <summary>
    /// dB → 선형 배율
    /// </summary>
    public static float DbToLinear(float _db)
    {
        return Mathf.Pow(10f, _db / 20f);
    }

    /// <summary>
    /// 캐시를 지운다 (목록을 코드로 바꾼 뒤 호출)
    /// </summary>
    public void InvalidateCache()
    {
        eventMap = null;
        weaponMap = null;
        monsterMap = null;
        effectMap = null;
        dimensionMap = null;
        bgmSceneMap = null;
        groupLinear = null;
    }

    private void EnsureEventMap()
    {
        if (null != eventMap)
        {
            return;
        }

        eventMap = new Dictionary<int, SoundEvent>(null != events ? events.Count : 0);
        if (null == events)
        {
            return;
        }
        foreach (SoundEvent e in events)
        {
            if (null == e || SoundId.None == e.Id)
            {
                continue;
            }
            if (true == eventMap.ContainsKey((int)e.Id))
            {
                Debug.LogWarning($"[SoundLibrary] 같은 ID 의 이벤트가 여러 개입니다. ({e.Id}, {e.name}) 먼저 등록된 것을 사용합니다.", this);
                continue;
            }
            eventMap.Add((int)e.Id, e);
        }
    }

    private void EnsureWeaponMap()
    {
        if (null != weaponMap)
        {
            return;
        }

        weaponMap = new Dictionary<string, int>(null != weapons ? weapons.Length : 0, StringComparer.Ordinal);
        if (null == weapons)
        {
            return;
        }
        for (int i = 0; i < weapons.Length; ++i)
        {
            string key = weapons[i].weaponKey;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            if (true == weaponMap.ContainsKey(key))
            {
                Debug.LogWarning($"[SoundLibrary] 같은 무기 키가 여러 개입니다. ({key}) 먼저 등록된 것을 사용합니다.", this);
                continue;
            }
            weaponMap.Add(key, i);
        }
    }

    private void OnValidate()
    {
        InvalidateCache();
    }
}
