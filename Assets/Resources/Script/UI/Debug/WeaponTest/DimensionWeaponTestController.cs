#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 차원 무기 테스트 씬 전용 컨트롤러 (좌 / 우 분할 화면, 실게임과 같은 진영 카메라 + 오버레이 + StageSpace 구성)
/// - 무기 교체 : DimensionFusionRegistry.Recipes 로 버튼을 만들고, 고르면 현재 차원 무기를 파괴한 뒤
///   resultPrefab 을 부모 없이 만들어 Initialize 한다. StageSpace 가 준비될 때까지 생성 / Initialize 를 미룬다
///   (PlayerStats.AddDimensionWeapon 은 해제 기능이 없으므로 쓰지 않고 컨트롤러가 직접 소유한다. 강화 없음)
/// - 더미 : 좌 / 우 진영 모두 각 플레이어 위치 기준 같은 오프셋에 만든다 (좌 LMonster, 우 RMonster).
///   StageTargets 는 진영 카메라 뷰와 겹치는 대상만 잡으므로 오프셋을 카메라 뷰 안으로 자른다
/// - 패시브 : RewardData(StatRewardEffect) 별 좌 / 우 스택. 스택이 바뀌면 그 플레이어 수정치를 지우고
///   그 플레이어 스택 수만큼 RewardData.Apply 로 다시 적용한다 (차원 무기는 양쪽 스탯을 합산)
/// - 초기화 : 무기 재생성, 패시브 스택 0, 플레이어 원위치, 더미 재배치, 데미지 미터 리셋
/// - 무기 교체 / 패시브 변경 / 초기화 시 모든 더미의 데미지 미터를 리셋해 현재 구성의 DPS 를 잰다
///
/// UI 버튼 목록은 템플릿 버튼(비활성)을 복제해서 런타임에 만든다. 라벨은 버튼 자식의 TMP_Text
/// </summary>
public class DimensionWeaponTestController
    : MonoBehaviour
{
    // PlayerStats.AddWeapon 의 side 기반 레이어 규칙과 같은 이름
    private const string LeftAttackLayer = "LAttack";
    private const string RightAttackLayer = "RAttack";
    private const string LeftMonsterLayer = "LMonster";
    private const string RightMonsterLayer = "RMonster";

    private const int SideCount = 2;
    private const int LeftIndex = 0;
    private const int RightIndex = 1;

    [Header("플레이어")]
    [Tooltip("왼쪽 플레이어 (비어 있으면 PlayerStats.Find(Left))")]
    [SerializeField]
    private PlayerStats leftPlayer;

    [Tooltip("오른쪽 플레이어 (비어 있으면 PlayerStats.Find(Right))")]
    [SerializeField]
    private PlayerStats rightPlayer;

    [Tooltip("왼쪽 진영 카메라 (더미 배치를 뷰 안으로 자를 때 사용)")]
    [SerializeField]
    private Camera leftCamera;

    [Tooltip("오른쪽 진영 카메라 (더미 배치를 뷰 안으로 자를 때 사용)")]
    [SerializeField]
    private Camera rightCamera;

    [Tooltip("시작 시 플레이어가 원래 가진 일반 무기와 그 발사체를 지운다 (DPS 측정에 섞이지 않도록)")]
    [SerializeField]
    private bool clearExistingWeapons = true;

    [Header("차원 무기")]
    [Tooltip("합성법 목록 (Recipes 의 resultPrefab 으로 무기를 만든다)")]
    [SerializeField]
    private DimensionFusionRegistry registry;

    [Tooltip("시작 무기 (-1 이면 없음)")]
    [SerializeField]
    private int initialWeaponIndex = 0;

    [Tooltip("패시브가 바뀌면 현재 차원 무기를 다시 만든다 (발사 시점 값을 쓰는 진행 중 연출 정리)")]
    [SerializeField]
    private bool rebuildWeaponOnPassiveChange = true;

    [Header("더미")]
    [Tooltip("배치할 더미 프리팹 (Assets/prefab/Monster/Dummy/Dummy.prefab)")]
    [SerializeField]
    private GameObject dummyPrefab;

    [Tooltip("더미 배치 프리셋 (플레이어 위치 기준 오프셋, 좌 / 우 공통). 기본값은 반쪽 화면에 맞춘 값")]
    [SerializeField]
    private List<WeaponTestDummyLayout> layouts = CreateHalfScreenLayouts();

    [Tooltip("시작 배치 (-1 이면 없음)")]
    [SerializeField]
    private int initialLayoutIndex = 0;

    [Tooltip("배치 위치를 진영 카메라 뷰 안으로 자른다 (뷰 밖 더미는 화면 공간 대상이 아니다)")]
    [SerializeField]
    private bool clampToCameraView = true;

    [Tooltip("카메라 뷰 가장자리에서 안쪽으로 둘 여백 (월드 단위)")]
    [Min(0f)]
    [SerializeField]
    private float cameraViewMargin = 0.8f;

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
    private Button resetButton;

    [Tooltip("현재 무기 이름 표시 (선택)")]
    [SerializeField]
    private TMP_Text weaponNameText;

    [Tooltip("무기가 없을 때 표시")]
    [SerializeField]
    private string noWeaponText = "-";

    [Tooltip("StageSpace 준비 대기 중 무기 이름 뒤에 붙는 표시")]
    [SerializeField]
    private string pendingWeaponText = " (대기)";

    [Header("UI : DPS")]
    [Tooltip("좌 / 우 합계 DPS 표시 (선택)")]
    [SerializeField]
    private TMP_Text dpsText;

    [Tooltip("DPS 포맷 ({0} = 왼쪽 합계, {1} = 오른쪽 합계, {2} = 전체 합계)")]
    [SerializeField]
    private string dpsFormat = "L DPS {0:0.#}\nR DPS {1:0.#}\n합계 {2:0.#}";

    [Tooltip("DPS 표시 갱신 간격 (초, 실시간)")]
    [Min(0.02f)]
    [SerializeField]
    private float dpsRefreshInterval = 0.2f;

    [Header("패시브")]
    [Tooltip("스택할 패시브 목록 (Assets/prefab/Upgrade/PassiveBuff, PassiveNuff 의 StatRewardEffect 보상). effect 가 StatRewardEffect 가 아니면 비활성")]
    [SerializeField]
    private RewardData[] passives;

    [Tooltip("패시브 하나의 최대 스택 (플레이어별)")]
    [Min(1)]
    [SerializeField]
    private int maxPassiveStack = 99;

    [Header("UI : 패시브")]
    [Tooltip("패시브 행 템플릿 (비활성으로 두고 복제)")]
    [SerializeField]
    private DimensionWeaponTestPassiveRow passiveRowTemplate;

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

    [Tooltip("무기 최종 수치 줄 포맷 ({0} = 데미지, {1} = 공격 속도, {2} = 범위, {3} = 발사 수, {4} = 지속 시간)")]
    [SerializeField]
    private string weaponStatLineFormat = "무기  피해 {0:0.#}  공속 {1:0.##}  범위 {2:0.#}  수 {3}  지속 {4:0.#}";

    [Tooltip("스탯 요약 한 줄 포맷 ({0} = 이름, {1} = 왼쪽 ΣAdd, {2} = 왼쪽 배율, {3} = 오른쪽 ΣAdd, {4} = 오른쪽 배율, {5} = 미적용 표시)")]
    [SerializeField]
    private string statSummaryLineFormat = "{0}  L {1} ×{2:0.00}  R {3} ×{4:0.00}{5}";

    [Tooltip("현재 차원 무기의 statTags 에 없는 수치 뒤에 붙는 표시")]
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

    // 진영 하나의 런타임 정보 (0 = Left, 1 = Right)
    private class SideState
    {
        public PlayerSide side;
        public PlayerStats player;
        public Camera camera;
        public Rigidbody2D body;
        public HealthSystem health;
        public Vector3 homePosition;
        public int attackLayer = -1;
        public int monsterLayer = -1;
        public int[] passiveStacks = new int[0];
        public readonly List<DummyEntry> dummies = new List<DummyEntry>();
        public readonly List<WeaponTestDamageMeter> meters = new List<WeaponTestDamageMeter>();
    }

    private readonly SideState[] sides = new SideState[SideCount];

    // Start 가 초기화 실패로 끝나면 null (공개 메서드 가드)
    private bool HasSides => null != sides[LeftIndex] && null != sides[RightIndex];
    private readonly List<Button> weaponButtons = new List<Button>();
    private readonly List<Button> layoutButtons = new List<Button>();
    private readonly List<DimensionWeaponTestPassiveRow> passiveRows = new List<DimensionWeaponTestPassiveRow>();

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
        ValueType.PickupRange,
    };

    // passives 와 같은 순서. 사용 가능 여부 / 라벨용 1스택 변화량 (스택은 SideState 별)
    private bool[] passiveValid = new bool[0];
    private string[] passiveDeltaLabels = new string[0];

    private DimensionWeapon currentWeapon;
    private int currentWeaponIndex = -1;
    // StageSpace 준비 전에 고른 무기 (-1 이면 없음)
    private int pendingWeaponIndex = -1;
    private int currentLayoutIndex = -1;

    private float dpsRefreshTimer;

    // 스탯 요약 문자열 재사용
    private readonly System.Text.StringBuilder summaryBuilder = new System.Text.StringBuilder();

    public DimensionWeapon CurrentWeapon => currentWeapon;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public bool IsWeaponPending => 0 <= pendingWeaponIndex;
    public int CurrentLayoutIndex => currentLayoutIndex;
    public int PassiveCount => passiveValid.Length;
    public int WeaponCount => (null != registry) ? registry.Recipes.Count : 0;

    #region Unity
    private void Start()
    {
        if (null == leftPlayer)
        {
            leftPlayer = PlayerStats.Find(PlayerSide.Left);
        }
        if (null == rightPlayer)
        {
            rightPlayer = PlayerStats.Find(PlayerSide.Right);
        }
        if (null == leftPlayer || null == rightPlayer)
        {
            Debug.LogError($"[DimensionWeaponTestController] 좌 / 우 PlayerStats 가 모두 필요합니다. (Left {leftPlayer}, Right {rightPlayer})", this);
            enabled = false;
            return;
        }
        if (PlayerSide.Left != leftPlayer.Side || PlayerSide.Right != rightPlayer.Side)
        {
            Debug.LogWarning($"[DimensionWeaponTestController] 플레이어 side 가 맞지 않습니다. (left={leftPlayer.Side}, right={rightPlayer.Side}) 차원 무기는 PlayerStats.Find(side) 로 스탯을 읽습니다.", this);
        }
        if (null == registry)
        {
            Debug.LogWarning("[DimensionWeaponTestController] registry 가 연결되지 않았습니다.", this);
        }

        sides[LeftIndex] = CreateSideState(PlayerSide.Left, leftPlayer, leftCamera);
        sides[RightIndex] = CreateSideState(PlayerSide.Right, rightPlayer, rightCamera);

        if (true == clearExistingWeapons)
        {
            foreach (SideState s in sides)
            {
                ClearExistingWeapons(s);
            }
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
        RefreshDpsText();
    }

    private void Update()
    {
        // StageSpace 준비를 기다리던 무기를 만든다
        if (true == IsWeaponPending && true == TryCreatePendingWeapon())
        {
            ResetMeters();
            RefreshWeaponUI();
        }

        dpsRefreshTimer -= Time.unscaledDeltaTime;
        if (dpsRefreshTimer <= 0f)
        {
            dpsRefreshTimer = dpsRefreshInterval;
            RefreshDpsText();
            // 차원 무기 스탯은 프레임 캐시라 이벤트 직후 값이 늦게 반영될 수 있어 주기적으로 다시 그린다
            RefreshStatSummary();
        }
    }

    private void LateUpdate()
    {
        if (false == lockDummyPosition)
        {
            return;
        }

        // 넉백(Knockback 은 Update 에서 이동) 뒤에 배치 위치로 되돌린다
        foreach (SideState s in sides)
        {
            if (null == s)
            {
                continue;
            }

            foreach (DummyEntry d in s.dummies)
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
    }

    private void OnDestroy()
    {
        UnbindControlButtons();
    }
    #endregion

    #region Weapon
    /// <summary>
    /// Recipes[_index] 의 차원 무기로 교체한다 (현재 무기를 지우고 새로 만든다. StageSpace 준비 전이면 준비 후 생성)
    /// </summary>
    public bool SelectWeapon(int _index)
    {
        DimensionWeaponData data = GetWeaponData(_index);
        if (null == data || null == data.ResultPrefab)
        {
            Debug.LogWarning($"[DimensionWeaponTestController] {_index} 번 합성법의 resultPrefab 이 없습니다.", this);
            return false;
        }

        DestroyCurrentWeapon();

        currentWeaponIndex = _index;
        pendingWeaponIndex = _index;
        TryCreatePendingWeapon();

        ResetMeters();
        RefreshWeaponUI();
        return true;
    }

    /// <summary>
    /// 대기 중인 무기를 StageSpace 가 준비됐으면 만들고 Initialize 한다
    /// </summary>
    /// <returns>이번에 만들었으면 true</returns>
    private bool TryCreatePendingWeapon()
    {
        if (false == IsWeaponPending)
        {
            return false;
        }

        StageSpace space = StageSpace.Instance;
        if (null == space || false == space.IsReady)
        {
            return false;
        }

        int index = pendingWeaponIndex;
        pendingWeaponIndex = -1;

        DimensionWeaponData data = GetWeaponData(index);
        if (null == data || null == data.ResultPrefab)
        {
            currentWeaponIndex = -1;
            return false;
        }

        // 실게임(DimensionFusionEffect) 과 같이 부모 없이 만든다. 레이어 / 위치는 Initialize 가 Stage / 화면 중앙으로 맞춘다
        currentWeapon = Instantiate(data.ResultPrefab);
        currentWeapon.Initialize(data);
        currentWeaponIndex = index;
        return true;
    }

    /// <summary>
    /// 현재 무기를 끄고 파괴한다 (발사체 / 연출은 무기 자식이라 함께 사라진다). 대기 중인 무기도 취소
    /// </summary>
    private void DestroyCurrentWeapon()
    {
        if (null != currentWeapon)
        {
            // 풀 해제 / 유지형 연출 정리를 OnDisable 에서 먼저 하도록 끈 뒤 파괴한다
            currentWeapon.gameObject.SetActive(false);
            Destroy(currentWeapon.gameObject);
        }
        currentWeapon = null;
        currentWeaponIndex = -1;
        pendingWeaponIndex = -1;
    }

    /// <summary>
    /// 현재 무기를 다시 만든다 (대기 중이면 그대로 둔다)
    /// </summary>
    private void RebuildCurrentWeapon()
    {
        if (null == currentWeapon || currentWeaponIndex < 0)
        {
            return;
        }

        int index = currentWeaponIndex;
        DestroyCurrentWeapon();
        currentWeaponIndex = index;
        pendingWeaponIndex = index;
        TryCreatePendingWeapon();
    }

    private DimensionWeaponData GetWeaponData(int _index)
    {
        if (null == registry)
        {
            return null;
        }

        IReadOnlyList<DimensionWeaponData> recipes = registry.Recipes;
        if (_index < 0 || _index >= recipes.Count)
        {
            return null;
        }
        return recipes[_index];
    }

    private string GetWeaponLabel(int _index)
    {
        DimensionWeaponData data = GetWeaponData(_index);
        if (null == data)
        {
            return noWeaponText;
        }

        string label = StringTable.Resolve(data.DisplayNameKey);
        return (true == string.IsNullOrEmpty(label)) ? data.name : label;
    }

    /// <summary>
    /// 플레이어가 원래 가진 일반 무기와 그 진영 공격 레이어의 발사체를 지운다
    /// </summary>
    private void ClearExistingWeapons(SideState _side)
    {
        Weapon[] existing = _side.player.GetComponentsInChildren<Weapon>(true);
        foreach (Weapon w in existing)
        {
            // Weapon.OnDisable 에서 유지형 발사체가 풀로 돌아간다
            w.gameObject.SetActive(false);
        }

        if (0 <= _side.attackLayer)
        {
            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsInactive.Include);
            foreach (Projectile p in projectiles)
            {
                if (null != p && _side.attackLayer == p.gameObject.layer)
                {
                    Destroy(p.gameObject);
                }
            }
        }

        foreach (Weapon w in existing)
        {
            Destroy(w.gameObject);
        }
    }
    #endregion

    #region Dummy
    /// <summary>
    /// layouts[_index] 배치로 좌 / 우 더미를 다시 만든다 (기존 더미는 제거, 위치는 각 플레이어 현재 위치 기준)
    /// </summary>
    public bool SelectLayout(int _index)
    {
        if (false == HasSides)
        {
            return false;
        }
        if (null == layouts || _index < 0 || _index >= layouts.Count || null == layouts[_index])
        {
            Debug.LogWarning($"[DimensionWeaponTestController] {_index} 번 배치가 없습니다.", this);
            return false;
        }
        if (null == dummyPrefab)
        {
            Debug.LogWarning("[DimensionWeaponTestController] dummyPrefab 이 연결되지 않았습니다.", this);
            return false;
        }

        Vector2[] offsets = layouts[_index].offsets;
        int count = (null != offsets) ? offsets.Length : 0;

        foreach (SideState s in sides)
        {
            ClearDummies(s);

            Vector3 origin = s.player.transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = ClampToView(s, origin + (Vector3)offsets[i]);
                SpawnDummy(s, position, i);
            }
        }

        currentLayoutIndex = _index;
        RefreshLayoutButtons();
        return true;
    }

    /// <summary>
    /// _position 을 진영 카메라 뷰(여백 제외) 안으로 자른다 (카메라가 없거나 끄면 그대로)
    /// </summary>
    private Vector3 ClampToView(SideState _side, Vector3 _position)
    {
        Camera cam = _side.camera;
        if (false == clampToCameraView || null == cam || false == cam.orthographic)
        {
            return _position;
        }

        Vector3 center = cam.transform.position;
        float halfHeight = Mathf.Max(0f, cam.orthographicSize - cameraViewMargin);
        float halfWidth = Mathf.Max(0f, cam.orthographicSize * cam.aspect - cameraViewMargin);

        _position.x = Mathf.Clamp(_position.x, center.x - halfWidth, center.x + halfWidth);
        _position.y = Mathf.Clamp(_position.y, center.y - halfHeight, center.y + halfHeight);
        _position.z = 0f;
        return _position;
    }

    private void SpawnDummy(SideState _side, Vector3 _position, int _index)
    {
        // Knockback 은 Rigidbody2D 가 없으면 transform.root 를 움직이므로 부모 없이 씬 루트에 만든다
        GameObject go = Instantiate(dummyPrefab, _position, Quaternion.identity);
        go.name = $"{dummyPrefab.name}_{_side.side}_{_index}";

        if (true == disableDummyMovement)
        {
            foreach (Straight s in go.GetComponentsInChildren<Straight>(true))
            {
                s.enabled = false;
            }
        }

        // StageTargets 는 LMonster / RMonster 레이어 대상만 그 진영 대상으로 모은다
        if (0 <= _side.monsterLayer)
        {
            LayerUtils.SetLayer(go, _side.monsterLayer);
        }

        HealthSystem health = go.GetComponentInChildren<HealthSystem>(true);
        if (null == health)
        {
            Debug.LogWarning($"[DimensionWeaponTestController] {go.name} 에 HealthSystem 이 없어 데미지를 잴 수 없습니다.", go);
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
        _side.dummies.Add(entry);
        _side.meters.Add(meter);
    }

    private void ClearDummies(SideState _side)
    {
        foreach (DummyEntry d in _side.dummies)
        {
            if (null != d.root)
            {
                Destroy(d.root);
            }
        }
        _side.dummies.Clear();
        _side.meters.Clear();
    }

    /// <summary>
    /// 좌 / 우 모든 더미의 데미지 미터를 0 부터 다시 잰다
    /// </summary>
    public void ResetMeters()
    {
        foreach (SideState s in sides)
        {
            if (null == s)
            {
                continue;
            }

            foreach (WeaponTestDamageMeter m in s.meters)
            {
                if (null != m)
                {
                    m.ResetMeter();
                }
            }
        }
        RefreshDpsText();
    }

    /// <summary>
    /// _side 진영 더미 DPS 합계
    /// </summary>
    public float GetTotalDps(PlayerSide _side)
    {
        SideState s = sides[PlayerSide.Left == _side ? LeftIndex : RightIndex];
        if (null == s)
        {
            return 0f;
        }

        float total = 0f;
        foreach (WeaponTestDamageMeter m in s.meters)
        {
            if (null != m)
            {
                total += m.Dps;
            }
        }
        return total;
    }

    private void RefreshDpsText()
    {
        if (null == dpsText)
        {
            return;
        }

        float left = GetTotalDps(PlayerSide.Left);
        float right = GetTotalDps(PlayerSide.Right);
        dpsText.text = string.Format(System.Globalization.CultureInfo.InvariantCulture, dpsFormat, left, right, left + right);
    }

    /// <summary>
    /// 반쪽 화면(진영 카메라 뷰 약 ±7 x ±8) 에 들어가는 기본 프리셋 4종 (단일 / 포위 / 군집 / 일렬·산개)
    /// - 컴포넌트를 처음 붙일 때의 초기값이며, 이후에는 인스펙터 값이 우선
    /// </summary>
    public static List<WeaponTestDummyLayout> CreateHalfScreenLayouts()
    {
        List<WeaponTestDummyLayout> result = new List<WeaponTestDummyLayout>();

        // 단일 타겟 : 정면 1개
        result.Add(new WeaponTestDummyLayout("단일", new Vector2[] { new Vector2(3f, 0f) }));

        // 원형 포위 : 플레이어 주위 8개
        result.Add(new WeaponTestDummyLayout("포위", WeaponTestDummyLayout.Ring(Vector2.zero, 2.5f, 8)));

        // 밀집 군집 : 정면 한 지점에 촘촘히 10개
        result.Add(new WeaponTestDummyLayout("군집", WeaponTestDummyLayout.Cluster(new Vector2(4f, 0f), 0.35f, 10)));

        // 일렬 / 산개 : 가로 일렬 5개 + 반쪽 화면 안에 흩어진 6개
        List<Vector2> lineAndFar = new List<Vector2>();
        lineAndFar.AddRange(WeaponTestDummyLayout.Line(new Vector2(1.5f, 0f), new Vector2(1f, 0f), 5));
        lineAndFar.Add(new Vector2(2f, 5f));
        lineAndFar.Add(new Vector2(4.5f, -4.5f));
        lineAndFar.Add(new Vector2(5.5f, 3f));
        lineAndFar.Add(new Vector2(-4f, 3.5f));
        lineAndFar.Add(new Vector2(-5.5f, -2f));
        lineAndFar.Add(new Vector2(0f, -6f));
        result.Add(new WeaponTestDummyLayout("일렬/산개", lineAndFar.ToArray()));

        return result;
    }
    #endregion

    #region Reset
    /// <summary>
    /// 무기 재생성, 패시브 스택 0, 플레이어 원위치, 더미 재배치, 데미지 미터 리셋
    /// </summary>
    public void ResetAll()
    {
        if (false == HasSides)
        {
            return;
        }
        foreach (SideState s in sides)
        {
            ClearPassiveStacks(s);
            ResetPlayerPosition(s);
        }
        RefreshPassiveRows();

        // 대기 중이던 무기도 같은 번호로 다시 고른다
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

    private void ResetPlayerPosition(SideState _side)
    {
        if (null != _side.body)
        {
            _side.body.position = _side.homePosition;
            _side.body.linearVelocity = Vector2.zero;
        }
        _side.player.transform.position = _side.homePosition;
    }
    #endregion

    #region Passive
    /// <summary>
    /// _side 플레이어의 passives[_index] 현재 스택 (범위 밖이면 0)
    /// </summary>
    public int GetPassiveStack(PlayerSide _side, int _index)
    {
        SideState s = GetSide(_side);
        if (null == s || _index < 0 || _index >= s.passiveStacks.Length)
        {
            return 0;
        }
        return s.passiveStacks[_index];
    }

    /// <summary>
    /// _side 플레이어의 passives[_index] 스택을 _stack 으로 바꾼다 (0 ~ maxPassiveStack 으로 자른다)
    /// </summary>
    /// <returns>범위 밖 / 사용 불가 패시브거나 스택이 그대로면 false</returns>
    public bool SetPassiveStack(PlayerSide _side, int _index, int _stack)
    {
        SideState s = GetSide(_side);
        if (null == s || false == IsValidPassive(_index))
        {
            return false;
        }

        int stack = Mathf.Clamp(_stack, 0, maxPassiveStack);
        if (stack == s.passiveStacks[_index])
        {
            return false;
        }

        s.passiveStacks[_index] = stack;
        OnPassivesChanged(s);
        return true;
    }

    /// <summary>
    /// _side 플레이어의 passives[_index] 스택에 _delta 를 더한다 (0 ~ maxPassiveStack 으로 자른다)
    /// </summary>
    public bool AddPassiveStack(PlayerSide _side, int _index, int _delta)
    {
        return SetPassiveStack(_side, _index, GetPassiveStack(_side, _index) + _delta);
    }

    /// <summary>
    /// 좌 / 우 모든 패시브 스택을 0 으로 하고 플레이어 수정치를 지운다 (무기는 유지)
    /// </summary>
    public void ResetPassives()
    {
        bool hadStack = false;
        foreach (SideState s in sides)
        {
            if (true == HasAnyPassiveStack(s))
            {
                hadStack = true;
                ClearPassiveStacks(s);
            }
        }

        // 이미 모두 0 이면 무기 재생성 / 미터 리셋을 하지 않는다
        if (false == hadStack)
        {
            return;
        }

        if (true == rebuildWeaponOnPassiveChange)
        {
            RebuildCurrentWeapon();
        }

        ResetMeters();
        RefreshPassiveRows();
        RefreshWeaponUI();
    }

    private SideState GetSide(PlayerSide _side)
    {
        return sides[PlayerSide.Left == _side ? LeftIndex : RightIndex];
    }

    private bool IsValidPassive(int _index)
    {
        return 0 <= _index && _index < passiveValid.Length && true == passiveValid[_index];
    }

    private static bool HasAnyPassiveStack(SideState _side)
    {
        if (null == _side)
        {
            return false;
        }

        foreach (int stack in _side.passiveStacks)
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
    private static float CaptureHPRatio(SideState _side)
    {
        HealthSystem health = _side.health;
        if (null == health || health.MaxHP <= 0f)
        {
            return -1f;
        }
        return Mathf.Clamp01(health.CurrHP / health.MaxHP);
    }

    private static void RestoreHPRatio(SideState _side, float _ratio)
    {
        HealthSystem health = _side.health;
        if (_ratio < 0f || null == health || true == health.isDead)
        {
            return;
        }

        // 살아 있으면 최소 1 (HealthSystem.HandlePlayerStatChanged 와 같은 규칙)
        float maxHP = health.MaxHP;
        health.CurrHP = Mathf.Min(maxHP, Mathf.Max(1f, maxHP * _ratio));
    }

    /// <summary>
    /// 패시브 사용 가능 여부를 확인하고 1스택 변화량을 잰다 (왼쪽 플레이어로 잰다. 끝나면 양쪽 수정치는 비어 있다)
    /// - 1스택 변화량이 0 인 패시브는 경고 후 비활성
    /// </summary>
    private void InitPassives()
    {
        int count = (null != passives) ? passives.Length : 0;
        passiveValid = new bool[count];
        passiveDeltaLabels = new string[count];
        foreach (SideState s in sides)
        {
            s.passiveStacks = new int[count];
        }

        // 1) 측정 없이 형식만 확인한다
        bool hasCandidate = false;
        for (int i = 0; i < count; i++)
        {
            RewardData data = passives[i];
            if (null == data)
            {
                Debug.LogWarning($"[DimensionWeaponTestController] {i} 번 패시브가 비어 있습니다.", this);
                continue;
            }
            if (false == (data.GetEffect() is StatRewardEffect))
            {
                Debug.LogWarning($"[DimensionWeaponTestController] {data.name} 의 effect 가 StatRewardEffect 가 아니라 비활성 처리합니다.", data);
                continue;
            }
            if (false == data.TryGetKey(out ValueType _) || false == data.TryGetValue(out float _))
            {
                Debug.LogWarning($"[DimensionWeaponTestController] {data.name} 에 key / value 가 없어 비활성 처리합니다.", data);
                continue;
            }

            passiveValid[i] = true;
            hasCandidate = true;
        }

        // 2) 시작 전 양쪽 수정치를 지운다 (씬에서 미리 붙어 있던 수정치도 함께 사라진다)
        SideState measure = sides[LeftIndex];
        float measureRatio = CaptureHPRatio(measure);
        if (true == hasCandidate)
        {
            for (int i = 0; i < count; i++)
            {
                if (false == passiveValid[i])
                {
                    continue;
                }

                RewardData data = passives[i];
                data.TryGetKey(out ValueType key);
                if (false == TryMeasurePassiveDelta(measure, data, key, out string label))
                {
                    Debug.LogWarning($"[DimensionWeaponTestController] {data.name} 의 1스택 변화량이 0 이라 비활성 처리합니다.", data);
                    passiveValid[i] = false;
                    continue;
                }
                passiveDeltaLabels[i] = label;
            }
        }

        measure.player.ResetModifiers();
        RestoreHPRatio(measure, measureRatio);
        ClearPassiveStacks(sides[RightIndex]);
    }

    /// <summary>
    /// 빈 수정치에서 _data 를 한 번 적용해 ΣAdd / ΣMultiply 변화량을 문자열로 만든다
    /// (StatRewardEffect 의 Add / Multiply 모드가 비공개라 실제 적용 결과로 판단한다)
    /// </summary>
    /// <returns>변화량이 0 이면 false</returns>
    private static bool TryMeasurePassiveDelta(SideState _side, RewardData _data, ValueType _key, out string _label)
    {
        _side.player.ResetModifiers();
        _data.Apply(CreateRewardContext(_side));

        // 영구 강화 계층은 ResetModifiers 로 지워지지 않으므로 레벨업분만 본다
        float add = _side.player.GetLevelUpAdd(_key);
        float multiply = _side.player.GetMultiply(_key);

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

    private static RewardContext CreateRewardContext(SideState _side)
    {
        return new RewardContext { Player = _side.player, Side = _side.player.Side };
    }

    /// <summary>
    /// _side 스택을 모두 0 으로 하고 그 플레이어 수정치를 지운다 (무기 재생성 / UI 갱신은 하지 않는다)
    /// </summary>
    private static void ClearPassiveStacks(SideState _side)
    {
        if (null == _side)
        {
            return;
        }

        for (int i = 0; i < _side.passiveStacks.Length; i++)
        {
            _side.passiveStacks[i] = 0;
        }

        if (null != _side.player)
        {
            float hpRatio = CaptureHPRatio(_side);
            _side.player.ResetModifiers();
            RestoreHPRatio(_side, hpRatio);
        }
    }

    private void OnPassivesChanged(SideState _side)
    {
        ApplyPassives(_side);

        if (true == rebuildWeaponOnPassiveChange)
        {
            RebuildCurrentWeapon();
        }

        ResetMeters();
        RefreshPassiveRows();
        RefreshWeaponUI();
    }

    /// <summary>
    /// _side 플레이어 수정치를 지우고 그 플레이어 패시브를 스택 수만큼 실게임과 같은 RewardData.Apply 로 다시 적용한다
    /// </summary>
    private void ApplyPassives(SideState _side)
    {
        float hpRatio = CaptureHPRatio(_side);
        _side.player.ResetModifiers();

        RewardContext context = CreateRewardContext(_side);
        for (int i = 0; i < _side.passiveStacks.Length; i++)
        {
            if (false == passiveValid[i])
            {
                continue;
            }

            for (int s = 0; s < _side.passiveStacks[i]; s++)
            {
                passives[i].Apply(context);
            }
        }

        RestoreHPRatio(_side, hpRatio);
    }

    private void BuildPassiveRows()
    {
        if (null == passiveRowTemplate)
        {
            if (0 < passiveValid.Length)
            {
                Debug.LogWarning("[DimensionWeaponTestController] passiveRowTemplate 이 연결되지 않아 패시브 UI 를 만들지 않습니다.", this);
            }
            return;
        }

        passiveRowTemplate.gameObject.SetActive(false);
        Transform parent = (null != passiveRowContainer) ? passiveRowContainer : passiveRowTemplate.transform.parent;

        for (int i = 0; i < passiveValid.Length; i++)
        {
            DimensionWeaponTestPassiveRow row = Instantiate(passiveRowTemplate, parent);
            row.name = $"{passiveRowTemplate.name}_{i}";
            row.gameObject.SetActive(true);
            row.SetLabel(GetPassiveLabel(i));

            int index = i;
            BindPassiveButton(row.LeftMinusButton, PlayerSide.Left, index, -1);
            BindPassiveButton(row.LeftPlusButton, PlayerSide.Left, index, 1);
            BindPassiveButton(row.RightMinusButton, PlayerSide.Right, index, -1);
            BindPassiveButton(row.RightPlusButton, PlayerSide.Right, index, 1);
            passiveRows.Add(row);
        }

        RefreshPassiveRows();
    }

    private void BindPassiveButton(Button _button, PlayerSide _side, int _index, int _delta)
    {
        if (null != _button)
        {
            _button.onClick.AddListener(() => AddPassiveStack(_side, _index, _delta));
        }
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
        for (int i = 0; i < passiveRows.Count && i < passiveValid.Length; i++)
        {
            bool valid = passiveValid[i];
            foreach (SideState s in sides)
            {
                int stack = s.passiveStacks[i];
                passiveRows[i].SetStack(s.side, string.Format(passiveStackFormat, stack), true == valid && 0 < stack, true == valid && stack < maxPassiveStack);
            }
        }
    }

    /// <summary>
    /// 첫 줄은 현재 차원 무기의 최종 수치(양쪽 합산), 이후 스탯별 좌 / 우 ΣAdd 와 배율.
    /// 현재 무기의 statTags 에 없는 수치는 미적용 표시
    /// </summary>
    private void RefreshStatSummary()
    {
        if (null == statSummaryText || null == sides[LeftIndex] || null == sides[RightIndex])
        {
            return;
        }

        PlayerStats left = sides[LeftIndex].player;
        PlayerStats right = sides[RightIndex].player;
        System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;

        System.Text.StringBuilder builder = summaryBuilder;
        builder.Clear();
        if (null != currentWeapon)
        {
            builder.AppendFormat(culture, weaponStatLineFormat,
                currentWeapon.FinalDamage,
                currentWeapon.FinalAttackSpeed,
                currentWeapon.FinalRange,
                currentWeapon.FinalFireCount,
                currentWeapon.FinalLifetime);
        }

        foreach (ValueType type in SummaryTypes)
        {
            bool isWeaponStat = WeaponStatTag.None != WeaponStatTagUtils.FromValueType(type);
            bool notApplied = true == isWeaponStat && null != currentWeapon && false == currentWeapon.StatTags.Has(type);

            if (0 < builder.Length)
            {
                builder.Append('\n');
            }
            builder.AppendFormat(culture, statSummaryLineFormat,
                type.ToDisplayName(),
                FormatSigned(left.GetAdd(type), "0.##"),
                left.GetMultiplier(type),
                FormatSigned(right.GetAdd(type), "0.##"),
                right.GetMultiplier(type),
                (true == notApplied) ? notAppliedText : string.Empty);
        }
        statSummaryText.text = builder.ToString();
    }
    #endregion

    #region Setup
    private static SideState CreateSideState(PlayerSide _side, PlayerStats _player, Camera _camera)
    {
        SideState s = new SideState();
        s.side = _side;
        s.player = _player;
        s.camera = _camera;
        s.body = _player.GetComponent<Rigidbody2D>();
        s.health = _player.GetComponentInChildren<HealthSystem>(true);
        s.homePosition = _player.transform.position;

        bool isLeft = PlayerSide.Left == _side;
        s.attackLayer = LayerMask.NameToLayer(true == isLeft ? LeftAttackLayer : RightAttackLayer);
        s.monsterLayer = LayerMask.NameToLayer(true == isLeft ? LeftMonsterLayer : RightMonsterLayer);
        if (s.attackLayer < 0 || s.monsterLayer < 0)
        {
            Debug.LogWarning($"[DimensionWeaponTestController] {_side} 레이어를 찾지 못했습니다. (Attack {s.attackLayer}, Monster {s.monsterLayer})");
        }
        if (null == _camera)
        {
            Debug.LogWarning($"[DimensionWeaponTestController] {_side} 진영 카메라가 없어 더미 배치를 뷰 안으로 자르지 않습니다.");
        }
        return s;
    }
    #endregion

    #region UI
    private void BuildWeaponButtons()
    {
        int count = WeaponCount;
        string[] labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            labels[i] = GetWeaponLabel(i);
        }

        BuildButtons(weaponButtonTemplate, weaponButtonContainer, labels, weaponButtons, HandleWeaponButton);
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

    /// <summary>
    /// 무기 이름 / 선택 표시 / 스탯 요약 갱신
    /// </summary>
    private void RefreshWeaponUI()
    {
        if (null != weaponNameText)
        {
            if (null != currentWeapon)
            {
                weaponNameText.text = GetWeaponLabel(currentWeaponIndex);
            }
            else if (true == IsWeaponPending)
            {
                weaponNameText.text = GetWeaponLabel(pendingWeaponIndex) + pendingWeaponText;
            }
            else
            {
                weaponNameText.text = noWeaponText;
            }
        }

        // 선택된 무기 버튼은 눌리지 않게 해서 표시한다
        for (int i = 0; i < weaponButtons.Count; i++)
        {
            DimensionWeaponData data = GetWeaponData(i);
            weaponButtons[i].interactable = i != currentWeaponIndex && null != data && null != data.ResultPrefab;
        }

        // 무기마다 statTags 가 달라 "(미적용)" 표시가 바뀐다
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
