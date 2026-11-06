#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무기 테스트 씬 전용 컨트롤러
/// - 더미 배치 프리셋 전환 : 기존 더미를 지우고 플레이어 위치 기준 오프셋에 Dummy 프리팹을 다시 만든다
/// - 무기 교체 : WeaponRewardEffect 목록으로 버튼을 만들고, 고르면 현재 무기를 파괴한 뒤 새 무기를 만든다
///   (PlayerStats.AddWeapon 은 제거 기능이 없으므로 쓰지 않고, AddWeapon 과 같은 방식으로 직접 생성한다)
/// - 강화 : +1 / 최대. "Lv n/max" 표시, 업그레이드 테이블이 없으면 버튼 비활성
/// - 초기화 : 무기 재생성(업그레이드 0), 패시브 스택 0, 플레이어 원위치, 더미 재배치, 데미지 미터 리셋
/// - 패시브 : RewardData(StatRewardEffect) 별 스택. 스택이 바뀌면 플레이어 수정치를 지우고
///   모든 패시브를 스택 수만큼 RewardData.Apply 로 다시 적용한다 (실게임 경로). 무기 교체 / 강화 시에는 유지
/// - 무기 교체 / 강화 / 패시브 변경 / 초기화 시 모든 더미의 데미지 미터를 리셋해 현재 구성의 DPS 를 잰다
///
/// UI 버튼 목록은 템플릿 버튼(비활성)을 복제해서 런타임에 만든다. 라벨은 버튼 자식의 TMP_Text
/// </summary>
public class WeaponTestController
    : MonoBehaviour
{
    // PlayerStats.AddWeapon 의 side 기반 레이어 규칙과 같은 이름
    private const string LeftAttackLayer = "LAttack";
    private const string RightAttackLayer = "RAttack";
    private const string LeftMonsterLayer = "LMonster";
    private const string RightMonsterLayer = "RMonster";

    [Header("플레이어")]
    [Tooltip("테스트할 플레이어 (비어 있으면 씬에서 찾는다)")]
    [SerializeField]
    private PlayerStats player;

    [Tooltip("무기를 생성할 부모 (비어 있으면 플레이어 자식 중 weaponHolderName 을 찾고, 없으면 플레이어)")]
    [SerializeField]
    private Transform weaponHolder;

    [SerializeField]
    private string weaponHolderName = "WeaponHolder";

    [Tooltip("시작 시 플레이어가 원래 가진 무기를 지운다. 끄면 무기 교체 시 같은 공격 레이어의 다른 무기 발사체까지 지워지므로 켜 둘 것")]
    [SerializeField]
    private bool clearExistingWeapons = true;

    [Header("무기")]
    [Tooltip("교체할 무기 목록 (Assets/prefab/Upgrade/Effects/*RewardEffect.asset)")]
    [SerializeField]
    private WeaponRewardEffect[] weaponEffects;

    [Tooltip("시작 무기 (-1 이면 없음)")]
    [SerializeField]
    private int initialWeaponIndex = 0;

    [Header("더미")]
    [Tooltip("배치할 더미 프리팹 (Assets/prefab/Monster/Dummy/Dummy.prefab)")]
    [SerializeField]
    private GameObject dummyPrefab;

    [Tooltip("더미 배치 프리셋 (플레이어 위치 기준 오프셋)")]
    [SerializeField]
    private List<WeaponTestDummyLayout> layouts = WeaponTestDummyLayout.CreateDefaults();

    [Tooltip("시작 배치 (-1 이면 없음)")]
    [SerializeField]
    private int initialLayoutIndex = 0;

    [Tooltip("더미의 Straight 이동을 꺼서 정지시킨다")]
    [SerializeField]
    private bool disableDummyMovement = true;

    [Tooltip("넉백으로 밀려도 매 프레임 배치 위치로 되돌린다 (배치 유지)")]
    [SerializeField]
    private bool lockDummyPosition = true;

    [Tooltip("더미를 무적으로 만든다 (데미지는 받고 미터에 기록되지만 죽지 않는다)")]
    [SerializeField]
    private bool invincibleDummies = true;

    [SerializeField]
    private WeaponTestDamageMeterSettings meterSettings = new WeaponTestDamageMeterSettings();

    [Header("UI : 목록 (템플릿 버튼을 복제)")]
    [SerializeField]
    private Button weaponButtonTemplate;

    [Tooltip("무기 버튼을 넣을 부모 (비어 있으면 템플릿의 부모)")]
    [SerializeField]
    private Transform weaponButtonContainer;

    [SerializeField]
    private Button layoutButtonTemplate;

    [Tooltip("배치 버튼을 넣을 부모 (비어 있으면 템플릿의 부모)")]
    [SerializeField]
    private Transform layoutButtonContainer;

    [Header("UI : 조작")]
    [SerializeField]
    private Button upgradeOnceButton;

    [SerializeField]
    private Button upgradeMaxButton;

    [SerializeField]
    private Button resetButton;

    [Tooltip("현재 강화 단계 표시")]
    [SerializeField]
    private TMP_Text levelText;

    [Tooltip("현재 무기 이름 표시 (선택)")]
    [SerializeField]
    private TMP_Text weaponNameText;

    [Tooltip("강화 단계 포맷 ({0} = 현재, {1} = 최대)")]
    [SerializeField]
    private string levelFormat = "Lv {0}/{1}";

    [Tooltip("업그레이드 테이블이 없을 때 표시")]
    [SerializeField]
    private string noTableText = "Lv -";

    [Tooltip("무기가 없을 때 표시")]
    [SerializeField]
    private string noWeaponText = "-";

    [Header("패시브")]
    [Tooltip("스택할 패시브 목록 (Assets/prefab/Upgrade/PassiveBuff, PassiveNuff 의 StatRewardEffect 보상). effect 가 StatRewardEffect 가 아니면 비활성")]
    [SerializeField]
    private RewardData[] passives;

    [Tooltip("패시브 하나의 최대 스택")]
    [Min(1)]
    [SerializeField]
    private int maxPassiveStack = 99;

    [Tooltip("패시브가 바뀌면 현재 무기를 다시 만들고 강화 단계를 다시 적용한다 (발사 시점에 스탯을 읽어 둔 진행 중 발사체 정리)")]
    [SerializeField]
    private bool rebuildWeaponOnPassiveChange = true;

    [Header("UI : 패시브")]
    [Tooltip("패시브 행 템플릿 (비활성으로 두고 복제)")]
    [SerializeField]
    private WeaponTestPassiveRow passiveRowTemplate;

    [Tooltip("패시브 행을 넣을 부모 (비어 있으면 템플릿의 부모)")]
    [SerializeField]
    private Transform passiveRowContainer;

    [SerializeField]
    private Button passiveResetButton;

    [Tooltip("현재 스탯 요약 (선택)")]
    [SerializeField]
    private TMP_Text statSummaryText;

    [Tooltip("패시브 라벨 포맷 ({0} = 이름, {1} = 1스택 변화량)")]
    [SerializeField]
    private string passiveLabelFormat = "{1}";

    [Tooltip("스택 표시 포맷 ({0} = 스택)")]
    [SerializeField]
    private string passiveStackFormat = "×{0}";

    [Tooltip("비활성 패시브 라벨 포맷 ({0} = 에셋 이름)")]
    [SerializeField]
    private string invalidPassiveFormat = "{0} (사용 불가)";

    [Tooltip("스탯 요약 한 줄 포맷 ({0} = 이름, {1} = 부호 붙은 ΣAdd, {2} = 배율, {3} = 미적용 표시)")]
    [SerializeField]
    private string statSummaryLineFormat = "{0}  {1}  ×{2:0.00}{3}";

    [Tooltip("현재 무기의 WeaponStatTag 에 없는 무기 수치 뒤에 붙는 표시")]
    [SerializeField]
    private string notAppliedText = " (미적용)";

    // 더미 하나의 런타임 정보
    private struct DummyEntry
    {
        public GameObject root;
        public Rigidbody2D body;
        public Vector3 position;
        public WeaponTestDamageMeter meter;
    }

    private readonly List<DummyEntry> dummies = new List<DummyEntry>();
    private readonly List<WeaponTestDamageMeter> meters = new List<WeaponTestDamageMeter>();
    private readonly List<Button> weaponButtons = new List<Button>();
    private readonly List<Button> layoutButtons = new List<Button>();
    private readonly List<WeaponTestPassiveRow> passiveRows = new List<WeaponTestPassiveRow>();

    // 요약에 표시할 스탯 순서
    private static readonly ValueType[] SummaryTypes =
    {
        ValueType.Damage,
        ValueType.AttackSpeed,
        ValueType.Range,
        ValueType.Duration,
        ValueType.ProjectileCount,
        ValueType.MoveSpeed,
        ValueType.HP,
        ValueType.HPRegen,
    };

    // passives 와 같은 순서. 스택 / 사용 가능 여부 / 라벨용 1스택 변화량
    private int[] passiveStacks = new int[0];
    private bool[] passiveValid = new bool[0];
    private string[] passiveDeltaLabels = new string[0];

    private Weapon currentWeapon;
    private int currentWeaponIndex = -1;
    private int currentLayoutIndex = -1;

    private Vector3 playerHomePosition;
    private Rigidbody2D playerBody;
    private HealthSystem playerHealth;

    // 스탯 요약 문자열 재사용
    private readonly System.Text.StringBuilder summaryBuilder = new System.Text.StringBuilder();

    private int attackLayer = -1;
    private int monsterLayer = -1;

    public Weapon CurrentWeapon => currentWeapon;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public int CurrentLayoutIndex => currentLayoutIndex;
    public IReadOnlyList<WeaponTestDamageMeter> Meters => meters;
    public int PassiveCount => passiveStacks.Length;

    #region Unity
    private void Start()
    {
        if (null == player)
        {
            player = FindAnyObjectByType<PlayerStats>();
        }
        if (null == player)
        {
            Debug.LogError("[WeaponTestController] 씬에 PlayerStats 가 없습니다.", this);
            enabled = false;
            return;
        }

        playerHomePosition = player.transform.position;
        playerBody = player.GetComponent<Rigidbody2D>();
        playerHealth = player.GetComponentInChildren<HealthSystem>(true);

        ResolveWeaponHolder();
        ResolveLayers();

        if (true == clearExistingWeapons)
        {
            ClearExistingWeapons();
        }
        else
        {
            // 발사체 정리가 레이어 기준이라 다른 무기의 풀 발사체도 파괴된다
            Debug.LogWarning("[WeaponTest] clearExistingWeapons 가 꺼져 있습니다. 무기 교체 시 기존 무기의 발사체도 함께 지워집니다.", this);
        }

        BuildWeaponButtons();
        BuildLayoutButtons();
        BindControlButtons();

        InitPassives();
        BuildPassiveRows();

        if (0 <= initialLayoutIndex)
        {
            SelectLayout(initialLayoutIndex);
        }
        if (0 <= initialWeaponIndex)
        {
            SelectWeapon(initialWeaponIndex);
        }

        RefreshWeaponUI();
    }

    private void LateUpdate()
    {
        if (false == lockDummyPosition)
        {
            return;
        }

        // 넉백(Knockback 은 Update 에서 이동) 뒤에 배치 위치로 되돌린다
        foreach (DummyEntry d in dummies)
        {
            if (null == d.root)
            {
                continue;
            }

            if (null != d.body)
            {
                d.body.position = d.position;
                d.body.linearVelocity = Vector2.zero;
            }
            d.root.transform.position = d.position;
        }
    }

    private void OnDestroy()
    {
        UnbindControlButtons();
    }
    #endregion

    #region Weapon
    /// <summary>
    /// weaponEffects[_index] 의 무기로 교체한다 (현재 무기와 그 발사체를 지우고 업그레이드 0 으로 새로 만든다)
    /// </summary>
    public bool SelectWeapon(int _index)
    {
        Weapon prefab = GetWeaponPrefab(_index);
        if (null == prefab)
        {
            Debug.LogWarning($"[WeaponTestController] {_index} 번 무기 프리팹이 없습니다.", this);
            return false;
        }

        DestroyCurrentWeapon();

        currentWeapon = CreateWeapon(prefab);
        currentWeaponIndex = _index;

        ResetMeters();
        RefreshWeaponUI();
        return true;
    }

    /// <summary>
    /// 업그레이드를 한 단계 적용한다 (테이블이 없거나 최대면 false)
    /// </summary>
    public bool UpgradeOnce()
    {
        bool applied = TryApplyNextUpgrade();
        if (true == applied)
        {
            ResetMeters();
        }
        RefreshWeaponUI();
        return applied;
    }

    /// <summary>
    /// 남은 업그레이드를 모두 적용한다 (적용한 단계 수)
    /// </summary>
    public int UpgradeMax()
    {
        int count = 0;
        while (true == TryApplyNextUpgrade())
        {
            count++;
        }

        if (0 < count)
        {
            ResetMeters();
        }
        RefreshWeaponUI();
        return count;
    }

    private bool TryApplyNextUpgrade()
    {
        WeaponUpgradeTable table = GetCurrentTable();
        if (null == currentWeapon || null == table)
        {
            return false;
        }

        // GetStep 은 1 부터. 다음 단계 = 현재 단계 + 1
        int next = currentWeapon.UpgradeLevel + 1;
        WeaponUpgradeStep step = table.GetStep(next);
        if (null == step)
        {
            return false;
        }

        currentWeapon.ApplyUpgrade(step);
        return true;
    }

    private Weapon CreateWeapon(Weapon _prefab)
    {
        Transform parent = (null != weaponHolder) ? weaponHolder : player.transform;
        Weapon weapon = Instantiate(_prefab, parent);
        weapon.name = _prefab.name;
        weapon.owner = player.transform;
        weapon.SetOwnerStats(player);

        // PlayerStats.AddWeapon 과 같은 규칙 (무기 프리팹은 Default / targetLayer 0 이라 그대로 두면 공격하지 않는다)
        if (0 <= attackLayer && 0 <= monsterLayer)
        {
            weapon.targetLayer = 1 << monsterLayer;
            LayerUtils.SetLayer(weapon.gameObject, attackLayer);
        }
        return weapon;
    }

    /// <summary>
    /// 현재 무기를 끄고, 날아가는 중이거나 풀에 남은 발사체(보조 발사체 포함)를 모두 지운 뒤 무기를 파괴한다
    /// </summary>
    private void DestroyCurrentWeapon()
    {
        if (null != currentWeapon)
        {
            // Weapon.OnDisable 에서 유지형 발사체가 풀로 돌아간다
            currentWeapon.gameObject.SetActive(false);
        }

        ClearProjectiles();

        if (null != currentWeapon)
        {
            Destroy(currentWeapon.gameObject);
        }
        currentWeapon = null;
        currentWeaponIndex = -1;
    }

    /// <summary>
    /// 플레이어 공격 레이어의 발사체를 모두 지운다
    /// - 발사체는 무기 풀이 씬 루트에 만들고 무기가 파괴돼도 남으므로 직접 정리한다
    /// - 먼저 활성 발사체를 꺼서 풀로 돌려보낸 뒤(무기가 아직 살아 있을 때) 전부 파괴한다
    /// </summary>
    private void ClearProjectiles()
    {
        Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsInactive.Include);

        foreach (Projectile p in projectiles)
        {
            if (true == IsOwnedProjectile(p) && true == p.gameObject.activeSelf)
            {
                p.gameObject.SetActive(false);
            }
        }

        foreach (Projectile p in projectiles)
        {
            if (true == IsOwnedProjectile(p))
            {
                Destroy(p.gameObject);
            }
        }
    }

    private bool IsOwnedProjectile(Projectile _projectile)
    {
        if (null == _projectile)
        {
            return false;
        }

        // 레이어를 못 찾았으면 씬의 발사체를 모두 정리 대상으로 본다 (테스트 씬에는 플레이어 무기만 있다)
        return attackLayer < 0 || attackLayer == _projectile.gameObject.layer;
    }

    private void ClearExistingWeapons()
    {
        Weapon[] existing = player.GetComponentsInChildren<Weapon>(true);
        foreach (Weapon w in existing)
        {
            w.gameObject.SetActive(false);
        }

        ClearProjectiles();

        foreach (Weapon w in existing)
        {
            Destroy(w.gameObject);
        }
    }

    private Weapon GetWeaponPrefab(int _index)
    {
        WeaponRewardEffect effect = GetWeaponEffect(_index);
        return (null != effect) ? effect.WeaponPrefab : null;
    }

    private WeaponRewardEffect GetWeaponEffect(int _index)
    {
        if (null == weaponEffects || _index < 0 || _index >= weaponEffects.Length)
        {
            return null;
        }
        return weaponEffects[_index];
    }

    private WeaponUpgradeTable GetCurrentTable()
    {
        WeaponRewardEffect effect = GetWeaponEffect(currentWeaponIndex);
        return (null != effect) ? effect.UpgradeTable : null;
    }
    #endregion

    #region Dummy
    /// <summary>
    /// layouts[_index] 배치로 더미를 다시 만든다 (기존 더미는 제거, 위치는 현재 플레이어 위치 기준)
    /// </summary>
    public bool SelectLayout(int _index)
    {
        if (null == layouts || _index < 0 || _index >= layouts.Count || null == layouts[_index])
        {
            Debug.LogWarning($"[WeaponTestController] {_index} 번 배치가 없습니다.", this);
            return false;
        }
        if (null == dummyPrefab)
        {
            Debug.LogWarning("[WeaponTestController] dummyPrefab 이 연결되지 않았습니다.", this);
            return false;
        }

        ClearDummies();

        Vector3 origin = player.transform.position;
        Vector2[] offsets = layouts[_index].offsets;
        int count = (null != offsets) ? offsets.Length : 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = origin + (Vector3)offsets[i];
            SpawnDummy(position, i);
        }

        currentLayoutIndex = _index;
        RefreshLayoutButtons();
        return true;
    }

    private void SpawnDummy(Vector3 _position, int _index)
    {
        // Knockback 은 Rigidbody2D 가 없으면 transform.root 를 움직이므로 부모 없이 씬 루트에 만든다
        GameObject go = Instantiate(dummyPrefab, _position, Quaternion.identity);
        go.name = $"{dummyPrefab.name}_{_index}";

        if (true == disableDummyMovement)
        {
            foreach (Straight s in go.GetComponentsInChildren<Straight>(true))
            {
                s.enabled = false;
            }
        }

        // 무기 targetLayer(1 << monsterLayer) 에 맞도록 더미 레이어를 맞춘다
        if (0 <= monsterLayer)
        {
            LayerUtils.SetLayer(go, monsterLayer);
        }

        HealthSystem health = go.GetComponentInChildren<HealthSystem>(true);
        if (null == health)
        {
            Debug.LogWarning($"[WeaponTestController] {go.name} 에 HealthSystem 이 없어 데미지를 잴 수 없습니다.", go);
        }
        else
        {
            health.IsInvincible = invincibleDummies;
        }

        WeaponTestDamageMeter meter = go.GetComponent<WeaponTestDamageMeter>();
        if (null == meter)
        {
            meter = go.AddComponent<WeaponTestDamageMeter>();
        }
        meter.Bind(health, meterSettings);

        DummyEntry entry = new DummyEntry();
        entry.root = go;
        entry.body = go.GetComponentInChildren<Rigidbody2D>(true);
        entry.position = _position;
        entry.meter = meter;
        dummies.Add(entry);
        meters.Add(meter);
    }

    private void ClearDummies()
    {
        foreach (DummyEntry d in dummies)
        {
            if (null != d.root)
            {
                Destroy(d.root);
            }
        }
        dummies.Clear();
        meters.Clear();
    }

    /// <summary>
    /// 모든 더미의 데미지 미터를 0 부터 다시 잰다
    /// </summary>
    public void ResetMeters()
    {
        foreach (WeaponTestDamageMeter m in meters)
        {
            if (null != m)
            {
                m.ResetMeter();
            }
        }
    }
    #endregion

    #region Reset
    /// <summary>
    /// 무기 재생성(업그레이드 0), 패시브 스택 0, 플레이어 원위치, 더미 재배치, 데미지 미터 리셋
    /// </summary>
    public void ResetAll()
    {
        // 무기는 아래 SelectWeapon 에서 다시 만들므로 여기서는 재생성하지 않는다
        ClearPassiveStacks();
        RefreshPassiveRows();

        ResetPlayerPosition();

        int weaponIndex = currentWeaponIndex;
        if (0 <= weaponIndex)
        {
            SelectWeapon(weaponIndex);
        }

        if (0 <= currentLayoutIndex)
        {
            SelectLayout(currentLayoutIndex);
        }

        ResetMeters();
        RefreshWeaponUI();
    }

    private void ResetPlayerPosition()
    {
        if (null != playerBody)
        {
            playerBody.position = playerHomePosition;
            playerBody.linearVelocity = Vector2.zero;
        }
        player.transform.position = playerHomePosition;
    }
    #endregion

    #region Passive
    /// <summary>
    /// passives[_index] 의 현재 스택 (범위 밖이면 0)
    /// </summary>
    public int GetPassiveStack(int _index)
    {
        if (_index < 0 || _index >= passiveStacks.Length)
        {
            return 0;
        }
        return passiveStacks[_index];
    }

    /// <summary>
    /// passives[_index] 의 스택을 _stack 으로 바꾼다 (0 ~ maxPassiveStack 으로 자른다)
    /// </summary>
    /// <returns>범위 밖 / 사용 불가 패시브거나 스택이 그대로면 false</returns>
    public bool SetPassiveStack(int _index, int _stack)
    {
        if (false == IsValidPassive(_index))
        {
            return false;
        }

        int stack = Mathf.Clamp(_stack, 0, maxPassiveStack);
        if (stack == passiveStacks[_index])
        {
            return false;
        }

        passiveStacks[_index] = stack;
        OnPassivesChanged();
        return true;
    }

    /// <summary>
    /// passives[_index] 의 스택에 _delta 를 더한다 (0 ~ maxPassiveStack 으로 자른다)
    /// </summary>
    /// <returns>범위 밖 / 사용 불가 패시브거나 스택이 그대로면 false</returns>
    public bool AddPassiveStack(int _index, int _delta)
    {
        return SetPassiveStack(_index, GetPassiveStack(_index) + _delta);
    }

    /// <summary>
    /// 모든 패시브 스택을 0 으로 하고 플레이어 수정치를 지운다 (무기 / 강화 단계는 유지)
    /// </summary>
    public void ResetPassives()
    {
        // 이미 모두 0 이면 무기 재생성 / 미터 리셋을 하지 않는다
        if (false == HasAnyPassiveStack())
        {
            return;
        }

        ClearPassiveStacks();

        if (true == rebuildWeaponOnPassiveChange)
        {
            RebuildCurrentWeapon();
        }

        ResetMeters();
        RefreshPassiveRows();
        RefreshWeaponUI();
    }

    private bool IsValidPassive(int _index)
    {
        return 0 <= _index && _index < passiveValid.Length && true == passiveValid[_index];
    }

    private bool HasAnyPassiveStack()
    {
        foreach (int stack in passiveStacks)
        {
            if (0 < stack)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 현재 HP 비율 (HealthSystem 이 없으면 -1)
    /// - HealthSystem 은 최대 HP 가 늘면 늘어난 만큼 현재 HP 를 채우므로, 수정치를 지웠다 다시 붙이면 HP 가 가득 찬다.
    ///   재적용 전 비율을 저장해 두고 재적용 후 같은 비율로 되돌린다
    /// </summary>
    private float CaptureHPRatio()
    {
        if (null == playerHealth || playerHealth.MaxHP <= 0f)
        {
            return -1f;
        }
        return Mathf.Clamp01(playerHealth.CurrHP / playerHealth.MaxHP);
    }

    private void RestoreHPRatio(float _ratio)
    {
        if (_ratio < 0f || null == playerHealth || true == playerHealth.isDead)
        {
            return;
        }

        // 살아 있으면 최소 1 (HealthSystem.HandlePlayerStatChanged 와 같은 규칙)
        float maxHP = playerHealth.MaxHP;
        playerHealth.CurrHP = Mathf.Min(maxHP, Mathf.Max(1f, maxHP * _ratio));
    }

    /// <summary>
    /// 패시브 사용 가능 여부를 확인하고 1스택 변화량을 잰다 (끝나면 플레이어 수정치는 비어 있다)
    /// - 1스택 변화량이 0 인 패시브는 경고 후 비활성
    /// </summary>
    private void InitPassives()
    {
        int count = (null != passives) ? passives.Length : 0;
        passiveStacks = new int[count];
        passiveValid = new bool[count];
        passiveDeltaLabels = new string[count];

        // 1) 측정 없이 형식만 확인한다
        bool hasCandidate = false;
        for (int i = 0; i < count; i++)
        {
            RewardData data = passives[i];
            if (null == data)
            {
                Debug.LogWarning($"[WeaponTestController] {i} 번 패시브가 비어 있습니다.", this);
                continue;
            }
            if (false == (data.GetEffect() is StatRewardEffect))
            {
                Debug.LogWarning($"[WeaponTestController] {data.name} 의 effect 가 StatRewardEffect 가 아니라 비활성 처리합니다.", data);
                continue;
            }
            if (false == data.TryGetKey(out ValueType _) || false == data.TryGetValue(out float _))
            {
                Debug.LogWarning($"[WeaponTestController] {data.name} 에 key / value 가 없어 비활성 처리합니다.", data);
                continue;
            }

            passiveValid[i] = true;
            hasCandidate = true;
        }

        if (false == hasCandidate)
        {
            return;
        }

        // 2) 시작 전 수정치를 지운다 (측정은 빈 수정치에서 한 번씩 적용해 본다. 씬에서 미리 붙어 있던 수정치도 함께 사라진다)
        float hpRatio = CaptureHPRatio();
        for (int i = 0; i < count; i++)
        {
            if (false == passiveValid[i])
            {
                continue;
            }

            RewardData data = passives[i];
            data.TryGetKey(out ValueType key);
            if (false == TryMeasurePassiveDelta(data, key, out string label))
            {
                Debug.LogWarning($"[WeaponTestController] {data.name} 의 1스택 변화량이 0 이라 비활성 처리합니다.", data);
                passiveValid[i] = false;
                continue;
            }
            passiveDeltaLabels[i] = label;
        }

        player.ResetModifiers();
        RestoreHPRatio(hpRatio);
    }

    /// <summary>
    /// 빈 수정치에서 _data 를 한 번 적용해 ΣAdd / ΣMultiply 변화량을 문자열로 만든다
    /// (StatRewardEffect 의 Add / Multiply 모드가 비공개라 실제 적용 결과로 판단한다)
    /// </summary>
    /// <returns>변화량이 0 이면 false</returns>
    private bool TryMeasurePassiveDelta(RewardData _data, ValueType _key, out string _label)
    {
        player.ResetModifiers();
        _data.Apply(CreateRewardContext());

        float add = player.GetAdd(_key);
        float multiply = player.GetMultiply(_key);

        string result = string.Empty;
        if (false == Mathf.Approximately(0f, multiply))
        {
            result = FormatSigned(multiply * 100f, "0.#") + "%";
        }
        if (false == Mathf.Approximately(0f, add))
        {
            string addText = FormatSigned(add, "0.##");
            result = (true == string.IsNullOrEmpty(result)) ? addText : $"{result} {addText}";
        }
        if (true == string.IsNullOrEmpty(result))
        {
            _label = string.Empty;
            return false;
        }

        _label = $"{_key.ToDisplayName()} {result}";
        return true;
    }

    private static string FormatSigned(float _value, string _format)
    {
        string text = _value.ToString(_format, System.Globalization.CultureInfo.InvariantCulture);
        return (0f <= _value) ? "+" + text : text;
    }

    private RewardContext CreateRewardContext()
    {
        return new RewardContext { Player = player, Side = player.Side };
    }

    /// <summary>
    /// 스택을 모두 0 으로 하고 플레이어 수정치를 지운다 (무기 재생성 / UI 갱신은 하지 않는다)
    /// </summary>
    private void ClearPassiveStacks()
    {
        for (int i = 0; i < passiveStacks.Length; i++)
        {
            passiveStacks[i] = 0;
        }

        if (null != player)
        {
            float hpRatio = CaptureHPRatio();
            player.ResetModifiers();
            RestoreHPRatio(hpRatio);
        }
    }

    private void OnPassivesChanged()
    {
        ApplyPassives();

        if (true == rebuildWeaponOnPassiveChange)
        {
            RebuildCurrentWeapon();
        }

        ResetMeters();
        RefreshPassiveRows();
        RefreshWeaponUI();
    }

    /// <summary>
    /// 플레이어 수정치를 지우고 모든 패시브를 스택 수만큼 실게임과 같은 RewardData.Apply 로 다시 적용한다
    /// </summary>
    private void ApplyPassives()
    {
        float hpRatio = CaptureHPRatio();
        player.ResetModifiers();

        RewardContext context = CreateRewardContext();
        for (int i = 0; i < passiveStacks.Length; i++)
        {
            if (false == passiveValid[i])
            {
                continue;
            }

            for (int s = 0; s < passiveStacks[i]; s++)
            {
                passives[i].Apply(context);
            }
        }

        RestoreHPRatio(hpRatio);
    }

    /// <summary>
    /// 현재 무기를 다시 만들고 같은 강화 단계까지 다시 적용한다
    /// - Final* 는 매번 플레이어 스탯을 읽지만, 이미 발사된 발사체(Laser 빔 길이, Flame 분사 시간 / 크기, Lightning 낙뢰 수)는
    ///   발사 시점 값을 쓰므로 정리하고 새로 쏘게 한다
    /// </summary>
    private void RebuildCurrentWeapon()
    {
        if (null == currentWeapon || currentWeaponIndex < 0)
        {
            return;
        }

        int index = currentWeaponIndex;
        int level = currentWeapon.UpgradeLevel;
        Weapon prefab = GetWeaponPrefab(index);
        if (null == prefab)
        {
            return;
        }

        DestroyCurrentWeapon();

        currentWeapon = CreateWeapon(prefab);
        currentWeaponIndex = index;

        for (int i = 0; i < level; i++)
        {
            if (false == TryApplyNextUpgrade())
            {
                break;
            }
        }
    }

    private void BuildPassiveRows()
    {
        if (null == passiveRowTemplate)
        {
            if (0 < passiveStacks.Length)
            {
                Debug.LogWarning("[WeaponTestController] passiveRowTemplate 이 연결되지 않아 패시브 UI 를 만들지 않습니다.", this);
            }
            return;
        }

        passiveRowTemplate.gameObject.SetActive(false);
        Transform parent = (null != passiveRowContainer) ? passiveRowContainer : passiveRowTemplate.transform.parent;

        for (int i = 0; i < passiveStacks.Length; i++)
        {
            WeaponTestPassiveRow row = Instantiate(passiveRowTemplate, parent);
            row.name = $"{passiveRowTemplate.name}_{i}";
            row.gameObject.SetActive(true);
            row.SetLabel(GetPassiveLabel(i));

            int index = i;
            if (null != row.MinusButton)
            {
                row.MinusButton.onClick.AddListener(() => AddPassiveStack(index, -1));
            }
            if (null != row.PlusButton)
            {
                row.PlusButton.onClick.AddListener(() => AddPassiveStack(index, 1));
            }
            passiveRows.Add(row);
        }

        RefreshPassiveRows();
    }

    private string GetPassiveLabel(int _index)
    {
        RewardData data = passives[_index];
        if (false == passiveValid[_index])
        {
            return string.Format(invalidPassiveFormat, (null != data) ? data.name : _index.ToString());
        }
        return string.Format(passiveLabelFormat, data.GetName(), passiveDeltaLabels[_index]);
    }

    private void RefreshPassiveRows()
    {
        for (int i = 0; i < passiveRows.Count && i < passiveStacks.Length; i++)
        {
            bool valid = passiveValid[i];
            int stack = passiveStacks[i];
            passiveRows[i].SetStack(string.Format(passiveStackFormat, stack), true == valid && 0 < stack, true == valid && stack < maxPassiveStack);
        }
    }

    /// <summary>
    /// 스탯별 ΣAdd 와 배율. 현재 무기의 WeaponStatTag 에 없는 무기 수치는 미적용 표시
    /// </summary>
    private void RefreshStatSummary()
    {
        if (null == statSummaryText || null == player)
        {
            return;
        }

        System.Text.StringBuilder builder = summaryBuilder;
        builder.Clear();
        foreach (ValueType type in SummaryTypes)
        {
            bool isWeaponStat = WeaponStatTag.None != WeaponStatTagUtils.FromValueType(type);
            bool notApplied = true == isWeaponStat && null != currentWeapon && false == currentWeapon.IsAffectedBy(type);

            if (0 < builder.Length)
            {
                builder.Append('\n');
            }
            builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, statSummaryLineFormat,
                type.ToDisplayName(),
                FormatSigned(player.GetAdd(type), "0.##"),
                player.GetMultiplier(type),
                (true == notApplied) ? notAppliedText : string.Empty);
        }
        statSummaryText.text = builder.ToString();
    }
    #endregion

    #region Setup
    private void ResolveWeaponHolder()
    {
        if (null != weaponHolder)
        {
            return;
        }

        if (false == string.IsNullOrEmpty(weaponHolderName))
        {
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (weaponHolderName == t.name)
                {
                    weaponHolder = t;
                    return;
                }
            }
        }

        Debug.LogWarning($"[WeaponTestController] '{weaponHolderName}' 를 찾지 못해 플레이어 아래에 무기를 만듭니다.", this);
    }

    private void ResolveLayers()
    {
        bool isLeft = PlayerSide.Left == player.Side;
        attackLayer = LayerMask.NameToLayer(true == isLeft ? LeftAttackLayer : RightAttackLayer);
        monsterLayer = LayerMask.NameToLayer(true == isLeft ? LeftMonsterLayer : RightMonsterLayer);

        if (attackLayer < 0 || monsterLayer < 0)
        {
            Debug.LogWarning($"[WeaponTestController] {player.Side} 무기 레이어를 찾지 못했습니다. (Attack {attackLayer}, Monster {monsterLayer})", this);
        }
    }
    #endregion

    #region UI
    private void BuildWeaponButtons()
    {
        int count = (null != weaponEffects) ? weaponEffects.Length : 0;
        string[] labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            Weapon prefab = GetWeaponPrefab(i);
            labels[i] = (null != prefab) ? prefab.name : noWeaponText;
        }

        BuildButtons(weaponButtonTemplate, weaponButtonContainer, labels, weaponButtons, HandleWeaponButton);

        for (int i = 0; i < weaponButtons.Count; i++)
        {
            if (null == GetWeaponPrefab(i))
            {
                weaponButtons[i].interactable = false;
            }
        }
    }

    private void BuildLayoutButtons()
    {
        int count = (null != layouts) ? layouts.Count : 0;
        string[] labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            labels[i] = (null != layouts[i] && false == string.IsNullOrEmpty(layouts[i].displayName)) ? layouts[i].displayName : i.ToString();
        }

        BuildButtons(layoutButtonTemplate, layoutButtonContainer, labels, layoutButtons, HandleLayoutButton);
    }

    /// <summary>
    /// 템플릿 버튼을 _labels 개수만큼 복제한다 (템플릿은 숨긴다)
    /// </summary>
    private void BuildButtons(Button _template, Transform _container, string[] _labels, List<Button> _result, System.Action<int> _onClick)
    {
        if (null == _template)
        {
            return;
        }

        _template.gameObject.SetActive(false);
        Transform parent = (null != _container) ? _container : _template.transform.parent;

        for (int i = 0; i < _labels.Length; i++)
        {
            Button button = Instantiate(_template, parent);
            button.name = $"{_template.name}_{i}";
            button.gameObject.SetActive(true);

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (null != label)
            {
                label.text = _labels[i];
            }

            int index = i;
            button.onClick.AddListener(() => _onClick(index));
            _result.Add(button);
        }
    }

    private void BindControlButtons()
    {
        if (null != upgradeOnceButton)
        {
            upgradeOnceButton.onClick.AddListener(HandleUpgradeOnce);
        }
        if (null != upgradeMaxButton)
        {
            upgradeMaxButton.onClick.AddListener(HandleUpgradeMax);
        }
        if (null != resetButton)
        {
            resetButton.onClick.AddListener(ResetAll);
        }
        if (null != passiveResetButton)
        {
            passiveResetButton.onClick.AddListener(ResetPassives);
        }
    }

    private void UnbindControlButtons()
    {
        if (null != upgradeOnceButton)
        {
            upgradeOnceButton.onClick.RemoveListener(HandleUpgradeOnce);
        }
        if (null != upgradeMaxButton)
        {
            upgradeMaxButton.onClick.RemoveListener(HandleUpgradeMax);
        }
        if (null != resetButton)
        {
            resetButton.onClick.RemoveListener(ResetAll);
        }
        if (null != passiveResetButton)
        {
            passiveResetButton.onClick.RemoveListener(ResetPassives);
        }
    }

    private void HandleWeaponButton(int _index)
    {
        SelectWeapon(_index);
    }

    private void HandleLayoutButton(int _index)
    {
        SelectLayout(_index);
    }

    private void HandleUpgradeOnce()
    {
        UpgradeOnce();
    }

    private void HandleUpgradeMax()
    {
        UpgradeMax();
    }

    /// <summary>
    /// 강화 단계 / 무기 이름 / 강화 버튼 / 선택 표시 갱신
    /// </summary>
    private void RefreshWeaponUI()
    {
        WeaponUpgradeTable table = GetCurrentTable();
        bool hasTable = null != currentWeapon && null != table && 0 < table.StepCount;
        bool canUpgrade = true == hasTable && currentWeapon.UpgradeLevel < table.StepCount;

        if (null != levelText)
        {
            levelText.text = (true == hasTable) ? string.Format(levelFormat, currentWeapon.UpgradeLevel, table.StepCount) : noTableText;
        }

        if (null != weaponNameText)
        {
            weaponNameText.text = (null != currentWeapon) ? currentWeapon.name : noWeaponText;
        }

        if (null != upgradeOnceButton)
        {
            upgradeOnceButton.interactable = canUpgrade;
        }
        if (null != upgradeMaxButton)
        {
            upgradeMaxButton.interactable = canUpgrade;
        }

        // 선택된 무기 버튼은 눌리지 않게 해서 표시한다
        for (int i = 0; i < weaponButtons.Count; i++)
        {
            weaponButtons[i].interactable = i != currentWeaponIndex && null != GetWeaponPrefab(i);
        }

        // 무기마다 WeaponStatTag 가 달라 "(미적용)" 표시가 바뀐다
        RefreshStatSummary();
    }

    private void RefreshLayoutButtons()
    {
        for (int i = 0; i < layoutButtons.Count; i++)
        {
            layoutButtons[i].interactable = i != currentLayoutIndex;
        }
    }
    #endregion
}
#endif
