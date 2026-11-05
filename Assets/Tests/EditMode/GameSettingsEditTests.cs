using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// GameSettings 저장/로드, 이벤트, GetEffectiveVolume 검증
/// - "Settings." 키 PlayerPrefs 를 백업/삭제하고 종료 시 복원한다.
/// </summary>
public class GameSettingsEditTests
{
    private static readonly string[] CHANNELS = { "Master", "Effect", "BGM" };
    private readonly Dictionary<string, object> backup = new Dictionary<string, object>();

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
        foreach (string k in AllKeys())
        {
            PlayerPrefs.DeleteKey(k);
            if (backup.TryGetValue(k, out object v))
            {
                if (v is float f) { PlayerPrefs.SetFloat(k, f); } else { PlayerPrefs.SetInt(k, (int)v); }
            }
        }
        GameSettings.Reload();
    }

    [Test]
    public void Defaults_WhenNoSavedValues()
    {
        Assert.AreEqual(ScreenModes.DEFAULT_INDEX, GameSettings.ScreenModeIndex);
        Assert.AreEqual(GameSettings.DEFAULT_SHOW_ENEMY_HP_BAR, GameSettings.ShowEnemyHPBar);
        Assert.AreEqual(GameSettings.DEFAULT_SHOW_DAMAGE_TEXT, GameSettings.ShowDamageText);
        foreach (AudioChannel c in System.Enum.GetValues(typeof(AudioChannel)))
        {
            Assert.AreEqual(GameSettings.DEFAULT_VOLUME, GameSettings.GetVolume(c));
            Assert.AreEqual(GameSettings.DEFAULT_MUTE, GameSettings.IsMuted(c));
        }
    }

    [Test]
    public void SetValues_PersistAcrossReload()
    {
        GameSettings.ScreenModeIndex = 1;
        GameSettings.ShowEnemyHPBar = false;
        GameSettings.ShowDamageText = false;
        GameSettings.SetVolume(AudioChannel.Effect, 0.25f);
        GameSettings.SetMuted(AudioChannel.BGM, true);
        GameSettings.Save();

        GameSettings.Reload(); // PlayerPrefs 에서 다시 읽는다

        Assert.AreEqual(1, GameSettings.ScreenModeIndex);
        Assert.IsFalse(GameSettings.ShowEnemyHPBar);
        Assert.IsFalse(GameSettings.ShowDamageText);
        Assert.AreEqual(0.25f, GameSettings.GetVolume(AudioChannel.Effect), 1e-5f);
        Assert.IsTrue(GameSettings.IsMuted(AudioChannel.BGM));
    }

    [Test]
    public void Reload_ClampsOutOfRangeSavedValues()
    {
        PlayerPrefs.SetInt("Settings.ScreenMode", 999);
        PlayerPrefs.SetFloat("Settings.Volume.Master", 5f);
        PlayerPrefs.SetFloat("Settings.Volume.Effect", -3f);
        GameSettings.Reload();

        Assert.AreEqual(ScreenModes.Count - 1, GameSettings.ScreenModeIndex);
        Assert.AreEqual(1f, GameSettings.GetVolume(AudioChannel.Master));
        Assert.AreEqual(0f, GameSettings.GetVolume(AudioChannel.Effect));
    }

    [Test]
    public void ScreenModeIndex_ClampsNegativeAndOverflow()
    {
        GameSettings.ScreenModeIndex = -5;
        Assert.AreEqual(0, GameSettings.ScreenModeIndex);
        GameSettings.ScreenModeIndex = 9999;
        Assert.AreEqual(ScreenModes.Count - 1, GameSettings.ScreenModeIndex);
    }

    [Test]
    public void SetVolume_ClampsToZeroOne()
    {
        GameSettings.SetVolume(AudioChannel.BGM, 3f);
        Assert.AreEqual(1f, GameSettings.GetVolume(AudioChannel.BGM));
        GameSettings.SetVolume(AudioChannel.BGM, -1f);
        Assert.AreEqual(0f, GameSettings.GetVolume(AudioChannel.BGM));
    }

    [Test]
    public void Events_FireOnlyWhenValueChanges()
    {
        int hp = 0, dmg = 0, scr = 0;
        List<AudioChannel> vol = new List<AudioChannel>();
        System.Action<bool> hH = _ => ++hp;
        System.Action<bool> dH = _ => ++dmg;
        System.Action<int> sH = _ => ++scr;
        System.Action<AudioChannel> vH = c => vol.Add(c);
        GameSettings.OnShowEnemyHPBarChanged += hH;
        GameSettings.OnShowDamageTextChanged += dH;
        GameSettings.OnScreenModeChanged += sH;
        GameSettings.OnVolumeChanged += vH;
        try
        {
            GameSettings.ShowEnemyHPBar = true;   // 기본값과 동일
            GameSettings.ShowDamageText = true;
            GameSettings.ScreenModeIndex = GameSettings.ScreenModeIndex;
            GameSettings.SetVolume(AudioChannel.Master, 1f);
            GameSettings.SetMuted(AudioChannel.Master, false);
            Assert.AreEqual(0, hp + dmg + scr + vol.Count, "동일 값 설정은 이벤트 없음");

            GameSettings.ShowEnemyHPBar = false;
            GameSettings.ShowDamageText = false;
            GameSettings.ScreenModeIndex = 0;
            GameSettings.SetVolume(AudioChannel.Effect, 0.5f);
            GameSettings.SetMuted(AudioChannel.BGM, true);
            Assert.AreEqual(1, hp);
            Assert.AreEqual(1, dmg);
            Assert.AreEqual(1, scr);
            CollectionAssert.AreEqual(new[] { AudioChannel.Effect, AudioChannel.BGM }, vol);
        }
        finally
        {
            GameSettings.OnShowEnemyHPBarChanged -= hH;
            GameSettings.OnShowDamageTextChanged -= dH;
            GameSettings.OnScreenModeChanged -= sH;
            GameSettings.OnVolumeChanged -= vH;
        }
    }

    [Test]
    public void Reload_FiresEventsForChangedValues()
    {
        GameSettings.ShowDamageText = true;
        int dmg = 0;
        System.Action<bool> h = _ => ++dmg;
        GameSettings.OnShowDamageTextChanged += h;
        try
        {
            PlayerPrefs.SetInt("Settings.ShowDamageText", 0);
            GameSettings.Reload();
            Assert.AreEqual(1, dmg);
            Assert.IsFalse(GameSettings.ShowDamageText);
        }
        finally { GameSettings.OnShowDamageTextChanged -= h; }
    }

    [Test]
    public void EffectiveVolume_Defaults_AreFull()
    {
        Assert.AreEqual(1f, GameSettings.GetEffectiveVolume(AudioChannel.Master), 1e-5f);
        Assert.AreEqual(1f, GameSettings.GetEffectiveVolume(AudioChannel.Effect), 1e-5f);
        Assert.AreEqual(1f, GameSettings.GetEffectiveVolume(AudioChannel.BGM), 1e-5f);
    }

    [Test]
    public void EffectiveVolume_MultipliesMasterAndChannel()
    {
        GameSettings.SetVolume(AudioChannel.Master, 0.5f);
        GameSettings.SetVolume(AudioChannel.Effect, 0.4f);
        Assert.AreEqual(0.5f, GameSettings.GetEffectiveVolume(AudioChannel.Master), 1e-5f);
        Assert.AreEqual(0.2f, GameSettings.GetEffectiveVolume(AudioChannel.Effect), 1e-5f);
        Assert.AreEqual(0.5f, GameSettings.GetEffectiveVolume(AudioChannel.BGM), 1e-5f);
    }

    [Test]
    public void EffectiveVolume_MasterMute_ZerosEverything_ButKeepsSliderValue()
    {
        GameSettings.SetVolume(AudioChannel.Master, 0.8f);
        GameSettings.SetMuted(AudioChannel.Master, true);
        foreach (AudioChannel c in System.Enum.GetValues(typeof(AudioChannel)))
        {
            Assert.AreEqual(0f, GameSettings.GetEffectiveVolume(c));
        }
        Assert.AreEqual(0.8f, GameSettings.GetVolume(AudioChannel.Master), 1e-5f);

        GameSettings.SetMuted(AudioChannel.Master, false);
        Assert.AreEqual(0.8f, GameSettings.GetEffectiveVolume(AudioChannel.Master), 1e-5f);
    }

    [Test]
    public void EffectiveVolume_ChannelMute_OnlyAffectsThatChannel()
    {
        GameSettings.SetMuted(AudioChannel.Effect, true);
        Assert.AreEqual(0f, GameSettings.GetEffectiveVolume(AudioChannel.Effect));
        Assert.AreEqual(1f, GameSettings.GetEffectiveVolume(AudioChannel.BGM), 1e-5f);
        Assert.AreEqual(1f, GameSettings.GetEffectiveVolume(AudioChannel.Master), 1e-5f);
    }

    [Test]
    public void EffectiveVolume_ZeroMasterSlider_ZerosChannels()
    {
        GameSettings.SetVolume(AudioChannel.Master, 0f);
        Assert.AreEqual(0f, GameSettings.GetEffectiveVolume(AudioChannel.BGM));
    }

    [Test]
    public void ScreenMode_ForceNotify_FiresEvenForSameIndex()
    {
        int count = 0;
        int last = -1;
        System.Action<int> h = i => { count++; last = i; };
        GameSettings.OnScreenModeChanged += h;
        try
        {
            int cur = GameSettings.ScreenModeIndex;
            GameSettings.SetScreenModeIndex(cur, false);
            Assert.AreEqual(0, count);
            GameSettings.SetScreenModeIndex(cur, true);
            Assert.AreEqual(1, count);
            Assert.AreEqual(cur, last);
            Assert.AreEqual(cur, GameSettings.ScreenModeIndex);
        }
        finally
        {
            GameSettings.OnScreenModeChanged -= h;
        }
    }

    [Test]
    public void ScreenMode_ForceNotify_OutOfRange_IsClamped()
    {
        GameSettings.SetScreenModeIndex(9999, true);
        Assert.AreEqual(ScreenModes.Count - 1, GameSettings.ScreenModeIndex);
    }

    [Test]
    public void ScreenModes_DefaultIndex_IsFullScreenIndex()
    {
        Assert.AreEqual(ScreenModes.FullScreenIndex, ScreenModes.DEFAULT_INDEX);
    }

    [Test]
    public void GetChannelVolume_ExcludesMaster_AndHonorsChannelMute()
    {
        GameSettings.SetVolume(AudioChannel.Master, 0.5f);
        GameSettings.SetVolume(AudioChannel.BGM, 0.4f);
        Assert.AreEqual(0.4f, GameSettings.GetChannelVolume(AudioChannel.BGM), 1e-5f);
        Assert.AreEqual(1f, GameSettings.GetChannelVolume(AudioChannel.Master));

        GameSettings.SetMuted(AudioChannel.BGM, true);
        Assert.AreEqual(0f, GameSettings.GetChannelVolume(AudioChannel.BGM));

        GameSettings.SetMuted(AudioChannel.Master, true);
        Assert.AreEqual(1f, GameSettings.GetChannelVolume(AudioChannel.Master));
        Assert.AreEqual(0f, GameSettings.GetChannelVolume(AudioChannel.BGM));
        GameSettings.SetMuted(AudioChannel.BGM, false);
        Assert.AreEqual(0.4f, GameSettings.GetChannelVolume(AudioChannel.BGM), 1e-5f);
    }
}
