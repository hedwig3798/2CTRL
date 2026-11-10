using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 사운드 시스템 순수 로직 검증 (SoundThrottle / SoundLibrary / CombatSoundBinder.GetWeaponKey / SoundManager.GetPan)
/// - SoundEvent / SoundLibrary 는 CreateInstance 로 만들고 private 직렬화 필드는 리플렉션으로 채운다.
/// </summary>
public class SoundEditTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    private static void SetField(object _target, string _name, object _value)
    {
        FieldInfo f = _target.GetType().GetField(_name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(f, "필드 없음: " + _name);
        f.SetValue(_target, _value);
    }

    private SoundEvent MakeEvent(SoundId _id)
    {
        SoundEvent e = ScriptableObject.CreateInstance<SoundEvent>();
        SetField(e, "id", _id);
        created.Add(e);
        return e;
    }

    private SoundLibrary MakeLibrary(SoundEvent[] _events, WeaponSoundEntry[] _weapons)
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        SetField(lib, "events", new List<SoundEvent>(_events));
        SetField(lib, "weapons", _weapons);
        lib.InvalidateCache();
        created.Add(lib);
        return lib;
    }

    #region SoundThrottle
    [Test]
    public void Throttle_첫_재생은_항상_허용()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.UI_CLICK, null);
        Assert.IsTrue(t.TryAcquire(key, 0f, 1f, 1));
        Assert.AreEqual(1, t.GetVoices(key));
    }

    [Test]
    public void Throttle_최소간격_안이면_거부_경계에서_허용()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.UI_CLICK, null);
        Assert.IsTrue(t.TryAcquire(key, 10f, 0.5f, 0));
        Assert.IsFalse(t.TryAcquire(key, 10.25f, 0.5f, 0));
        Assert.IsTrue(t.TryAcquire(key, 10.5f, 0.5f, 0), "간격과 정확히 같으면 허용");
    }

    [Test]
    public void Throttle_거부되면_시각과_개수가_바뀌지_않는다()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.UI_CLICK, null);
        t.TryAcquire(key, 0f, 1f, 0);
        t.TryAcquire(key, 0.5f, 1f, 0);
        Assert.AreEqual(1, t.GetVoices(key));
        Assert.IsTrue(t.TryAcquire(key, 1f, 1f, 0), "거부된 호출이 시각을 갱신하면 안 됨");
    }

    [Test]
    public void Throttle_동시재생수_초과시_거부_Release로_회복()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.PLR_HIT, PlayerSide.Left);
        Assert.IsTrue(t.TryAcquire(key, 0f, 0f, 2));
        Assert.IsTrue(t.TryAcquire(key, 0f, 0f, 2));
        Assert.IsFalse(t.TryAcquire(key, 0f, 0f, 2));
        t.Release(key);
        Assert.AreEqual(1, t.GetVoices(key));
        Assert.IsTrue(t.TryAcquire(key, 0f, 0f, 2));
    }

    [Test]
    public void Throttle_제한값_0이하면_제한없음()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.UI_CLICK, null);
        for (int i = 0; i < 50; ++i)
        {
            Assert.IsTrue(t.TryAcquire(key, 0f, 0f, 0));
            Assert.IsTrue(t.TryAcquire(key, 0f, -1f, -1));
        }
    }

    [Test]
    public void Throttle_Release는_없는_키와_0개에서_안전하고_음수가_되지_않는다()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.UI_CLICK, null);
        Assert.DoesNotThrow(() => t.Release(key));
        t.TryAcquire(key, 0f, 0f, 0);
        t.Release(key);
        t.Release(key);
        Assert.AreEqual(0, t.GetVoices(key));
        Assert.AreEqual(0, t.GetVoices(12345));
    }

    [Test]
    public void Throttle_키는_ID_진영별로_분리된다()
    {
        int shared = SoundThrottle.MakeKey(SoundId.PLR_HIT, null);
        int left = SoundThrottle.MakeKey(SoundId.PLR_HIT, PlayerSide.Left);
        int right = SoundThrottle.MakeKey(SoundId.PLR_HIT, PlayerSide.Right);
        int other = SoundThrottle.MakeKey(SoundId.UI_CLICK, PlayerSide.Left);
        Assert.AreEqual(4, new HashSet<int> { shared, left, right, other }.Count);

        var t = new SoundThrottle();
        Assert.IsTrue(t.TryAcquire(left, 0f, 1f, 1));
        Assert.IsFalse(t.TryAcquire(left, 0.1f, 1f, 1));
        Assert.IsTrue(t.TryAcquire(right, 0.1f, 1f, 1), "반대 진영은 별개");
        Assert.IsTrue(t.TryAcquire(shared, 0.1f, 1f, 1), "공유 키는 별개");
        Assert.IsTrue(t.TryAcquire(other, 0.1f, 1f, 1), "다른 ID 는 별개");
    }

    [Test]
    public void Throttle_Clear는_기록을_지운다()
    {
        var t = new SoundThrottle();
        int key = SoundThrottle.MakeKey(SoundId.UI_CLICK, null);
        t.TryAcquire(key, 0f, 1f, 1);
        t.Clear();
        Assert.AreEqual(0, t.GetVoices(key));
        Assert.IsTrue(t.TryAcquire(key, 0f, 1f, 1));
    }
    #endregion

    #region SoundLibrary
    [Test]
    public void Library_TryGetEvent_등록된_ID를_찾고_None과_미등록은_false()
    {
        SoundEvent click = MakeEvent(SoundId.UI_CLICK);
        SoundLibrary lib = MakeLibrary(new[] { click, MakeEvent(SoundId.None) }, new WeaponSoundEntry[0]);

        Assert.IsTrue(lib.TryGetEvent(SoundId.UI_CLICK, out SoundEvent found));
        Assert.AreSame(click, found);
        Assert.IsFalse(lib.TryGetEvent(SoundId.None, out SoundEvent none));
        Assert.IsNull(none);
        Assert.IsFalse(lib.TryGetEvent(SoundId.PLR_HIT, out _));
    }

    [Test]
    public void Library_TryGetEvent_같은_ID가_중복이면_먼저_등록된_것()
    {
        SoundEvent first = MakeEvent(SoundId.UI_CLICK);
        SoundEvent second = MakeEvent(SoundId.UI_CLICK);
        SoundLibrary lib = MakeLibrary(new[] { first, null, second }, new WeaponSoundEntry[0]);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("같은 ID"));
        Assert.IsTrue(lib.TryGetEvent(SoundId.UI_CLICK, out SoundEvent found));
        Assert.AreSame(first, found);
    }

    [Test]
    public void Library_TryGetWeapon_키로_찾고_빈키_null_미등록은_false()
    {
        var entries = new[]
        {
            new WeaponSoundEntry { weaponKey = "Boomerang", fire = SoundId.WPN_BOOMERANG, hit = SoundId.PLR_HIT },
            new WeaponSoundEntry { weaponKey = "", fire = SoundId.UI_CLICK },
        };
        SoundLibrary lib = MakeLibrary(new SoundEvent[0], entries);

        Assert.IsTrue(lib.TryGetWeapon("Boomerang", out WeaponSoundEntry e));
        Assert.AreEqual(SoundId.WPN_BOOMERANG, e.fire);
        Assert.AreEqual(SoundId.PLR_HIT, e.hit);
        Assert.IsFalse(lib.TryGetWeapon("boomerang", out _), "대소문자 구분");
        Assert.IsFalse(lib.TryGetWeapon("Unknown", out _));
        Assert.IsFalse(lib.TryGetWeapon("", out _));
        Assert.IsFalse(lib.TryGetWeapon(null, out _));
    }

    [Test]
    public void Library_그룹_gain_dB를_선형으로_변환()
    {
        SoundLibrary lib = MakeLibrary(new SoundEvent[0], new WeaponSoundEntry[0]);
        SetField(lib, "groupGains", new[]
        {
            new SoundGroupGain(SoundGroup.Alert, 0f),
            new SoundGroupGain(SoundGroup.Weapon, -6f),
            new SoundGroupGain(SoundGroup.Hit, -20f),
            new SoundGroupGain(SoundGroup.UI, 6f),
        });
        lib.InvalidateCache();

        Assert.AreEqual(1f, lib.GetGroupGainLinear(SoundGroup.Alert), 1e-5f);
        Assert.AreEqual(0.501187f, lib.GetGroupGainLinear(SoundGroup.Weapon), 1e-4f);
        Assert.AreEqual(0.1f, lib.GetGroupGainLinear(SoundGroup.Hit), 1e-5f);
        Assert.AreEqual(1.995262f, lib.GetGroupGainLinear(SoundGroup.UI), 1e-4f);
        Assert.AreEqual(-6f, lib.GetGroupGainDb(SoundGroup.Weapon));
        Assert.AreEqual(1f, lib.GetGroupGainLinear(SoundGroup.Monster), 1e-5f, "목록에 없는 그룹은 0 dB");
        Assert.AreEqual(0f, lib.GetGroupGainDb(SoundGroup.Monster));
    }

    [Test]
    public void Library_DbToLinear_경계값()
    {
        Assert.AreEqual(1f, SoundLibrary.DbToLinear(0f), 1e-6f);
        Assert.AreEqual(0.1f, SoundLibrary.DbToLinear(-20f), 1e-6f);
        Assert.AreEqual(0.0001f, SoundLibrary.DbToLinear(-80f), 1e-6f);
    }

    [Test]
    public void Library_소스_수는_최소값으로_보정된다()
    {
        SoundLibrary lib = MakeLibrary(new SoundEvent[0], new WeaponSoundEntry[0]);
        SetField(lib, "oneShotVoices", 0);
        SetField(lib, "loopVoices", -3);
        Assert.AreEqual(1, lib.OneShotVoices);
        Assert.AreEqual(0, lib.LoopVoices);
    }
    #endregion

    #region CombatSoundBinder / SoundManager
    [TestCase("Boomerang(Clone)", "Boomerang")]
    [TestCase("Boomerang", "Boomerang")]
    [TestCase("Boomerang(Clone)(Clone)", "Boomerang")]
    [TestCase("Boomerang (Clone)", "Boomerang")]
    [TestCase("(Clone)", "")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void GetWeaponKey_Clone_접미사를_뗀다(string _name, string _expected)
    {
        Assert.AreEqual(_expected, CombatSoundBinder.GetWeaponKey(_name));
    }

    [Test]
    public void GetPan_진영별_값()
    {
        Assert.AreEqual(-0.45f, SoundManager.GetPan(PlayerSide.Left), 1e-6f);
        Assert.AreEqual(0.45f, SoundManager.GetPan(PlayerSide.Right), 1e-6f);
        Assert.AreEqual(0f, SoundManager.GetPan(null), 1e-6f);
    }

    [Test]
    public void SoundGroup_채널_매핑_BGM만_BGM채널()
    {
        foreach (SoundGroup g in System.Enum.GetValues(typeof(SoundGroup)))
        {
            AudioChannel expected = SoundGroup.BGM == g ? AudioChannel.BGM : AudioChannel.Effect;
            Assert.AreEqual(expected, g.ToChannel(), g.ToString());
        }
    }
    #endregion

    #region S2 라이브러리 / 진영 / 웨이브
    private SoundLibrary MakeS2Library()
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        SetField(lib, "monsters", new[]
        {
            new MonsterSoundEntry { monsterKey = "GolemData", die = SoundId.MON_DIE_L },
            new MonsterSoundEntry { monsterKey = "GolemData", die = SoundId.MON_DIE_S },
        });
        SetField(lib, "effects", new[] { new EffectSoundEntry { effectKey = "MineExplosion", sound = SoundId.UI_CLICK } });
        SetField(lib, "dimensions", new[] { new DimensionSoundEntry { dimensionKey = "Forest", confirm = SoundId.UI_CLICK } });
        lib.InvalidateCache();
        created.Add(lib);
        return lib;
    }

    [Test]
    public void TryGetMonster_찾기_없는키_빈키_중복은_먼저것()
    {
        SoundLibrary lib = MakeS2Library();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("같은 몬스터 키"));
        Assert.IsTrue(lib.TryGetMonster("GolemData", out MonsterSoundEntry e));
        Assert.AreEqual(SoundId.MON_DIE_L, e.die);
        Assert.IsFalse(lib.TryGetMonster("Nope", out _));
        Assert.IsFalse(lib.TryGetMonster("", out _));
        Assert.IsFalse(lib.TryGetMonster(null, out _));
    }

    [Test]
    public void TryGetEffect_TryGetDimension_찾기와_실패()
    {
        SoundLibrary lib = MakeS2Library();
        Assert.IsTrue(lib.TryGetEffect("MineExplosion", out EffectSoundEntry fx));
        Assert.AreEqual(SoundId.UI_CLICK, fx.sound);
        Assert.IsFalse(lib.TryGetEffect("mineexplosion", out _), "대소문자 구분");
        Assert.IsFalse(lib.TryGetEffect(null, out _));
        Assert.IsTrue(lib.TryGetDimension("Forest", out DimensionSoundEntry d));
        Assert.AreEqual(SoundId.UI_CLICK, d.confirm);
        Assert.IsFalse(lib.TryGetDimension("Storm", out _));
        Assert.IsFalse(lib.TryGetDimension("", out _));
    }

    [Test]
    public void 빈_라이브러리는_모든_TryGet이_false()
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        created.Add(lib);
        Assert.IsFalse(lib.TryGetMonster("a", out _));
        Assert.IsFalse(lib.TryGetEffect("a", out _));
        Assert.IsFalse(lib.TryGetDimension("a", out _));
    }

    [Test]
    public void IsStarUpgradeLevel_5와_10만_true()
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        created.Add(lib);
        Assert.IsTrue(lib.IsStarUpgradeLevel(5));
        Assert.IsTrue(lib.IsStarUpgradeLevel(10));
        foreach (int lv in new[] { -1, 0, 1, 4, 6, 9, 11 })
        {
            Assert.IsFalse(lib.IsStarUpgradeLevel(lv), lv.ToString());
        }
        SetField(lib, "starUpgradeLevels", null);
        Assert.IsFalse(lib.IsStarUpgradeLevel(5));
    }

    [Test]
    public void SoundLayerSide_GetSide_L_R_접두는_진영_그외는_null()
    {
        Assert.AreEqual(PlayerSide.Left, SoundLayerSide.GetSide(LayerMask.NameToLayer("LMonster")));
        Assert.AreEqual(PlayerSide.Left, SoundLayerSide.GetSide(LayerMask.NameToLayer("LPlayer")));
        Assert.AreEqual(PlayerSide.Right, SoundLayerSide.GetSide(LayerMask.NameToLayer("RMonster")));
        Assert.AreEqual(PlayerSide.Right, SoundLayerSide.GetSide(LayerMask.NameToLayer("RAttack")));
        Assert.IsNull(SoundLayerSide.GetSide(0));
        Assert.IsNull(SoundLayerSide.GetSide(-1));
        Assert.IsNull(SoundLayerSide.GetSide(32));
        Assert.IsNull(SoundLayerSide.GetSide(100));
        for (int i = 0; i < 32; ++i)
        {
            string n = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(n) || n.StartsWith("L") || n.StartsWith("R"))
            {
                continue;
            }
            Assert.IsNull(SoundLayerSide.GetSide(i), n);
        }
    }

    private Spawner MakeSpawner(int _waveCount)
    {
        GameObject go = new GameObject("TestSpawner");
        created.Add(go);
        Spawner sp = go.AddComponent<Spawner>();
        SetField(sp, "waveArray", new WaveData[_waveCount]);
        return sp;
    }

    [Test]
    public void GetWaveSound_0번_무음_마지막_FINAL_그외_START()
    {
        Spawner sp = MakeSpawner(5);
        Assert.AreEqual(SoundId.None, FlowSoundBinder.GetWaveSound(sp, 0));
        Assert.AreEqual(SoundId.None, FlowSoundBinder.GetWaveSound(sp, -1));
        Assert.AreEqual(SoundId.WAVE_START, FlowSoundBinder.GetWaveSound(sp, 1));
        Assert.AreEqual(SoundId.WAVE_START, FlowSoundBinder.GetWaveSound(sp, 3));
        Assert.AreEqual(SoundId.WAVE_FINAL, FlowSoundBinder.GetWaveSound(sp, 4));
        Assert.AreEqual(SoundId.WAVE_START, FlowSoundBinder.GetWaveSound(sp, 5), "범위 밖은 START");
        Assert.AreEqual(SoundId.WAVE_START, FlowSoundBinder.GetWaveSound(null, 4), "스포너 null 이면 FINAL 판정 불가");
    }

    [Test]
    public void GetWaveSound_웨이브가_1개면_0번은_여전히_무음()
    {
        Spawner sp = MakeSpawner(1);
        Assert.AreEqual(SoundId.None, FlowSoundBinder.GetWaveSound(sp, 0));
    }
    #endregion

    #region S3 BGM
    [Test]
    public void GetBgmBaseForWave_기본값_경계()
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        created.Add(lib);
        int rush = lib.RushWaveIndex;
        Assert.AreEqual(10, rush);
        foreach (int w in new[] { -1, 0, 1, 2 }) { Assert.AreEqual(SoundId.BGM_BASE_LOW, lib.GetBgmBaseForWave(w), "w=" + w); }
        foreach (int w in new[] { 3, 4, 5 }) { Assert.AreEqual(SoundId.BGM_BASE_MID, lib.GetBgmBaseForWave(w), "w=" + w); }
        foreach (int w in new[] { 6, 7, 8, 9 }) { Assert.AreEqual(SoundId.BGM_BASE_HIGH, lib.GetBgmBaseForWave(w), "w=" + w); }
        Assert.AreEqual(SoundId.BGM_RUSH, lib.GetBgmBaseForWave(10));
        foreach (int w in new[] { 11, 12, 50 }) { Assert.AreEqual(SoundId.BGM_BASE_HIGH, lib.GetBgmBaseForWave(w), "w=" + w); }
    }

    [Test]
    public void GetBgmBaseForWave_설정값을_바꾸면_따라간다()
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        created.Add(lib);
        SetField(lib, "bgmMidFromWave", 2);
        SetField(lib, "bgmHighFromWave", 4);
        SetField(lib, "rushWaveIndex", 6);
        Assert.AreEqual(SoundId.BGM_BASE_LOW, lib.GetBgmBaseForWave(1));
        Assert.AreEqual(SoundId.BGM_BASE_MID, lib.GetBgmBaseForWave(2));
        Assert.AreEqual(SoundId.BGM_BASE_MID, lib.GetBgmBaseForWave(3));
        Assert.AreEqual(SoundId.BGM_BASE_HIGH, lib.GetBgmBaseForWave(4));
        Assert.AreEqual(SoundId.BGM_BASE_HIGH, lib.GetBgmBaseForWave(5));
        Assert.AreEqual(SoundId.BGM_RUSH, lib.GetBgmBaseForWave(6));
        Assert.AreEqual(SoundId.BGM_BASE_HIGH, lib.GetBgmBaseForWave(7));
    }

    [Test]
    public void TryGetBgmScene_있음_없음_null()
    {
        SoundLibrary lib = ScriptableObject.CreateInstance<SoundLibrary>();
        created.Add(lib);
        SetField(lib, "bgmScenes", new[]
        {
            new BgmSceneEntry { sceneName = "TitleScene", mode = BgmMode.Single, single = SoundId.BGM_TITLE },
            new BgmSceneEntry { sceneName = "GameScene", mode = BgmMode.Combat },
        });
        lib.InvalidateCache();
        Assert.IsTrue(lib.TryGetBgmScene("TitleScene", out BgmSceneEntry t));
        Assert.AreEqual(BgmMode.Single, t.mode);
        Assert.AreEqual(SoundId.BGM_TITLE, t.single);
        Assert.IsTrue(lib.TryGetBgmScene("GameScene", out BgmSceneEntry g));
        Assert.AreEqual(BgmMode.Combat, g.mode);
        Assert.IsFalse(lib.TryGetBgmScene("Nope", out _));
        Assert.IsFalse(lib.TryGetBgmScene("", out _));
        Assert.IsFalse(lib.TryGetBgmScene(null, out _));
    }

    [Test]
    public void 실제_라이브러리_전투_BGM_7개는_샘플수가_같고_BGM_루프_일시정지무시()
    {
        SoundLibrary lib = Resources.Load<SoundLibrary>(SoundLibrary.RESOURCE_PATH);
        Assert.NotNull(lib, "SoundLibrary.asset 없음");
        SoundId[] ids =
        {
            SoundId.BGM_BASE_LOW, SoundId.BGM_BASE_MID, SoundId.BGM_BASE_HIGH, SoundId.BGM_RUSH,
            SoundId.BGM_LAYER_FOREST, SoundId.BGM_LAYER_FACTORY, SoundId.BGM_LAYER_STORM,
        };
        int samples = -1;
        foreach (SoundId id in ids)
        {
            Assert.IsTrue(lib.TryGetEvent(id, out SoundEvent e), id + " 이벤트 없음");
            Assert.AreEqual(SoundGroup.BGM, e.Group, id + " group");
            Assert.IsTrue(e.Loop, id + " loop");
            Assert.IsTrue(e.IgnorePause, id + " ignorePause");
            Assert.IsNotNull(e.Clips, id + " clips");
            Assert.Greater(e.Clips.Length, 0, id + " clips");
            Assert.IsNotNull(e.Clips[0], id + " clip null");
            if (samples < 0) { samples = e.Clips[0].samples; }
            Assert.AreEqual(samples, e.Clips[0].samples, id + " samples");
        }
    }
    #endregion
}
