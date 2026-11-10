using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// SoundManager PlayMode 검증
/// - 라이브러리 에셋이 없으므로 테스트가 만든 SoundLibrary 를 리플렉션으로 주입하고 소스를 다시 만든다.
/// - GameSettings 볼륨 PlayerPrefs 는 백업 후 복원한다.
/// </summary>
public class SoundPlayTests
{
    private const BindingFlags ANY = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly string[] CHANNELS = { "Master", "Effect", "BGM" };

    private readonly List<Object> created = new List<Object>();
    private readonly Dictionary<string, float> prefBackup = new Dictionary<string, float>();
    private readonly Dictionary<string, int> muteBackup = new Dictionary<string, int>();
    private readonly object pauseOwner = new object();

    private SoundLibrary library;

    [SetUp]
    public void SetUp()
    {
        prefBackup.Clear();
        muteBackup.Clear();
        foreach (string c in CHANNELS)
        {
            string vk = "Settings.Volume." + c;
            string mk = "Settings.Mute." + c;
            if (PlayerPrefs.HasKey(vk)) { prefBackup[vk] = PlayerPrefs.GetFloat(vk); }
            if (PlayerPrefs.HasKey(mk)) { muteBackup[mk] = PlayerPrefs.GetInt(mk); }
            PlayerPrefs.DeleteKey(vk);
            PlayerPrefs.DeleteKey(mk);
        }
        GameSettings.Reload();
        FireModeTestProjectile.ResetStatics();
    }

    [TearDown]
    public void TearDown()
    {
        GamePause.Resume(pauseOwner);
        CleanupBgm();
        WeaponTestKit.Cleanup();

        if (SoundManager.HasInstance)
        {
            SoundManager m = SoundManager.Instance;
            SoundManager.StopAllLoops();
            foreach (AudioSource s in m.GetComponents<AudioSource>())
            {
                Object.Destroy(s);
            }
            SetPrivate(m, "oneShots", System.Array.CreateInstance(VoiceType(), 0));
            SetPrivate(m, "loops", System.Array.CreateInstance(VoiceType(), 0));
            SetPrivate(m, "library", null);
            GetPrivate<SoundThrottle>(m, "throttle").Clear();
            SoundManager.ResetStats();
        }
        foreach (Object o in created)
        {
            if (null != o) { Object.DestroyImmediate(o); }
        }
        created.Clear();

        foreach (string c in CHANNELS)
        {
            string vk = "Settings.Volume." + c;
            string mk = "Settings.Mute." + c;
            PlayerPrefs.DeleteKey(vk);
            PlayerPrefs.DeleteKey(mk);
            if (prefBackup.TryGetValue(vk, out float f)) { PlayerPrefs.SetFloat(vk, f); }
            if (muteBackup.TryGetValue(mk, out int i)) { PlayerPrefs.SetInt(mk, i); }
        }
        GameSettings.Reload();
    }

    private static System.Type VoiceType()
    {
        return typeof(SoundManager).GetNestedType("Voice", BindingFlags.NonPublic);
    }

    private static void SetPrivate(object _t, string _name, object _v)
    {
        FieldInfo f = _t.GetType().GetField(_name, ANY);
        Assert.NotNull(f, _name);
        f.SetValue(_t, _v);
    }

    private static T GetPrivate<T>(object _t, string _name)
    {
        FieldInfo f = _t.GetType().GetField(_name, ANY);
        Assert.NotNull(f, _name);
        return (T)f.GetValue(_t);
    }

    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private SoundEvent MakeEvent(SoundId _id, SoundGroup _group, float _volume, bool _ignorePause = false)
    {
        SoundEvent e = Track(ScriptableObject.CreateInstance<SoundEvent>());
        AudioClip clip = Track(AudioClip.Create("testclip", 44100 * 5, 1, 44100, false));
        SetPrivate(e, "id", _id);
        SetPrivate(e, "clips", new[] { clip });
        SetPrivate(e, "group", _group);
        SetPrivate(e, "volume", _volume);
        SetPrivate(e, "ignorePause", _ignorePause);
        return e;
    }

    /// <summary>
    /// 라이브러리를 만들어 SoundManager 에 주입하고 소스를 만든다
    /// </summary>
    private IEnumerator Inject(params SoundEvent[] _events)
    {
        return InjectWith(null, _events);
    }

    private IEnumerator InjectWith(System.Action<SoundLibrary> _config, params SoundEvent[] _events)
    {
        yield return null;
        Assert.IsTrue(SoundManager.HasInstance, "SoundManager 자동 생성 안 됨");

        library = Track(ScriptableObject.CreateInstance<SoundLibrary>());
        SetPrivate(library, "events", new List<SoundEvent>(_events));
        SetPrivate(library, "weapons", new[]
        {
            new WeaponSoundEntry { weaponKey = "TestWeapon", fire = SoundId.WPN_BOOMERANG, hit = SoundId.None },
        });
        _config?.Invoke(library);
        library.InvalidateCache();

        SoundManager m = SoundManager.Instance;
        // 실제 라이브러리로 만든 기존 소스(재생 중인 소리 포함)를 치워 격리한다
        SoundManager.StopAllLoops();
        SetPrivate(m, "oneShots", System.Array.CreateInstance(VoiceType(), 0));
        SetPrivate(m, "loops", System.Array.CreateInstance(VoiceType(), 0));
        foreach (AudioSource old in m.GetComponents<AudioSource>())
        {
            old.Stop();
            old.clip = null;
            Object.Destroy(old);
        }
        yield return null;
        GetPrivate<SoundThrottle>(m, "throttle").Clear();
        SoundManager.ResetStats();
        SetPrivate(m, "library", library);
        typeof(SoundManager).GetMethod("CreateVoices", ANY).Invoke(m, null);
    }

    private static AudioSource PlayingSource()
    {
        foreach (AudioSource s in SoundManager.Instance.GetComponents<AudioSource>())
        {
            if (null != s.clip) { return s; }
        }
        return null;
    }

    [UnityTest]
    public IEnumerator 매니저가_자동_생성되고_AudioListener가_활성_1개()
    {
        yield return null;
        Assert.IsTrue(SoundManager.HasInstance);
        Assert.IsNotNull(SoundManager.Instance.GetComponent<AudioListener>());
        Assert.IsNotNull(SoundManager.Instance.GetComponent<CombatSoundBinder>());

        AudioListener[] active = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int enabled = 0;
        foreach (AudioListener l in active) { if (l.enabled) { ++enabled; } }
        Assert.AreEqual(1, enabled, "활성 AudioListener 수");
    }

    [UnityTest]
    public IEnumerator 라이브러리_없으면_Play가_예외없이_무음()
    {
        yield return null;
        Assert.DoesNotThrow(() => SoundManager.Play(SoundId.UI_CLICK));
        Assert.AreEqual(0, SoundManager.Instance.ActiveOneShotCount);
        Assert.AreEqual(SoundManager.INVALID_HANDLE, SoundManager.StartLoop(SoundId.UI_CLICK));
    }

    [UnityTest]
    public IEnumerator Pause_Resume이_AudioListener_pause에_반영된다()
    {
        yield return null;
        Assert.IsFalse(AudioListener.pause);
        GamePause.Pause(pauseOwner);
        Assert.IsTrue(AudioListener.pause);
        GamePause.Resume(pauseOwner);
        Assert.IsFalse(AudioListener.pause);
    }

    [UnityTest]
    public IEnumerator 볼륨은_채널x이벤트x그룹이고_Master는_곱하지_않는다()
    {
        SoundEvent e = MakeEvent(SoundId.WPN_BOOMERANG, SoundGroup.Weapon, 0.5f);
        yield return Inject(e);
        GameSettings.SetVolume(AudioChannel.Master, 0.3f);
        GameSettings.SetVolume(AudioChannel.Effect, 0.6f);

        SoundManager.Play(SoundId.WPN_BOOMERANG);
        AudioSource s = PlayingSource();
        Assert.IsNotNull(s, "재생된 소스 없음");
        Assert.AreEqual(1, SoundManager.Instance.ActiveOneShotCount);

        float expected = 0.6f * 0.5f * library.GetGroupGainLinear(SoundGroup.Weapon);
        Assert.AreEqual(expected, s.volume, 1e-4f);
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.WPN_BOOMERANG).played);
    }

    [UnityTest]
    public IEnumerator 볼륨_변경시_재생중_소스가_갱신되고_Master_변경은_무시()
    {
        SoundEvent e = MakeEvent(SoundId.WPN_BOOMERANG, SoundGroup.Weapon, 1f);
        yield return Inject(e);
        GameSettings.SetVolume(AudioChannel.Effect, 0.8f);
        SoundManager.Play(SoundId.WPN_BOOMERANG);
        AudioSource s = PlayingSource();
        Assert.IsNotNull(s);
        float gain = library.GetGroupGainLinear(SoundGroup.Weapon);
        Assert.AreEqual(0.8f * gain, s.volume, 1e-4f);

        GameSettings.SetVolume(AudioChannel.Effect, 0.2f);
        Assert.AreEqual(0.2f * gain, s.volume, 1e-4f);

        GameSettings.SetMuted(AudioChannel.Effect, true);
        Assert.AreEqual(0f, s.volume, 1e-6f, "채널 음소거");
        GameSettings.SetMuted(AudioChannel.Effect, false);

        GameSettings.SetVolume(AudioChannel.Master, 0.1f);
        Assert.AreEqual(0.2f * gain, s.volume, 1e-4f, "Master 는 곱하지 않음");

        GameSettings.SetVolume(AudioChannel.BGM, 0.05f);
        Assert.AreEqual(0.2f * gain, s.volume, 1e-4f, "다른 채널 변경은 영향 없음");
    }

    [UnityTest]
    public IEnumerator BGM_그룹은_BGM채널_음량을_받는다()
    {
        SoundEvent e = MakeEvent(SoundId.UI_CLICK, SoundGroup.BGM, 1f);
        yield return Inject(e);
        GameSettings.SetVolume(AudioChannel.Effect, 0.9f);
        GameSettings.SetVolume(AudioChannel.BGM, 0.4f);
        SoundManager.Play(SoundId.UI_CLICK);
        AudioSource s = PlayingSource();
        Assert.IsNotNull(s);
        Assert.AreEqual(0.4f * library.GetGroupGainLinear(SoundGroup.BGM), s.volume, 1e-4f);
    }

    [UnityTest]
    public IEnumerator 일시정지_무시_이벤트는_ignoreListenerPause가_켜진다()
    {
        SoundEvent e = MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f, true);
        SoundEvent w = MakeEvent(SoundId.WPN_BOOMERANG, SoundGroup.Weapon, 1f, false);
        yield return Inject(e, w);

        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Right);
        AudioSource ui = PlayingSource();
        Assert.IsTrue(ui.ignoreListenerPause);
        Assert.AreEqual(0.45f, ui.panStereo, 1e-5f);

        SoundManager.Play(SoundId.WPN_BOOMERANG, PlayerSide.Left);
        int checkedCount = 0;
        foreach (AudioSource s in SoundManager.Instance.GetComponents<AudioSource>())
        {
            if (null != s.clip && s != ui)
            {
                Assert.IsFalse(s.ignoreListenerPause);
                Assert.AreEqual(-0.45f, s.panStereo, 1e-5f);
                ++checkedCount;
            }
        }
        Assert.AreEqual(1, checkedCount);
    }

    [UnityTest]
    public IEnumerator 미등록_ID는_Missing으로_집계된다()
    {
        yield return Inject(MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f));
        SoundManager.Play(SoundId.PLR_HIT);
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.PLR_HIT).missing);
        Assert.AreEqual(0, SoundManager.GetStats(SoundId.PLR_HIT).played);
        SoundManager.Play(SoundId.None);
        Assert.AreEqual(0, SoundManager.GetStats(SoundId.None).missing);
    }

    [UnityTest]
    public IEnumerator 최소간격_제한은_Throttled로_집계된다()
    {
        SoundEvent e = MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f);
        SetPrivate(e, "minInterval", 5f);
        yield return Inject(e);
        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Left);
        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Left);
        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Right);
        SoundStats st = SoundManager.GetStats(SoundId.UI_CLICK);
        Assert.AreEqual(2, st.played);
        Assert.AreEqual(1, st.throttled);
    }

    [UnityTest]
    public IEnumerator 루프는_StartLoop_StopLoop로_시작과_정지()
    {
        SoundEvent e = MakeEvent(SoundId.WPN_FLAME_LOOP, SoundGroup.Weapon, 1f);
        SetPrivate(e, "loop", true);
        yield return Inject(e);

        int handle = SoundManager.StartLoop(SoundId.WPN_FLAME_LOOP);
        Assert.AreNotEqual(SoundManager.INVALID_HANDLE, handle);
        Assert.AreEqual(1, SoundManager.Instance.ActiveLoopCount);
        SoundManager.StopLoop(handle);
        Assert.AreEqual(0, SoundManager.Instance.ActiveLoopCount);
        Assert.DoesNotThrow(() => SoundManager.StopLoop(handle));
        Assert.DoesNotThrow(() => SoundManager.StopLoop(SoundManager.INVALID_HANDLE));
    }

    [UnityTest]
    public IEnumerator Weapon_Fired_구독으로_발사음_통계가_증가한다()
    {
        SoundEvent e = MakeEvent(SoundId.WPN_BOOMERANG, SoundGroup.Weapon, 1f);
        yield return Inject(e);

        FireModeTestProjectile tpl = WeaponTestKit.Template<FireModeTestProjectile>();
        WeaponTestKit.Make(WEAPON_TYPE.Capped, tpl, WeaponTestKit.Marker(Vector2.zero), 1f, 4f, 5f, 1);

        yield return MonsterTestFactory.WaitUntil(() => SoundManager.GetStats(SoundId.WPN_BOOMERANG).played >= 1, 2f);
        Assert.GreaterOrEqual(SoundManager.GetStats(SoundId.WPN_BOOMERANG).played, 1);
        Assert.GreaterOrEqual(FireModeTestProjectile.LaunchFrames.Count, 1);
    }

    #region S2 위치 컬링 / 루프 / 연출 / 웨이브
    private Camera MakeSideCamera(PlayerSide _side, Vector3 _pos)
    {
        GameObject go = new GameObject("SoundTestCamera");
        go.transform.position = new Vector3(_pos.x, _pos.y, -10f);
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.cullingMask = 1 << SoundLayerSide.GetMonsterLayer(_side);
        Track(go);
        typeof(SoundManager).GetMethod("ClearSideCameras", ANY).Invoke(SoundManager.Instance, null);
        return cam;
    }

    [UnityTest]
    public IEnumerator 위치_Play는_진영_카메라_밖이면_culled_안이면_재생()
    {
        yield return Inject(MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f));
        MakeSideCamera(PlayerSide.Left, new Vector3(100f, 0f, 0f));

        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Left, new Vector3(100f, 0f, 0f));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).played, "화면 안 재생");
        Assert.AreEqual(0, SoundManager.GetStats(SoundId.UI_CLICK).culled);

        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Left, new Vector3(300f, 0f, 0f));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).culled, "화면 밖 컬링");
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).played);
    }

    [UnityTest]
    public IEnumerator 위치_Play_뷰포트_여유_경계는_재생_바깥은_컬링()
    {
        yield return Inject(MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f));
        Camera cam = MakeSideCamera(PlayerSide.Left, Vector3.zero);
        float halfW = cam.orthographicSize * cam.aspect;
        float margin = library.CullViewportMargin;
        float edge = halfW * (1f + 2f * margin);

        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Left, new Vector3(edge * 0.98f, 0f, 0f));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).played);
        SoundManager.Play(SoundId.UI_CLICK, PlayerSide.Left, new Vector3(edge * 1.05f, 0f, 0f));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).culled);
    }

    [UnityTest]
    public IEnumerator 위치_Play_진영_null이면_컬링없이_재생_None은_무시()
    {
        yield return Inject(MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f));
        MakeSideCamera(PlayerSide.Left, Vector3.zero);
        SoundManager.Play(SoundId.UI_CLICK, null, new Vector3(9999f, 0f, 0f));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).played);
        Assert.AreEqual(0, SoundManager.GetStats(SoundId.UI_CLICK).culled);
        Assert.DoesNotThrow(() => SoundManager.Play(SoundId.None, PlayerSide.Left, new Vector3(9999f, 0f, 0f)));
    }

    [UnityTest]
    public IEnumerator 루프_이벤트를_Play에_넘기면_거부되어_missing()
    {
        SoundEvent e = MakeEvent(SoundId.WPN_FLAME_LOOP, SoundGroup.Weapon, 1f);
        SetPrivate(e, "loop", true);
        yield return Inject(e);

        SoundManager.Play(SoundId.WPN_FLAME_LOOP);
        SoundStats st = SoundManager.GetStats(SoundId.WPN_FLAME_LOOP);
        Assert.AreEqual(0, st.played);
        Assert.AreEqual(1, st.missing);
        Assert.AreEqual(0, SoundManager.Instance.ActiveLoopCount);
        Assert.AreEqual(0, SoundManager.Instance.ActiveOneShotCount);
    }

    private IEnumerator InjectLoopWeapon()
    {
        SoundEvent e = MakeEvent(SoundId.WPN_FLAME_LOOP, SoundGroup.Weapon, 1f);
        SetPrivate(e, "loop", true);
        yield return InjectWith(lib => SetPrivate(lib, "weapons", new[]
        {
            new WeaponSoundEntry { weaponKey = "TestWeapon", loop = SoundId.WPN_FLAME_LOOP },
        }), e);
    }

    [UnityTest]
    public IEnumerator 무기_루프는_발사체_0에서_1이면_시작_0이_되면_정지()
    {
        yield return InjectLoopWeapon();
        FireModeTestProjectile tpl = WeaponTestKit.Template<FireModeTestProjectile>();
        FireModeTestProjectile.Allow = false;
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Capped, tpl, WeaponTestKit.Marker(Vector2.zero), 1f, 4f, 5f, 1);
        yield return null;
        yield return null;
        Assert.AreEqual(0, w.ActiveProjectileCount);
        Assert.AreEqual(0, SoundManager.Instance.ActiveLoopCount, "발사체 없으면 루프 없음");

        FireModeTestProjectile.Allow = true;
        yield return MonsterTestFactory.WaitUntil(() => SoundManager.Instance.ActiveLoopCount >= 1, 2f);
        Assert.GreaterOrEqual(w.ActiveProjectileCount, 1);
        Assert.AreEqual(1, SoundManager.Instance.ActiveLoopCount, "발사체 수와 무관하게 루프 1개");

        FireModeTestProjectile.Allow = false;
        foreach (FireModeTestProjectile p in Object.FindObjectsByType<FireModeTestProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            p.gameObject.SetActive(false);
        }
        yield return MonsterTestFactory.WaitUntil(() => 0 == SoundManager.Instance.ActiveLoopCount, 2f);
        Assert.AreEqual(0, w.ActiveProjectileCount);
        Assert.AreEqual(0, SoundManager.Instance.ActiveLoopCount);
    }

    [UnityTest]
    public IEnumerator 무기_루프는_무기가_비활성되면_정지()
    {
        yield return InjectLoopWeapon();
        FireModeTestProjectile tpl = WeaponTestKit.Template<FireModeTestProjectile>();
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Capped, tpl, WeaponTestKit.Marker(Vector2.zero), 1f, 4f, 5f, 1);
        yield return MonsterTestFactory.WaitUntil(() => SoundManager.Instance.ActiveLoopCount >= 1, 2f);
        Assert.AreEqual(1, SoundManager.Instance.ActiveLoopCount);

        w.gameObject.SetActive(false);
        yield return null;
        Assert.AreEqual(0, SoundManager.Instance.ActiveLoopCount);
    }

    [UnityTest]
    public IEnumerator 루프_설정_없는_무기는_루프를_만들지_않는다()
    {
        yield return Inject(MakeEvent(SoundId.WPN_FLAME_LOOP, SoundGroup.Weapon, 1f));
        FireModeTestProjectile tpl = WeaponTestKit.Template<FireModeTestProjectile>();
        WeaponTestKit.Make(WEAPON_TYPE.Capped, tpl, WeaponTestKit.Marker(Vector2.zero), 1f, 4f, 5f, 1);
        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 1, 2f);
        yield return null;
        Assert.AreEqual(0, SoundManager.Instance.ActiveLoopCount);
    }

    private PooledEffect MakeEffect(string _name)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(false);
        PooledEffect e = go.AddComponent<PooledEffect>();
        Track(go);
        return e;
    }

    [UnityTest]
    public IEnumerator EffectPool_Spawned가_연출음을_재생하고_매핑없으면_무음()
    {
        yield return InjectWith(lib => SetPrivate(lib, "effects", new[]
        {
            new EffectSoundEntry { effectKey = "SoundTestFx", sound = SoundId.UI_CLICK },
        }), MakeEvent(SoundId.UI_CLICK, SoundGroup.UI, 1f));

        PooledEffect fx = MakeEffect("SoundTestFx");
        PooledEffect other = MakeEffect("OtherFx");

        EffectPool.Spawn(fx, Vector3.zero, 0f, LayerMask.NameToLayer("LMonster"));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).played);
        EffectPool.Spawn(other, Vector3.zero, 0f, LayerMask.NameToLayer("LMonster"));
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.UI_CLICK).played, "매핑 없는 연출은 무음");
    }

    private Spawner MakeSpawner(int _count)
    {
        GameObject go = new GameObject("SoundTestSpawner");
        go.SetActive(false);
        Spawner sp = go.AddComponent<Spawner>();
        SetPrivate(sp, "waveArray", new WaveData[_count]);
        Track(go);
        go.SetActive(true);
        return sp;
    }

    [UnityTest]
    public IEnumerator Spawner_OnWaveChanged_같은_인덱스는_1회만_처리()
    {
        yield return Inject(MakeEvent(SoundId.WAVE_START, SoundGroup.Alert, 1f));
        FlowSoundBinder binder = SoundManager.Instance.GetComponent<FlowSoundBinder>();
        Assert.NotNull(binder);

        Spawner a = MakeSpawner(5);
        Spawner b = MakeSpawner(5);
        typeof(FlowSoundBinder).GetMethod("BindScene", ANY).Invoke(binder, null);

        a.OnWaveChanged?.Invoke(a, 0);
        Assert.AreEqual(0, SoundManager.GetStats(SoundId.WAVE_START).played, "0번 무음");
        a.OnWaveChanged?.Invoke(a, 1);
        b.OnWaveChanged?.Invoke(b, 1);
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.WAVE_START).played, "같은 번호 2번 -> 1회");
        a.OnWaveChanged?.Invoke(a, 0);
        Assert.AreEqual(1, SoundManager.GetStats(SoundId.WAVE_START).played, "되돌아간 번호 무시");
    }

    [UnityTest]
    public IEnumerator GetWaveSound_rushWaveIndex는_WAVE_RUSH()
    {
        yield return Inject(MakeEvent(SoundId.WAVE_START, SoundGroup.Alert, 1f));
        Spawner s = MakeSpawner(20);
        int rush = library.RushWaveIndex;
        Assert.AreEqual(SoundId.WAVE_RUSH, FlowSoundBinder.GetWaveSound(s, rush));
        Assert.AreEqual(SoundId.WAVE_FINAL, FlowSoundBinder.GetWaveSound(s, 19));
        Assert.AreEqual(SoundId.WAVE_START, FlowSoundBinder.GetWaveSound(s, rush + 1));
        Spawner shortS = MakeSpawner(rush + 1);
        Assert.AreEqual(SoundId.WAVE_RUSH, FlowSoundBinder.GetWaveSound(shortS, rush), "러시가 마지막이어도 RUSH 우선");
    }
    #endregion

    #region S3 BGM
    private static BgmDirector Director()
    {
        return SoundManager.Instance.GetComponent<BgmDirector>();
    }

    private static AudioSource VoiceSource(BgmDirector _d, string _field, int _index)
    {
        System.Array arr = GetPrivate<System.Array>(_d, _field);
        object voice = arr.GetValue(_index);
        return (AudioSource)voice.GetType().GetField("source", ANY).GetValue(voice);
    }

    private static AudioSource BaseSource(BgmDirector _d, int _i) { return VoiceSource(_d, "baseVoices", _i); }
    private static AudioSource LayerSource(BgmDirector _d, int _i) { return VoiceSource(_d, "layerVoices", _i); }

    private void CleanupBgm()
    {
        if (false == SoundManager.HasInstance) { return; }
        BgmDirector d = Director();
        if (null == d) { return; }
        System.Array all = GetPrivate<System.Array>(d, "allVoices");
        MethodInfo stop = typeof(BgmDirector).GetMethod("StopVoice", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (object v in all) { stop.Invoke(null, new[] { v }); }
        SetPrivate(d, "mode", BgmMode.None);
        SetPrivate(d, "combatPending", false);
        SetPrivate(d, "combatRunning", false);
        SetPrivate(d, "intensityIndex", 0);
        SetPrivate(d, "lastWaveIndex", -1);
        SetPrivate(d, "library", null);
        typeof(BgmDirector).GetMethod("UnbindScene", ANY).Invoke(d, null);
        DimensionSelection.Clear();
    }

    private static IEnumerator WaitReal(System.Func<bool> _cond, float _timeout)
    {
        float end = Time.realtimeSinceStartup + _timeout;
        while (false == _cond() && Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
    }

    /// <summary>
    /// BGM 이벤트 6종(베이스 4 + 포레스트 / 폭풍 레이어) 라이브러리를 주입하고, 디렉터를 정지 상태로 되돌린다
    /// 빠른 검증을 위해 bpm 240 (1마디 = 1초), 크로스페이드 0.5마디
    /// </summary>
    private IEnumerator InjectBgm()
    {
        SoundId[] ids = { SoundId.BGM_BASE_LOW, SoundId.BGM_BASE_MID, SoundId.BGM_BASE_HIGH, SoundId.BGM_RUSH, SoundId.BGM_LAYER_FOREST, SoundId.BGM_LAYER_STORM };
        SoundEvent[] events = new SoundEvent[ids.Length];
        for (int i = 0; i < ids.Length; ++i)
        {
            events[i] = MakeEvent(ids[i], SoundGroup.BGM, 1f, true);
            SetPrivate(events[i], "loop", true);
        }
        yield return InjectWith(lib =>
        {
            SetPrivate(lib, "bgmBpm", 240f);
            SetPrivate(lib, "bgmCrossfadeBars", 0.5f);
            SetPrivate(lib, "dimensions", new[]
            {
                new DimensionSoundEntry { dimensionKey = "Forest", layer = SoundId.BGM_LAYER_FOREST },
                new DimensionSoundEntry { dimensionKey = "Storm", layer = SoundId.BGM_LAYER_STORM },
            });
        }, events);

        BgmDirector d = Director();
        Assert.NotNull(d, "BgmDirector 없음");
        CleanupBgm();
        SetPrivate(d, "library", library);
        SetPrivate(d, "channelVolume", GameSettings.GetChannelVolume(AudioChannel.BGM));

        DimensionData forest = Track(ScriptableObject.CreateInstance<DimensionData>());
        forest.name = "Forest";
        DimensionData storm = Track(ScriptableObject.CreateInstance<DimensionData>());
        storm.name = "Storm";
        DimensionSelection.Set(forest, storm);
    }

    private Spawner MakeBgmSpawner()
    {
        GameObject go = new GameObject("BgmTestSpawner");
        go.SetActive(false);
        Spawner sp = go.AddComponent<Spawner>();
        SetPrivate(sp, "waveArray", new WaveData[20]);
        Track(go);
        go.SetActive(true);
        return sp;
    }

    private IEnumerator StartCombat()
    {
        BgmDirector d = Director();
        typeof(BgmDirector).GetMethod("RequestCombat", ANY).Invoke(d, null);
        yield return WaitReal(() => GetPrivate<bool>(d, "combatRunning"), 3f);
        Assert.IsTrue(GetPrivate<bool>(d, "combatRunning"), "전투 BGM 시작 안 됨");
        yield return new WaitForSecondsRealtime(0.4f);
    }

    [UnityTest]
    public IEnumerator BGM_Combat이_아니면_CurrentIntensity는_None()
    {
        yield return InjectBgm();
        Assert.AreEqual(SoundId.None, BgmDirector.CurrentIntensity);
        Assert.AreEqual(BgmMode.None, BgmDirector.CurrentMode);
    }

    [UnityTest]
    public IEnumerator BGM_Combat_시작시_6소스가_함께_재생되고_현재_강도만_들린다()
    {
        yield return InjectBgm();
        MakeBgmSpawner();
        yield return StartCombat();
        BgmDirector d = Director();

        Assert.AreEqual(BgmMode.Combat, BgmDirector.CurrentMode);
        Assert.AreEqual(SoundId.BGM_BASE_LOW, BgmDirector.CurrentIntensity);

        int refSamples = BaseSource(d, 0).timeSamples;
        for (int i = 0; i < 4; ++i)
        {
            AudioSource s = BaseSource(d, i);
            Assert.NotNull(s.clip, "base " + i);
            Assert.IsTrue(s.isPlaying, "base " + i + " isPlaying");
            Assert.Less(Mathf.Abs(s.timeSamples - refSamples), 4410, "base " + i + " 동기");
            Assert.IsTrue(s.ignoreListenerPause);
            Assert.IsTrue(s.loop);
        }
        for (int i = 0; i < 2; ++i)
        {
            AudioSource s = LayerSource(d, i);
            Assert.NotNull(s.clip, "layer " + i);
            Assert.IsTrue(s.isPlaying, "layer " + i);
            Assert.Less(Mathf.Abs(s.timeSamples - refSamples), 4410, "layer " + i + " 동기");
        }
        Assert.Less(LayerSource(d, 0).panStereo, 0f, "왼쪽 레이어 패닝");
        Assert.Greater(LayerSource(d, 1).panStereo, 0f, "오른쪽 레이어 패닝");

        float expected = library.GetGroupGainLinear(SoundGroup.BGM) * GameSettings.GetChannelVolume(AudioChannel.BGM);
        Assert.AreEqual(expected, BaseSource(d, 0).volume, 1e-4f);
        for (int i = 1; i < 4; ++i)
        {
            Assert.AreEqual(0f, BaseSource(d, i).volume, 1e-5f, "base " + i + " 무음");
        }
        Assert.Greater(LayerSource(d, 0).volume, 0f);
        Assert.Greater(LayerSource(d, 1).volume, 0f);
    }

    [UnityTest]
    public IEnumerator BGM_웨이브_변경으로_강도가_전환되고_같은_인덱스는_한번만()
    {
        yield return InjectBgm();
        Spawner sp = MakeBgmSpawner();
        Spawner sp2 = MakeBgmSpawner();
        yield return StartCombat();
        BgmDirector d = Director();

        sp.OnWaveChanged?.Invoke(sp, 3);
        sp2.OnWaveChanged?.Invoke(sp2, 3);
        Assert.AreEqual(SoundId.BGM_BASE_MID, BgmDirector.CurrentIntensity);
        Assert.AreEqual(3, GetPrivate<int>(d, "lastWaveIndex"));

        float expected = library.GetGroupGainLinear(SoundGroup.BGM) * GameSettings.GetChannelVolume(AudioChannel.BGM);
        yield return WaitReal(() => Mathf.Abs(BaseSource(d, 1).volume - expected) < 1e-4f, 4f);
        Assert.AreEqual(expected, BaseSource(d, 1).volume, 1e-4f, "MID 들림");
        Assert.AreEqual(0f, BaseSource(d, 0).volume, 1e-4f, "LOW 무음");

        sp.OnWaveChanged?.Invoke(sp, 10);
        Assert.AreEqual(SoundId.BGM_RUSH, BgmDirector.CurrentIntensity);
        sp.OnWaveChanged?.Invoke(sp, 6);
        Assert.AreEqual(SoundId.BGM_RUSH, BgmDirector.CurrentIntensity, "낮은 인덱스는 무시");
        yield return WaitReal(() => Mathf.Abs(BaseSource(d, 3).volume - expected) < 1e-4f, 4f);
        Assert.AreEqual(expected, BaseSource(d, 3).volume, 1e-4f, "RUSH 들림");
        Assert.AreEqual(0f, BaseSource(d, 1).volume, 1e-4f);
    }

    [UnityTest]
    public IEnumerator BGM_일시정지는_볼륨_감쇠와_로우패스를_걸고_재개하면_복구()
    {
        yield return InjectBgm();
        yield return StartCombat();
        BgmDirector d = Director();
        AudioSource src = BaseSource(d, 0);
        AudioLowPassFilter filter = src.GetComponent<AudioLowPassFilter>();
        float normal = src.volume;
        Assert.Greater(normal, 0f);
        Assert.IsFalse(filter.enabled);

        GamePause.Pause(pauseOwner);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsTrue(src.isPlaying, "일시정지 중에도 재생");
        Assert.AreEqual(normal * SoundLibrary.DbToLinear(library.BgmPauseDb), src.volume, 1e-4f);
        Assert.IsTrue(filter.enabled);
        Assert.AreEqual(library.BgmPauseCutoff, filter.cutoffFrequency, 1f);

        GamePause.Resume(pauseOwner);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(normal, src.volume, 1e-4f);
        Assert.IsFalse(filter.enabled);
    }

    [UnityTest]
    public IEnumerator BGM_채널_볼륨_변경은_반영되고_Master는_포함되지_않는다()
    {
        yield return InjectBgm();
        GameSettings.SetVolume(AudioChannel.BGM, 1f);
        SetPrivate(Director(), "channelVolume", 1f);
        yield return StartCombat();
        BgmDirector d = Director();
        AudioSource src = BaseSource(d, 0);
        float full = src.volume;
        Assert.Greater(full, 0f);

        GameSettings.SetVolume(AudioChannel.BGM, 0.5f);
        yield return null;
        yield return null;
        Assert.AreEqual(full * 0.5f, src.volume, 1e-4f);

        GameSettings.SetVolume(AudioChannel.Master, 0.1f);
        yield return null;
        yield return null;
        Assert.AreEqual(full * 0.5f, src.volume, 1e-4f, "Master 는 곱하지 않는다");
    }
    #endregion
}
