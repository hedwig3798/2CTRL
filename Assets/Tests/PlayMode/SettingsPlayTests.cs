using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 설정 기능 PlayMode 검증
/// - GameSettings -> AudioListener.volume (SettingsApplier)
/// - HPSliderBinder 적만 숨김 / DamageTextBinder 토글 / FloatingTextUI 정렬 레이어 / PauseMenu 일시정지 카운트
/// - "Settings." PlayerPrefs 는 시작 때 백업·삭제하고 종료 때 복원한다.
/// </summary>
public class SettingsPlayTests
{
    private static readonly string[] CHANNELS = { "Master", "Effect", "BGM" };
    private readonly Dictionary<string, object> backup = new Dictionary<string, object>();
    private readonly List<Object> created = new List<Object>();
    private readonly List<object> pauseRequesters = new List<object>();
    private float prevListenerVolume;

    private static IEnumerable<string> AllKeys()
    {
        yield return "Settings.ScreenMode";
        yield return "Settings.ShowEnemyHPBar";
        yield return "Settings.ShowDamageText";
        foreach (string c in CHANNELS)
        {
            yield return "Settings.Volume." + c;
            yield return "Settings.Mute." + c;
        }
    }

    [SetUp]
    public void SetUp()
    {
        prevListenerVolume = AudioListener.volume;
        backup.Clear();
        foreach (string k in AllKeys())
        {
            if (PlayerPrefs.HasKey(k))
            {
                backup[k] = k.Contains(".Volume.") ? (object)PlayerPrefs.GetFloat(k) : PlayerPrefs.GetInt(k);
            }
            PlayerPrefs.DeleteKey(k);
        }
        GameSettings.Reload();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (object r in pauseRequesters)
        {
            GamePause.Resume(r);
        }
        pauseRequesters.Clear();

        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();

        foreach (string k in AllKeys())
        {
            PlayerPrefs.DeleteKey(k);
            if (backup.TryGetValue(k, out object v))
            {
                if (v is float f) { PlayerPrefs.SetFloat(k, f); } else { PlayerPrefs.SetInt(k, (int)v); }
            }
        }
        GameSettings.Reload();
        AudioListener.volume = prevListenerVolume;
        Time.timeScale = 1f;
    }

    private GameObject NewGO(string _name, bool _active = true)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(_active);
        created.Add(go);
        return go;
    }

    private static void SetField(object _obj, string _name, object _value)
    {
        FieldInfo f = _obj.GetType().GetField(_name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, $"필드 없음 : {_name}");
        if (f.FieldType.IsEnum && _value is int i)
        {
            _value = System.Enum.ToObject(f.FieldType, i);
        }
        f.SetValue(_obj, _value);
    }

    #region AudioListener

    private SettingsApplier GetApplier()
    {
        if (SettingsApplier.HasInstance)
        {
            return SettingsApplier.Instance;
        }
        GameObject go = NewGO("TestApplier");
        return go.AddComponent<SettingsApplier>();
    }

    [UnityTest]
    public IEnumerator Applier_MasterVolumeAndMute_DriveAudioListener()
    {
        GetApplier();
        yield return null;

        GameSettings.SetVolume(AudioChannel.Master, 0.3f);
        Assert.AreEqual(0.3f, AudioListener.volume, 1e-4f);

        GameSettings.SetMuted(AudioChannel.Master, true);
        Assert.AreEqual(0f, AudioListener.volume, 1e-4f);

        GameSettings.SetMuted(AudioChannel.Master, false);
        Assert.AreEqual(0.3f, AudioListener.volume, 1e-4f);
    }

    [UnityTest]
    public IEnumerator Applier_NonMasterChannelChange_DoesNotTouchListener()
    {
        GetApplier();
        yield return null;
        GameSettings.SetVolume(AudioChannel.Master, 0.6f);
        float before = AudioListener.volume;

        GameSettings.SetVolume(AudioChannel.Effect, 0.1f);
        GameSettings.SetMuted(AudioChannel.BGM, true);

        Assert.AreEqual(before, AudioListener.volume, 1e-5f);
    }

    #endregion

    #region HPSliderBinder

    private class HPRig
    {
        public HealthSystem health;
        public SliderUI slider;
        public HPSliderBinder binder;
    }

    private HPRig NewHPBar(int _owner)
    {
        GameObject canvasGO = NewGO("TestCanvas");
        canvasGO.AddComponent<Canvas>();

        GameObject hpGO = NewGO("TestHP");
        HealthSystem hs = hpGO.AddComponent<HealthSystem>();
        hs.maxHP = 100f;
        hs.currHP = 100f;

        GameObject sliderGO = NewGO("TestSlider", false);
        sliderGO.transform.SetParent(canvasGO.transform, false);
        SliderUI slider = sliderGO.AddComponent<SliderUI>();
        HPSliderBinder binder = sliderGO.AddComponent<HPSliderBinder>();
        SetField(binder, "target", hs);
        SetField(binder, "owner", _owner);
        sliderGO.SetActive(true);

        return new HPRig { health = hs, slider = slider, binder = binder };
    }

    /// <summary>
    /// 체력바 Canvas 가 그려지는지 (적 체력바 숨김은 Canvas.enabled 로 한다)
    /// </summary>
    private static bool IsShown(HPRig _r)
    {
        Canvas c = _r.slider.GetComponentInParent<Canvas>(true);
        return null != c && true == c.enabled;
    }

    [Test]
    public void HPBar_Enemy_HiddenWhenSettingOff_ShownWhenOn()
    {
        HPRig enemy = NewHPBar(2);
        Assert.IsTrue(enemy.binder.IsEnemy);
        Assert.IsTrue(IsShown(enemy), "기본(켜짐)에서는 보임");

        GameSettings.ShowEnemyHPBar = false;
        Assert.IsFalse(IsShown(enemy));
        Assert.IsTrue(enemy.slider.gameObject.activeSelf, "오브젝트는 켜 둔 채 Canvas 만 끔");

        GameSettings.ShowEnemyHPBar = true;
        Assert.IsTrue(IsShown(enemy));
    }

    [Test]
    public void HPBar_Player_NeverHidden()
    {
        HPRig player = NewHPBar(1);
        Assert.IsFalse(player.binder.IsEnemy);

        GameSettings.ShowEnemyHPBar = false;
        Assert.IsTrue(IsShown(player));
        Assert.IsNull(player.slider.GetComponent<CanvasGroup>(), "숨김에 CanvasGroup 을 쓰지 않는다");
    }

    [Test]
    public void HPBar_EnemyAndPlayerSideBySide_OnlyEnemyHidden()
    {
        HPRig enemy = NewHPBar(2);
        HPRig player = NewHPBar(1);

        GameSettings.ShowEnemyHPBar = false;

        Assert.IsFalse(IsShown(enemy));
        Assert.IsTrue(IsShown(player));
    }

    [Test]
    public void HPBar_Enemy_AppliesCurrentSettingWhenReEnabled()
    {
        HPRig enemy = NewHPBar(2);
        GameObject go = enemy.slider.gameObject;

        go.SetActive(false);
        GameSettings.ShowEnemyHPBar = false; // 비활성 동안 변경 (이벤트 구독 해제 상태)
        go.SetActive(true);

        Assert.IsFalse(IsShown(enemy));
    }

    [Test]
    public void HPBar_Enemy_StillSyncsHPWhileHidden()
    {
        HPRig enemy = NewHPBar(2);
        GameSettings.ShowEnemyHPBar = false;

        enemy.health.SetCurrHP(25f);

        Assert.AreEqual(0.25f, enemy.slider.Ratio, 1e-4f);
    }

    #endregion

    #region DamageTextBinder / FloatingTextUI

    private DamageTextBinder NewDamageBinder(out HealthSystem _hs)
    {
        GameObject go = NewGO("TestDamageTarget", false);
        _hs = go.AddComponent<HealthSystem>();
        _hs.maxHP = 100f;
        _hs.currHP = 100f;
        DamageTextBinder b = go.AddComponent<DamageTextBinder>();
        go.SetActive(true);
        return b;
    }

    private static int CountTexts(bool _activeOnly)
    {
        FloatingTextUI[] all = Object.FindObjectsByType<FloatingTextUI>(FindObjectsInactive.Include);
        int n = 0;
        foreach (FloatingTextUI t in all)
        {
            if (false == _activeOnly || t.gameObject.activeSelf)
            {
                ++n;
            }
        }
        return n;
    }

    [Test]
    public void DamageText_Enabled_SpawnsText()
    {
        NewDamageBinder(out HealthSystem hs);
        int before = CountTexts(true);

        hs.OnDamaged?.Invoke(7f);

        Assert.AreEqual(before + 1, CountTexts(true));
    }

    [Test]
    public void DamageText_Disabled_DoesNotCreateAnyText()
    {
        NewDamageBinder(out HealthSystem hs);
        GameSettings.ShowDamageText = false;
        int allBefore = CountTexts(false);

        hs.OnDamaged?.Invoke(7f);
        hs.OnDamaged?.Invoke(0f);

        Assert.AreEqual(allBefore, CountTexts(false), "꺼진 상태에서는 생성조차 안 함");
    }

    [Test]
    public void DamageText_TurningOff_RemovesExistingTexts_AndReEnableWorks()
    {
        NewDamageBinder(out HealthSystem hs);
        int baseline = CountTexts(true);
        hs.OnDamaged?.Invoke(1f);
        hs.OnDamaged?.Invoke(2f);
        hs.OnDamaged?.Invoke(3f);
        Assert.AreEqual(baseline + 3, CountTexts(true));

        GameSettings.ShowDamageText = false;
        Assert.AreEqual(baseline, CountTexts(true), "떠 있던 텍스트가 즉시 사라져야 함");

        hs.OnDamaged?.Invoke(4f);
        Assert.AreEqual(baseline, CountTexts(true));

        GameSettings.ShowDamageText = true;
        hs.OnDamaged?.Invoke(5f);
        Assert.AreEqual(baseline + 1, CountTexts(true), "다시 켜면 풀 재사용으로 정상 출력");
    }

    [Test]
    public void DamageText_DisabledBinder_ReceivesNoTextAfterReenable_WhenSettingOff()
    {
        DamageTextBinder b = NewDamageBinder(out HealthSystem hs);
        b.gameObject.SetActive(false);
        GameSettings.ShowDamageText = false;
        b.gameObject.SetActive(true);
        int baseline = CountTexts(true);

        hs.OnDamaged?.Invoke(9f);

        Assert.AreEqual(baseline, CountTexts(true));
    }

    [Test]
    public void FloatingText_Create_UsesNonDefaultSortingLayer()
    {
        GameObject parent = NewGO("TextParent");
        FloatingTextUI ui = FloatingTextUI.Create(parent.transform);
        Renderer r = ui.GetComponent<Renderer>();

        Assert.IsNotNull(r);
        Assert.AreNotEqual(0, r.sortingLayerID);
        Assert.AreNotEqual("Default", r.sortingLayerName);
        Assert.AreNotEqual(0, FloatingTextUI.DefaultSortingLayerID());
    }

    [Test]
    public void FloatingText_Setup_DefaultLayerZero_FallsBackToNonDefault()
    {
        GameObject parent = NewGO("TextParent");
        FloatingTextUI ui = FloatingTextUI.Create(parent.transform);

        ui.Setup(null, TMPro.FontStyles.Normal, 0, 7);

        Renderer r = ui.GetComponent<Renderer>();
        Assert.AreNotEqual(0, r.sortingLayerID);
        Assert.AreEqual(FloatingTextUI.DefaultSortingLayerID(), r.sortingLayerID);
        Assert.AreEqual(7, r.sortingOrder);
    }

    [Test]
    public void FloatingText_Setup_InvalidLayerId_FallsBackToNonDefault()
    {
        GameObject parent = NewGO("TextParent");
        FloatingTextUI ui = FloatingTextUI.Create(parent.transform);

        ui.Setup(null, TMPro.FontStyles.Normal, 123456789, 5);

        Renderer r = ui.GetComponent<Renderer>();
        Assert.AreNotEqual(0, r.sortingLayerID);
        Assert.AreEqual(5, r.sortingOrder);
    }

    [Test]
    public void DamageText_SpawnedText_UsesNonDefaultSortingLayer()
    {
        NewDamageBinder(out HealthSystem hs);
        hs.OnDamaged?.Invoke(3f);

        bool found = false;
        foreach (FloatingTextUI t in Object.FindObjectsByType<FloatingTextUI>(FindObjectsInactive.Include))
        {
            Renderer r = t.GetComponent<Renderer>();
            Assert.AreNotEqual(0, r.sortingLayerID, "Default 정렬 레이어 금지");
            found = true;
        }
        Assert.IsTrue(found);
    }

    #endregion

    #region PauseMenu

    private PauseMenu NewPauseMenu(out GameObject _panel)
    {
        GameObject go = NewGO("TestPause", false);
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(go.transform, false);
        PauseMenu menu = go.AddComponent<PauseMenu>();
        SetField(menu, "panel", _panel);
        go.SetActive(true);
        return menu;
    }

    [Test]
    public void Pause_OpenClose_RestoresPauseState()
    {
        Assume.That(GamePause.IsPaused, Is.False);
        PauseMenu menu = NewPauseMenu(out GameObject panel);
        Assert.IsFalse(panel.activeSelf);

        menu.Open();
        Assert.IsTrue(menu.IsOpen);
        Assert.IsTrue(GamePause.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.IsTrue(panel.activeSelf);

        menu.Close();
        Assert.IsFalse(menu.IsOpen);
        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsFalse(panel.activeSelf);
    }

    [Test]
    public void Pause_RepeatedOpenClose_DoesNotLeakCount()
    {
        PauseMenu menu = NewPauseMenu(out GameObject _);
        for (int i = 0; i < 5; ++i)
        {
            menu.Open();
            menu.Open(); // 중복 호출
            menu.Close();
            menu.Close();
        }
        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void Pause_CloseWithoutOpen_IsNoop()
    {
        PauseMenu menu = NewPauseMenu(out GameObject _);
        menu.Close();
        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void Pause_WhenOtherRequesterPaused_DoesNotOpen_AndDoesNotResumeOther()
    {
        PauseMenu menu = NewPauseMenu(out GameObject panel);
        object other = new object();
        pauseRequesters.Add(other);
        GamePause.Pause(other);

        menu.Open();
        Assert.IsFalse(menu.IsOpen);
        Assert.IsFalse(panel.activeSelf);

        menu.Close();
        Assert.IsTrue(GamePause.IsPaused, "다른 요청자의 정지를 풀면 안 됨");
        Assert.AreEqual(0f, Time.timeScale);
    }

    [Test]
    public void Pause_OtherRequesterJoinsWhileOpen_CloseKeepsPaused()
    {
        PauseMenu menu = NewPauseMenu(out GameObject _);
        menu.Open();
        object other = new object();
        pauseRequesters.Add(other);
        GamePause.Pause(other);

        menu.Close();
        Assert.IsTrue(GamePause.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);

        GamePause.Resume(other);
        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void Pause_DisabledWhileOpen_Resumes()
    {
        PauseMenu menu = NewPauseMenu(out GameObject _);
        menu.Open();
        menu.gameObject.SetActive(false);

        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void Pause_DestroyedWhileOpen_Resumes()
    {
        PauseMenu menu = NewPauseMenu(out GameObject _);
        menu.Open();
        Object.DestroyImmediate(menu.gameObject);

        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void Pause_OpenSettingsWithoutPanel_DoesNotThrow()
    {
        PauseMenu menu = NewPauseMenu(out GameObject _);
        Assert.DoesNotThrow(() => menu.OpenSettings()); // 닫힌 상태
        menu.Open();
        Assert.DoesNotThrow(() => menu.OpenSettings()); // settingsPanel 없음
        menu.Close();
    }

    #endregion
}
