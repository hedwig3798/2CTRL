using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 최종 보스: BossSpace 카메라 왕복, FinalBossDirector.Begin 부작용, BossHitProxy, 처치 -> 승리
/// 프리팹·씬 없이 런타임 조립
/// </summary>
public class FinalBossPlayTests
{
    private readonly List<Object> created = new List<Object>();
    private GameOverManager gameOver;
    private FinalBossData data;

    private class Rig
    {
        public Camera leftOverlay;
        public Camera rightOverlay;
        public Camera left;
        public Camera right;
        public PlayerStats leftPlayer;
        public PlayerStats rightPlayer;
        public FinalBossDirector director;
        public FinalBoss boss;
        public BossHitProxy leftProxy;
        public BossHitProxy rightProxy;
    }

    [TearDown]
    public void TearDown()
    {
        if (null != gameOver)
        {
            GamePause.Resume(gameOver);
        }
        Time.timeScale = 1f;
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
        gameOver = null;
        MonsterTestFactory.Cleanup();
    }

    private static void SetField(object _target, string _name, object _value)
    {
        FieldInfo f = _target.GetType().GetField(_name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, _name);
        f.SetValue(_target, _value);
    }

    private GameObject Make(string _name, bool _active = true)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(_active);
        created.Add(go);
        return go;
    }

    private Camera MakeCamera(string _name, Rect _rect, Vector2 _pos, float _size)
    {
        GameObject go = Make(_name);
        go.transform.position = new Vector3(_pos.x, _pos.y, -10f);
        Camera c = go.AddComponent<Camera>();
        c.orthographic = true;
        c.orthographicSize = _size;
        c.rect = _rect;
        return c;
    }

    private PlayerStats MakePlayer(PlayerSide _side, Vector2 _pos)
    {
        GameObject go = Make("Player" + _side, false);
        go.transform.position = _pos;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = 100f;
        hs.currHP = 100f;
        go.AddComponent<DamagePipeline>();
        PlayerStats ps = go.AddComponent<PlayerStats>();
        SetField(ps, "side", _side);
        go.SetActive(true);
        return ps;
    }

    private FinalBossData MakeData()
    {
        FinalBossData d = ScriptableObject.CreateInstance<FinalBossData>();
        created.Add(d);
        d.maxHP = 1000f;
        d.introDuration = 0.05f;
        d.entryDuration = 0.05f;
        d.patternInterval = new[] { 1000f, 1000f, 1000f };
        d.enrageTime = 100000f;
        return d;
    }

    private Rig MakeRig(bool _withProxies)
    {
        Rig r = new Rig();
        r.leftOverlay = MakeCamera("OverlayL", new Rect(0f, 0f, 0.5f, 1f), new Vector2(0f, 500f), 6f);
        r.rightOverlay = MakeCamera("OverlayR", new Rect(0.5f, 0f, 0.5f, 1f), new Vector2(0f, 500f), 6f);
        r.left = MakeCamera("CamL", new Rect(0f, 0f, 0.5f, 1f), new Vector2(-100f, 0f), 6f);
        r.right = MakeCamera("CamR", new Rect(0.5f, 0f, 0.5f, 1f), new Vector2(100f, 0f), 6f);
        r.leftPlayer = MakePlayer(PlayerSide.Left, new Vector2(-100f, 0f));
        r.rightPlayer = MakePlayer(PlayerSide.Right, new Vector2(100f, 0f));

        data = MakeData();

        GameObject bossGo = Make("FinalBoss", false);
        r.boss = bossGo.AddComponent<FinalBoss>();

        GameObject dgo = Make("Director", false);
        r.director = dgo.AddComponent<FinalBossDirector>();
        SetField(r.director, "victoryDelay", 0f); // 기존 테스트는 즉시 승리 기준
        SetField(r.director, "leftCamera", r.left);
        SetField(r.director, "rightCamera", r.right);
        SetField(r.director, "leftOverlayCamera", r.leftOverlay);
        SetField(r.director, "rightOverlayCamera", r.rightOverlay);
        SetField(r.director, "leftPlayer", r.leftPlayer);
        SetField(r.director, "rightPlayer", r.rightPlayer);
        SetField(r.director, "finalBoss", r.boss);
        SetField(r.director, "data", data);
        SetField(r.director, "spawners", new Spawner[] { MonsterTestFactory.CreateSpawner(r.leftPlayer.transform) });

        if (true == _withProxies)
        {
            GameObject lp = Make("ProxyL", false);
            r.leftProxy = lp.AddComponent<BossHitProxy>();
            GameObject rp = Make("ProxyR", false);
            r.rightProxy = rp.AddComponent<BossHitProxy>();
            lp.SetActive(true);
            rp.SetActive(true);
            SetField(r.director, "leftProxy", r.leftProxy);
            SetField(r.director, "rightProxy", r.rightProxy);
        }

        GameObject gmo = Make("GameOverMgr", false);
        gameOver = gmo.AddComponent<GameOverManager>();
        SetSpawnersEmpty(gameOver);
        gmo.SetActive(true);
        SetField(r.director, "gameOverManager", gameOver);

        r.director.gameObject.SetActive(true);
        return r;
    }

    private static void SetSpawnersEmpty(GameOverManager _gm)
    {
        SetField(_gm, "spawners", new Spawner[0]);
    }

    private static IEnumerator WaitUntil(System.Func<bool> _cond, float _timeout = 5f)
    {
        float t = 0f;
        while (false == _cond() && t < _timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    #region BossSpace 카메라 왕복
    [UnityTest]
    public IEnumerator BossSpace_SideWorldRoundTrip_WithSplitCameras()
    {
        Camera l = MakeCamera("CamL", new Rect(0f, 0f, 0.5f, 1f), new Vector2(-100f, 0f), 6f);
        Camera rr = MakeCamera("CamR", new Rect(0.5f, 0f, 0.5f, 1f), new Vector2(100f, 0f), 6f);
        float width = l.orthographicSize * l.aspect * 2f;
        BossSpace.OverlayCenters(new Vector2(0f, 500f), width, width, 0f, out Vector2 lc, out Vector2 rc);
        Camera ovL = MakeCamera("OverlayL", new Rect(0f, 0f, 0.5f, 1f), lc, 6f);
        Camera ovR = MakeCamera("OverlayR", new Rect(0.5f, 0f, 0.5f, 1f), rc, 6f);
        yield return null;

        BossSpace space = new BossSpace(ovL, ovR, l, rr);
        Vector2[] pts = { new Vector2(0f, 0f), new Vector2(-3f, 2f), new Vector2(5f, -4f) };
        foreach (PlayerSide side in new[] { PlayerSide.Left, PlayerSide.Right })
        {
            foreach (Vector2 p in pts)
            {
                Vector2 world = space.ToSideWorld(side, p);
                Vector2 back = space.FromSideWorld(side, world);
                Assert.AreEqual(p.x, back.x, 1e-2f, side + " x");
                Assert.AreEqual(p.y, back.y, 1e-2f, side + " y");
            }
        }

        // 좌측 반쪽 왼쪽 끝 -> 좌측 카메라 월드 왼쪽 끝
        Rect leftSide = space.SideRect(PlayerSide.Left);
        Vector2 w = space.ToSideWorld(PlayerSide.Left, new Vector2(leftSide.xMin, leftSide.center.y));
        Assert.AreEqual(-100f - l.orthographicSize * l.aspect, w.x, 1e-2f);

        Assert.IsTrue(space.TryToSideWorld(leftSide.center, out PlayerSide s1, out Vector2 _));
        Assert.AreEqual(PlayerSide.Left, s1);
        Rect rightSide = space.SideRect(PlayerSide.Right);
        Assert.IsTrue(space.TryToSideWorld(rightSide.center, out PlayerSide s2, out Vector2 _));
        Assert.AreEqual(PlayerSide.Right, s2);
        Assert.IsFalse(space.TryToSideWorld(new Vector2(9999f, 0f), out PlayerSide _, out Vector2 _));
        Assert.Greater(space.SideScale(PlayerSide.Left), 0f);
    }
    #endregion

    #region Director.Begin
    [UnityTest]
    public IEnumerator Begin_RemovesEnemiesSilently_AbsorbsGems_FreezesFollow_Clamps()
    {
        Rig r = MakeRig(false);

        // 활성 적: OnEnemyDeath 가 없어야 한다
        GameObject enemy = Make("Enemy", false);
        enemy.AddComponent<CircleCollider2D>();
        HealthSystem eh = enemy.AddComponent<HealthSystem>();
        eh.maxHP = 10f;
        eh.currHP = 10f;
        enemy.AddComponent<DamagePipeline>();
        enemy.SetActive(true);

        // EXP 보석
        GameObject gemGo = Make("Gem");
        ExpItem gem = gemGo.AddComponent<ExpItem>();
        SetField(gem, "baseExp", 7f);
        SetField(gem, "expRate", 1f);
        GameObject expGo = Make("ExpSystem");
        expGo.AddComponent<ExpSystem>();
        yield return null;

        // 카메라 Follow
        Follow follow = r.left.gameObject.AddComponent<Follow>();
        Assert.IsTrue(follow.enabled);

        int deathCount = 0;
        float expGained = 0f;
        System.Action<GameObject> onDeath = _ => ++deathCount;
        System.Action<PlayerSide, float> onExp = (_s, _e) => expGained += _e;
        EventManager.Instance.OnEnemyDeath += onDeath;
        EventManager.Instance.OnExpGained += onExp;
        int startCount = 0;
        System.Action onStart = () => ++startCount;
        EventManager.Instance.OnFinalBossStart += onStart;

        // 플레이어를 화면 밖으로
        r.leftPlayer.transform.position = new Vector3(-100f + 500f, 300f, 0f);

        LogAssert.ignoreFailingMessages = true;
        bool began = r.director.Begin();
        LogAssert.ignoreFailingMessages = false;
        Assert.IsTrue(began);
        Assert.IsTrue(r.director.HasBegun);
        Assert.IsTrue(r.director.Begin(), "second Begin is idempotent");

        Assert.IsFalse(enemy.activeSelf, "enemy deactivated");
        Assert.AreEqual(0, deathCount, "no OnEnemyDeath");
        Assert.IsFalse(gemGo.activeSelf, "gem absorbed");
        Assert.AreEqual(7f, expGained, 1e-3f, "exp event from absorbed gem");
        Assert.IsFalse(follow.enabled, "Follow disabled");
        Assert.AreEqual(1, startCount, "OnFinalBossStart once");

        yield return null;
        yield return null;
        Rect view = BossMath.ViewRect(r.left.transform.position, r.left.orthographicSize, r.left.aspect, data.playerClampMargin);
        Vector3 p = r.leftPlayer.transform.position;
        Assert.LessOrEqual(p.x, view.xMax + 1e-3f);
        Assert.GreaterOrEqual(p.x, view.xMin - 1e-3f);
        Assert.LessOrEqual(p.y, view.yMax + 1e-3f);
        Assert.GreaterOrEqual(p.y, view.yMin - 1e-3f);

        EventManager.Instance.OnEnemyDeath -= onDeath;
        EventManager.Instance.OnExpGained -= onExp;
        EventManager.Instance.OnFinalBossStart -= onStart;
    }

    [UnityTest]
    public IEnumerator Begin_MissingReferences_ReturnsFalse()
    {
        GameObject dgo = Make("Director", false);
        FinalBossDirector d = dgo.AddComponent<FinalBossDirector>();
        SetField(d, "spawners", new Spawner[] { MonsterTestFactory.CreateSpawner(dgo.transform) });
        dgo.SetActive(true);
        yield return null;
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("FinalBossDirector"));
        Assert.IsFalse(d.Begin());
        Assert.IsFalse(d.HasBegun);
    }
    #endregion

    #region BossHitProxy
    [UnityTest]
    public IEnumerator Proxy_Damage_ReducesBossHP_ProxySurvives_ExecuteIsNormalDamage()
    {
        Rig r = MakeRig(true);
        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(r.director.Begin());
        yield return WaitUntil(() => null != r.director.Boss && true == r.director.Boss.IsVulnerable);
        Assert.IsTrue(r.boss.IsVulnerable, "boss became vulnerable");
        yield return WaitUntil(() => true == r.leftProxy.IsLive || true == r.rightProxy.IsLive);

        BossHitProxy proxy = true == r.leftProxy.IsLive ? r.leftProxy : r.rightProxy;
        Assert.IsTrue(proxy.IsLive, "proxy live when boss overlaps side");

        float before = r.boss.CurrHP;
        DamageMassage msg = new DamageMassage();
        msg.damage = 50f;
        proxy.Pipeline.ProcessDamage(ref msg);
        Assert.AreEqual(before - 50f, r.boss.CurrHP, 1e-3f, "boss HP reduced");

        HealthSystem ph = proxy.GetComponent<HealthSystem>();
        Assert.IsFalse(ph.isDead, "proxy not dead");

        // 즉사 면역: 일반 데미지로 처리
        DamageMassage ex = new DamageMassage();
        ex.damage = 5f;
        ph.Execute(ref ex);
        Assert.IsFalse(ph.isDead);
        Assert.IsFalse(r.boss.IsDead);
        Assert.AreEqual(before - 55f, r.boss.CurrHP, 1e-3f, "execute = normal damage");

        // 거대한 데미지에도 프록시는 죽지 않는다
        DamageMassage big = new DamageMassage();
        big.damage = 10f;
        for (int i = 0; i < 3; ++i)
        {
            proxy.Pipeline.ProcessDamage(ref big);
        }
        Assert.IsFalse(ph.isDead);
        LogAssert.ignoreFailingMessages = false;
    }
    #endregion

    #region 처치 -> 승리
    [UnityTest]
    public IEnumerator BossDefeated_TriggersVictory()
    {
        Rig r = MakeRig(true);
        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(r.director.Begin());
        yield return WaitUntil(() => true == r.boss.IsVulnerable);
        Assert.IsTrue(r.boss.IsVulnerable);
        Assert.IsFalse(gameOver.IsGameOver);

        r.boss.ApplyDamage(r.boss.MaxHP + 1f);
        Assert.IsTrue(r.boss.IsDead);
        Assert.IsTrue(gameOver.IsGameOver);
        Assert.IsTrue(gameOver.IsVictory);
        yield return null;
        Assert.IsFalse(r.leftProxy.IsLive, "proxy shut down");
        Assert.IsFalse(r.rightProxy.IsLive);
        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator BossDefeated_WithVictoryDelay_PausesThenVictory()
    {
        Rig r = MakeRig(true);
        SetField(r.director, "victoryDelay", 0.3f);
        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(r.director.Begin());
        yield return WaitUntil(() => true == r.boss.IsVulnerable);

        r.boss.ApplyDamage(r.boss.MaxHP + 1f);
        Assert.IsTrue(r.boss.IsDead);
        Assert.IsFalse(gameOver.IsGameOver, "결과 화면은 처치 연출 뒤");
        Assert.IsTrue(GamePause.IsPaused, "대기 중 일시정지");

        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsTrue(gameOver.IsVictory);
        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator BossPhase_FollowsHP()
    {
        Rig r = MakeRig(false);
        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(r.director.Begin());
        yield return WaitUntil(() => true == r.boss.IsVulnerable);
        Assert.AreEqual(0, r.boss.Phase);
        r.boss.ApplyDamage(r.boss.MaxHP * 0.35f);
        Assert.AreEqual(1, r.boss.Phase);
        r.boss.ApplyDamage(r.boss.MaxHP * 0.33f);
        Assert.AreEqual(2, r.boss.Phase);
        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator WavesFinished_WithoutDirector_TriggersVictoryAsBefore()
    {
        GameObject gmo = Make("GameOverMgr", false);
        gameOver = gmo.AddComponent<GameOverManager>();
        SetSpawnersEmpty(gameOver);
        gmo.SetActive(true);
        yield return null;
        Assert.IsNull(FinalBossDirector.Active);

        MethodInfo m = typeof(GameOverManager).GetMethod("HandleWavesFinished", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m);
        LogAssert.ignoreFailingMessages = true;
        m.Invoke(gameOver, new object[] { null });
        LogAssert.ignoreFailingMessages = false;
        Assert.IsTrue(gameOver.IsGameOver);
        Assert.IsTrue(gameOver.IsVictory);
    }

    [UnityTest]
    public IEnumerator WavesFinished_WithDirector_StartsBossFightInsteadOfVictory()
    {
        Rig r = MakeRig(false);
        yield return null;
        Assert.AreSame(r.director, FinalBossDirector.Active);

        MethodInfo m = typeof(GameOverManager).GetMethod("HandleWavesFinished", BindingFlags.NonPublic | BindingFlags.Instance);
        LogAssert.ignoreFailingMessages = true;
        m.Invoke(gameOver, new object[] { null });
        LogAssert.ignoreFailingMessages = false;
        Assert.IsTrue(r.director.HasBegun);
        Assert.IsFalse(gameOver.IsGameOver, "no victory yet");
    }
    #endregion

    #region 리뷰 수정 재테스트
    [UnityTest]
    public IEnumerator Begin_ReleasesHazardZonesAndTelegraphs_WithoutBlast()
    {
        Rig r = MakeRig(false);

        GameObject hz = Make("Hazard", false);
        hz.AddComponent<CircleCollider2D>().isTrigger = true;
        HazardZone zone = hz.AddComponent<HazardZone>();
        hz.SetActive(true);
        zone.Activate(new Vector2(500f, 500f), 1f, 30f, 0.5f, 1f, ~0);
        Assert.IsTrue(hz.activeSelf);

        BossTelegraphStyle style = ScriptableObject.CreateInstance<BossTelegraphStyle>();
        created.Add(style);
        BossTelegraph plain = BossTelegraph.Spawn(style, 0, new Vector2(500f, 500f));
        BossTelegraph mine = BossTelegraph.Spawn(style, 0, new Vector2(510f, 500f));
        GameObject targetGo = Make("MineTarget", false);
        targetGo.layer = MonsterTestFactory.TargetLayer;
        targetGo.transform.position = new Vector3(510f, 500f, 0f);
        targetGo.AddComponent<CircleCollider2D>().radius = 0.3f;
        HealthSystem th = targetGo.AddComponent<HealthSystem>();
        th.maxHP = 50f;
        th.currHP = 50f;
        targetGo.AddComponent<DamagePipeline>();
        targetGo.SetActive(true);
        Physics2D.SyncTransforms();
        mine.ArmBlast(0.1f, 3f, 10f, MonsterTestFactory.TargetMask, null, Color.red);

        int blasts = 0;
        System.Action<Vector2, float> onBlast = (_c, _r) => ++blasts;
        BossTelegraph.AnyBlast += onBlast;

        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(r.director.Begin());
        LogAssert.ignoreFailingMessages = false;

        Assert.IsFalse(hz.activeSelf, "hazard zone released");
        Assert.IsFalse(plain.gameObject.activeSelf, "telegraph released");
        Assert.IsFalse(mine.gameObject.activeSelf, "armed mine released");

        yield return new WaitForSecondsRealtime(0.4f);
        BossTelegraph.AnyBlast -= onBlast;
        Assert.AreEqual(0, blasts, "no blast");
        Assert.AreEqual(50f, th.CurrHP, 1e-3f, "no mine damage");
    }

    [UnityTest]
    public IEnumerator SchedulerStopAndRemoveAll_WithWarnedUnspawned_PublishesWarningCancelled()
    {
        GameObject go = Make("SchedGo", false);
        MidBossScheduler sched = go.AddComponent<MidBossScheduler>();
        SetField(sched, "warned", new[] { true });
        SetField(sched, "spawned", new[] { false });

        int cancelled = 0;
        System.Action<PlayerSide> h = _s => ++cancelled;
        EventManager.Instance.OnBossWarningCancelled += h;
        sched.StopAndRemoveAll();
        Assert.AreEqual(1, cancelled);

        // 경고 안 한/이미 출현한 보스만 있으면 발행 없음
        SetField(sched, "warned", new[] { true, false });
        SetField(sched, "spawned", new[] { true, false });
        sched.StopAndRemoveAll();
        Assert.AreEqual(1, cancelled);
        EventManager.Instance.OnBossWarningCancelled -= h;
        yield return null;
    }

    [UnityTest]
    public IEnumerator BossDeath_WithoutEventManagerSubscribers_StillVictoryOnce()
    {
        Rig r = MakeRig(true);
        LogAssert.ignoreFailingMessages = true;
        Assert.IsTrue(r.director.Begin());
        yield return WaitUntil(() => true == r.boss.IsVulnerable);
        Assert.IsTrue(r.boss.IsVulnerable);

        int overs = 0;
        gameOver.OnGameOver += () => ++overs;
        EventManager.Instance.OnFinalBossDefeated = null; // 구독 전부 제거

        r.boss.ApplyDamage(r.boss.MaxHP + 1f);
        Assert.IsTrue(gameOver.IsVictory);
        Assert.AreEqual(1, overs, "victory once");

        r.boss.ApplyDamage(10f);
        yield return null;
        Assert.AreEqual(1, overs);
        LogAssert.ignoreFailingMessages = false;
    }

    [UnityTest]
    public IEnumerator WavesFinished_AfterGameOver_DoesNotBeginBossFight()
    {
        Rig r = MakeRig(false);
        yield return null;
        LogAssert.ignoreFailingMessages = true;
        gameOver.TriggerGameOver(PlayerSide.Left);
        MethodInfo m = typeof(GameOverManager).GetMethod("HandleWavesFinished", BindingFlags.NonPublic | BindingFlags.Instance);
        m.Invoke(gameOver, new object[] { null });
        LogAssert.ignoreFailingMessages = false;
        Assert.IsFalse(r.director.HasBegun);
        Assert.IsFalse(gameOver.IsVictory);
    }
    #endregion
}
