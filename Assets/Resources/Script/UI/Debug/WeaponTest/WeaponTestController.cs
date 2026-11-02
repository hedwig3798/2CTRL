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
/// - 초기화 : 무기 재생성(업그레이드 0), 플레이어 원위치, 더미 재배치, 데미지 미터 리셋
/// - 무기 교체 / 강화 / 초기화 시 모든 더미의 데미지 미터를 리셋해 현재 구성의 DPS 를 잰다
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

    private Weapon currentWeapon;
    private int currentWeaponIndex = -1;
    private int currentLayoutIndex = -1;

    private Vector3 playerHomePosition;
    private Rigidbody2D playerBody;

    private int attackLayer = -1;
    private int monsterLayer = -1;

    public Weapon CurrentWeapon => currentWeapon;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public int CurrentLayoutIndex => currentLayoutIndex;
    public IReadOnlyList<WeaponTestDamageMeter> Meters => meters;

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
    /// 무기 재생성(업그레이드 0), 플레이어 원위치, 더미 재배치, 데미지 미터 리셋
    /// </summary>
    public void ResetAll()
    {
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
