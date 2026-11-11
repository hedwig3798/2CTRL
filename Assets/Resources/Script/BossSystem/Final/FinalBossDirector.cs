using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최종 보스전 진행 (씬에 하나 배치)
/// - GameOverManager 가 모든 웨이브 종료 시 Begin 을 부른다 (활성 Director 가 없으면 기존대로 승리)
/// - Begin: 스폰 정지 → 적 조용히 제거 → EXP 보석 흡수 → 카메라 고정 → 플레이어 화면 고정 → BGM 전환 → 인트로 → 보스 등장
/// - 보스 처치(OnFinalBossDefeated) → GameOverManager.TriggerVictory
/// - 보스 월드 ↔ 진영 월드 변환은 Space(BossSpace)
/// </summary>
public class FinalBossDirector
    : MonoBehaviour
{
    private const string BOSS_LAYER_NAME = "Boss";

    [Header("카메라")]
    [SerializeField]
    private Camera leftCamera;

    [SerializeField]
    private Camera rightCamera;

    [Tooltip("왼쪽 진영 카메라 스택의 URP Overlay 카메라 (보스 레이어만 그림)")]
    [SerializeField]
    private Camera leftOverlayCamera;

    [Tooltip("오른쪽 진영 카메라 스택의 URP Overlay 카메라 (보스 레이어만 그림)")]
    [SerializeField]
    private Camera rightOverlayCamera;

    [Tooltip("보스 월드 무대 중심 (진영 월드와 겹치지 않는 먼 곳)")]
    [SerializeField]
    private Vector2 bossWorldOrigin = new Vector2(0f, 500f);

    [Header("플레이어")]
    [SerializeField]
    private PlayerStats leftPlayer;

    [SerializeField]
    private PlayerStats rightPlayer;

    [Header("보스")]
    [Tooltip("씬 인스턴스(비활성으로 시작) 또는 프리팹")]
    [SerializeField]
    private FinalBoss finalBoss;

    [SerializeField]
    private FinalBossData data;

    [SerializeField]
    private BossHitProxy leftProxy;

    [SerializeField]
    private BossHitProxy rightProxy;

    [Header("기타 (비우면 씬에서 찾는다)")]
    [SerializeField]
    private GameOverManager gameOverManager;

    [SerializeField]
    private Spawner[] spawners;

    [Tooltip("보스 처치 후 결과 화면까지 기다리는 실시간 초 (처치 연출용). 이 동안 게임은 일시정지된다. 0 이면 즉시 승리")]
    [SerializeField]
    [Min(0f)]
    private float victoryDelay = 1.2f;

    private static FinalBossDirector active;

    /// <summary>
    /// 씬의 활성 Director (없으면 null)
    /// </summary>
    public static FinalBossDirector Active => active;

    private BossSpace space;
    private FinalBoss bossInstance;
    private bool hasBegun;
    private bool isIntroPlaying;
    private bool victoryRequested;
    private int bossLayer;
    private readonly Transform[] playerBodies = new Transform[2];
    private readonly HealthSystem[] playerHealth = new HealthSystem[2];
    private readonly DamagePipeline[] playerPipes = new DamagePipeline[2];
    private readonly List<HealthSystem> aliveBuffer = new List<HealthSystem>();
    private readonly List<ExpItem> gemBuffer = new List<ExpItem>();

    public BossSpace Space => space;
    public FinalBossData Data => data;
    public FinalBoss Boss => bossInstance;
    public bool HasBegun => hasBegun;
    public bool IsIntroPlaying => isIntroPlaying;
    public int BossLayer => bossLayer;
    public bool IsGameOver => null != gameOverManager && true == gameOverManager.IsGameOver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        active = null;
    }

    #region Unity
    private void Awake()
    {
        if (null == spawners || 0 == spawners.Length)
        {
            spawners = FindObjectsByType<Spawner>();
        }
        if (null == gameOverManager)
        {
            gameOverManager = FindAnyObjectByType<GameOverManager>();
        }

        // 씬에 놓인 보스는 등장 전까지 꺼 둔다
        if (null != finalBoss && true == finalBoss.gameObject.scene.IsValid())
        {
            finalBoss.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        active = this;
        EventManager events = EventManager.Instance;
        if (null != events)
        {
            events.OnFinalBossDefeated -= HandleDefeated;
            events.OnFinalBossDefeated += HandleDefeated;
        }
    }

    private void OnDisable()
    {
        if (this == active)
        {
            active = null;
        }
        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnFinalBossDefeated -= HandleDefeated;
        }
    }

    private void OnDestroy()
    {
        if (null != bossInstance)
        {
            bossInstance.OnDefeated -= HandleDefeated;
        }
    }
    #endregion

    #region 시작
    /// <summary>
    /// 최종 보스전을 시작한다. 참조가 모자라 시작할 수 없으면 false (호출 측이 기존대로 승리 처리)
    /// </summary>
    public bool Begin()
    {
        if (true == hasBegun)
        {
            return true;
        }
        if (false == CanBegin())
        {
            return false;
        }
        hasBegun = true;

        bossLayer = ResolveBossLayer();
        LayoutOverlays();
        space = new BossSpace(leftOverlayCamera, rightOverlayCamera, leftCamera, rightCamera);
        CachePlayer(PlayerSide.Left, leftPlayer);
        CachePlayer(PlayerSide.Right, rightPlayer);

        StopSpawners();
        RemoveAllEnemies();
        ClearHazards();
        AbsorbAllGems();
        FreezeCamera(leftCamera);
        FreezeCamera(rightCamera);
        ClampPlayer(PlayerSide.Left, leftCamera);
        ClampPlayer(PlayerSide.Right, rightCamera);

        BgmDirector.PlayOverride(data.bossBgm);

        EventManager events = EventManager.Instance;
        if (null != events)
        {
            events.OnFinalBossStart?.Invoke();
            events.OnFinalBossHPChanged?.Invoke(data.maxHP, data.maxHP);
        }

        StartCoroutine(IntroRoutine());
        return true;
    }

    private bool CanBegin()
    {
        if (null == leftPlayer)
        {
            leftPlayer = PlayerStats.Find(PlayerSide.Left);
        }
        if (null == rightPlayer)
        {
            rightPlayer = PlayerStats.Find(PlayerSide.Right);
        }

        if (null == leftCamera || null == rightCamera || null == leftOverlayCamera || null == rightOverlayCamera
            || null == leftPlayer || null == rightPlayer || null == finalBoss || null == data)
        {
            Debug.LogWarning("[FinalBossDirector] 참조가 모자라 최종 보스전을 시작하지 못했습니다. 기존대로 승리 처리합니다.");
            return false;
        }
        return true;
    }

    private static bool layerWarned;

    /// <summary>
    /// 보스 레이어 번호 (없으면 경고 후 Default)
    /// </summary>
    public static int ResolveBossLayer()
    {
        int layer = LayerMask.NameToLayer(BOSS_LAYER_NAME);
        if (layer < 0)
        {
            if (false == layerWarned)
            {
                layerWarned = true;
                Debug.LogWarning("[FinalBossDirector] \"" + BOSS_LAYER_NAME + "\" 레이어가 없습니다. Default 레이어를 씁니다.");
            }
            return 0;
        }
        return layer;
    }

    /// <summary>
    /// 오버레이 두 대를 보스 월드에 배치한다: 무대 중심 bossWorldOrigin, 좌우 뷰 폭 + 가운데 구분선(화면 픽셀 틈을 월드로 환산)
    /// ortho 는 진영 카메라와 같게, 보스 레이어만 그리게 한다 (카메라 스택 등록은 씬에서)
    /// </summary>
    private void LayoutOverlays()
    {
        float leftWidth = leftCamera.orthographicSize * leftCamera.aspect * 2f;
        float rightWidth = rightCamera.orthographicSize * rightCamera.aspect * 2f;
        float gapPixels = rightCamera.pixelRect.xMin - leftCamera.pixelRect.xMax;
        float gap = BossSpace.PixelsToWorld(gapPixels, leftCamera.orthographicSize, leftCamera.pixelRect.height);

        BossSpace.OverlayCenters(bossWorldOrigin, leftWidth, rightWidth, gap, out Vector2 leftCenter, out Vector2 rightCenter);
        PlaceOverlay(leftOverlayCamera, leftCamera, leftCenter);
        PlaceOverlay(rightOverlayCamera, rightCamera, rightCenter);
    }

    private void PlaceOverlay(Camera _overlay, Camera _side, Vector2 _center)
    {
        _overlay.orthographic = true;
        _overlay.orthographicSize = _side.orthographicSize;
        _overlay.cullingMask = 1 << bossLayer;
        _overlay.transform.position = new Vector3(_center.x, _center.y, _side.transform.position.z);
    }

    private void CachePlayer(PlayerSide _side, PlayerStats _stats)
    {
        int i = (int)_side;
        ArrowMovemnt move = _stats.GetComponentInChildren<ArrowMovemnt>(true);
        playerBodies[i] = null != move ? move.transform : _stats.transform;
        playerPipes[i] = BossKeywordUtil.FindTargetPipeline(_stats.transform);
        playerHealth[i] = null != playerPipes[i] ? playerPipes[i].GetComponent<HealthSystem>() : _stats.GetComponentInChildren<HealthSystem>(true);
    }

    private void StopSpawners()
    {
        if (null == spawners)
        {
            return;
        }
        foreach (Spawner spawner in spawners)
        {
            if (null != spawner)
            {
                spawner.StopSpawning();
            }
        }
    }

    /// <summary>
    /// 살아 있는 적을 EXP·OnEnemyDeath·통계 없이 비활성화한다 (풀 몬스터는 OnDisable 에서 풀로 돌아간다)
    /// </summary>
    private void RemoveAllEnemies()
    {
        HealthSystem.CollectAlive(~0, aliveBuffer, true);
        foreach (HealthSystem health in aliveBuffer)
        {
            if (null == health)
            {
                continue;
            }
            if (null != health.GetComponentInParent<BossHitProxy>())
            {
                continue;
            }

            Spawnable spawnable = health.GetComponentInParent<Spawnable>();
            GameObject target = null != spawnable ? spawnable.gameObject : health.gameObject;
            target.SetActive(false);
        }
        aliveBuffer.Clear();
    }

    /// <summary>
    /// 남은 위험물 회수: 몬스터 장판(HazardZone), 중간 보스 예고·지뢰(BossTelegraph, ArmBlast 포함)
    /// 몬스터 투사체는 현재 없다 (몬스터 공격은 즉발 범위 판정 MonsterAreaDamage 와 장판·예고뿐)
    /// </summary>
    private static void ClearHazards()
    {
        HazardZone.DeactivateAll();
        BossTelegraph.ReleaseAll();
    }

    /// <summary>
    /// 땅에 남은 EXP 보석을 그 보석이 보이는 진영의 플레이어가 전부 흡수한다
    /// </summary>
    private void AbsorbAllGems()
    {
        ExpItem.CollectActive(gemBuffer);
        foreach (ExpItem gem in gemBuffer)
        {
            if (null == gem)
            {
                continue;
            }
            int mask = 1 << gem.gameObject.layer;
            PlayerStats owner = 0 != (rightCamera.cullingMask & mask) ? rightPlayer : leftPlayer;
            ItemCollector.Absorb(gem, owner);
        }
        gemBuffer.Clear();
    }

    private static void FreezeCamera(Camera _camera)
    {
        Follow follow = _camera.GetComponent<Follow>();
        if (null != follow)
        {
            follow.enabled = false;
        }
    }

    private void ClampPlayer(PlayerSide _side, Camera _camera)
    {
        PlayerScreenClamp.Attach(playerBodies[(int)_side], _camera, data.playerClampMargin);
    }

    private IEnumerator IntroRoutine()
    {
        isIntroPlaying = true;
        float t = 0f;
        while (t < data.introDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }
        isIntroPlaying = false;
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        bossInstance = true == finalBoss.gameObject.scene.IsValid() ? finalBoss : Instantiate(finalBoss);

        // 승리는 보스 이벤트를 직접 구독해서도 처리한다 (EventManager 구독이 없어도 동작, 중복 호출은 RequestVictory 가 막는다)
        bossInstance.OnDefeated -= HandleDefeated;
        bossInstance.OnDefeated += HandleDefeated;
        LayerUtils.SetLayer(bossInstance.gameObject, bossLayer);
        bossInstance.gameObject.SetActive(true);

        if (null != leftProxy)
        {
            leftProxy.Setup(this, bossInstance, PlayerSide.Left);
        }
        if (null != rightProxy)
        {
            rightProxy.Setup(this, bossInstance, PlayerSide.Right);
        }

        bossInstance.Begin(this, data, bossLayer);
    }
    #endregion

    #region 보스용 API
    /// <summary>
    /// 플레이어의 보스 월드 위치 (없거나 죽었으면 false)
    /// </summary>
    public bool TryGetPlayerBossPos(PlayerSide _side, out Vector2 _bossPos)
    {
        int i = (int)_side;
        _bossPos = Vector2.zero;
        if (null == space || null == playerBodies[i])
        {
            return false;
        }
        HealthSystem health = playerHealth[i];
        if (null != health && true == health.isDead)
        {
            return false;
        }
        _bossPos = space.FromSideWorld(_side, playerBodies[i].position);
        return true;
    }

    /// <summary>
    /// 플레이어에게 데미지 (DamagePipeline 경유, 무적 시간 존중). 공격자는 그 진영 히트 프록시
    /// </summary>
    public void DamagePlayer(PlayerSide _side, float _damage, Vector2 _bossPoint)
    {
        DamagePipeline target = playerPipes[(int)_side];
        if (null == target || null == space)
        {
            return;
        }
        BossHitProxy proxy = PlayerSide.Left == _side ? leftProxy : rightProxy;
        DamagePipeline attacker = null != proxy ? proxy.Pipeline : null;
        BossKeywordUtil.DamageTarget(target, _damage, attacker, space.ToSideWorld(_side, _bossPoint));
    }
    #endregion

    private void HandleDefeated()
    {
        if (null != leftProxy)
        {
            leftProxy.Shutdown();
        }
        if (null != rightProxy)
        {
            rightProxy.Shutdown();
        }

        RequestVictory();
    }

    // 승리 요청은 한 번만 (보스 OnDefeated 와 EventManager.OnFinalBossDefeated 양쪽에서 온다)
    private void RequestVictory()
    {
        if (true == victoryRequested)
        {
            return;
        }
        victoryRequested = true;

        if (null == gameOverManager)
        {
            gameOverManager = FindAnyObjectByType<GameOverManager>();
        }

        // 처치 연출(unscaled)이 결과 화면에 가려지지 않도록 일시정지한 채 잠깐 기다린 뒤 승리
        if (0f < victoryDelay && true == isActiveAndEnabled)
        {
            GamePause.Pause(this);
            StartCoroutine(DelayedVictory());
            return;
        }

        TriggerVictoryNow();
    }

    private IEnumerator DelayedVictory()
    {
        yield return new WaitForSecondsRealtime(victoryDelay);
        // 결과 화면이 자체 일시정지를 걸기 전에 풀어 둔다 (남겨 두면 씬을 다시 시작해도 정지가 유지됨)
        GamePause.Resume(this);
        TriggerVictoryNow();
    }

    private void TriggerVictoryNow()
    {
        if (null != gameOverManager)
        {
            gameOverManager.TriggerVictory();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 디버그: 웨이브와 상관없이 최종 보스전을 시작한다
    /// </summary>
    public bool DebugBegin()
    {
        return Begin();
    }

    /// <summary>
    /// 디버그: 보스 HP 를 최대치의 _ratio 로 맞춘다 (보스 등장 후)
    /// </summary>
    public void DebugSetBossHPRatio(float _ratio)
    {
        if (null != bossInstance)
        {
            bossInstance.DebugSetHPRatio(_ratio);
        }
    }
#endif
}
