using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 양쪽 플레이어의 전투 통계를 모으는 매니져 (씬에 하나 배치)
/// - EventManager 이벤트를 구독하고, 핸들러는 StatRecord 를 큐에 넣기만 한다
/// - 큐는 별도 코루틴이 다음 프레임부터 프레임당 maxRecordsPerFrame 개씩 처리한다 (지연 기록)
///   - 큐가 비면 코루틴을 끝내고, 다시 쌓일 때 새로 시작한다 (대기 비용 0)
///   - yield return null 만 쓰므로 Time.timeScale = 0 에서도 진행된다
/// - Flush 로 남은 큐를 즉시 전부 처리한다 (게임 오버 시)
/// - 씬에 배치된 것만 사용하며 자동 생성 / 씬 전환 유지는 하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class StatisticsManager
    : MonoBehaviour
{
    private static StatisticsManager instance;

    public static StatisticsManager Instance
    {
        get
        {
            if (null == instance)
            {
                instance = FindAnyObjectByType<StatisticsManager>();
            }
            return instance;
        }
    }

    public static bool HasInstance => null != instance;

    [Tooltip("코루틴이 한 프레임에 처리할 최대 기록 수")]
    [SerializeField]
    private int maxRecordsPerFrame = 64;

    [Tooltip("왼쪽 플레이어 킬로 셀 몬스터 레이어 이름")]
    [SerializeField]
    private string leftMonsterLayerName = "LMonster";

    [Tooltip("오른쪽 플레이어 킬로 셀 몬스터 레이어 이름")]
    [SerializeField]
    private string rightMonsterLayerName = "RMonster";

    /// <summary>
    /// 큐 처리(코루틴 한 프레임분 또는 Flush)가 끝날 때마다 호출
    /// </summary>
    public Action OnRecorded;

    // Awake 에서 maxRecordsPerFrame * 4 용량으로 다시 만든다 (EditMode 테스트는 기본 용량 그대로)
    private Queue<StatRecord> pending = new Queue<StatRecord>();
    private readonly PlayerCombatStats leftStats = new PlayerCombatStats();
    private readonly PlayerCombatStats rightStats = new PlayerCombatStats();

    // 무기 → 표시용 키 캐시 (데미지마다 GetComponent / 문자열 처리를 하지 않도록)
    private readonly Dictionary<Weapon, string> weaponKeyCache = new Dictionary<Weapon, string>();

    private Coroutine processRoutine;

    // 게임 오버 후 기록을 막는다 (ResetAll 로 해제)
    private bool isLocked = false;

    // 레이어는 처음 쓸 때 찾는다 (EditMode 테스트에서는 Awake 가 불리지 않으므로)
    private bool isLayerCached = false;
    private int leftMonsterLayer = -1;
    private int rightMonsterLayer = -1;

    private const string CLONE_SUFFIX = "(Clone)";

    #region Getter
    /// <summary>
    /// side 플레이어의 통계 (아직 큐에 남은 기록은 반영되지 않았을 수 있다)
    /// </summary>
    public PlayerCombatStats Get(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? leftStats : rightStats;
    }

    /// <summary>
    /// 아직 처리하지 않은 기록 수
    /// </summary>
    public int PendingCount => pending.Count;

    public bool IsProcessing => null != processRoutine;

    /// <summary>
    /// 잠겨 있으면 새 기록을 받지 않는다
    /// </summary>
    public bool IsLocked => isLocked;
    #endregion

    #region Record
    /// <summary>
    /// 기록을 큐에 넣는다. 플레이 중이고 처리 코루틴이 잠들어 있으면 깨운다
    /// 잠겨 있으면 무시한다
    /// </summary>
    public void Enqueue(StatRecord _record)
    {
        if (true == isLocked)
        {
            return;
        }

        pending.Enqueue(_record);
        WakeUp();
    }

    /// <summary>
    /// 죽은 몬스터의 레이어로 킬 귀속을 판정해 큐에 넣는다. 몬스터 레이어가 아니면 무시
    /// </summary>
    /// <returns>기록했으면 true</returns>
    public bool EnqueueKill(GameObject _monster)
    {
        if (null == _monster)
        {
            return false;
        }

        if (false == TryGetKillSide(_monster.layer, out PlayerSide side))
        {
            return false;
        }

        Enqueue(new StatRecord(STAT_TYPE.Kill, side, 1f));
        return true;
    }

    /// <summary>
    /// 무기 데미지를 큐에 넣는다
    /// </summary>
    public void EnqueueWeaponDamage(PlayerSide _side, Weapon _weapon, float _damage)
    {
        string key = GetWeaponKey(_weapon);
        if (true == string.IsNullOrEmpty(key))
        {
            return;
        }

        Enqueue(new StatRecord(STAT_TYPE.WeaponDamage, _side, _damage, key));
    }

    /// <summary>
    /// 남은 큐를 지금 전부 처리한다 (게임 오버 등 즉시 결과가 필요할 때)
    /// </summary>
    public void Flush()
    {
        if (0 == pending.Count)
        {
            return;
        }

        while (0 < pending.Count)
        {
            Apply(pending.Dequeue());
        }

        OnRecorded?.Invoke();
    }

    /// <summary>
    /// 이후 기록을 막는다 (게임 오버 시 Flush 직후). 이미 쌓인 큐는 그대로 둔다
    /// </summary>
    public void Lock()
    {
        isLocked = true;
    }

    /// <summary>
    /// 모든 통계와 남은 큐를 비우고 잠금을 푼다
    /// </summary>
    public void ResetAll()
    {
        isLocked = false;
        pending.Clear();
        leftStats.Reset();
        rightStats.Reset();
        weaponKeyCache.Clear();
    }

    /// <summary>
    /// 무기 통계 키. 무기의 DescriptionHolder 이름을 우선 쓰고, 없으면 GameObject 이름에서 (Clone) 을 뗀다
    /// </summary>
    public string GetWeaponKey(Weapon _weapon)
    {
        if (null == _weapon)
        {
            return null;
        }

        if (true == weaponKeyCache.TryGetValue(_weapon, out string cached))
        {
            return cached;
        }

        string key = null;
        if (true == _weapon.TryGetComponent(out DescriptionHolder holder))
        {
            // 언어를 바꿔도 같은 무기가 한 항목으로 모이도록 번역 전 키를 쓴다 (표시할 때 GameOverUI 가 번역)
            key = holder.GetNameKey();
        }

        if (true == string.IsNullOrEmpty(key))
        {
            key = _weapon.gameObject.name;
            if (true == key.EndsWith(CLONE_SUFFIX, StringComparison.Ordinal))
            {
                key = key.Substring(0, key.Length - CLONE_SUFFIX.Length).TrimEnd();
            }
        }

        weaponKeyCache[_weapon] = key;
        return key;
    }

    private void Apply(StatRecord _record)
    {
        Get(_record.side).Apply(_record);
    }

    private bool TryGetKillSide(int _layer, out PlayerSide _side)
    {
        CacheLayers();

        if (0 <= leftMonsterLayer && leftMonsterLayer == _layer)
        {
            _side = PlayerSide.Left;
            return true;
        }

        if (0 <= rightMonsterLayer && rightMonsterLayer == _layer)
        {
            _side = PlayerSide.Right;
            return true;
        }

        _side = PlayerSide.Left;
        return false;
    }

    private void CacheLayers()
    {
        if (true == isLayerCached)
        {
            return;
        }

        isLayerCached = true;
        leftMonsterLayer = LayerMask.NameToLayer(leftMonsterLayerName);
        rightMonsterLayer = LayerMask.NameToLayer(rightMonsterLayerName);

        if (leftMonsterLayer < 0 || rightMonsterLayer < 0)
        {
            Debug.LogWarning($"[StatisticsManager] 몬스터 레이어를 찾지 못했습니다. ({leftMonsterLayerName} {leftMonsterLayer}, {rightMonsterLayerName} {rightMonsterLayer})", this);
        }
    }
    #endregion

    #region Process
    /// <summary>
    /// 플레이 중이고 코루틴이 돌고 있지 않으면 시작한다 (EditMode 에서는 시작하지 않고 Flush 로 처리)
    /// </summary>
    private void WakeUp()
    {
        if (null != processRoutine)
        {
            return;
        }

        if (false == Application.isPlaying || false == isActiveAndEnabled)
        {
            return;
        }

        processRoutine = StartCoroutine(ProcessRoutine());
    }

    private IEnumerator ProcessRoutine()
    {
        int limit = Mathf.Max(1, maxRecordsPerFrame);

        while (true)
        {
            // 발행한 프레임이 아니라 다음 프레임부터 처리한다 (timeScale 과 무관)
            yield return null;

            int count = 0;
            while (count < limit && 0 < pending.Count)
            {
                Apply(pending.Dequeue());
                ++count;
            }

            if (0 < count)
            {
                OnRecorded?.Invoke();
            }

            if (0 == pending.Count)
            {
                break;
            }
        }

        processRoutine = null;
    }
    #endregion

    #region Event Handlers
    private void HandleEnemyDeath(GameObject _monster)
    {
        EnqueueKill(_monster);
    }

    private void HandlePlayerDamaged(PlayerSide _side, float _damage)
    {
        Enqueue(new StatRecord(STAT_TYPE.DamageTaken, _side, _damage));
    }

    private void HandleWeaponDamage(PlayerSide _side, Weapon _weapon, float _damage)
    {
        EnqueueWeaponDamage(_side, _weapon, _damage);
    }

    private void HandleExpGained(PlayerSide _side, float _exp)
    {
        Enqueue(new StatRecord(STAT_TYPE.Exp, _side, _exp));
    }
    #endregion

    #region Unity
    private void Awake()
    {
        if (null != instance && this != instance)
        {
            Debug.LogWarning($"[StatisticsManager] 매니져가 여러 개입니다. ({instance.name}, {name}) 먼저 등록된 것을 사용합니다.");
            // Destroy 는 프레임 끝에 처리되므로 그동안 OnEnable 구독이 일어나지 않게 먼저 끈다
            enabled = false;
            Destroy(this);
            return;
        }

        instance = this;

        if (0 == pending.Count)
        {
            pending = new Queue<StatRecord>(Mathf.Max(1, maxRecordsPerFrame) * 4);
        }
        CacheLayers();
    }

    private void OnEnable()
    {
        EventManager events = EventManager.Instance;
        if (null == events)
        {
            return;
        }

        events.OnEnemyDeath -= HandleEnemyDeath;
        events.OnEnemyDeath += HandleEnemyDeath;
        events.OnPlayerDamaged -= HandlePlayerDamaged;
        events.OnPlayerDamaged += HandlePlayerDamaged;
        events.OnWeaponDamage -= HandleWeaponDamage;
        events.OnWeaponDamage += HandleWeaponDamage;
        events.OnExpGained -= HandleExpGained;
        events.OnExpGained += HandleExpGained;

        // 꺼져 있는 동안 쌓인 기록이 있으면 다시 처리
        if (0 < pending.Count)
        {
            WakeUp();
        }
    }

    private void OnDisable()
    {
        // 비활성화되면 코루틴을 멈추고 비운다 (남은 큐는 OnEnable 에서 다시 처리)
        if (null != processRoutine)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        if (false == EventManager.HasInstance)
        {
            return;
        }

        EventManager events = EventManager.Instance;
        events.OnEnemyDeath -= HandleEnemyDeath;
        events.OnPlayerDamaged -= HandlePlayerDamaged;
        events.OnWeaponDamage -= HandleWeaponDamage;
        events.OnExpGained -= HandleExpGained;
    }

    private void OnDestroy()
    {
        if (this == instance)
        {
            instance = null;
        }
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
    #endregion
}
