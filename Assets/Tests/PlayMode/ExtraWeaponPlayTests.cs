using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 차원별 추가 무기 (덩굴/정령/뿌리/톱날/회오리/낙뢰) + 독 장판 회귀 검증. 프리팹 없이 코드로 구성
/// </summary>
public class ExtraWeaponPlayTests
{
    private static LayerMask Mask => MonsterTestFactory.TargetMask;

    [TearDown]
    public void TearDown()
    {
        WeaponTestKit.Cleanup();
    }

    private static MonsterTestFactory.Rig NewMonster(Vector2 _pos)
    {
        MonsterTestFactory.Rig r = MonsterTestFactory.Create(MonsterTestFactory.NewData(0f), _pos, 100000f, true);
        MonsterTestFactory.Init(r, WeaponTestKit.Marker(new Vector2(50f, 50f)));
        return r;
    }

    #region 덩굴
    [Test]
    public void Vine_segmentSpacing_미만이면_Launch_false_이상이면_true()
    {
        Transform owner = WeaponTestKit.Marker(Vector2.zero);
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<VineProjectile>(), owner, 6f);

        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.CountActive<VineProjectile>(), "첫 조각");

        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.CountActive<VineProjectile>(), "제자리에서는 쌓이지 않아야 함");

        owner.position = new Vector3(0.69f, 0f, 0f);
        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.CountActive<VineProjectile>(), "0.69 < 0.7 이면 만들지 않음");

        owner.position = new Vector3(0.71f, 0f, 0f);
        w.Fire();
        Assert.AreEqual(2, WeaponTestKit.CountActive<VineProjectile>(), "0.71 >= 0.7 이면 만든다");

        // 새 마지막 조각 기준으로 다시 계산한다
        owner.position = new Vector3(1.0f, 0f, 0f);
        w.Fire();
        Assert.AreEqual(2, WeaponTestKit.CountActive<VineProjectile>());
    }

    [Test]
    public void Vine_마지막_조각이_사라지면_같은_자리에도_다시_깐다()
    {
        Transform owner = WeaponTestKit.Marker(Vector2.zero);
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<VineProjectile>(), owner, 6f);

        w.Fire();
        VineProjectile first = WeaponTestKit.FirstActive<VineProjectile>();
        Assert.IsNotNull(first);
        first.gameObject.SetActive(false);

        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.CountActive<VineProjectile>());
    }

    [Test]
    public void Vine_조각은_주인_위치에_생긴다_적이_없어도()
    {
        Transform owner = WeaponTestKit.Marker(new Vector2(3f, -2f));
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<VineProjectile>(), owner, 6f);

        w.Fire();
        VineProjectile v = WeaponTestKit.FirstActive<VineProjectile>();
        Assert.IsNotNull(v);
        Assert.AreEqual(3f, v.transform.position.x, 0.001f);
        Assert.AreEqual(-2f, v.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator Vine_조각_반경_안의_적만_틱_데미지를_받는다()
    {
        GameObject inside = MonsterTestFactory.CreateTarget(new Vector2(0.2f, 0f), 1000f);
        GameObject outside = MonsterTestFactory.CreateTarget(new Vector2(2.5f, 0f), 1000f);
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<VineProjectile>(), WeaponTestKit.Marker(Vector2.zero), 6f);

        w.Fire();
        yield return MonsterTestFactory.Wait(0.2f);

        Assert.AreEqual(994f, WeaponTestKit.Hp(inside), 0.01f, "첫 틱은 바로 타격");
        Assert.AreEqual(1000f, WeaponTestKit.Hp(outside), 0.01f);
    }
    #endregion

    #region 뿌리
    [Test]
    public void Root_적이_없어도_사거리_안에_생긴다()
    {
        Transform owner = WeaponTestKit.Marker(new Vector2(1f, 1f));
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<RootProjectile>(), owner, 5f, 0f, 6f);

        for (int i = 0; i < 20; ++i)
        {
            w.Fire();
        }

        RootProjectile[] roots = Object.FindObjectsByType<RootProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Assert.AreEqual(20, roots.Length);
        foreach (RootProjectile r in roots)
        {
            Assert.LessOrEqual(Vector2.Distance(r.transform.position, owner.position), 6.001f);
        }
    }

    [UnityTest]
    public IEnumerator Root_FlagSlowZone_없이도_적을_항상_둔화시키고_데미지를_준다()
    {
        MonsterTestFactory.Rig m = NewMonster(new Vector2(0.2f, 0f));
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<RootProjectile>(), WeaponTestKit.Marker(Vector2.zero), 5f, 0f, 0f, 1, 1 << 0);
        Assert.IsFalse(w.HasFlag(WeaponParam.FlagSlowZone));

        w.Fire();
        yield return MonsterTestFactory.Wait(0.2f);

        Assert.Less(m.brain.Context.SlowMultiplier, 1f, "뿌리는 플래그 없이 둔화");
        Assert.Less(m.health.currHP, 100000f, "데미지");
    }

    [UnityTest]
    public IEnumerator Root_반경_밖의_적은_둔화되지_않는다()
    {
        MonsterTestFactory.Rig m = NewMonster(new Vector2(4f, 0f));
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<RootProjectile>(), WeaponTestKit.Marker(Vector2.zero), 5f, 0f, 0f, 1, 1 << 0);

        w.Fire();
        yield return MonsterTestFactory.Wait(0.2f);

        Assert.AreEqual(1f, m.brain.Context.SlowMultiplier, 0.0001f);
    }

    [UnityTest]
    public IEnumerator Root_FlagRootChase_지역이_가까운_적쪽으로_이동한다()
    {
        NewMonster(new Vector2(4f, 0f));
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<RootProjectile>(), WeaponTestKit.Marker(Vector2.zero), 5f, 0f, 0f, 1, 1 << 0);
        w.AddModifier(WeaponParam.FlagRootChase, StatModifierMode.Add, 1f);

        w.Fire();
        RootProjectile root = WeaponTestKit.FirstActive<RootProjectile>();
        root.transform.position = Vector3.zero;
        // 생성 위치는 사거리 0 이라 주인 위치. 추적 탐색은 사거리 8 로 넓힌다
        w.range = 8f;
        float before = Vector2.Distance(root.transform.position, new Vector2(4f, 0f));

        yield return MonsterTestFactory.Wait(1.0f);

        float after = Vector2.Distance(root.transform.position, new Vector2(4f, 0f));
        Assert.Less(after, before - 0.5f, "추적 이동");
    }
    #endregion

    #region 정령
    private static Weapon NewSpiritWeapon(int _hitCount)
    {
        Transform owner = WeaponTestKit.Marker(Vector2.zero);
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Capped, WeaponTestKit.Template<SpiritProjectile>(), owner, 8f, 10f, 4f, 1);
        w.hitCount = _hitCount;
        return w;
    }

    private static IEnumerator WaitSpirit(Weapon _w)
    {
        yield return MonsterTestFactory.WaitUntil(() => null != WeaponTestKit.FirstActive<SpiritProjectile>(), 2f);
        // 다음 정령이 곧바로 생겨 결과를 흐리지 않도록 쿨타임을 늘린다
        _w.attackSpeed = 0.1f;
    }

    [UnityTest]
    public IEnumerator Spirit_hitCount_틱을_다_쓰면_소멸한다()
    {
        GameObject target = MonsterTestFactory.CreateTarget(new Vector2(2f, 0f), 1000f);
        Weapon w = NewSpiritWeapon(3);

        yield return WaitSpirit(w);
        SpiritProjectile spirit = WeaponTestKit.FirstActive<SpiritProjectile>();
        Assert.IsNotNull(spirit, "정령 발사");
        Assert.AreEqual(3, spirit.RemainingHits);

        yield return MonsterTestFactory.WaitUntil(() => false == spirit.gameObject.activeSelf, 5f);

        Assert.IsFalse(spirit.gameObject.activeSelf, "소멸");
        Assert.AreEqual(1000f - 3f * 8f, WeaponTestKit.Hp(target), 0.01f, "정확히 hitCount 번 타격");
    }

    [UnityTest]
    public IEnumerator Spirit_틱_간격은_tickInterval을_지킨다()
    {
        GameObject target = MonsterTestFactory.CreateTarget(new Vector2(1.5f, 0f), 1000f);
        Weapon w = NewSpiritWeapon(6);

        yield return WaitSpirit(w);
        SpiritProjectile spirit = WeaponTestKit.FirstActive<SpiritProjectile>();
        yield return MonsterTestFactory.WaitUntil(() => WeaponTestKit.Hp(target) < 1000f, 3f);
        float t0 = Time.time;
        yield return MonsterTestFactory.WaitUntil(() => WeaponTestKit.Hp(target) <= 1000f - 16f, 3f);

        Assert.AreEqual(1000f - 16f, WeaponTestKit.Hp(target), 0.01f);
        Assert.GreaterOrEqual(Time.time - t0, 0.3f, "두 번째 타격은 틱 간격(0.4초) 뒤");
        Assert.IsTrue(spirit.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Spirit_대상이_죽으면_남은_틱으로_다음_적을_추적한다()
    {
        GameObject a = MonsterTestFactory.CreateTarget(new Vector2(1.5f, 0f), 8f);
        GameObject b = MonsterTestFactory.CreateTarget(new Vector2(3f, 0f), 1000f);
        Weapon w = NewSpiritWeapon(4);

        yield return WaitSpirit(w);
        SpiritProjectile spirit = WeaponTestKit.FirstActive<SpiritProjectile>();
        yield return MonsterTestFactory.WaitUntil(() => false == spirit.gameObject.activeSelf, 8f);

        Assert.IsTrue(a.GetComponent<HealthSystem>().isDead, "A 는 첫 타격에 사망");
        Assert.IsFalse(spirit.gameObject.activeSelf);
        Assert.AreEqual(1000f - 3f * 8f, WeaponTestKit.Hp(b), 0.01f, "남은 3틱을 B 에게 사용");
    }

    [UnityTest]
    public IEnumerator Spirit_다음_적이_없으면_대기로_돌아가고_남은_틱을_유지한다()
    {
        GameObject a = MonsterTestFactory.CreateTarget(new Vector2(1.5f, 0f), 8f);
        Weapon w = NewSpiritWeapon(4);

        yield return WaitSpirit(w);
        SpiritProjectile spirit = WeaponTestKit.FirstActive<SpiritProjectile>();
        yield return MonsterTestFactory.WaitUntil(() => a.GetComponent<HealthSystem>().isDead, 5f);
        yield return MonsterTestFactory.Wait(0.3f);

        Assert.IsTrue(spirit.gameObject.activeSelf, "남은 틱이 있으면 소멸하지 않음");
        Assert.IsFalse(spirit.IsChasing, "대기로 복귀");
        Assert.AreEqual(3, spirit.RemainingHits);
    }

    [UnityTest]
    public IEnumerator Spirit_사거리_밖의_적은_추적하지_않는다()
    {
        GameObject far = MonsterTestFactory.CreateTarget(new Vector2(9f, 0f), 1000f);
        Weapon w = NewSpiritWeapon(3);

        yield return WaitSpirit(w);
        SpiritProjectile spirit = WeaponTestKit.FirstActive<SpiritProjectile>();
        yield return MonsterTestFactory.Wait(0.6f);

        Assert.IsFalse(spirit.IsChasing);
        Assert.AreEqual(1000f, WeaponTestKit.Hp(far), 0.01f);
    }
    #endregion

    #region 낙뢰
    private static Weapon NewLightning(int _extraStrike, float _areaRadius = 0.8f)
    {
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<LightningProjectile>(), WeaponTestKit.Marker(Vector2.zero), 25f, 0f, 8f);
        w.areaRadius = _areaRadius;
        if (_extraStrike > 0)
        {
            w.AddModifier(WeaponParam.StrikeCount, StatModifierMode.Add, _extraStrike);
        }
        return w;
    }

    private static GameObject[] RingTargets(int _count, float _radius)
    {
        GameObject[] list = new GameObject[_count];
        for (int i = 0; i < _count; ++i)
        {
            float rad = Mathf.PI * 2f * i / _count;
            list[i] = MonsterTestFactory.CreateTarget(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _radius, 100000f);
        }
        return list;
    }

    private static int CountHit(GameObject[] _list, out int _maxHitsOnOne)
    {
        int n = 0;
        _maxHitsOnOne = 0;
        foreach (GameObject go in _list)
        {
            float lost = 100000f - WeaponTestKit.Hp(go);
            if (lost > 0.01f)
            {
                ++n;
            }
            _maxHitsOnOne = Mathf.Max(_maxHitsOnOne, Mathf.RoundToInt(lost / 25f));
        }
        return n;
    }

    [UnityTest]
    public IEnumerator Lightning_StrikeCount_명을_중복없이_선택해_각각_한_번씩_타격한다()
    {
        GameObject[] targets = RingTargets(5, 5f);
        Weapon w = NewLightning(2);

        for (int trial = 0; trial < 5; ++trial)
        {
            foreach (GameObject t in targets)
            {
                t.GetComponent<HealthSystem>().currHP = 100000f;
            }

            w.Fire();
            LightningProjectile bolt = WeaponTestKit.FirstActive<LightningProjectile>();
            Assert.IsNotNull(bolt);
            Assert.AreEqual(3, bolt.TargetCount, "대상 3명 (StrikeCount 1 + 2)");

            yield return MonsterTestFactory.WaitUntil(() => bolt.IsStriking, 2f);
            Assert.IsTrue(bolt.IsStriking);
            yield return null;

            int hitCount = CountHit(targets, out int maxOnOne);
            Assert.AreEqual(3, hitCount, "서로 다른 3명이 맞아야 함 (trial " + trial + ")");
            Assert.AreEqual(1, maxOnOne, "한 명이 두 번 맞음 = 중복 선택 (trial " + trial + ")");

            yield return MonsterTestFactory.WaitUntil(() => false == bolt.gameObject.activeSelf, 2f);
        }
    }

    [UnityTest]
    public IEnumerator Lightning_적이_StrikeCount보다_적으면_있는_만큼만_고른다()
    {
        RingTargets(2, 5f);
        Weapon w = NewLightning(2);

        w.Fire();
        LightningProjectile bolt = WeaponTestKit.FirstActive<LightningProjectile>();
        Assert.IsNotNull(bolt);
        Assert.AreEqual(2, bolt.TargetCount);
        yield return null;
    }

    [Test]
    public void Lightning_사거리_안에_적이_없으면_발사하지_않는다()
    {
        MonsterTestFactory.CreateTarget(new Vector2(20f, 0f), 100f);
        Weapon w = NewLightning(0);

        w.Fire();
        Assert.AreEqual(0, WeaponTestKit.CountActive<LightningProjectile>());
    }

    [UnityTest]
    public IEnumerator Lightning_표식_시간_전에는_데미지가_없고_반경_안_적도_맞는다()
    {
        GameObject target = MonsterTestFactory.CreateTarget(new Vector2(3f, 0f), 1000f);
        GameObject near = MonsterTestFactory.CreateTarget(new Vector2(3.5f, 0f), 1000f);
        // 둘 중 하나가 선택돼도 두 명 모두 낙뢰 반경 안이라 둘 다 맞는다
        Weapon w = NewLightning(0);

        w.Fire();
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.AreEqual(1000f, WeaponTestKit.Hp(target), 0.01f);
        Assert.AreEqual(1000f, WeaponTestKit.Hp(near), 0.01f);

        yield return MonsterTestFactory.Wait(0.6f);
        Assert.AreEqual(975f, WeaponTestKit.Hp(target), 0.01f);
        Assert.AreEqual(975f, WeaponTestKit.Hp(near), 0.01f);
    }

    private static GameObject Probe(float _x, float _y)
    {
        return MonsterTestFactory.CreateTarget(new Vector2(_x, _y), 100000f);
    }

    private static float Lost(GameObject _go)
    {
        return 100000f - WeaponTestKit.Hp(_go);
    }

    [UnityTest]
    public IEnumerator Lightning_StormLink_3명이면_삼각형_안쪽과_테두리_적에게_60퍼센트()
    {
        MonsterTestFactory.CreateTarget(new Vector2(-3f, 0f), 100000f);
        MonsterTestFactory.CreateTarget(new Vector2(3f, 0f), 100000f);
        MonsterTestFactory.CreateTarget(new Vector2(0f, 4f), 100000f);
        Weapon w = NewLightning(2);
        w.AddModifier(WeaponParam.FlagStormLink, StatModifierMode.Add, 1f);

        w.Fire();
        Assert.AreEqual(3, WeaponTestKit.FirstActive<LightningProjectile>().TargetCount);

        // 대상 선정 이후에 만들어 선정 대상에서 빠진다
        GameObject inside = Probe(0f, 1.5f);
        GameObject edge = Probe(0f, -0.2f);
        GameObject outsideBelow = Probe(0f, -1.5f);
        GameObject outsideNearEdge = Probe(0f, -0.5f);
        GameObject outsideSide = Probe(-3.5f, 3.5f);

        yield return MonsterTestFactory.Wait(0.9f);

        Assert.AreEqual(15f, Lost(inside), 0.01f, "삼각형 안쪽");
        Assert.AreEqual(15f, Lost(edge), 0.01f, "테두리 폭(0.3) 안");
        Assert.AreEqual(0f, Lost(outsideBelow), 0.01f, "삼각형 밖");
        Assert.AreEqual(0f, Lost(outsideNearEdge), 0.01f, "테두리 폭 바깥");
        Assert.AreEqual(0f, Lost(outsideSide), 0.01f, "삼각형 밖(꼭짓점 바깥쪽)");
    }

    [UnityTest]
    public IEnumerator Lightning_StormLink_플래그가_없으면_연결_데미지가_없다()
    {
        MonsterTestFactory.CreateTarget(new Vector2(-3f, 0f), 100000f);
        MonsterTestFactory.CreateTarget(new Vector2(3f, 0f), 100000f);
        MonsterTestFactory.CreateTarget(new Vector2(0f, 4f), 100000f);
        Weapon w = NewLightning(2);

        w.Fire();
        GameObject inside = Probe(0f, 1.5f);

        yield return MonsterTestFactory.Wait(0.9f);

        Assert.AreEqual(0f, Lost(inside), 0.01f);
    }

    [UnityTest]
    public IEnumerator Lightning_StormLink_2명이면_두_점_사이_선_두께_안의_적에게만()
    {
        MonsterTestFactory.CreateTarget(new Vector2(-3f, 0f), 100000f);
        MonsterTestFactory.CreateTarget(new Vector2(3f, 0f), 100000f);
        Weapon w = NewLightning(1);
        w.AddModifier(WeaponParam.FlagStormLink, StatModifierMode.Add, 1f);

        w.Fire();
        Assert.AreEqual(2, WeaponTestKit.FirstActive<LightningProjectile>().TargetCount);

        GameObject center = Probe(0f, 0f);
        GameObject above = Probe(1f, 0.25f);
        GameObject tooFar = Probe(0f, 0.5f);
        GameObject beyondEnd = Probe(4.8f, 0f);

        yield return MonsterTestFactory.Wait(0.9f);

        Assert.AreEqual(15f, Lost(center), 0.01f, "선 위");
        Assert.AreEqual(15f, Lost(above), 0.01f, "선에서 0.25 (반두께 0.3 안)");
        Assert.AreEqual(0f, Lost(tooFar), 0.01f, "선에서 0.5");
        Assert.AreEqual(0f, Lost(beyondEnd), 0.01f, "선분 끝 바깥");
    }

    [UnityTest]
    public IEnumerator Lightning_StormLink_1명이면_연결_데미지가_없다()
    {
        MonsterTestFactory.CreateTarget(new Vector2(3f, 0f), 100000f);
        Weapon w = NewLightning(0);
        w.AddModifier(WeaponParam.FlagStormLink, StatModifierMode.Add, 1f);

        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.FirstActive<LightningProjectile>().TargetCount);
        GameObject probe = Probe(3f, 2.0f);

        yield return MonsterTestFactory.Wait(0.9f);

        Assert.AreEqual(0f, Lost(probe), 0.01f);
    }
    #endregion

    #region 톱날
    private static Camera NewCamera()
    {
        GameObject go = new GameObject("TestCamera");
        go.transform.position = new Vector3(0f, 0f, -10f);
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 2f;
        cam.cullingMask = -1;
        WeaponTestKit.Track(go);
        return cam;
    }

    private static GameObject HugeTarget()
    {
        GameObject t = MonsterTestFactory.CreateTarget(Vector2.zero, 100000f);
        BoxCollider2D box = t.AddComponent<BoxCollider2D>();
        box.size = new Vector2(200f, 200f);
        Physics2D.SyncTransforms();
        return t;
    }

    private static Weapon NewSaw()
    {
        return WeaponTestKit.Make(WEAPON_TYPE.Wave, WeaponTestKit.Template<SawProjectile>(), WeaponTestKit.Marker(Vector2.zero), 15f, 0f, 5f, 1);
    }

    [Test]
    public void Saw_카메라가_없으면_발사하지_않는다()
    {
        Weapon w = NewSaw();
        if (Camera.allCamerasCount > 0)
        {
            Assert.Ignore("다른 카메라가 있어 확인 불가");
        }

        w.Fire();
        Assert.AreEqual(0, WeaponTestKit.CountActive<SawProjectile>());
    }

    [UnityTest]
    public IEnumerator Saw_관통하며_같은_적은_한_번만_타격하고_화면_밖으로_나가면_소멸한다()
    {
        NewCamera();
        GameObject target = HugeTarget();
        Weapon w = NewSaw();

        w.Fire();
        SawProjectile saw = WeaponTestKit.FirstActive<SawProjectile>();
        Assert.IsNotNull(saw);

        yield return MonsterTestFactory.WaitUntil(() => false == saw.gameObject.activeSelf, 8f);

        Assert.IsFalse(saw.gameObject.activeSelf, "화면 밖에서 소멸");
        Assert.AreEqual(100000f - 15f, WeaponTestKit.Hp(target), 0.01f, "같은 적은 한 번만");
    }

    [UnityTest]
    public IEnumerator Saw_FlagSawBounce_튕긴_뒤_소멸하지_않고_타격_기록이_초기화된다()
    {
        NewCamera();
        GameObject target = HugeTarget();
        Weapon w = NewSaw();
        w.AddModifier(WeaponParam.FlagSawBounce, StatModifierMode.Add, 1f);

        w.Fire();
        SawProjectile saw = WeaponTestKit.FirstActive<SawProjectile>();
        Assert.IsNotNull(saw);

        yield return MonsterTestFactory.Wait(5f);

        Assert.IsTrue(saw.gameObject.activeSelf, "튕긴 뒤에도 소멸하지 않음");
        Assert.LessOrEqual(WeaponTestKit.Hp(target), 100000f - 30f, "튕길 때마다 같은 적을 다시 타격");
    }

    [UnityTest]
    public IEnumerator Saw_날아가던_톱날도_FlagSawBounce를_얻으면_소멸하지_않는다()
    {
        NewCamera();
        Weapon w = NewSaw();

        w.Fire();
        SawProjectile saw = WeaponTestKit.FirstActive<SawProjectile>();
        Assert.IsNotNull(saw);
        yield return MonsterTestFactory.Wait(0.2f);

        w.AddModifier(WeaponParam.FlagSawBounce, StatModifierMode.Add, 1f);
        yield return MonsterTestFactory.Wait(5f);

        Assert.IsTrue(saw.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Saw_크기_업그레이드가_날아가던_톱날에_반영된다()
    {
        NewCamera();
        Weapon w = NewSaw();

        w.Fire();
        SawProjectile saw = WeaponTestKit.FirstActive<SawProjectile>();
        float before = saw.CurrentRadius;
        yield return null;

        w.AddModifier(WeaponParam.ProjectileScale, StatModifierMode.Multiply, 0.2f);
        yield return null;
        yield return null;

        Assert.AreEqual(before * 1.2f, saw.CurrentRadius, 0.001f);
    }
    #endregion

    #region 회오리
    private static Weapon NewTornado(float _lifetime)
    {
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<TornadoProjectile>(), WeaponTestKit.Marker(Vector2.zero), 6f, 0f, 5f, 1);
        w.lifetime = _lifetime;
        return w;
    }

    [UnityTest]
    public IEnumerator Tornado_기본은_수명이_지나면_소멸한다()
    {
        Weapon w = NewTornado(0.5f);

        w.Fire();
        TornadoProjectile t = WeaponTestKit.FirstActive<TornadoProjectile>();
        Assert.IsNotNull(t);
        float start = Time.time;

        yield return MonsterTestFactory.WaitUntil(() => false == t.gameObject.activeSelf, 3f);

        Assert.IsFalse(t.gameObject.activeSelf);
        Assert.GreaterOrEqual(Time.time - start, 0.4f);
    }

    [UnityTest]
    public IEnumerator Tornado_FlagTornadoPermanent면_수명을_무시한다()
    {
        Weapon w = NewTornado(0.3f);
        w.AddModifier(WeaponParam.FlagTornadoPermanent, StatModifierMode.Add, 1f);

        w.Fire();
        TornadoProjectile t = WeaponTestKit.FirstActive<TornadoProjectile>();

        yield return MonsterTestFactory.Wait(1.5f);

        Assert.IsTrue(t.gameObject.activeSelf);
        Assert.AreEqual(1f, t.RemainRate, 0.0001f);
    }

    [UnityTest]
    public IEnumerator Tornado_살아있는_회오리도_영구_플래그를_얻으면_소멸하지_않는다()
    {
        Weapon w = NewTornado(1.0f);

        w.Fire();
        TornadoProjectile t = WeaponTestKit.FirstActive<TornadoProjectile>();
        yield return MonsterTestFactory.Wait(0.3f);
        Assert.IsTrue(t.gameObject.activeSelf);

        w.AddModifier(WeaponParam.FlagTornadoPermanent, StatModifierMode.Add, 1f);
        yield return MonsterTestFactory.Wait(1.5f);

        Assert.IsTrue(t.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Tornado_영구_상태에서_leashDistance_밖이면_주인_쪽으로_돌아온다()
    {
        Weapon w = NewTornado(1.0f);
        w.AddModifier(WeaponParam.FlagTornadoPermanent, StatModifierMode.Add, 1f);

        w.Fire();
        TornadoProjectile t = WeaponTestKit.FirstActive<TornadoProjectile>();
        t.transform.position = new Vector3(10f, 0f, 0f);

        yield return MonsterTestFactory.Wait(1.5f);

        float distance = Vector2.Distance(t.transform.position, Vector2.zero);
        Assert.Less(distance, 9f, "주인 쪽으로 이동해야 함");
    }

    [Test]
    public void Tornado_주인_근처에_생성된다()
    {
        Transform owner = WeaponTestKit.Marker(new Vector2(2f, 3f));
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<TornadoProjectile>(), owner, 6f, 0f, 5f, 1);
        for (int i = 0; i < 10; ++i)
        {
            w.Fire();
        }

        foreach (TornadoProjectile t in Object.FindObjectsByType<TornadoProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Assert.LessOrEqual(Vector2.Distance(t.transform.position, owner.position), 1.51f);
        }
    }
    #endregion

    #region 독 장판 회귀
    private static Weapon NewPoison(int _layerMask)
    {
        return WeaponTestKit.Make(WEAPON_TYPE.Repeat, WeaponTestKit.Template<PoisonZoneProjectile>(true), WeaponTestKit.Marker(Vector2.zero), 4f, 0f, 5f, 1, _layerMask);
    }

    [Test]
    public void Poison_적이_없으면_발사하지_않는다()
    {
        Weapon w = NewPoison(Mask);

        w.Fire();
        Assert.AreEqual(0, WeaponTestKit.CountActive<PoisonZoneProjectile>());
    }

    [UnityTest]
    public IEnumerator Poison_장판_안의_적만_틱_데미지를_받는다()
    {
        GameObject inside = MonsterTestFactory.CreateTarget(new Vector2(0.2f, 0f), 1000f);
        GameObject outside = MonsterTestFactory.CreateTarget(new Vector2(2.5f, 0f), 1000f);
        Weapon w = NewPoison(Mask);

        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.CountActive<PoisonZoneProjectile>());
        yield return MonsterTestFactory.Wait(0.2f);

        Assert.AreEqual(996f, WeaponTestKit.Hp(inside), 0.01f, "착지 즉시 첫 타격");
        Assert.AreEqual(1000f, WeaponTestKit.Hp(outside), 0.01f);

        yield return MonsterTestFactory.Wait(0.6f);
        Assert.LessOrEqual(WeaponTestKit.Hp(inside), 992f + 0.01f, "다음 틱");
    }

    [UnityTest]
    public IEnumerator Poison_FlagSlowZone_없으면_둔화하지_않고_있으면_둔화한다()
    {
        MonsterTestFactory.Rig m = NewMonster(new Vector2(0.2f, 0f));
        Weapon w = NewPoison(1 << 0);

        w.Fire();
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.AreEqual(1f, m.brain.Context.SlowMultiplier, 0.0001f, "플래그 없이는 둔화 없음");
        Assert.Less(m.health.currHP, 100000f, "데미지는 들어감");

        w.AddModifier(WeaponParam.FlagSlowZone, StatModifierMode.Add, 1f);
        yield return MonsterTestFactory.Wait(0.8f);
        Assert.Less(m.brain.Context.SlowMultiplier, 1f, "플래그가 켜지면 둔화");
    }
    #endregion
}
