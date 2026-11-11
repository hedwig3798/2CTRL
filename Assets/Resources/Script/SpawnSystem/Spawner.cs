using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 몬스터 보정 종류 (레벨업 보상으로 Spawner 의 비율을 바꿀 때 사용)
/// - HP / 데미지 / 속도 비율은 스폰될 때 BlackBoard 로 전달하므로, 보상으로 바뀐 비율은 이후 스폰되는 몬스터부터 적용된다
/// </summary>
public enum SpawnModifierType
{
    /// <summary>
    /// 개체수 비율 (스폰 간격 = spawnInterval / 비율)
    /// </summary>
    Count,
    HP,
    Damage,
    Speed,
}

/// <summary>
/// spawable ��ü�� �����ϴ� ������
/// </summary>
public class Spawner
    : MonoBehaviour
{
    [SerializeField]
    [Header("Managers")]
    private DropManager dropManager;

    [SerializeField]
    [Header("Spawn Setting")]
    private Transform spawnLocation;

    [SerializeField]
    private WaveData[] waveArray;

    private Dictionary<Spawnable, IObjectPool<Spawnable>> poolDict
        = new Dictionary<Spawnable, IObjectPool<Spawnable>>();

    /// <summary>
    /// 생성한 몬스터의 외형 대상과 프리팹 기본 외형 (외형을 되돌릴 때 사용)
    /// </summary>
    private class SkinTarget
    {
        public MonsterData data;
        public Animator animator;
        public SpriteRenderer spriteRenderer;
        public RuntimeAnimatorController originalController;
        public Sprite originalSprite;
        public bool isSkinned;
    }

    // MonsterData -> 이 스포너에서 쓸 외형 (SetMonsterSkins)
    private readonly Dictionary<MonsterData, MonsterSkin> skinMap = new Dictionary<MonsterData, MonsterSkin>();
    private readonly Dictionary<Spawnable, SkinTarget> skinTargets = new Dictionary<Spawnable, SkinTarget>();

    // 생성한 몬스터 -> 처치 시 EXP 배율 (MonsterData.expValue, 생성할 때 한 번만 찾는다. 없으면 DropManager 기본값)
    private readonly Dictionary<Spawnable, float> expRates = new Dictionary<Spawnable, float>();

    // 생성한 몬스터 중 고정 HP·데미지·속도를 쓰는 개체 (MidBoss 가 붙은 중간 보스. 웨이브/보상 배율 무시, 처치 드롭은 MidBoss 가 한다)
    private readonly HashSet<Spawnable> fixedRateObjects = new HashSet<Spawnable>();

    // 중간 보스 생존 중 개체수 곱셈 보정 (보스마다 하나, 최종 Count 에 모두 곱한다. 보상 보정과 별개)
    private readonly List<float> bossCountMultipliers = new List<float>();

    //
    [Header("Spawn Data")]
    public Transform target;
    [Tooltip("기본 이동 속도 비율 (보상으로 보정됨)")]
    public float speedRate;
    [Tooltip("기본 HP 비율 (보상으로 보정됨)")]
    public float HPRate;
    [Tooltip("기본 데미지 비율 (보상으로 보정됨)")]
    public float damagerate;

    private static readonly int ModifierCount = System.Enum.GetValues(typeof(SpawnModifierType)).Length;

    // 보상으로 누적된 보정값. 최종값 = (base + ΣAdd) × (1 + ΣMultiply)
    private readonly float[] addRates = new float[ModifierCount];
    private readonly float[] mulRates = new float[ModifierCount];

    // 개체수 비율이 이 값보다 작아지지 않게 한다 (스폰 간격이 무한히 길어지지 않도록)
    private const float MinCountRate = 0.1f;

    // 진행 중인 웨이브 번호 (Wave 코루틴이 갱신, 시작 전이면 -1). 이후 스폰(소환 / 분열 포함)부터 그 웨이브 배율을 쓴다
    private int currentWaveIndex = -1;

    private bool isFinished = false;

    // 웨이브 진행 시간 (Wave 코루틴이 기록, Time.time 기준이라 일시정지 동안 멈춘다)
    private bool isStarted = false;
    private float waveStartTime = 0f;
    private float totalWaveTime = 0f;
    // 진행 중인 웨이브가 시작된 시각과 길이, 그 뒤 남은 웨이브들의 time 합
    // (WaitForSeconds 는 프레임 단위로 끝나 웨이브마다 조금씩 늦어지므로 웨이브마다 다시 맞춘다)
    private float currentWaveStartTime = 0f;
    private float currentWaveTime = 0f;
    private float timeAfterCurrentWave = 0f;

    /// <summary>
    /// 마지막 웨이브의 time 이 끝났을 때 한 번 호출 (이후에도 마지막 웨이브 스폰은 계속된다)
    /// </summary>
    public System.Action<Spawner> OnAllWavesFinished;

    /// <summary>
    /// 웨이브가 시작될 때 (이 Spawner, 웨이브 번호 0 부터). 웨이브 효과음 등에 쓴다
    /// </summary>
    public System.Action<Spawner, int> OnWaveChanged;

    /// <summary>
    /// 웨이브 데이터 수 (마지막 웨이브 = WaveCount - 1)
    /// </summary>
    public int WaveCount => null != waveArray ? waveArray.Length : 0;

    /// <summary>
    /// 모든 웨이브의 time 이 끝났는지
    /// </summary>
    public bool IsFinished => isFinished;

    /// <summary>
    /// 이 스포너의 드롭 매니져 (중간 보스가 처치 시 보석을 흩뿌릴 때 사용)
    /// </summary>
    public DropManager DropManager => dropManager;

    /// <summary>
    /// 웨이브가 시작됐는지 (Start 의 Wave 코루틴 시작 시점부터 true)
    /// </summary>
    public bool IsStarted => isStarted;

    /// <summary>
    /// 첫 웨이브를 시작한 Time.time (시작 전이면 -1)
    /// </summary>
    public float WaveStartTime => true == isStarted ? waveStartTime : -1f;

    /// <summary>
    /// 모든 웨이브 time 의 합 (Wave 코루틴이 건너뛰는 null 웨이브 / spwanArray 가 null 인 웨이브는 제외)
    /// - 시작 전이면 지금 웨이브 목록으로 계산하고, 시작 후에는 시작할 때 계산한 값
    /// </summary>
    public float TotalWaveTime => true == isStarted ? totalWaveTime : CalculateWaveTime(0);

    /// <summary>
    /// 마지막 웨이브가 끝날 때까지 남은 시간 (스케일 게임 시간, 0 미만은 0)
    /// - 시작 전이면 TotalWaveTime, 모든 웨이브가 끝나면 0
    /// </summary>
    public float RemainingTime
    {
        get
        {
            if (false == isStarted)
            {
                return TotalWaveTime;
            }
            if (true == isFinished)
            {
                return 0f;
            }
            return Mathf.Max(0f, timeAfterCurrentWave + currentWaveTime - (Time.time - currentWaveStartTime));
        }
    }

    /// <summary>
    /// _startIndex 번 웨이브부터 끝까지 time 합 (Wave 코루틴이 건너뛰는 웨이브 제외, 음수 time 은 0)
    /// </summary>
    private float CalculateWaveTime(int _startIndex)
    {
        if (null == waveArray)
        {
            return 0f;
        }

        float sum = 0f;
        for (int i = _startIndex; i < waveArray.Length; i++)
        {
            if (null == waveArray[i] || null == waveArray[i].spwanArray)
            {
                continue;
            }
            sum += Mathf.Max(0f, waveArray[i].time);
        }
        return sum;
    }

    /// <summary>
    /// 보상으로 몬스터 비율을 보정한다
    /// </summary>
    public void AddRate(SpawnModifierType _type, StatModifierMode _mode, float _value)
    {
        int i = (int)_type;
        switch (_mode)
        {
            case StatModifierMode.Add:
                addRates[i] += _value;
                break;
            case StatModifierMode.Multiply:
                mulRates[i] += _value;
                break;
        }

        Debug.Log($"[Spawner] {_type} {_mode} {_value:+0.##;-0.##} → {GetRate(_type):0.##}", this);
    }

    /// <summary>
    /// 최종 비율 = 현재 웨이브 배율 × 보상 보정 ((base + ΣAdd) × (1 + ΣMultiply)) (Count 의 base 는 1, 웨이브 배율 없음)
    /// </summary>
    public float GetRate(SpawnModifierType _type)
    {
        int i = (int)_type;
        float rate = GetWaveRate(_type) * (GetBaseRate(_type) + addRates[i]) * (1f + mulRates[i]);
        if (SpawnModifierType.Count == _type)
        {
            rate *= BossCountMultiplier;
        }
        return SpawnModifierType.Count == _type ? Mathf.Max(MinCountRate, rate) : Mathf.Max(0f, rate);
    }

    /// <summary>
    /// 살아 있는 중간 보스들의 개체수 배율 곱 (없으면 1)
    /// </summary>
    public float BossCountMultiplier
    {
        get
        {
            float m = 1f;
            for (int i = 0; i < bossCountMultipliers.Count; ++i)
            {
                m *= bossCountMultipliers[i];
            }
            return m;
        }
    }

    /// <summary>
    /// 중간 보스 개체수 배율을 하나 더한다 (최종 Count 에 곱함, 0.7 = ×0.7)
    /// </summary>
    public void AddBossCountMultiplier(float _multiplier)
    {
        bossCountMultipliers.Add(Mathf.Max(0f, _multiplier));
    }

    /// <summary>
    /// AddBossCountMultiplier 로 넣은 배율 하나를 뺀다. 없으면 false
    /// </summary>
    public bool RemoveBossCountMultiplier(float _multiplier)
    {
        float m = Mathf.Max(0f, _multiplier);
        for (int i = 0; i < bossCountMultipliers.Count; ++i)
        {
            if (true == Mathf.Approximately(bossCountMultipliers[i], m))
            {
                bossCountMultipliers.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 현재 웨이브의 배율 (웨이브 시작 전이거나 Count 면 1)
    /// </summary>
    public float GetWaveRate(SpawnModifierType _type)
    {
        WaveData wave = CurrentWave;
        if (null == wave)
        {
            return 1f;
        }

        switch (_type)
        {
            case SpawnModifierType.HP:
                return wave.hpRate;
            case SpawnModifierType.Damage:
                return wave.damageRate;
            case SpawnModifierType.Speed:
                return wave.speedRate;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// 진행 중인 웨이브 번호 (0 부터, 시작 전이면 -1)
    /// </summary>
    public int CurrentWaveIndex => currentWaveIndex;

    /// <summary>
    /// 진행 중인 웨이브 (시작 전이면 null)
    /// </summary>
    public WaveData CurrentWave
    {
        get
        {
            if (null == waveArray || currentWaveIndex < 0 || currentWaveIndex >= waveArray.Length)
            {
                return null;
            }
            return waveArray[currentWaveIndex];
        }
    }

    private float GetBaseRate(SpawnModifierType _type)
    {
        switch (_type)
        {
            case SpawnModifierType.HP:
                return HPRate;
            case SpawnModifierType.Damage:
                return damagerate;
            case SpawnModifierType.Speed:
                return speedRate;
            default:
                return 1f;
        }
    }

    private void Awake()
    {
        PreparePools();
    }

    /// <summary>
    /// 웨이브를 바꾼다 (Start 전에 호출해야 첫 웨이브부터 적용된다)
    /// - 비어 있으면 기존 웨이브를 유지한다
    /// </summary>
    public void SetWaves(WaveData[] _waves)
    {
        if (null == _waves || 0 == _waves.Length)
        {
            Debug.LogWarning($"[Spawner] 넣을 웨이브가 비어 있어 기존 웨이브를 유지합니다. ({name})", this);
            return;
        }

        waveArray = _waves;
        PreparePools();
    }

    /// <summary>
    /// 이 스포너에서 나오는 몬스터의 외형을 바꾼다 (MonsterData 기준, 이후 스폰부터 적용)
    /// - 목록에 없는 몬스터는 프리팹 기본 외형을 쓴다
    /// </summary>
    public void SetMonsterSkins(MonsterSkin[] _skins)
    {
        skinMap.Clear();
        if (null == _skins)
        {
            return;
        }

        foreach (MonsterSkin skin in _skins)
        {
            if (null == skin.monster)
            {
                continue;
            }

            if (true == skinMap.ContainsKey(skin.monster))
            {
                Debug.LogWarning($"[Spawner] {skin.monster.name} 의 외형이 중복되어 뒤의 값을 씁니다.", this);
            }
            skinMap[skin.monster] = skin;
        }
    }

    /// <summary>
    /// 이 스포너에서 나올 중간 보스 목록을 넣는다 (같은 오브젝트의 MidBossScheduler 가 출현시킨다. 없으면 붙인다)
    /// </summary>
    /// <summary>
    /// 스폰을 완전히 멈춘다 (최종 보스 진입): 웨이브·스폰 코루틴 정지, 중간 보스 스케줄 정지·활성 보스 조용히 제거
    /// 마지막 웨이브의 스폰 코루틴은 웨이브가 끝나도 계속 돌기 때문에 여기서 끊는다
    /// </summary>
    public void StopSpawning()
    {
        StopAllCoroutines();

        MidBossScheduler scheduler = GetComponent<MidBossScheduler>();
        if (null != scheduler)
        {
            scheduler.StopAndRemoveAll();
        }
    }

    public void SetMidBosses(MidBossEntry[] _entries)
    {
        MidBossScheduler scheduler = GetComponent<MidBossScheduler>();
        if (null == scheduler)
        {
            scheduler = gameObject.AddComponent<MidBossScheduler>();
        }
        scheduler.SetEntries(_entries);
    }

    /// <summary>
    /// 웨이브에 있는 프리팹의 풀을 미리 만든다
    /// </summary>
    private void PreparePools()
    {
        if (null == waveArray)
        {
            return;
        }

        foreach (var wd in waveArray)
        {
            if (null == wd || null == wd.spwanArray)
            {
                continue;
            }

            foreach (var sd in wd.spwanArray)
            {
                if (null == sd.spawnObject)
                {
                    continue;
                }

                GetPool(sd.spawnObject);
            }
        }
    }

    /// <summary>
    /// 프리팹의 풀. 없으면 새로 만든다 (웨이브에 없는 소환/분열 몬스터용)
    /// </summary>
    private IObjectPool<Spawnable> GetPool(Spawnable _prefab)
    {
        if (true == poolDict.TryGetValue(_prefab, out IObjectPool<Spawnable> pool))
        {
            return pool;
        }

        pool = new ObjectPool<Spawnable>
            (
                createFunc: () => CreateObject(_prefab)
                , OnSpawn
                , OnRelease
                , OnDespawn
                , true
                , 100
                , 200
            );
        poolDict[_prefab] = pool;
        return pool;
    }

    /// <summary>
    /// 지정한 위치에 몬스터를 스폰하고 현재 보정값으로 초기화한다
    /// 실패하면 null
    /// </summary>
    public Spawnable SpawnAt(Spawnable _prefab, Vector2 _position)
    {
        if (null == _prefab)
        {
            return null;
        }

        IObjectPool<Spawnable> pool = GetPool(_prefab);
        Spawnable sa = pool.Get();
        if (null == sa)
        {
            return null;
        }

        sa.transform.position = _position;
        ApplySkin(sa);

        if (null == sa.blackBoardHandler)
        {
            Debug.LogError("it has no BlackBoardHandler");
            pool.Release(sa);
            return null;
        }

        BlackBoard data = sa.blackBoardHandler.GetBlackBoard();
        if (null == data)
        {
            Debug.LogError("BlackBoardHandler has no data");
            pool.Release(sa);
            return null;
        }

        // 중간 보스는 고정 수치 (배율 1) 이고, 일반 보석 대신 MidBoss 가 처치 시 보석을 흩뿌린다
        bool isFixedRate = fixedRateObjects.Contains(sa);
        data.dropManager = true == isFixedRate ? null : dropManager;
        data.spawner = this;
        data.SetFloat(DATA_TYPE.HPRate, true == isFixedRate ? 1f : GetRate(SpawnModifierType.HP));
        data.SetFloat(DATA_TYPE.moveSpeedRate, true == isFixedRate ? 1f : GetRate(SpawnModifierType.Speed));
        data.SetFloat(DATA_TYPE.damageRate, true == isFixedRate ? 1f : GetRate(SpawnModifierType.Damage));
        // 0 이면 DropManager 기본값을 쓴다
        data.SetFloat(DATA_TYPE.expRate, expRates.TryGetValue(sa, out float expRate) ? expRate : 0f);
        data.SetTransform(DATA_TYPE.moveTarget, target);
        sa.blackBoardHandler.Initialize();

        return sa;
    }

    /// <summary>
    /// MonsterData 에 맞는 외형을 적용한다. 외형이 없으면 프리팹 기본값으로 되돌린다
    /// - 본체(SpriteRenderer 오브젝트) 에 Animator 가 있으면 컨트롤러만 바꾸고 스프라이트는 건드리지 않는다 (애니메이션이 덮어쓰므로)
    /// - Animator 가 없으면 스프라이트를 바꾼다
    /// </summary>
    private void ApplySkin(Spawnable _object)
    {
        if (false == skinTargets.TryGetValue(_object, out SkinTarget target))
        {
            return;
        }

        bool hasSkin = skinMap.TryGetValue(target.data, out MonsterSkin skin);
        if (false == hasSkin && false == target.isSkinned)
        {
            return;
        }
        target.isSkinned = hasSkin;

        if (null != target.animator)
        {
            RuntimeAnimatorController controller = null != skin.animator ? skin.animator : target.originalController;
            // 같은 컨트롤러를 다시 넣으면 애니메이터 상태가 초기화되므로 바뀔 때만 넣는다
            if (controller != target.animator.runtimeAnimatorController)
            {
                target.animator.runtimeAnimatorController = controller;
            }
        }
        else if (null != target.spriteRenderer)
        {
            target.spriteRenderer.sprite = null != skin.sprite ? skin.sprite : target.originalSprite;
        }
    }

    /// <summary>
    /// 생성한 몬스터의 외형 대상과 기본 외형, EXP 배율을 기억한다 (MonsterData 가 없으면 외형을 바꾸지 않고 EXP 는 기본값)
    /// </summary>
    private void RegisterSkinTarget(Spawnable _object)
    {
        MonsterBrain brain = _object.GetComponentInChildren<MonsterBrain>(true);
        if (null == brain || null == brain.Data)
        {
            return;
        }

        expRates[_object] = brain.Data.expValue;

        SkinTarget target = new SkinTarget();
        target.data = brain.Data;
        target.spriteRenderer = null != brain.Context ? brain.Context.spriteRenderer : null;
        if (null == target.spriteRenderer)
        {
            target.spriteRenderer = _object.GetComponentInChildren<SpriteRenderer>(true);
        }
        // 다른 자식의 Animator 를 잡지 않도록 본체(SpriteRenderer) 오브젝트의 Animator 만 쓴다
        target.animator = null != target.spriteRenderer ? target.spriteRenderer.GetComponent<Animator>() : null;
        target.originalController = null != target.animator ? target.animator.runtimeAnimatorController : null;
        target.originalSprite = null != target.spriteRenderer ? target.spriteRenderer.sprite : null;
        skinTargets[_object] = target;
    }

    private void Start()
    {
        if (null == waveArray || 0 == waveArray.Length)
        {
            Debug.LogError("[Spawner] WaveData�� �����ϴ�.");
            return;
        }

        StartCoroutine(Wave());
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool debugSkipWaveRequested = false;

    /// <summary>
    /// 디버그 전용 : 진행 중인 웨이브의 남은 시간을 건너뛰고 다음 웨이브로 넘어간다
    /// - 마지막 웨이브에서 호출하면 모든 웨이브가 끝난 것으로 처리된다 (OnAllWavesFinished)
    /// - 시작 전이거나 이미 끝났으면 false
    /// </summary>
    public bool DebugSkipWave()
    {
        if (false == isStarted || true == isFinished)
        {
            return false;
        }

        debugSkipWaveRequested = true;
        return true;
    }
#endif

    private Spawnable CreateObject(Spawnable _sa)
    {
        Spawnable sa = Instantiate(_sa);
        sa.SetPool(GetPool(_sa));

        Transform[] transforms = sa.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            t.gameObject.layer = gameObject.layer;
        }

        if (null != sa.blackBoardHandler)
        {
            BlackBoard data = sa.blackBoardHandler.GetBlackBoard();
            data.dropManager = dropManager;
        }

        RegisterSkinTarget(sa);

        if (null != sa.GetComponent<MidBoss>())
        {
            fixedRateObjects.Add(sa);
        }

        return sa;
    }

    private void OnSpawn(Spawnable _object)
    {
        _object.gameObject.SetActive(true);
    }

    private void OnRelease(Spawnable _object)
    {
        _object.MarkReturningToPool();
        if (_object.gameObject.activeSelf)
        {
            _object.gameObject.SetActive(false);
        }
    }

    private void OnDespawn(Spawnable _object)
    {
        skinTargets.Remove(_object);
        expRates.Remove(_object);
        fixedRateObjects.Remove(_object);
        Destroy(_object.gameObject);
    }

    IEnumerator Spawn(SpawnData _data)
    {
        if (null == _data.spawnObject)
        {
            yield break;
        }

        Transform origin = null != spawnLocation ? spawnLocation : transform;
        float interval = -1f;
        WaitForSeconds flag = null;
        while (true)
        {
            // 개체수 보상으로 비율이 바뀌었을 때만 대기 시간을 다시 만든다
            float newInterval = Mathf.Max(0.01f, _data.spawnInterval / GetRate(SpawnModifierType.Count));
            if (false == Mathf.Approximately(interval, newInterval))
            {
                interval = newInterval;
                flag = new WaitForSeconds(interval);
            }

            float dis = Random.Range(_data.spawnRange.x, _data.spawnRange.y);
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }

            Vector2 spawnPos = origin.position;
            spawnPos += dir * dis;

            SpawnAt(_data.spawnObject, spawnPos);

            yield return flag;
        }
    }

    IEnumerator Wave()
    {
        List<Coroutine> spawCoroutine = new List<Coroutine>();

        // SetWaves 로 Start 전에 웨이브가 바뀔 수 있으므로 총 시간은 여기서 계산한다
        totalWaveTime = CalculateWaveTime(0);
        waveStartTime = Time.time;
        currentWaveStartTime = waveStartTime;
        currentWaveTime = 0f;
        timeAfterCurrentWave = totalWaveTime;
        isStarted = true;

        for (int i = 0; i < waveArray.Length; i++)
        {
            if (null == waveArray[i] || null == waveArray[i].spwanArray)
            {
                continue;
            }

            foreach (Coroutine c in spawCoroutine)
            {
                if (null != c)
                {
                    StopCoroutine(c);
                }
            }
            spawCoroutine.Clear();

            // 이후 스폰부터 이 웨이브의 HP / 데미지 / 속도 배율을 쓴다
            currentWaveIndex = i;

            // 남은 시간 계산용 (이전 웨이브가 프레임 단위로 늦게 끝난 만큼 다시 맞춘다)
            currentWaveStartTime = Time.time;
            currentWaveTime = Mathf.Max(0f, waveArray[i].time);
            timeAfterCurrentWave = CalculateWaveTime(i + 1);

            for (int j = 0; j < waveArray[i].spwanArray.Length; j++)
            {
                spawCoroutine.Add(StartCoroutine(Spawn(waveArray[i].spwanArray[j])));
            }

            OnWaveChanged?.Invoke(this, i);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 디버그 : DebugSkipWave 가 요청되면 대기를 끊고 다음 웨이브로 넘어간다
            debugSkipWaveRequested = false;
            float waveEndTime = currentWaveStartTime + currentWaveTime;
            while (Time.time < waveEndTime && false == debugSkipWaveRequested)
            {
                yield return null;
            }
            debugSkipWaveRequested = false;
#else
            yield return new WaitForSeconds(waveArray[i].time);
#endif
        }

        isFinished = true;
        OnAllWavesFinished?.Invoke(this);
    }
}
