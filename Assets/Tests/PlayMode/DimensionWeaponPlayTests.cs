using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 차원 무기 PlayMode 테스트 (프리팹·씬 없이 코드로 카메라 4대 + StageSpace + 플레이어 2 + 적 더미를 구성)
/// StageSpace 변환 / StageTargets 재구성 / 합성 Apply / 무기 6종 스모크·일시정지 / 데미지 통계 이벤트
/// </summary>
public class DimensionWeaponPlayTests
{
    private const float CAM_SIZE = 5f;
    private static readonly Vector2 LeftCamPos = new Vector2(-100f, 0f);
    private static readonly Vector2 RightCamPos = new Vector2(100f, 0f);

    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    private Camera leftCam;
    private Camera rightCam;
    private Camera leftOverlay;
    private Camera rightOverlay;
    private StageSpace space;
    private PlayerStats leftPlayer;
    private PlayerStats rightPlayer;

    private class TestDimensionWeapon
        : DimensionWeapon
    {
        protected override void Fire()
        {
        }
    }

    #region 구성
    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        for (int i = created.Count - 1; i >= 0; --i)
        {
            if (null != created[i])
            {
                UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
        space = null;
    }

    private T Track<T>(T _o) where T : UnityEngine.Object
    {
        created.Add(_o);
        return _o;
    }

    private GameObject Make(string _name, bool _active = true)
    {
        GameObject go = Track(new GameObject(_name));
        go.SetActive(_active);
        return go;
    }

    private static void SetField(object _target, string _name, object _value)
    {
        Type t = _target.GetType();
        FieldInfo f = null;
        while (null == f && null != t)
        {
            f = t.GetField(_name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            t = t.BaseType;
        }
        Assert.IsNotNull(f, "field " + _name);
        f.SetValue(_target, _value);
    }

    private Camera MakeCamera(string _name, Rect _rect, Vector2 _pos)
    {
        GameObject go = Make(_name);
        go.transform.position = new Vector3(_pos.x, _pos.y, -10f);
        Camera c = go.AddComponent<Camera>();
        c.orthographic = true;
        c.orthographicSize = CAM_SIZE;
        c.rect = _rect;
        return c;
    }

    private PlayerStats MakePlayer(PlayerSide _side, Vector2 _pos)
    {
        GameObject go = Make("Player" + _side, false);
        go.transform.position = _pos;
        PlayerStats ps = go.AddComponent<PlayerStats>();
        SetField(ps, "side", _side);
        go.SetActive(true);
        return ps;
    }

    /// <summary>
    /// 카메라 4대 + StageSpace (+ 플레이어 2). StageSpace 는 Start 에서 배치되므로 호출 뒤 한 프레임 기다릴 것
    /// </summary>
    private void BuildRig(bool _withPlayers = true)
    {
        leftCam = MakeCamera("CamL", new Rect(0f, 0f, 0.49f, 1f), LeftCamPos);
        rightCam = MakeCamera("CamR", new Rect(0.51f, 0f, 0.49f, 1f), RightCamPos);
        leftOverlay = MakeCamera("OverlayL", new Rect(0f, 0f, 0.49f, 1f), new Vector2(0f, 500f));
        rightOverlay = MakeCamera("OverlayR", new Rect(0.51f, 0f, 0.49f, 1f), new Vector2(0f, 500f));

        if (true == _withPlayers)
        {
            leftPlayer = MakePlayer(PlayerSide.Left, LeftCamPos);
            rightPlayer = MakePlayer(PlayerSide.Right, RightCamPos);
        }

        GameObject go = Make("StageSpace", false);
        space = go.AddComponent<StageSpace>();
        SetField(space, "leftCamera", leftCam);
        SetField(space, "rightCamera", rightCam);
        SetField(space, "leftOverlayCamera", leftOverlay);
        SetField(space, "rightOverlayCamera", rightOverlay);
        go.SetActive(true);
    }

    private IEnumerator BuildRigReady(bool _withPlayers = true)
    {
        BuildRig(_withPlayers);
        yield return null;
        yield return null;
        Assert.IsTrue(space.IsReady, "StageSpace ready");
    }

    private HealthSystem MakeEnemy(PlayerSide _side, Vector2 _world, float _hp = 1000000f, int _layer = -1)
    {
        GameObject go = Make("Enemy" + _side, false);
        go.layer = _layer >= 0 ? _layer : LayerMask.NameToLayer(PlayerSide.Left == _side ? "LMonster" : "RMonster");
        go.transform.position = _world;
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = _hp;
        hs.currHP = _hp;
        go.AddComponent<DamagePipeline>();
        go.SetActive(true);
        return hs;
    }

    private static Vector2 CamPos(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? LeftCamPos : RightCamPos;
    }

    private static float HalfWidth(Camera _cam)
    {
        return _cam.orthographicSize * _cam.aspect;
    }

    /// <summary>
    /// 양쪽 화면 안에 격자로 적을 둔다
    /// </summary>
    private List<HealthSystem> MakeEnemyGrid(float _hp = 1000000f)
    {
        List<HealthSystem> list = new List<HealthSystem>();
        foreach (PlayerSide side in new[] { PlayerSide.Left, PlayerSide.Right })
        {
            Camera cam = PlayerSide.Left == side ? leftCam : rightCam;
            float hw = HalfWidth(cam) * 0.7f;
            Vector2 c = CamPos(side);
            for (int ix = -2; ix <= 2; ++ix)
            {
                for (int iy = -1; iy <= 1; ++iy)
                {
                    Vector2 p = c + new Vector2(ix * hw * 0.5f, iy * 2.5f);
                    if (Mathf.Abs(ix) + Mathf.Abs(iy) == 0)
                    {
                        p += new Vector2(0.8f, 0.8f); // 플레이어 위 겹침 회피
                    }
                    list.Add(MakeEnemy(side, p, _hp));
                }
            }
        }
        return list;
    }

    private static float TotalHp(List<HealthSystem> _list)
    {
        float sum = 0f;
        foreach (HealthSystem h in _list)
        {
            sum += h.CurrHP;
        }
        return sum;
    }

    private static bool TreeContains(StageQuadTree _tree, HealthSystem _h)
    {
        List<StageTarget> all = new List<StageTarget>();
        _tree.QueryRect(_tree.Bounds, all);
        foreach (StageTarget t in all)
        {
            if (t.health == _h)
            {
                return true;
            }
        }
        return false;
    }

    private static int TreeCount(StageQuadTree _tree, HealthSystem _h)
    {
        List<StageTarget> all = new List<StageTarget>();
        _tree.QueryRect(_tree.Bounds, all);
        int n = 0;
        foreach (StageTarget t in all)
        {
            if (t.health == _h)
            {
                ++n;
            }
        }
        return n;
    }
    #endregion

    #region 1. StageSpace 변환
    [UnityTest]
    public IEnumerator StageSpace_RoundTrip_SurvivesCameraMovement()
    {
        yield return BuildRigReady(false);

        Vector2[] points = { Vector2.zero, new Vector2(1.5f, -2f), new Vector2(-3f, 4f) };
        foreach (PlayerSide side in new[] { PlayerSide.Left, PlayerSide.Right })
        {
            Camera cam = PlayerSide.Left == side ? leftCam : rightCam;
            foreach (Vector2 offset in points)
            {
                Vector2 world = CamPos(side) + offset;
                Vector2 stage = space.WorldToStage(side, world);
                Vector2 back = space.StageToWorld(side, stage);
                Assert.AreEqual(world.x, back.x, 1e-3f, side + " x");
                Assert.AreEqual(world.y, back.y, 1e-3f, side + " y");
            }

            // 카메라를 따라 움직이는 점은 화면 공간 위치가 그대로
            Vector2 w0 = CamPos(side) + new Vector2(1f, 1f);
            Vector2 stage0 = space.WorldToStage(side, w0);
            Vector2 delta = new Vector2(7f, -3f);
            cam.transform.position += (Vector3)delta;
            Vector2 stage1 = space.WorldToStage(side, w0 + delta);
            Assert.AreEqual(stage0.x, stage1.x, 1e-3f, side + " follow x");
            Assert.AreEqual(stage0.y, stage1.y, 1e-3f, side + " follow y");

            // 움직인 뒤에도 왕복 일치
            Vector2 w2 = (Vector2)cam.transform.position + new Vector2(-2f, 2f);
            Vector2 back2 = space.StageToWorld(side, space.WorldToStage(side, w2));
            Assert.AreEqual(w2.x, back2.x, 1e-3f);
            Assert.AreEqual(w2.y, back2.y, 1e-3f);
        }
    }

    [UnityTest]
    public IEnumerator StageSpace_LeftAndRightMapToDifferentHalves()
    {
        yield return BuildRigReady(false);
        Vector2 l = space.WorldToStage(PlayerSide.Left, LeftCamPos);
        Vector2 r = space.WorldToStage(PlayerSide.Right, RightCamPos);
        Assert.Less(l.x, space.StageCenter.x);
        Assert.Greater(r.x, space.StageCenter.x);
        Assert.AreEqual(l.y, r.y, 1e-3f);
    }

    [UnityTest]
    public IEnumerator StageSpace_IsOnScreen_InsideTrue_OutsideFalse_FollowsCamera()
    {
        yield return BuildRigReady(false);
        float hw = HalfWidth(leftCam);
        Assert.IsTrue(space.IsOnScreen(PlayerSide.Left, LeftCamPos));
        Assert.IsTrue(space.IsOnScreen(PlayerSide.Left, LeftCamPos + new Vector2(hw - 0.05f, CAM_SIZE - 0.05f)));
        Assert.IsFalse(space.IsOnScreen(PlayerSide.Left, LeftCamPos + new Vector2(hw + 0.5f, 0f)));
        Assert.IsFalse(space.IsOnScreen(PlayerSide.Left, LeftCamPos + new Vector2(0f, CAM_SIZE + 0.5f)));

        Vector2 far = LeftCamPos + new Vector2(30f, 0f);
        Assert.IsFalse(space.IsOnScreen(PlayerSide.Left, far));
        leftCam.transform.position += new Vector3(30f, 0f, 0f);
        Assert.IsTrue(space.IsOnScreen(PlayerSide.Left, far), "camera moved to it");
    }

    [UnityTest]
    public IEnumerator StageSpace_Targets_ExcludesOffScreenEnemy_IncludesWhenCameraArrives()
    {
        yield return BuildRigReady(false);
        float hw = HalfWidth(leftCam);
        HealthSystem inside = MakeEnemy(PlayerSide.Left, LeftCamPos + new Vector2(1f, 1f));
        HealthSystem outside = MakeEnemy(PlayerSide.Left, LeftCamPos + new Vector2(hw + 3f, 0f));
        yield return null;
        yield return null;

        StageQuadTree tree = StageSpace.Targets;
        Assert.IsNotNull(tree);
        Assert.IsTrue(TreeContains(tree, inside), "inside");
        Assert.IsFalse(TreeContains(tree, outside), "outside excluded");

        leftCam.transform.position += new Vector3(hw + 3f, 0f, 0f);
        yield return null;
        yield return null;
        Assert.IsTrue(TreeContains(StageSpace.Targets, outside), "camera arrived");
        Assert.IsFalse(TreeContains(StageSpace.Targets, inside), "now outside");
    }
    #endregion

    #region 2. StageTargets 재구성
    [UnityTest]
    public IEnumerator StageTargets_BothSidesCaptured_WithSideAndStagePos()
    {
        yield return BuildRigReady(false);
        HealthSystem le = MakeEnemy(PlayerSide.Left, LeftCamPos + new Vector2(1f, 0f));
        HealthSystem re = MakeEnemy(PlayerSide.Right, RightCamPos + new Vector2(-1f, 0f));
        yield return null;
        yield return null;

        List<StageTarget> all = new List<StageTarget>();
        StageSpace.Targets.QueryRect(StageSpace.Targets.Bounds, all);
        StageTarget? lt = null;
        StageTarget? rt = null;
        foreach (StageTarget t in all)
        {
            if (t.health == le) { lt = t; }
            if (t.health == re) { rt = t; }
        }
        Assert.IsTrue(lt.HasValue, "left enemy");
        Assert.IsTrue(rt.HasValue, "right enemy");
        Assert.AreEqual(PlayerSide.Left, lt.Value.side);
        Assert.AreEqual(PlayerSide.Right, rt.Value.side);
        Vector2 expected = space.WorldToStage(PlayerSide.Left, LeftCamPos + new Vector2(1f, 0f));
        Assert.AreEqual(expected.x, lt.Value.stagePos.x, 1e-3f);
        Assert.AreEqual(expected.y, lt.Value.stagePos.y, 1e-3f);
        Assert.Greater(lt.Value.radius, 0f);
    }

    [UnityTest]
    public IEnumerator StageTargets_UnknownLayerEnemy_Excluded_AndRebuildDoesNotAccumulate()
    {
        yield return BuildRigReady(false);
        HealthSystem def = MakeEnemy(PlayerSide.Left, LeftCamPos, 100f, 0); // Default 레이어
        HealthSystem ok = MakeEnemy(PlayerSide.Left, LeftCamPos + new Vector2(1f, 0f));
        for (int i = 0; i < 5; ++i)
        {
            yield return null;
        }
        Assert.IsFalse(TreeContains(StageSpace.Targets, def), "default layer excluded");
        Assert.AreEqual(1, TreeCount(StageSpace.Targets, ok), "rebuilt each frame without duplicates");
    }

    [UnityTest]
    public IEnumerator StageTargets_DisabledEnemy_Excluded()
    {
        yield return BuildRigReady(false);
        HealthSystem e = MakeEnemy(PlayerSide.Right, RightCamPos + new Vector2(1f, 0f));
        yield return null;
        yield return null;
        Assert.IsTrue(TreeContains(StageSpace.Targets, e));
        e.gameObject.SetActive(false);
        yield return null;
        yield return null;
        Assert.IsFalse(TreeContains(StageSpace.Targets, e));
    }

    [UnityTest]
    public IEnumerator StageTargets_FinalBossProxies_MergedIntoOneTarget()
    {
        yield return BuildRigReady(false);

        FinalBossData data = Track(ScriptableObject.CreateInstance<FinalBossData>());
        data.bodyRadius = 60f; // 양쪽 화면 사각형과 모두 겹치게
        GameObject bossGo = Make("FinalBoss", false);
        FinalBoss boss = bossGo.AddComponent<FinalBoss>();
        SetField(boss, "data", data);
        bossGo.transform.position = space.StageCenter;
        bossGo.SetActive(true);

        BossHitProxy lp = Make("ProxyL", false).AddComponent<BossHitProxy>();
        BossHitProxy rp = Make("ProxyR", false).AddComponent<BossHitProxy>();
        lp.gameObject.SetActive(true);
        rp.gameObject.SetActive(true);
        lp.Setup(null, boss, PlayerSide.Left);
        rp.Setup(null, boss, PlayerSide.Right);

        // 프록시는 LateUpdate 에서 (디렉터 없이) 스스로 꺼지므로, 같은 프레임에 직접 재구성해 본다
        MethodInfo setLive = typeof(BossHitProxy).GetMethod("SetLive", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(setLive);
        setLive.Invoke(lp, new object[] { true });
        setLive.Invoke(rp, new object[] { true });
        Assert.IsTrue(lp.GetComponent<HealthSystem>().isActiveAndEnabled && rp.GetComponent<HealthSystem>().isActiveAndEnabled);

        StageTargets targets = new StageTargets("LMonster", "RMonster", 0.3f);
        StageQuadTree tree = new StageQuadTree();
        targets.Rebuild(tree, space.Space);

        Assert.AreEqual(1, tree.Count, "two proxies of one boss -> one target");
        StageTarget t = tree.GetItem(0);
        Assert.AreEqual(space.StageCenter.x, t.stagePos.x, 1e-3f, "boss position");
        Assert.AreEqual(60f, t.radius, 1e-3f, "boss body radius");
    }
    #endregion

    #region 3. 합성 Apply
    private class FusionRig
    {
        public Weapon a;
        public Weapon b;
        public DimensionWeaponData recipe;
        public DimensionWeapon resultPrefab;
    }

    private FusionRig MakeFusion()
    {
        FusionRig r = new FusionRig();
        r.a = MakeMaterial("MatA");
        r.b = MakeMaterial("MatB");
        r.resultPrefab = Make("FusionResult").AddComponent<TestDimensionWeapon>();
        r.recipe = Track(ScriptableObject.CreateInstance<DimensionWeaponData>());
        r.recipe.name = "TestFusion";
        r.recipe.SetRecipe(r.a, r.b, r.resultPrefab);
        return r;
    }

    private Weapon MakeMaterial(string _name)
    {
        GameObject go = Make(_name, false); // 비활성 원본 (복제본도 비활성이라 Update 가 돌지 않는다)
        Weapon w = go.AddComponent<Weapon>();
        w.attackSpeed = 0f;
        return w;
    }

    private static void Apply(DimensionWeaponData _recipe)
    {
        RewardData reward = DimensionFusionEffect.CreateReward(_recipe);
        Assert.IsNotNull(reward);
        reward.GetEffect().Apply(null, reward);
    }

    private static int CountDimensionWeapons()
    {
        return UnityEngine.Object.FindObjectsByType<DimensionWeapon>(FindObjectsInactive.Include).Length;
    }

    [UnityTest]
    public IEnumerator FusionApply_RemovesMaterials_CreatesOneWeapon_RegistersBothSlots()
    {
        yield return BuildRigReady();
        FusionRig r = MakeFusion();
        Assert.IsTrue(leftPlayer.AddWeapon(r.a));
        Assert.IsTrue(rightPlayer.AddWeapon(r.b));
        Assert.AreEqual(1, leftPlayer.WeaponCount);
        Assert.AreEqual(1, rightPlayer.WeaponCount);
        int before = CountDimensionWeapons();

        int leftChanged = 0;
        int rightChanged = 0;
        leftPlayer.OnWeaponsChanged += _ => ++leftChanged;
        rightPlayer.OnWeaponsChanged += _ => ++rightChanged;

        Apply(r.recipe);
        yield return null;

        Assert.IsFalse(leftPlayer.HasWeapon(r.a), "left material removed");
        Assert.IsFalse(rightPlayer.HasWeapon(r.b), "right material removed");
        Assert.IsTrue(leftPlayer.HasDimensionWeapon(r.recipe));
        Assert.IsTrue(rightPlayer.HasDimensionWeapon(r.recipe));
        Assert.AreEqual(1, leftPlayer.WeaponCount, "slot count stays 1");
        Assert.AreEqual(1, rightPlayer.WeaponCount);
        Assert.AreEqual(before + 1, CountDimensionWeapons(), "exactly one dimension weapon created");
        Assert.AreSame(leftPlayer.DimensionWeapons[0], rightPlayer.DimensionWeapons[0], "shared instance");
        Assert.IsTrue(leftPlayer.DimensionWeapons[0].IsActivated);
        Assert.IsNull(leftPlayer.DimensionWeapons[0].transform.parent, "no parent");
        Assert.IsTrue(leftPlayer.DimensionWeapons[0].IsPlaced, "placed at stage center");
        Assert.AreEqual(2, leftChanged, "remove + add");
        Assert.AreEqual(2, rightChanged);
    }

    [UnityTest]
    public IEnumerator FusionApply_SwappedMaterials_Works()
    {
        yield return BuildRigReady();
        FusionRig r = MakeFusion();
        leftPlayer.AddWeapon(r.b);
        rightPlayer.AddWeapon(r.a);
        Apply(r.recipe);
        Assert.IsFalse(leftPlayer.HasWeapon(r.b));
        Assert.IsFalse(rightPlayer.HasWeapon(r.a));
        Assert.IsTrue(leftPlayer.HasDimensionWeapon(r.recipe) && rightPlayer.HasDimensionWeapon(r.recipe));
    }

    [UnityTest]
    public IEnumerator FusionApply_SecondApply_DoesNothing()
    {
        yield return BuildRigReady();
        FusionRig r = MakeFusion();
        leftPlayer.AddWeapon(r.a);
        rightPlayer.AddWeapon(r.b);

        RewardData reward = DimensionFusionEffect.CreateReward(r.recipe);
        DimensionFusionEffect effect = (DimensionFusionEffect)reward.GetEffect();
        effect.Apply(null, reward);
        Assert.IsTrue(effect.IsApplied);
        int count = CountDimensionWeapons();

        int changed = 0;
        leftPlayer.OnWeaponsChanged += _ => ++changed;
        effect.Apply(null, reward); // 같은 효과 재적용

        // 재료를 다시 얻어도 이미 보유한 차원 무기라 새 효과로도 적용되지 않는다
        leftPlayer.AddWeapon(r.a);
        rightPlayer.AddWeapon(r.b);
        int changedAfterAdd = changed;
        Apply(r.recipe);

        Assert.AreEqual(count, CountDimensionWeapons(), "no second dimension weapon");
        Assert.AreEqual(1, leftPlayer.DimensionWeapons.Count);
        Assert.AreEqual(changedAfterAdd, changed, "no change events from duplicate apply");
        Assert.IsTrue(leftPlayer.HasWeapon(r.a), "materials untouched by duplicate apply");
        yield return null;
    }

    [UnityTest]
    public IEnumerator FusionApply_MissingMaterial_DoesNothing()
    {
        yield return BuildRigReady();
        FusionRig r = MakeFusion();
        leftPlayer.AddWeapon(r.a);
        // 오른쪽이 재료가 없음
        int before = CountDimensionWeapons();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("재료"));
        Apply(r.recipe);

        Assert.IsTrue(leftPlayer.HasWeapon(r.a), "material kept");
        Assert.IsFalse(leftPlayer.HasDimensionWeapon(r.recipe));
        Assert.AreEqual(before, CountDimensionWeapons());
        yield return null;
    }

    [UnityTest]
    public IEnumerator FusionApply_SameSideHasBothMaterials_DoesNothing()
    {
        yield return BuildRigReady();
        FusionRig r = MakeFusion();
        leftPlayer.AddWeapon(r.a);
        leftPlayer.AddWeapon(r.b);
        int before = CountDimensionWeapons();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("재료"));
        Apply(r.recipe);
        Assert.AreEqual(before, CountDimensionWeapons());
        Assert.IsTrue(leftPlayer.HasWeapon(r.a) && leftPlayer.HasWeapon(r.b));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FusionApply_NoPlayers_WarnsAndDoesNothing()
    {
        yield return BuildRigReady(false);
        FusionRig r = MakeFusion();
        int before = CountDimensionWeapons();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("플레이어"));
        Apply(r.recipe);
        Assert.AreEqual(before, CountDimensionWeapons());
    }

    [Test]
    public void FusionEffect_CreateReward_NullOrInvalidRecipe_ReturnsNull()
    {
        Assert.IsNull(DimensionFusionEffect.CreateReward(null));
        DimensionWeaponData empty = Track(ScriptableObject.CreateInstance<DimensionWeaponData>());
        Assert.IsNull(DimensionFusionEffect.CreateReward(empty));
    }
    #endregion

    #region 4. 무기 6종
    private const float SMOKE_TIMEOUT = 15f;

    private DimensionWeapon SpawnWeapon(Type _type)
    {
        if (StageSpace.StageLayer < 0)
        {
            // 프리팹·레이어 통합 전: Stage 레이어가 없으면 경고 1회 (통합 후에는 나오지 않음)
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DimensionWeapon\]"));
        }
        GameObject go = Make("DW_" + _type.Name);
        DimensionWeapon w = (DimensionWeapon)go.AddComponent(_type);
        w.Initialize(null);
        return w;
    }

    [UnityTest]
    public IEnumerator Weapon_Smoke_DealsDamage_NoErrors(
        [ValueSource(nameof(WeaponTypes))] Type _type)
    {
        yield return BuildRigReady();
        List<HealthSystem> enemies = MakeEnemyGrid();
        float start = TotalHp(enemies);
        DimensionWeapon w = SpawnWeapon(_type);

        float t = 0f;
        while (t < SMOKE_TIMEOUT && TotalHp(enemies) >= start)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.IsTrue(w.IsPlaced, "placed");
        Assert.Less(TotalHp(enemies), start, _type.Name + " dealt no damage within " + SMOKE_TIMEOUT + "s");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Weapon_Paused_TimeScaleZero_NoDamage(
        [ValueSource(nameof(WeaponTypes))] Type _type)
    {
        yield return BuildRigReady();
        List<HealthSystem> enemies = MakeEnemyGrid();
        float start = TotalHp(enemies);
        SpawnWeapon(_type);

        Time.timeScale = 0f;
        float t = 0f;
        while (t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.AreEqual(start, TotalHp(enemies), 0.0001f, _type.Name + " dealt damage while paused");
        Time.timeScale = 1f;
        LogAssert.NoUnexpectedReceived();
    }

    public static IEnumerable<Type> WeaponTypes()
    {
        yield return typeof(BlazeBoomerang);
        yield return typeof(PhantomWhirl);
        yield return typeof(PlagueMine);
        yield return typeof(PrismLaser);
        yield return typeof(SpiritSquadron);
        yield return typeof(ThornSaw);
    }
    #endregion

    #region 5. 데미지 통계
    [UnityTest]
    public IEnumerator StageDamage_Apply_PublishesDimensionWeaponDamage()
    {
        yield return BuildRigReady();
        TestDimensionWeapon weapon = Make("DW").AddComponent<TestDimensionWeapon>();
        HealthSystem target = MakeEnemy(PlayerSide.Left, LeftCamPos + Vector2.one, 100f);

        DimensionWeapon gotWeapon = null;
        float gotAmount = -1f;
        int calls = 0;
        Action<DimensionWeapon, float> handler = (w, a) => { gotWeapon = w; gotAmount = a; ++calls; };
        EventManager.Instance.OnDimensionWeaponDamage += handler;
        try
        {
            Assert.IsTrue(StageDamage.Apply(target, 30f, weapon));
            Assert.AreEqual(1, calls);
            Assert.AreSame(weapon, gotWeapon);
            Assert.AreEqual(30f, gotAmount, 1e-4f);
            Assert.AreEqual(70f, target.CurrHP, 1e-4f);

            // 남은 HP 보다 큰 데미지는 실제로 깎인 양만 발행
            Assert.IsTrue(StageDamage.Apply(target, 500f, weapon));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(70f, gotAmount, 1e-3f);
        }
        finally
        {
            EventManager.Instance.OnDimensionWeaponDamage -= handler;
        }
    }

    [UnityTest]
    public IEnumerator StageDamage_Apply_NoEventForZeroDamage_DeadOrNullTarget()
    {
        yield return BuildRigReady();
        TestDimensionWeapon weapon = Make("DW").AddComponent<TestDimensionWeapon>();
        HealthSystem target = MakeEnemy(PlayerSide.Left, LeftCamPos + Vector2.one, 100f);

        int calls = 0;
        Action<DimensionWeapon, float> handler = (w, a) => ++calls;
        EventManager.Instance.OnDimensionWeaponDamage += handler;
        try
        {
            Assert.IsFalse(StageDamage.Apply(target, 0f, weapon));
            Assert.IsFalse(StageDamage.Apply(target, -5f, weapon));
            Assert.IsFalse(StageDamage.Apply((HealthSystem)null, 10f, weapon));
            Assert.AreEqual(0, calls);
            Assert.AreEqual(100f, target.CurrHP, 1e-4f);
        }
        finally
        {
            EventManager.Instance.OnDimensionWeaponDamage -= handler;
        }
    }

    [UnityTest]
    public IEnumerator StageDamage_Apply_WithoutSource_DoesNotPublishDimensionEvent()
    {
        yield return BuildRigReady();
        HealthSystem target = MakeEnemy(PlayerSide.Left, LeftCamPos + Vector2.one, 100f);
        int calls = 0;
        Action<DimensionWeapon, float> handler = (w, a) => ++calls;
        EventManager.Instance.OnDimensionWeaponDamage += handler;
        try
        {
            Assert.IsTrue(StageDamage.Apply(target, 10f));
            Assert.AreEqual(0, calls);
            Assert.AreEqual(90f, target.CurrHP, 1e-4f);
        }
        finally
        {
            EventManager.Instance.OnDimensionWeaponDamage -= handler;
        }
    }
    #endregion
}
