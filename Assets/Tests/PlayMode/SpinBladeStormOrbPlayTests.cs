using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 회전 칼날(SpinBlade) / 전격 구체(StormOrb) 검증. 프리팹 없이 코드로 구성
/// 몬스터 루트 레이어(27/28) 로 진영을 나누고, 전격 구체는 몸체(레이어 0) 콜라이더에 닿는다
/// </summary>
public class SpinBladeStormOrbPlayTests
{
    private const int FactionA = MonsterTestFactory.MonsterLayer; // 27
    private const int FactionB = 28;
    private const float Hp = 100000f;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [TearDown]
    public void TearDown()
    {
        WeaponTestKit.Cleanup();
    }

    #region helper
    private static MonsterTestFactory.Rig NewMonster(Vector2 _pos, int _layer = FactionA, float _hp = Hp, params MonsterKeyword[] _keywords)
    {
        MonsterTestFactory.Rig r = MonsterTestFactory.Create(MonsterTestFactory.NewData(0f, _keywords), _pos, _hp, true);
        // 트리거 이벤트가 오도록 실제 몬스터처럼 Rigidbody2D 를 둔다 (정적 콜라이더와 키네마틱 트리거는 이벤트가 없다)
        Rigidbody2D mrb = r.body.AddComponent<Rigidbody2D>();
        mrb.bodyType = RigidbodyType2D.Kinematic;
        mrb.sleepMode = RigidbodySleepMode2D.NeverSleep; // 정지한 상태에서도 OnTriggerStay2D 가 오도록
        r.root.layer = _layer;
        r.body.layer = _layer; // 수집은 HealthSystem 오브젝트(몸체) 레이어 기준
        MonsterTestFactory.Init(r, WeaponTestKit.Marker(new Vector2(500f, 500f)));
        return r;
    }

    // 프로젝트 충돌 매트릭스상 Default(0) 는 아무와도 충돌하지 않으므로 실제 레이어 (RAttack 12 / RMonster 10) 를 쓴다
    private const int OrbAttackLayer = 12;
    private const int OrbMonsterBodyLayer = 10;

    private static MonsterTestFactory.Rig NewOrbMonster(Vector2 _pos)
    {
        MonsterTestFactory.Rig r = NewMonster(_pos);
        r.body.layer = OrbMonsterBodyLayer;
        return r;
    }

    private static Vector2 Polar(float _deg, float _r)
    {
        float rad = _deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _r;
    }

    private static void SetField(object _o, string _name, object _v)
    {
        FieldInfo f = _o.GetType().GetField(_name, Any);
        Assert.IsNotNull(f, _name);
        f.SetValue(_o, _v);
    }

    private static Transform[] MakeBlades(Transform _parent, int _count)
    {
        Transform[] blades = new Transform[_count];
        for (int i = 0; i < _count; ++i)
        {
            GameObject b = new GameObject("Blade" + i);
            b.transform.SetParent(_parent, false);
            blades[i] = b.transform;
        }
        return blades;
    }

    private static SpinBladeProjectile SpinTemplate()
    {
        SpinBladeProjectile p = WeaponTestKit.Template<SpinBladeProjectile>();
        SetField(p, "blades", MakeBlades(p.transform, 2));
        return p;
    }

    private static Weapon SpinWeapon(Transform _owner, int _mask = 1 << FactionA, float _damage = 100f)
    {
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, SpinTemplate(), _owner, _damage, 1f / 60f, 0f, 1, _mask);
        w.statTags = WeaponStatTag.Damage;
        return w;
    }

    private static void SetTimer(Weapon _w, float _v)
    {
        typeof(Weapon).GetField("timer", Any).SetValue(_w, _v);
    }

    private static PlayerStats NewPlayer()
    {
        GameObject go = new GameObject("TestPlayer");
        go.SetActive(false);
        WeaponTestKit.Track(go);
        return go.AddComponent<PlayerStats>();
    }
    #endregion

    #region SpinBlade 타이밍
    [UnityTest]
    public IEnumerator SpinBlade_타이머는_0에서_시작해_60초_뒤에_첫_발동한다()
    {
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        float t0 = Time.time;
        yield return MonsterTestFactory.Wait(0.5f);

        float elapsed = Time.time - t0;
        Assert.AreEqual(60f, w.FinalCoolTime, 0.001f);
        Assert.AreEqual(60f - elapsed, w.RemainingCoolTime, 0.1f, "남은 쿨타임이 경과 시간만큼 줄어야 함");
        Assert.Greater(w.RemainingCoolTime, 59f, "획득 직후에는 약 60초가 남아 있어야 함 (바로 발동하지 않음)");
        Assert.AreEqual(0, WeaponTestKit.CountActive<SpinBladeProjectile>(), "60초 전에는 발동하지 않음");

        SetTimer(w, 59.95f);
        yield return MonsterTestFactory.WaitUntil(() => WeaponTestKit.CountActive<SpinBladeProjectile>() > 0, 0.3f);
        Assert.AreEqual(1, WeaponTestKit.CountActive<SpinBladeProjectile>(), "타이머가 60초에 닿으면 발동");
        Assert.Greater(w.RemainingCoolTime, 59f, "발동 후 쿨타임이 다시 찬다");
    }

    [UnityTest]
    public IEnumerator SpinBlade_예고는_남은_쿨타임_1_5초_이하에서만_표시된다()
    {
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));

        GameObject tgo = new GameObject("Telegraph");
        tgo.SetActive(false);
        tgo.transform.SetParent(w.transform, false);
        SpinBladeTelegraph tg = tgo.AddComponent<SpinBladeTelegraph>();
        SetField(tg, "blades", MakeBlades(tgo.transform, 2));
        tgo.SetActive(true);

        yield return null;
        Assert.IsFalse(tg.IsVisible, "쿨타임이 충분히 남으면 숨김");

        SetTimer(w, 58.4f); // 남은 1.6
        yield return null;
        Assert.IsFalse(tg.IsVisible, "1.6초 남음 -> 숨김");

        SetTimer(w, 58.6f); // 남은 1.4
        yield return null;
        Assert.IsTrue(tg.IsVisible, "1.4초 남음 -> 표시");

        SetTimer(w, 59.99f);
        yield return MonsterTestFactory.Wait(0.1f);
        Assert.IsFalse(tg.IsVisible, "발동 직후 쿨타임이 다시 차면 숨김");
    }
    #endregion

    #region SpinBlade 타격
    [UnityTest]
    public IEnumerator SpinBlade_발동하면_같은_진영_전부_타격하고_사라지며_상대_진영은_타격하지_않는다()
    {
        MonsterTestFactory.Rig[] a =
        {
            NewMonster(Polar(10f, 3f)), NewMonster(Polar(100f, 3f)), NewMonster(Polar(170f, 3f))
            , NewMonster(Polar(250f, 200f)), NewMonster(Polar(320f, 3f))
        };
        MonsterTestFactory.Rig b = NewMonster(Polar(45f, 3f), FactionB);
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));

        w.Fire();
        SpinBladeProjectile p = WeaponTestKit.FirstActive<SpinBladeProjectile>();
        Assert.IsNotNull(p);
        Assert.AreEqual(5, p.TargetCount, "화면 밖 포함 같은 진영 전부, 상대 진영 제외");
        Assert.AreEqual(Hp, a[2].health.CurrHP, 0.001f, "170도 대상은 회전 시작 시점에는 아직 맞지 않음 (나눠서 처리)");
        for (int i = 0; i < p.transform.childCount; ++i)
        {
            Assert.AreEqual(p.BladeRadius, Vector2.Distance(p.transform.GetChild(i).position, Vector2.zero), 0.01f);
        }

        yield return MonsterTestFactory.Wait(0.7f);

        foreach (MonsterTestFactory.Rig r in a)
        {
            Assert.AreEqual(Hp - 100f, r.health.CurrHP, 0.01f);
        }
        Assert.AreEqual(Hp, b.health.CurrHP, 0.001f, "상대 진영은 미타격");
        Assert.AreEqual(0, WeaponTestKit.CountActive<SpinBladeProjectile>(), "회전이 끝나면 사라짐");
    }

    [UnityTest]
    public IEnumerator SpinBlade_처리는_회전_시간에_분산되어_도중에는_일부만_끝에는_전부_처리된다()
    {
        const int count = 20;
        MonsterTestFactory.Rig[] all = new MonsterTestFactory.Rig[count];
        for (int i = 0; i < count; ++i)
        {
            all[i] = NewMonster(Polar(i * 17f + 3f, 3f));
        }
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        System.Func<int> hits = () =>
        {
            int n = 0;
            foreach (MonsterTestFactory.Rig r in all)
            {
                if (r.health.CurrHP < Hp - 99f) ++n;
            }
            return n;
        };

        w.Fire();
        Assert.AreEqual(0, hits(), "발동 직후에는 아직 없음");
        float t0 = Time.time;
        while (Time.time - t0 < 0.2f)
        {
            yield return null;
        }
        int mid = hits();
        Assert.Greater(mid, 0, "도중에 일부는 처리");
        Assert.Less(mid, count, "도중에는 전부 처리되지 않음");

        yield return MonsterTestFactory.Wait(0.5f);
        Assert.AreEqual(count, hits(), "끝에는 전부");
    }

    private static HealthSystem NewBat(Vector2 _pos)
    {
        GameObject bat = new GameObject("Bat");
        bat.SetActive(false);
        bat.layer = FactionA;
        bat.transform.position = _pos;
        WeaponTestKit.Track(bat);
        bat.AddComponent<CircleCollider2D>();
        HealthSystem hs = bat.AddComponent<HealthSystem>();
        hs.maxHP = Hp;
        hs.currHP = Hp;
        bat.AddComponent<DamagePipeline>();
        bat.SetActive(true);
        return hs;
    }

    [UnityTest]
    public IEnumerator SpinBlade_MonsterBrain_없는_적도_타격하고_별10이면_즉사시킨다()
    {
        HealthSystem bat = NewBat(Polar(40f, 3f));
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        w.Fire();
        Assert.AreEqual(1, WeaponTestKit.FirstActive<SpinBladeProjectile>().TargetCount);
        yield return MonsterTestFactory.Wait(0.7f);
        Assert.AreEqual(Hp - 100f, bat.CurrHP, 0.01f);
        Assert.IsFalse(bat.isDead);

        w.AddModifier(WeaponParam.FlagSpinBladeExecute, StatModifierMode.Add, 1f);
        w.Fire();
        yield return MonsterTestFactory.Wait(0.7f);
        Assert.IsTrue(bat.isDead);
        Assert.IsTrue(bat.IsExecuted);
    }

    [UnityTest]
    public IEnumerator SpinBlade_회전_시작_뒤에_생긴_몬스터는_대상이_아니다()
    {
        NewMonster(Polar(10f, 3f));
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        w.Fire();
        MonsterTestFactory.Rig late = NewMonster(Polar(10f, 3f));

        yield return MonsterTestFactory.Wait(0.7f);
        Assert.AreEqual(Hp, late.health.CurrHP, 0.001f);
    }

    [UnityTest]
    public IEnumerator SpinBlade_대상이_65개_이상이어도_전부_처리한다()
    {
        const int count = 72;
        MonsterTestFactory.Rig[] all = new MonsterTestFactory.Rig[count];
        for (int i = 0; i < count; ++i)
        {
            all[i] = NewMonster(Polar(i * 5f + 1f, (0 == i % 3) ? 150f : 3f + (i % 5)));
        }
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));

        w.Fire();
        Assert.AreEqual(count, WeaponTestKit.FirstActive<SpinBladeProjectile>().TargetCount);

        yield return MonsterTestFactory.Wait(0.7f);
        int hit = 0;
        foreach (MonsterTestFactory.Rig r in all)
        {
            if (r.health.CurrHP < Hp - 99f)
            {
                ++hit;
            }
        }
        Assert.AreEqual(count, hit);
    }

    [UnityTest]
    public IEnumerator SpinBlade_별10_즉사는_부활_분열을_무시하고_다운_중인_몬스터도_처치한다()
    {
        ReviveKeyword rv = MonsterTestFactory.So<ReviveKeyword>();
        rv.reviveCount = 2;
        rv.reviveDelay = 0.1f;
        rv.hpRatio = 0.5f;

        MonsterTestFactory.Rig plain = NewMonster(Polar(30f, 3f), FactionA, Hp, rv);
        MonsterTestFactory.Rig downed = NewMonster(Polar(200f, 3f), FactionA, 500f, rv);
        downed.health.Kill();
        Assert.IsTrue(downed.health.IsDowned);

        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero), 1 << FactionA, 100f);
        w.AddModifier(WeaponParam.FlagSpinBladeExecute, StatModifierMode.Add, 1f);
        w.Fire();

        yield return MonsterTestFactory.Wait(0.7f);
        Assert.IsTrue(plain.health.isDead);
        Assert.IsFalse(plain.health.IsDowned, "부활 가로채기를 무시");
        Assert.AreEqual(MonsterStateId.Dead, plain.brain.CurrentState);
        Assert.IsTrue(downed.health.isDead);
        Assert.IsFalse(downed.health.IsDowned, "다운 상태 몬스터도 최종 사망");
        Assert.AreEqual(MonsterStateId.Dead, downed.brain.CurrentState);

        yield return MonsterTestFactory.Wait(0.4f);
        Assert.IsTrue(plain.health.isDead, "다시 일어나지 않음");
        Assert.IsTrue(downed.health.isDead, "다시 일어나지 않음");
    }

    [UnityTest]
    public IEnumerator SpinBlade_즉사가_아니면_다운_중인_몬스터는_건드리지_않는다()
    {
        ReviveKeyword rv = MonsterTestFactory.So<ReviveKeyword>();
        rv.reviveCount = 1;
        rv.reviveDelay = 5f;
        rv.hpRatio = 0.5f;
        MonsterTestFactory.Rig downed = NewMonster(Polar(200f, 3f), FactionA, 500f, rv);
        downed.health.Kill();

        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        w.Fire();
        Assert.AreEqual(0, WeaponTestKit.FirstActive<SpinBladeProjectile>().TargetCount);

        yield return MonsterTestFactory.Wait(0.6f);
        Assert.IsTrue(downed.health.IsDowned);
    }

    [UnityTest]
    public IEnumerator SpinBlade_즉사의_통계_데미지는_남은_HP_기준이다()
    {
        EventManager em = EventManager.Instance;
        float sum = 0f;
        int calls = 0;
        System.Action<PlayerSide, Weapon, float> handler = (side, weapon, dmg) => { sum += dmg; ++calls; };
        em.OnWeaponDamage += handler;
        try
        {
            MonsterTestFactory.Rig m = NewMonster(Polar(30f, 3f), FactionA, 250f);
            Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero), 1 << FactionA, 100f);
            w.SetOwnerStats(NewPlayer());
            w.AddModifier(WeaponParam.FlagSpinBladeExecute, StatModifierMode.Add, 1f);
            w.Fire();

            yield return MonsterTestFactory.Wait(0.7f);
            Assert.IsTrue(m.health.isDead);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(250f, sum, 0.01f);
        }
        finally
        {
            em.OnWeaponDamage -= handler;
        }
    }

    [UnityTest]
    public IEnumerator SpinBlade_회전_도중_무기가_꺼져도_안전하고_더_이상_타격하지_않는다()
    {
        MonsterTestFactory.Rig late = NewMonster(Polar(170f, 3f)); // 회전 후반에 맞을 대상
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        w.Fire();
        yield return null;
        Assert.AreEqual(1, WeaponTestKit.CountActive<SpinBladeProjectile>());

        w.gameObject.SetActive(false);
        yield return null;
        yield return null;
        Assert.AreEqual(0, WeaponTestKit.CountActive<SpinBladeProjectile>(), "무기가 꺼지면 칼날도 사라짐");

        yield return MonsterTestFactory.Wait(0.6f);
        Assert.AreEqual(Hp, late.health.CurrHP, 0.001f, "꺼진 뒤에는 타격하지 않음");
    }

    [UnityTest]
    public IEnumerator SpinBlade_회전_도중_무기가_파괴돼도_예외가_없다()
    {
        NewMonster(Polar(170f, 3f));
        Weapon w = SpinWeapon(WeaponTestKit.Marker(Vector2.zero));
        w.Fire();
        yield return null;

        Object.Destroy(w.gameObject);
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.AreEqual(0, WeaponTestKit.CountActive<SpinBladeProjectile>());
    }
    #endregion

    #region StormOrb
    private static StormOrbProjectile OrbTemplate()
    {
        StormOrbProjectile t = WeaponTestKit.Template<StormOrbProjectile>(true);
        t.gameObject.AddComponent<OrbitMovement>();
        CircleCollider2D col = t.gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.3f;
        Rigidbody2D rb = t.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        return t;
    }

    private static Weapon OrbWeapon(Transform _owner, bool _stationary)
    {
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Persistent, OrbTemplate(), _owner, 9f, 2f, 2f, 2, 1 << OrbMonsterBodyLayer);
        w.gameObject.layer = OrbAttackLayer;
        w.statTags = WeaponStatTag.Damage | WeaponStatTag.Range | WeaponStatTag.ProjectileCount;
        if (_stationary)
        {
            // 공전 속도 0 (1 + (-1)). 구체가 (+r, 0) / (-r, 0) 에 멈춘다
            w.AddModifier(WeaponParam.OrbitSpeedRate, StatModifierMode.Multiply, -1f);
        }
        return w;
    }

    [UnityTest]
    public IEnumerator StormOrb_같은_구체는_같은_적을_0_5초에_한_번만_타격한다()
    {
        MonsterTestFactory.Rig m = NewOrbMonster(new Vector2(2f, 0f));
        Weapon w = OrbWeapon(WeaponTestKit.Marker(Vector2.zero), true);
        Assert.AreEqual(2, w.FinalFireCount);

        yield return MonsterTestFactory.Wait(0.3f);
        Assert.AreEqual(Hp - 9f, m.health.CurrHP, 0.01f, "0.3초 안에는 정확히 1회");

        yield return MonsterTestFactory.Wait(0.9f);
        float loss = Hp - m.health.CurrHP;
        Assert.GreaterOrEqual(loss, 18f - 0.01f, "0.5초 뒤 다시 타격");
        Assert.LessOrEqual(loss, 27f + 0.01f, "매 프레임 타격하면 안 됨 (1.2초 동안 최대 3회)");
        Assert.AreEqual(0f, Mathf.Repeat(loss, 9f) > 4.5f ? 9f - Mathf.Repeat(loss, 9f) : Mathf.Repeat(loss, 9f), 0.01f, "9 의 배수");
    }

    [UnityTest]
    public IEnumerator StormOrb_공전_반경은_Range_패시브를_반영한다()
    {
        Transform owner = WeaponTestKit.Marker(new Vector2(10f, 5f));
        Weapon w = OrbWeapon(owner, false);
        PlayerStats player = NewPlayer();
        player.AddModifier(ValueType.Range, StatModifierMode.Multiply, 0.5f);
        w.SetOwnerStats(player);

        yield return MonsterTestFactory.Wait(0.1f);
        StormOrbProjectile orb = WeaponTestKit.FirstActive<StormOrbProjectile>();
        Assert.IsNotNull(orb);
        Assert.AreEqual(3f, orb.FinalOrbitRadius, 0.001f, "(2 + 0) x 1.5");
        Assert.AreEqual(3f, Vector2.Distance(orb.transform.position, owner.position), 0.05f);

        // Range 태그를 빼면 패시브 영향 없음
        w.statTags = WeaponStatTag.Damage;
        yield return null;
        Assert.AreEqual(2f, orb.FinalOrbitRadius, 0.001f);
        yield return null;
        Assert.AreEqual(2f, Vector2.Distance(orb.transform.position, owner.position), 0.05f);

        // 반경 업그레이드 (+20%)
        w.AddModifier(WeaponParam.OrbitRadius, StatModifierMode.Multiply, 0.2f);
        Assert.AreEqual(2.4f, orb.FinalOrbitRadius, 0.001f);
    }

    [UnityTest]
    public IEnumerator StormOrb_공전_속도와_구체_수_업그레이드가_반영된다()
    {
        Weapon w = OrbWeapon(WeaponTestKit.Marker(Vector2.zero), false);
        yield return MonsterTestFactory.Wait(0.1f);
        StormOrbProjectile orb = WeaponTestKit.FirstActive<StormOrbProjectile>();
        Assert.AreEqual(2, WeaponTestKit.CountActive<StormOrbProjectile>());
        Assert.AreEqual(200f, orb.FinalAngularSpeed, 0.01f, "1.8초에 1바퀴");

        w.AddModifier(WeaponParam.OrbitSpeedRate, StatModifierMode.Multiply, 0.2f);
        Assert.AreEqual(240f, orb.FinalAngularSpeed, 0.01f);

        w.AddModifier(WeaponParam.FireCount, StatModifierMode.Add, 2f);
        yield return MonsterTestFactory.Wait(0.1f);
        Assert.AreEqual(4, WeaponTestKit.CountActive<StormOrbProjectile>(), "★5 구체 +2");
    }

    [UnityTest]
    public IEnumerator StormOrb_별10_전기선은_선_위의_적을_0_3초마다_60퍼센트로_한_번씩_타격한다()
    {
        MonsterTestFactory.Rig m = NewOrbMonster(new Vector2(0f, 0f)); // 두 구체 (+-2, 0) 사이 한가운데
        Weapon w = OrbWeapon(WeaponTestKit.Marker(Vector2.zero), true);

        yield return MonsterTestFactory.Wait(0.5f);
        Assert.AreEqual(Hp, m.health.CurrHP, 0.001f, "플래그 없이는 선 데미지 없음");

        w.AddModifier(WeaponParam.FlagStormOrbLink, StatModifierMode.Add, 1f);
        yield return MonsterTestFactory.Wait(0.5f);

        float loss = Hp - m.health.CurrHP;
        float tick = 9f * 0.6f;
        float ticks = loss / tick;
        Assert.AreEqual(Mathf.Round(ticks), ticks, 0.01f, "틱 데미지 = 9 x 0.6 의 배수");
        Assert.GreaterOrEqual(ticks, 2f - 0.01f, "0.3초 간격 틱");
        Assert.LessOrEqual(ticks, 3f + 0.01f, "선이 1개이므로 틱당 1회만");
    }
    #endregion
}
