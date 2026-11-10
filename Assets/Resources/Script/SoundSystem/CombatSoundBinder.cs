using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 이미 있는 전역 이벤트를 구독해 전투 효과음을 재생한다 (SoundManager 가 같은 오브젝트에 붙인다)
/// - OnPlayerDamaged → PLR_HIT(진영) / OnPlayerDeath → PLR_DEATH(진영) / OnLevelUp → LVL_UP(공유)
/// - OnExpGained → EXP_PICKUP(진영). SoundLibrary.ExpComboWindow 안에 연속이면 반음씩 올린다 (진영별, 끊기면 리셋)
/// - OnEnemyDeath → 몬스터 종류별 사망음 (MonsterBrain.Data 에셋 이름 → SoundLibrary.TryGetMonster, 없으면 DefaultMonsterDie). 위치 컬링
///   진영은 레이어 (SoundLayerSide: LMonster = Left, RMonster = Right), 그 외는 공유
/// - OnWeaponDamage → SoundLibrary 무기 항목의 hit / Weapon.Fired → 무기 항목의 fire (항목이 없으면 재생 안 함)
/// - 무기 루프 : 항목에 loop 가 있는 무기만 Weapon.Enabled 때 등록하고, 살아 있는 발사체 수가 0 ↔ 1 이상으로 바뀔 때 StartLoop / StopLoop (무기당 1개)
/// - 몬스터 행동 (위치 컬링) : 돌진·자폭 예고 → MON_BOMBER_TELL, 순간이동 → MON_TELEPORT, 소환 → MON_SUMMON, 부활 → MON_REVIVE
/// - 연출 : EffectPool.Spawned → SoundLibrary.TryGetEffect(프리팹 이름) (위치 컬링, 진영은 레이어)
/// - PLR_LOW_HP : 플레이어 HP 비율 <= SoundLibrary.LowHpRatio 이고 살아 있으면 진영별 루프. 회복 / 사망 / 씬 전환 때 정지
/// - EventManager 는 씬에 배치된 것이 먼저 등록되도록 직접 만들지 않는다.
///   sceneLoaded 마다 -= 후 += 로 다시 구독하고, 그때 없으면 생길 때까지 기다린다.
/// </summary>
[DisallowMultipleComponent]
public class CombatSoundBinder
    : MonoBehaviour
{
    private const string CLONE_SUFFIX = "(Clone)";
    private const int SIDE_COUNT = 2;

    /// <summary>
    /// 루프를 내는 무기 하나의 상태
    /// </summary>
    private class WeaponLoop
    {
        public Weapon weapon;
        public SoundId id;
        public bool isPlaying;
        public int handle;
        // 시작에 실패했을 때 다시 시도할 시각 (unscaled)
        public float nextRetryTime;
    }

    [Tooltip("루프 시작이 실패(제한 / 소스 부족 등)했을 때 조건이 계속 참이면 다시 시도하는 간격 (초, 실제 시간)")]
    [Min(0.05f)]
    [SerializeField]
    private float loopRetryInterval = 0.5f;

    // 구독 중인 EventManager (씬이 바뀌어 사라질 수 있다)
    private EventManager bound;
    private bool isBound;

    // 무기 인스턴스 → 소리 항목 캐시 (이름 문자열을 매번 만들지 않도록. 항목이 없으면 default = None)
    private readonly Dictionary<Weapon, WeaponSoundEntry> weaponCache = new Dictionary<Weapon, WeaponSoundEntry>();

    // 루프 무기 목록 (Weapon.Enabled 때 추가, Disabled 때 제거)
    private readonly List<WeaponLoop> weaponLoops = new List<WeaponLoop>();
    private readonly Stack<WeaponLoop> weaponLoopPool = new Stack<WeaponLoop>();

    // MonsterData → 사망 ID, 연출 프리팹 → ID (처음 한 번만 이름으로 찾는다)
    private readonly Dictionary<MonsterData, SoundId> monsterDieCache = new Dictionary<MonsterData, SoundId>();
    private readonly Dictionary<PooledEffect, SoundId> effectCache = new Dictionary<PooledEffect, SoundId>();

    // 진영별 PLR_LOW_HP 루프
    private readonly PlayerStats[] lowHpStats = new PlayerStats[SIDE_COUNT];
    private readonly HealthSystem[] lowHpHealth = new HealthSystem[SIDE_COUNT];
    private readonly bool[] lowHpPlaying = new bool[SIDE_COUNT];
    private readonly int[] lowHpHandle = new int[SIDE_COUNT];
    private readonly float[] lowHpRetryTime = new float[SIDE_COUNT];

    // 진영별 경험치 콤보 (마지막 획득 시각, 현재 반음)
    private readonly float[] lastExpTime = new float[SIDE_COUNT];
    private readonly float[] expSemitones = new float[SIDE_COUNT];

    private void Awake()
    {
        ResetExpCombo();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Weapon.Fired += HandleWeaponFired;
        Weapon.Enabled += HandleWeaponEnabled;
        Weapon.Disabled += HandleWeaponDisabled;
        MonsterBrain.AnyActionTelegraph += HandleMonsterTelegraph;
        MonsterBrain.AnyActionStarted += HandleMonsterActionStarted;
        MonsterBrain.AnyRevived += HandleMonsterRevived;
        EffectPool.Spawned += HandleEffectSpawned;
        Bind();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Weapon.Fired -= HandleWeaponFired;
        Weapon.Enabled -= HandleWeaponEnabled;
        Weapon.Disabled -= HandleWeaponDisabled;
        MonsterBrain.AnyActionTelegraph -= HandleMonsterTelegraph;
        MonsterBrain.AnyActionStarted -= HandleMonsterActionStarted;
        MonsterBrain.AnyRevived -= HandleMonsterRevived;
        EffectPool.Spawned -= HandleEffectSpawned;
        Unbind();
        StopAllWeaponLoops();
        StopLowHpLoops();
    }

    private void Update()
    {
        // 씬 로드 때 EventManager 가 없었거나 구독 중이던 것이 파괴됐으면, 새로 생길 때까지 기다렸다가 구독한다
        if ((false == isBound || null == bound) && true == EventManager.HasInstance)
        {
            Bind();
        }

        UpdateWeaponLoops();
        UpdateLowHp();
    }

    private void HandleSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        // Single 로드는 SoundManager 가 루프를 모두 멈추므로 핸들만 버린다 (새 씬에서 조건이 맞으면 다시 시작)
        if (LoadSceneMode.Single == _mode)
        {
            for (int i = 0; i < weaponLoops.Count; ++i)
            {
                weaponLoops[i].isPlaying = false;
                weaponLoops[i].handle = SoundManager.INVALID_HANDLE;
            }
            for (int i = 0; i < SIDE_COUNT; ++i)
            {
                lowHpPlaying[i] = false;
                lowHpHandle[i] = SoundManager.INVALID_HANDLE;
            }
        }

        weaponCache.Clear();
        effectCache.Clear();
        monsterDieCache.Clear();
        ResetExpCombo();
        Bind();
    }

    #region Bind

    /// <summary>
    /// 현재 EventManager 에 -= 후 += 로 구독한다 (없으면 만들지 않고 넘어간다)
    /// </summary>
    private void Bind()
    {
        if (false == EventManager.HasInstance && null == FindAnyObjectByType<EventManager>())
        {
            // 이전 매니져가 사라졌을 수 있으므로 상태만 정리
            Unbind();
            return;
        }

        EventManager current = EventManager.Instance;
        if (null == current)
        {
            Unbind();
            return;
        }

        if (null != bound && bound != current)
        {
            Unbind();
        }

        current.OnPlayerDamaged -= HandlePlayerDamaged;
        current.OnPlayerDeath -= HandlePlayerDeath;
        current.OnLevelUp -= HandleLevelUp;
        current.OnExpGained -= HandleExpGained;
        current.OnEnemyDeath -= HandleEnemyDeath;
        current.OnWeaponDamage -= HandleWeaponDamage;

        current.OnPlayerDamaged += HandlePlayerDamaged;
        current.OnPlayerDeath += HandlePlayerDeath;
        current.OnLevelUp += HandleLevelUp;
        current.OnExpGained += HandleExpGained;
        current.OnEnemyDeath += HandleEnemyDeath;
        current.OnWeaponDamage += HandleWeaponDamage;

        bound = current;
        isBound = true;
    }

    private void Unbind()
    {
        // 종료 중 매니져가 새로 생성되지 않도록 HasInstance 와 보관한 참조만 쓴다
        if (true == EventManager.HasInstance && null != bound)
        {
            bound.OnPlayerDamaged -= HandlePlayerDamaged;
            bound.OnPlayerDeath -= HandlePlayerDeath;
            bound.OnLevelUp -= HandleLevelUp;
            bound.OnExpGained -= HandleExpGained;
            bound.OnEnemyDeath -= HandleEnemyDeath;
            bound.OnWeaponDamage -= HandleWeaponDamage;
        }
        bound = null;
        isBound = false;
    }

    #endregion

    #region Handlers

    private void HandlePlayerDamaged(PlayerSide _side, float _amount)
    {
        SoundManager.Play(SoundId.PLR_HIT, _side);
    }

    private void HandlePlayerDeath(PlayerSide _side)
    {
        SoundManager.Play(SoundId.PLR_DEATH, _side);
    }

    private void HandleLevelUp(GameObject _target, int _levels)
    {
        SoundManager.Play(SoundId.LVL_UP);
    }

    private void HandleExpGained(PlayerSide _side, float _exp)
    {
        SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
        if (null == library)
        {
            return;
        }

        int i = (int)_side;
        if (i < 0 || SIDE_COUNT <= i)
        {
            return;
        }

        // 버려진 소리도 콤보는 이어지도록 재생 전에 갱신한다
        float now = Time.unscaledTime;
        if (now - lastExpTime[i] <= library.ExpComboWindow)
        {
            expSemitones[i] = Mathf.Min(expSemitones[i] + library.ExpComboStepSemitones, library.ExpComboMaxSemitones);
        }
        else
        {
            expSemitones[i] = 0f;
        }
        lastExpTime[i] = now;

        float pitchScale = Mathf.Pow(2f, expSemitones[i] / 12f);
        SoundManager.Play(SoundId.EXP_PICKUP, _side, pitchScale);
    }

    private void HandleEnemyDeath(GameObject _enemy)
    {
        if (null == _enemy)
        {
            return;
        }
        SoundManager.Play(GetMonsterDieId(_enemy), SoundLayerSide.GetSide(_enemy.layer), _enemy.transform.position);
    }

    private void HandleWeaponDamage(PlayerSide _side, Weapon _weapon, float _amount)
    {
        if (true == TryGetWeaponEntry(_weapon, out WeaponSoundEntry entry))
        {
            SoundManager.Play(entry.hit, _side);
        }
    }

    private void HandleWeaponFired(Weapon _weapon)
    {
        if (false == TryGetWeaponEntry(_weapon, out WeaponSoundEntry entry))
        {
            return;
        }
        SoundManager.Play(entry.fire, GetWeaponSide(_weapon));
    }

    private void HandleMonsterTelegraph(MonsterBrain _brain, KeywordRuntime _rt)
    {
        // 봄버의 돌진 / 자폭 예고 (Alert)
        if (_rt is DashRuntime || _rt is SelfDestructRuntime)
        {
            PlayMonster(SoundId.MON_BOMBER_TELL, _brain);
        }
    }

    private void HandleMonsterActionStarted(MonsterBrain _brain, KeywordRuntime _rt)
    {
        switch (_rt)
        {
            case TeleportRuntime _:
                PlayMonster(SoundId.MON_TELEPORT, _brain);
                break;
            case SummonerRuntime _:
                PlayMonster(SoundId.MON_SUMMON, _brain);
                break;
        }
    }

    private void HandleMonsterRevived(MonsterBrain _brain)
    {
        PlayMonster(SoundId.MON_REVIVE, _brain);
    }

    private static void PlayMonster(SoundId _id, MonsterBrain _brain)
    {
        if (null == _brain)
        {
            return;
        }
        SoundManager.Play(_id, SoundLayerSide.GetSide(_brain.gameObject.layer), _brain.transform.position);
    }

    private void HandleEffectSpawned(PooledEffect _prefab, Vector3 _position, int _layer)
    {
        if (null == _prefab)
        {
            return;
        }

        if (false == effectCache.TryGetValue(_prefab, out SoundId id))
        {
            SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
            if (null == library)
            {
                return;
            }
            id = true == library.TryGetEffect(_prefab.name, out EffectSoundEntry entry) ? entry.sound : SoundId.None;
            effectCache.Add(_prefab, id);
        }

        if (SoundId.None != id)
        {
            SoundManager.Play(id, SoundLayerSide.GetSide(_layer), _position);
        }
    }

    #endregion

    #region Weapon Loop

    private void HandleWeaponEnabled(Weapon _weapon)
    {
        if (false == TryGetWeaponEntry(_weapon, out WeaponSoundEntry entry) || SoundId.None == entry.loop)
        {
            return;
        }
        if (0 <= FindWeaponLoop(_weapon))
        {
            return;
        }

        WeaponLoop loop = 0 < weaponLoopPool.Count ? weaponLoopPool.Pop() : new WeaponLoop();
        loop.weapon = _weapon;
        loop.id = entry.loop;
        loop.isPlaying = false;
        loop.handle = SoundManager.INVALID_HANDLE;
        weaponLoops.Add(loop);
    }

    private void HandleWeaponDisabled(Weapon _weapon)
    {
        // 파괴된 무기가 키로 남지 않도록 (다시 켜지면 Enabled 에서 다시 찾는다)
        weaponCache.Remove(_weapon);

        int index = FindWeaponLoop(_weapon);
        if (0 <= index)
        {
            RemoveWeaponLoop(index);
        }
    }

    private int FindWeaponLoop(Weapon _weapon)
    {
        for (int i = 0; i < weaponLoops.Count; ++i)
        {
            if (_weapon == weaponLoops[i].weapon)
            {
                return i;
            }
        }
        return -1;
    }

    private void RemoveWeaponLoop(int _index)
    {
        WeaponLoop loop = weaponLoops[_index];
        SoundManager.StopLoop(loop.handle);
        weaponLoops.RemoveAt(_index);
        loop.weapon = null;
        loop.isPlaying = false;
        loop.handle = SoundManager.INVALID_HANDLE;
        weaponLoopPool.Push(loop);
    }

    /// <summary>
    /// 살아 있는 발사체가 0 ↔ 1 이상으로 바뀔 때만 시작 / 정지한다 (시작에 실패해도 다음 전환까지 다시 시도하지 않는다)
    /// 음량은 발사체 수와 무관하게 이벤트 값 그대로
    /// </summary>
    private void UpdateWeaponLoops()
    {
        float now = Time.unscaledTime;
        for (int i = weaponLoops.Count - 1; i >= 0; --i)
        {
            WeaponLoop loop = weaponLoops[i];
            Weapon weapon = loop.weapon;
            if (null == weapon)
            {
                // Disabled 없이 사라진 경우
                RemoveWeaponLoop(i);
                continue;
            }

            bool want = true == weapon.isActiveAndEnabled && 0 < weapon.ActiveProjectileCount;
            if (true == want)
            {
                if (false == loop.isPlaying && now >= loop.nextRetryTime)
                {
                    loop.handle = SoundManager.StartLoop(loop.id, GetWeaponSide(weapon));
                    loop.isPlaying = SoundManager.INVALID_HANDLE != loop.handle;
                    if (false == loop.isPlaying)
                    {
                        loop.nextRetryTime = now + loopRetryInterval;
                    }
                }
            }
            else
            {
                if (true == loop.isPlaying)
                {
                    SoundManager.StopLoop(loop.handle);
                }
                loop.isPlaying = false;
                loop.handle = SoundManager.INVALID_HANDLE;
                loop.nextRetryTime = 0f;
            }
        }
    }

    private void StopAllWeaponLoops()
    {
        for (int i = weaponLoops.Count - 1; i >= 0; --i)
        {
            RemoveWeaponLoop(i);
        }
    }

    private static PlayerSide? GetWeaponSide(Weapon _weapon)
    {
        PlayerStats stats = _weapon.OwnerStats;
        if (null != stats)
        {
            return stats.Side;
        }
        return SoundLayerSide.GetSide(_weapon.gameObject.layer);
    }

    #endregion

    #region Low HP

    private void UpdateLowHp()
    {
        SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
        if (null == library)
        {
            return;
        }

        float ratio = library.LowHpRatio;
        float now = Time.unscaledTime;
        for (int i = 0; i < SIDE_COUNT; ++i)
        {
            PlayerSide side = (PlayerSide)i;
            HealthSystem health = GetPlayerHealth(i, side);

            bool want = null != health && true == health.isActiveAndEnabled && false == health.isDead
                && 0f < health.MaxHP && health.CurrHP / health.MaxHP <= ratio;
            if (true == want)
            {
                if (false == lowHpPlaying[i] && now >= lowHpRetryTime[i])
                {
                    lowHpHandle[i] = SoundManager.StartLoop(SoundId.PLR_LOW_HP, side);
                    lowHpPlaying[i] = SoundManager.INVALID_HANDLE != lowHpHandle[i];
                    if (false == lowHpPlaying[i])
                    {
                        lowHpRetryTime[i] = now + loopRetryInterval;
                    }
                }
            }
            else
            {
                if (true == lowHpPlaying[i])
                {
                    SoundManager.StopLoop(lowHpHandle[i]);
                }
                lowHpPlaying[i] = false;
                lowHpHandle[i] = SoundManager.INVALID_HANDLE;
                lowHpRetryTime[i] = 0f;
            }
        }
    }

    /// <summary>
    /// 진영 플레이어의 HealthSystem (PlayerStats 가 바뀔 때만 다시 찾는다)
    /// </summary>
    private HealthSystem GetPlayerHealth(int _index, PlayerSide _side)
    {
        PlayerStats stats = PlayerStats.Find(_side);
        if (stats != lowHpStats[_index])
        {
            lowHpStats[_index] = stats;
            lowHpHealth[_index] = null != stats ? stats.GetComponentInChildren<HealthSystem>(true) : null;
        }
        return lowHpHealth[_index];
    }

    private void StopLowHpLoops()
    {
        for (int i = 0; i < SIDE_COUNT; ++i)
        {
            SoundManager.StopLoop(lowHpHandle[i]);
            lowHpHandle[i] = SoundManager.INVALID_HANDLE;
            lowHpPlaying[i] = false;
        }
    }

    #endregion

    #region Lookup

    /// <summary>
    /// 몬스터의 사망 소리 (MonsterData 별 캐시. MonsterBrain / 데이터가 없으면 DefaultMonsterDie)
    /// </summary>
    private SoundId GetMonsterDieId(GameObject _enemy)
    {
        SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
        if (null == library)
        {
            return SoundId.None;
        }

        if (false == _enemy.TryGetComponent(out MonsterBrain brain) || null == brain.Data)
        {
            return library.DefaultMonsterDie;
        }

        MonsterData data = brain.Data;
        if (false == monsterDieCache.TryGetValue(data, out SoundId id))
        {
            id = true == library.TryGetMonster(data.name, out MonsterSoundEntry entry) && SoundId.None != entry.die
                ? entry.die
                : library.DefaultMonsterDie;
            monsterDieCache.Add(data, id);
        }
        return id;
    }

    /// <summary>
    /// 무기의 소리 항목 (처음 한 번만 이름으로 찾고 캐시). 항목이 없으면 false
    /// </summary>
    private bool TryGetWeaponEntry(Weapon _weapon, out WeaponSoundEntry _entry)
    {
        _entry = default;
        if (null == _weapon)
        {
            return false;
        }

        if (false == weaponCache.TryGetValue(_weapon, out _entry))
        {
            SoundLibrary library = SoundManager.HasInstance ? SoundManager.Instance.Library : null;
            if (null == library)
            {
                return false;
            }

            library.TryGetWeapon(GetWeaponKey(_weapon.name), out _entry);
            weaponCache.Add(_weapon, _entry);
        }
        return SoundId.None != _entry.fire || SoundId.None != _entry.hit || SoundId.None != _entry.loop;
    }

    /// <summary>
    /// 인스턴스 이름에서 "(Clone)" 을 떼어 프리팹 이름으로 만든다
    /// </summary>
    public static string GetWeaponKey(string _instanceName)
    {
        if (string.IsNullOrEmpty(_instanceName))
        {
            return string.Empty;
        }

        string key = _instanceName;
        while (key.EndsWith(CLONE_SUFFIX, System.StringComparison.Ordinal))
        {
            key = key.Substring(0, key.Length - CLONE_SUFFIX.Length).TrimEnd();
        }
        return key;
    }

    #endregion

    private void ResetExpCombo()
    {
        for (int i = 0; i < SIDE_COUNT; ++i)
        {
            lastExpTime[i] = float.NegativeInfinity;
            expSemitones[i] = 0f;
        }
    }
}
