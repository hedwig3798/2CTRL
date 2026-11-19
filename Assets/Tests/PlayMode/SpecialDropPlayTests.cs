using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 특수 드랍 아이템 PlayMode 테스트 (프리팹 없이 직접 구성)
/// - 무적: DamagePipeline 경유 데미지 / 넉백 0, 5초 후 정상 피격, 재습득 갱신, 렌더러 복구
/// - 자석: 같은 진영 레이어 보석만 끌려와 각각 흡수 (EXP 증가), 풀 재사용 시 Pull 보정값 초기화
/// 물리 트리거는 실제 레이어(LPlayer / LItem / RItem) 를 쓴다 (Default 끼리는 충돌이 꺼져 있다)
/// </summary>
public class SpecialDropPlayTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        for (int i = created.Count - 1; i >= 0; --i)
        {
            if (null != created[i])
            {
                Object.Destroy(created[i]);
            }
        }
        created.Clear();
    }

    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private static void SetField(object _t, string _f, object _v)
    {
        FieldInfo fi = _t.GetType().GetField(_f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(fi, "field not found " + _f);
        fi.SetValue(_t, _v);
    }

    private static int L_ITEM => LayerMask.NameToLayer("LItem");
    private static int R_ITEM => LayerMask.NameToLayer("RItem");

    private class Rig
    {
        public GameObject root;
        public PlayerStats stats;
        public HealthSystem health;
        public InvincibleBuff buff;
        public Knockback knockback;
        public DamagePipeline pipeline;
        public SpriteRenderer sprite;
        public ItemCollector collector;
    }

    private Rig MakePlayer(Vector2 _position, bool _withBuff = true)
    {
        Rig rig = new Rig();
        GameObject root = Track(new GameObject("SpecialPlayer"));
        root.SetActive(false);
        root.transform.position = _position;
        rig.root = root;

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;

        rig.stats = root.AddComponent<PlayerStats>();
        SetField(rig.stats, "side", PlayerSide.Left);

        rig.health = root.AddComponent<HealthSystem>();
        rig.health.maxHP = 100f;
        rig.health.currHP = 100f;

        if (true == _withBuff)
        {
            rig.buff = root.AddComponent<InvincibleBuff>();
        }
        rig.knockback = root.AddComponent<Knockback>();
        rig.pipeline = root.AddComponent<DamagePipeline>();

        GameObject spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(root.transform, false);
        rig.sprite = spriteGo.AddComponent<SpriteRenderer>();
        if (null != rig.buff)
        {
            // 깜빡임 대상은 직렬화로 지정한다 (자동 수집 없음)
            SetField(rig.buff, "blinkRenderers", new SpriteRenderer[] { rig.sprite });
        }

        GameObject collectorGo = new GameObject("ItemCollector");
        collectorGo.transform.SetParent(root.transform, false);
        collectorGo.layer = LayerMask.NameToLayer("LPlayer");
        CircleCollider2D circle = collectorGo.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 1f;
        rig.collector = collectorGo.AddComponent<ItemCollector>();
        SetField(rig.collector, "owner", root);

        root.SetActive(true);
        return rig;
    }

    private static DamageMassage Hit(Rig _rig)
    {
        return new DamageMassage
        {
            damage = 10f,
            knockbackForce = 20f,
            hitPoint = (Vector2)_rig.root.transform.position + new Vector2(-1f, 0f),
        };
    }

    // 게임 시간으로 _seconds 만큼 프레임을 돌린다
    private static IEnumerator RunGameSeconds(float _seconds)
    {
        float t = 0f;
        while (t < _seconds)
        {
            yield return null;
            t += Time.deltaTime;
        }
    }

    // ---------- 무적 ----------

    [UnityTest]
    public IEnumerator Invincible_BlocksDamageAndKnockback_ThroughPipeline()
    {
        Rig rig = MakePlayer(new Vector2(500f, 500f));
        yield return null;

        float hp0 = rig.health.currHP;
        int damagedCalls = 0;
        rig.health.OnDamaged += _ => ++damagedCalls;
        Vector3 pos0 = rig.root.transform.position;

        rig.buff.Activate(5f);
        DamageMassage msg = Hit(rig);
        rig.pipeline.ProcessDamage(ref msg);
        yield return null;
        yield return null;
        yield return null;

        Assert.AreEqual(0f, msg.damage, "무적 단계가 데미지를 0 으로");
        Assert.AreEqual(0f, msg.knockbackForce, "넉백도 0 으로");
        Assert.AreEqual(hp0, rig.health.currHP, 1e-4f, "HP 그대로");
        Assert.AreEqual(0, damagedCalls, "OnDamaged 미호출");
        Assert.IsFalse(rig.knockback.IsKnockedBack);
        Assert.AreEqual(pos0.x, rig.root.transform.position.x, 1e-4f, "밀리지 않는다");
        Assert.AreEqual(pos0.y, rig.root.transform.position.y, 1e-4f);
    }

    [UnityTest]
    public IEnumerator Invincible_ExpiresAfter5Seconds_ThenNormalHit()
    {
        Rig rig = MakePlayer(new Vector2(510f, 500f));
        yield return null;
        Time.timeScale = 20f;

        float hp0 = rig.health.currHP;
        int damagedCalls = 0;
        float lastDamage = 0f;
        rig.health.OnDamaged += _d => { ++damagedCalls; lastDamage = _d; };

        rig.buff.Activate(5f);

        // 4.5초 시점에도 무적
        float elapsed = 0f;
        while (elapsed < 4.5f)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        Assert.IsTrue(rig.buff.IsActive, "4.5초 시점엔 아직 무적");
        DamageMassage midHit = Hit(rig);
        rig.pipeline.ProcessDamage(ref midHit);
        Assert.AreEqual(hp0, rig.health.currHP, 1e-4f);
        Assert.AreEqual(0, damagedCalls);

        // 5초 경과 대기
        int guard = 0;
        while (true == rig.buff.IsActive && guard++ < 200)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        Assert.IsFalse(rig.buff.IsActive, "5초 후 무적 종료");
        Assert.GreaterOrEqual(elapsed, 5f - 1e-3f, "5초가 지나기 전에 끝나면 안 된다");
        Assert.LessOrEqual(elapsed, 5f + Time.maximumDeltaTime + 1e-3f, "5초에서 한 프레임 이상 늦지 않는다");

        Time.timeScale = 1f;
        Vector3 pos0 = rig.root.transform.position;
        DamageMassage msg = Hit(rig);
        rig.pipeline.ProcessDamage(ref msg);
        Assert.AreEqual(10f, msg.damage, "종료 후 데미지 그대로");
        Assert.AreEqual(hp0 - 10f, rig.health.currHP, 1e-4f, "HP 감소");
        Assert.AreEqual(1, damagedCalls);
        Assert.AreEqual(10f, lastDamage, 1e-4f);

        yield return null;
        yield return null;
        Assert.Greater(rig.root.transform.position.x, pos0.x, "넉백으로 hitPoint 반대쪽(+x) 으로 밀린다");
    }

    [UnityTest]
    public IEnumerator Invincible_Reactivate_RefreshesRemainTime_NotAccumulate()
    {
        Rig rig = MakePlayer(new Vector2(520f, 500f));
        yield return null;

        rig.buff.Activate(5f);
        yield return RunGameSeconds(1.5f);
        Assert.Less(rig.buff.RemainTime, 4f, "시간이 흘렀다");

        rig.buff.Activate(5f);
        Assert.AreEqual(5f, rig.buff.RemainTime, 1e-4f, "남은 시간이 5초로 갱신");

        yield return null;
        Assert.LessOrEqual(rig.buff.RemainTime, 5f, "누적(최대 8.5) 금지");
    }

    [UnityTest]
    public IEnumerator Invincible_PickupItem_ActivatesAndRepickupRefreshes()
    {
        Rig rig = MakePlayer(new Vector2(530f, 500f));
        yield return null;

        PowerUpItem item = MakePowerUp(POWER_UP_TYPE.Invincible, new Vector2(530f, 500f));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.IsTrue(rig.buff.IsActive, "습득하면 무적");
        Assert.AreEqual(item.InvincibleDuration, 5f, 1e-4f);
        Assert.IsFalse(item.gameObject.activeSelf, "습득한 아이템은 꺼진다");

        yield return RunGameSeconds(1.5f);
        float remain = rig.buff.RemainTime;
        Assert.Less(remain, 4f);

        // 풀에서 다시 꺼낸 것처럼 재활성화 + 재습득 (트리거를 다시 만들기 위해 collector 를 껐다 켠다)
        item.gameObject.SetActive(true);
        rig.collector.gameObject.SetActive(false);
        rig.collector.gameObject.SetActive(true);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.IsFalse(item.gameObject.activeSelf, "다시 먹었다");
        Assert.Greater(rig.buff.RemainTime, 4.5f, "재습득 시 남은 시간 5초로 갱신");
        Assert.LessOrEqual(rig.buff.RemainTime, 5f, "누적 금지");
    }

    [UnityTest]
    public IEnumerator Invincible_PlayerWithoutBuff_PickupDoesNotThrow()
    {
        Rig rig = MakePlayer(new Vector2(540f, 500f), false);
        yield return null;

        PowerUpItem item = MakePowerUp(POWER_UP_TYPE.Invincible, new Vector2(540f, 500f));
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InvincibleBuff"));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return null;

        Assert.IsFalse(item.gameObject.activeSelf, "버프가 없어도 아이템은 수집된다");
    }

    private const float FullAlpha = 1f;

    private static bool IsDimmed(Rig _rig)
    {
        return _rig.sprite.color.a < FullAlpha - 0.01f;
    }

    [UnityTest]
    public IEnumerator Invincible_RendererBlinksAndRestoresOnExpire()
    {
        Rig rig = MakePlayer(new Vector2(550f, 500f));
        yield return null;
        Assert.IsFalse(IsDimmed(rig));

        rig.buff.Activate(0.6f);
        bool sawHidden = false;
        float t = 0f;
        while (true == rig.buff.IsActive && t < 3f)
        {
            yield return null;
            t += Time.deltaTime;
            if (true == IsDimmed(rig))
            {
                sawHidden = true;
            }
            Assert.IsTrue(rig.sprite.enabled, "깜빡임은 enabled 를 건드리지 않는다");
        }
        yield return null;

        Assert.IsTrue(sawHidden, "무적 중 깜빡인다 (alpha)");
        Assert.IsFalse(rig.buff.IsActive);
        Assert.AreEqual(FullAlpha, rig.sprite.color.a, 1e-4f, "종료 시 원래 alpha 복구");
    }

    [UnityTest]
    public IEnumerator Invincible_RendererRestoredOnDeactivateAndDisable()
    {
        Rig rig = MakePlayer(new Vector2(560f, 500f));
        yield return null;

        // 숨김 상태가 된 순간에 Deactivate
        rig.buff.Activate(5f);
        yield return HideOnce(rig);
        Assert.IsTrue(IsDimmed(rig), "숨김 상태 확인");
        rig.buff.Deactivate();
        Assert.IsFalse(IsDimmed(rig), "Deactivate 로 복구");

        // 숨김 상태가 된 순간에 컴포넌트 비활성화
        rig.buff.Activate(5f);
        yield return HideOnce(rig);
        Assert.IsTrue(IsDimmed(rig));
        rig.buff.enabled = false;
        Assert.IsFalse(IsDimmed(rig), "OnDisable 로 복구");
        Assert.IsFalse(rig.buff.IsActive);

        // 오브젝트 비활성화
        rig.buff.enabled = true;
        rig.buff.Activate(5f);
        yield return HideOnce(rig);
        Assert.IsTrue(IsDimmed(rig));
        rig.root.SetActive(false);
        Assert.IsFalse(IsDimmed(rig), "오브젝트 비활성화로도 복구");
    }

    [UnityTest]
    public IEnumerator Invincible_KeepsOriginalAlphaAndRgb()
    {
        Rig rig = MakePlayer(new Vector2(570f, 500f));
        yield return null;
        rig.sprite.color = new Color(1f, 1f, 1f, 0.5f);

        rig.buff.Activate(5f);
        yield return HideOnce(rig);
        Assert.Less(rig.sprite.color.a, 0.5f, "원래 alpha 기준으로 흐려진다");

        // 깜빡이는 동안 다른 시스템이 rgb 를 바꿔도 유지된다
        rig.sprite.color = new Color(1f, 0f, 0f, rig.sprite.color.a);
        rig.buff.Deactivate();
        Assert.AreEqual(0.5f, rig.sprite.color.a, 1e-4f, "원래 alpha 로 복구");
        Assert.AreEqual(0f, rig.sprite.color.g, 1e-4f, "rgb 는 건드리지 않는다");
    }

    [UnityTest]
    public IEnumerator Invincible_PausedStaysVisibleAndTimeStops()
    {
        Rig rig = MakePlayer(new Vector2(575f, 500f));
        yield return null;

        rig.buff.Activate(5f);
        yield return HideOnce(rig);
        Assert.IsTrue(IsDimmed(rig));

        Time.timeScale = 0f;
        yield return null;
        float remain = rig.buff.RemainTime;
        yield return null;
        yield return null;

        Assert.IsFalse(IsDimmed(rig), "일시정지 중에는 보이는 상태로 고정");
        Assert.AreEqual(remain, rig.buff.RemainTime, 1e-5f, "일시정지 중 시간 정지");
        Assert.IsTrue(rig.buff.IsActive);
        Time.timeScale = 1f;
    }

    [UnityTest]
    public IEnumerator Invincible_EndsOnDeathAndDown()
    {
        // 사망 (OnDeath)
        Rig rig = MakePlayer(new Vector2(580f, 500f));
        yield return null;
        rig.buff.Activate(5f);
        yield return HideOnce(rig);
        rig.health.Kill();
        Assert.IsFalse(rig.buff.IsActive, "사망하면 무적 종료");
        Assert.IsFalse(IsDimmed(rig), "사망 시 렌더러 복구");

        // 다운 (DeathInterceptor 가 가로챔 → isDead + IsDowned)
        Rig rig2 = MakePlayer(new Vector2(590f, 500f));
        yield return null;
        rig2.health.DeathInterceptor = () => true;
        rig2.buff.Activate(5f);
        yield return HideOnce(rig2);
        rig2.health.Kill();
        Assert.IsTrue(rig2.health.IsDowned);
        yield return null;
        Assert.IsFalse(rig2.buff.IsActive, "다운되면 무적 종료");
        Assert.IsFalse(IsDimmed(rig2), "다운 시 렌더러 복구");

        // 다운 상태에서는 다시 켜지지 않는다
        rig2.buff.Activate(5f);
        Assert.IsFalse(rig2.buff.IsActive);
    }

    [UnityTest]
    public IEnumerator Invincible_IgnoresForcedPush()
    {
        Rig rig = MakePlayer(new Vector2(600f, 500f));
        yield return null;

        PlayerForcedMove forced = PlayerForcedMove.For(rig.root.transform);
        Vector3 before = rig.root.transform.position;

        rig.buff.Activate(5f);
        forced.Push(new Vector2(3f, 0f), 0f);
        forced.Push(new Vector2(3f, 0f), 0.2f);
        Assert.IsFalse(forced.IsMoving, "무적 중 강제 이동 무시");
        yield return RunGameSeconds(0.3f);
        Assert.AreEqual(before.x, rig.root.transform.position.x, 1e-3f);

        rig.buff.Deactivate();
        forced.Push(new Vector2(3f, 0f), 0f);
        Assert.AreEqual(before.x + 3f, rig.root.transform.position.x, 1e-3f, "무적이 끝나면 다시 밀린다");
    }

    private static IEnumerator HideOnce(Rig _rig)
    {
        float t = 0f;
        while (false == _rig.buff.IsHidden && t < 2f)
        {
            yield return null;
            t += Time.deltaTime;
        }
    }

    // ---------- PowerUpItem ----------

    private PowerUpItem MakePowerUp(POWER_UP_TYPE _type, Vector2 _position, int _layer = -1)
    {
        GameObject go = Track(new GameObject("PowerUp_" + _type));
        go.SetActive(false);
        go.tag = "item";
        go.layer = _layer >= 0 ? _layer : L_ITEM;
        go.transform.position = _position;
        go.AddComponent<CircleCollider2D>().radius = 0.1f;
        PowerUpItem item = go.AddComponent<PowerUpItem>();
        SetField(item, "type", _type);
        go.SetActive(true);
        return item;
    }

    [UnityTest]
    public IEnumerator PowerUpItem_Collect_OnlyOnce_AndResetsOnEnable()
    {
        PowerUpItem item = MakePowerUp(POWER_UP_TYPE.Magnet, new Vector2(900f, 900f));
        yield return null;

        Assert.AreEqual(L_ITEM, item.Layer);
        Assert.IsTrue(item.Collect());
        Assert.IsFalse(item.gameObject.activeSelf);
        Assert.IsFalse(item.Collect(), "이미 수집된 것은 false");

        item.gameObject.SetActive(true);
        Assert.IsTrue(item.Collect(), "풀 재사용(OnEnable) 시 수집 상태 초기화");

        item.gameObject.SetActive(true);
        Assert.IsFalse(item.Collect() && item.Collect());
        item.Initialize(null);
        Assert.IsTrue(item.Collect(), "Initialize 도 수집 상태 초기화");
    }

    [UnityTest]
    public IEnumerator PowerUpItem_Collect_DisablesOwnerWhenSet()
    {
        GameObject ownerGo = Track(new GameObject("PowerUpOwner"));
        GameObject child = new GameObject("PowerUpChild");
        child.transform.SetParent(ownerGo.transform, false);
        PowerUpItem item = child.AddComponent<PowerUpItem>();
        SetField(item, "owner", ownerGo);
        yield return null;

        Assert.IsTrue(item.Collect());
        Assert.IsFalse(ownerGo.activeSelf, "owner 가 지정되면 owner 를 끈다");
    }

    // ---------- 자석 ----------

    private (GameObject go, ExpItem exp, SlingshotMovement move) MakeGem(Vector2 _position, int _layer, float _baseExp = 1f)
    {
        GameObject gem = Track(new GameObject("Gem"));
        gem.SetActive(false);
        gem.tag = "item";
        gem.layer = _layer;
        gem.transform.position = _position;
        gem.AddComponent<CircleCollider2D>().radius = 0.05f;
        ExpItem exp = gem.AddComponent<ExpItem>();
        SetField(exp, "baseExp", _baseExp);
        SlingshotMovement move = gem.AddComponent<SlingshotMovement>();
        gem.SetActive(true);

        BlackBoard data = new BlackBoard();
        data.SetFloat(DATA_TYPE.expRate, 1f);
        exp.Initialize(data);
        move.Initialize(data);
        return (gem, exp, move);
    }

    private ExpSystem MakeExpSystem()
    {
        LevelTable table = Track(ScriptableObject.CreateInstance<LevelTable>());
        table.maxLevel = 99;
        table.baseExp = 100000f;

        GameObject expGo = Track(new GameObject("ExpSystem"));
        ExpSystem expSystem = expGo.AddComponent<ExpSystem>();
        expSystem.levelTable = table;
        return expSystem;
    }

    [UnityTest]
    public IEnumerator PullGems_OnlySameLayer_AreDrawn()
    {
        Transform target = Track(new GameObject("MagnetTarget")).transform;
        target.position = new Vector2(1500f, 1500f);

        var l1 = MakeGem(new Vector2(1510f, 1500f), L_ITEM);
        var l2 = MakeGem(new Vector2(1500f, 1490f), L_ITEM);
        var r1 = MakeGem(new Vector2(1505f, 1505f), R_ITEM);
        yield return null;

        int pulled = ItemCollector.PullGems(L_ITEM, target, 20f, 40f);

        Assert.AreEqual(2, pulled, "같은 레이어 보석 2개만");
        Assert.AreEqual(target, l1.move.target);
        Assert.AreEqual(target, l2.move.target);
        Assert.IsFalse(l1.move.isStop);
        Assert.IsFalse(l2.move.isStop);
        Assert.IsNull(r1.move.target, "다른 레이어 보석은 그대로");
        Assert.IsTrue(r1.move.isStop);

        Vector3 r1Pos = r1.go.transform.position;
        yield return RunGameSeconds(0.3f);
        Assert.AreEqual(r1Pos, r1.go.transform.position, "다른 레이어 보석은 움직이지 않는다");
    }

    [UnityTest]
    public IEnumerator PullGems_NullTarget_OrNoGems_ReturnsZero()
    {
        Assert.AreEqual(0, ItemCollector.PullGems(L_ITEM, null, 20f, 40f));

        Transform target = Track(new GameObject("MagnetTarget")).transform;
        var r1 = MakeGem(new Vector2(1600f, 1600f), R_ITEM);
        yield return null;
        Assert.AreEqual(0, ItemCollector.PullGems(L_ITEM, target, 20f, 40f), "해당 레이어 보석 없음");
        Assert.IsNull(r1.move.target);
    }

    [UnityTest]
    public IEnumerator PullGems_SkipsAlreadyCollectedAndInactiveGems()
    {
        Transform target = Track(new GameObject("MagnetTarget")).transform;
        target.position = new Vector2(1700f, 1700f);

        var gem1 = MakeGem(new Vector2(1710f, 1700f), L_ITEM);
        var gem2 = MakeGem(new Vector2(1700f, 1710f), L_ITEM);
        yield return null;

        gem2.exp.Collect(); // 수집되면 꺼진다
        Assert.AreEqual(1, ItemCollector.PullGems(L_ITEM, target, 20f, 40f));
        Assert.AreEqual(target, gem1.move.target);
        Assert.IsNull(gem2.move.target);
    }

    [UnityTest]
    public IEnumerator Magnet_PickupDrawsOwnGemsAndAbsorbsEach()
    {
        ExpSystem expSystem = MakeExpSystem();
        Rig rig = MakePlayer(new Vector2(2000f, 2000f));
        yield return null;

        float baseline = expSystem.currExp;
        float perGem = 1f * expSystem.ExpGainMultiplier;

        // 수집 범위(반경 1) 밖의 같은 진영 보석 3개 + 상대 진영 보석 1개
        var g1 = MakeGem(new Vector2(2008f, 2000f), L_ITEM);
        var g2 = MakeGem(new Vector2(2000f, 1991f), L_ITEM);
        var g3 = MakeGem(new Vector2(1993f, 2003f), L_ITEM);
        var enemyGem = MakeGem(new Vector2(2004f, 2004f), R_ITEM);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.AreEqual(baseline, expSystem.currExp, 1e-4f, "아직 아무것도 먹지 않았다");
        Assert.IsTrue(g1.go.activeSelf && g2.go.activeSelf && g3.go.activeSelf);

        // 자석 습득
        MakePowerUp(POWER_UP_TYPE.Magnet, new Vector2(2000f, 2000f));

        float t = 0f;
        while ((g1.go.activeSelf || g2.go.activeSelf || g3.go.activeSelf) && t < 6f)
        {
            yield return null;
            t += Time.deltaTime;
        }

        Assert.IsFalse(g1.go.activeSelf, "g1 흡수");
        Assert.IsFalse(g2.go.activeSelf, "g2 흡수");
        Assert.IsFalse(g3.go.activeSelf, "g3 흡수");
        Assert.AreEqual(baseline + 3f * perGem, expSystem.currExp, 1e-3f, "보석마다 각각 EXP 증가 (상대 진영 보석 제외)");

        Assert.IsTrue(enemyGem.go.activeSelf, "다른 진영 보석은 남아 있다");
        Assert.IsNull(enemyGem.move.target);
        Assert.AreEqual(new Vector2(2004f, 2004f), (Vector2)enemyGem.go.transform.position);
    }

    // ---------- SlingshotMovement.Pull ----------

    [UnityTest]
    public IEnumerator Pull_NullTarget_IsNoOp()
    {
        var gem = MakeGem(new Vector2(3000f, 3000f), L_ITEM);
        yield return null;

        gem.move.Pull(null, 20f, 40f);
        Assert.IsNull(gem.move.target);
        Assert.IsTrue(gem.move.isStop);
    }

    [UnityTest]
    public IEnumerator Pull_Initialize_ResetsPullCorrection()
    {
        Transform target = Track(new GameObject("PullTarget")).transform;
        target.position = new Vector2(3100f, 3000f);

        var pulled = MakeGem(new Vector2(3000f, 3000f), L_ITEM);
        var reused = MakeGem(new Vector2(3000f, 3050f), L_ITEM);
        foreach (SlingshotMovement m in new[] { pulled.move, reused.move })
        {
            m.maxSpeed = 1f;
            m.acceleration = 1f;
            m.slingSpeed = 0f;
            m.Initialize(null);
        }

        // 한쪽만 자석 보정, 다른 쪽은 보정 후 풀로 돌아가 재초기화 (Pull → Initialize → 일반 추적)
        pulled.move.Pull(target, 50f, 100f);
        reused.move.Pull(target, 50f, 100f);
        reused.move.Initialize(null);
        reused.move.target = target;
        reused.move.isStop = false;

        Vector2 pulledStart = pulled.go.transform.position;
        Vector2 reusedStart = reused.go.transform.position;
        yield return RunGameSeconds(0.5f);

        float pulledMoved = Vector2.Distance(pulledStart, pulled.go.transform.position);
        float reusedMoved = Vector2.Distance(reusedStart, reused.go.transform.position);

        Assert.Greater(pulledMoved, 3f, "자석 보정이 있으면 원래 maxSpeed(1) 보다 훨씬 빠르게 끌려온다");
        Assert.LessOrEqual(reusedMoved, 1f * 0.6f + 0.2f, "Initialize 후엔 보정값이 초기화되어 maxSpeed 를 넘지 못한다");
    }

    [UnityTest]
    public IEnumerator Pull_DoesNotOvershootTarget()
    {
        Transform target = Track(new GameObject("PullTarget")).transform;
        target.position = new Vector2(3200f, 3000f);

        var gem = MakeGem(new Vector2(3200.3f, 3000f), L_ITEM);
        gem.move.slingSpeed = 0f;
        gem.move.Initialize(null);
        gem.move.Pull(target, 200f, 5000f);
        yield return RunGameSeconds(0.5f);

        Assert.Less(Vector2.Distance(gem.go.transform.position, target.position), 0.05f, "목표를 지나쳐 진동하지 않고 도착");
    }
}
