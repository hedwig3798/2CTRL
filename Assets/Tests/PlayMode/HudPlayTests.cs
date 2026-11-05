using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD (WaveTimerUI / WeaponHUD / WeaponSlotUI) 와 Spawner 남은 시간 PlayMode 검증
/// </summary>
public class HudPlayTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    private static void SetField(object _o, string _n, object _v)
    {
        FieldInfo f = _o.GetType().GetField(_n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, _n);
        f.SetValue(_o, _v);
    }

    private WaveData Wave(float _time, bool _hasSpawnArray = true)
    {
        WaveData w = ScriptableObject.CreateInstance<WaveData>();
        w.time = _time;
        w.spwanArray = _hasSpawnArray ? new SpawnData[0] : null;
        created.Add(w);
        return w;
    }

    /// <summary>
    /// 비활성 스포너 (활성화하면 웨이브 시작)
    /// </summary>
    private Spawner MakeSpawner(params WaveData[] _waves)
    {
        GameObject go = new GameObject("HudSpawner");
        go.SetActive(false);
        created.Add(go);
        Spawner sp = go.AddComponent<Spawner>();
        sp.SetWaves(_waves);
        return sp;
    }

    private WaveTimerUI MakeTimer(out TextMeshProUGUI _text, params Spawner[] _spawners)
    {
        GameObject go = new GameObject("HudTimer");
        go.SetActive(false);
        created.Add(go);
        _text = go.AddComponent<TextMeshProUGUI>();
        WaveTimerUI ui = go.AddComponent<WaveTimerUI>();
        SetField(ui, "spawners", _spawners);
        go.SetActive(true);
        return ui;
    }

    #region Spawner timing
    [UnityTest]
    public IEnumerator Spawner_Start_SetsStartedAndTimes_ThenFinishesAtZero()
    {
        Spawner sp = MakeSpawner(Wave(0.3f), null, Wave(5f, false), Wave(0.3f));
        Assert.IsFalse(sp.IsStarted);
        Assert.AreEqual(-1f, sp.WaveStartTime);
        Assert.AreEqual(0.6f, sp.TotalWaveTime, 0.001f);
        Assert.AreEqual(0.6f, sp.RemainingTime, 0.001f);

        sp.gameObject.SetActive(true);
        yield return null;

        Assert.IsTrue(sp.IsStarted);
        Assert.GreaterOrEqual(sp.WaveStartTime, 0f);
        Assert.AreEqual(0.6f, sp.TotalWaveTime, 0.001f);
        Assert.LessOrEqual(sp.RemainingTime, 0.6f);
        Assert.Greater(sp.RemainingTime, 0f);

        yield return MonsterTestFactory.WaitUntil(() => sp.IsFinished, 3f);
        Assert.IsTrue(sp.IsFinished);
        Assert.AreEqual(0f, sp.RemainingTime);

        yield return new WaitForSeconds(0.2f);
        Assert.AreEqual(0f, sp.RemainingTime);
        Assert.AreEqual(0.6f, sp.TotalWaveTime, 0.001f);
    }

    [UnityTest]
    public IEnumerator Spawner_Remaining_DecreasesMonotonically()
    {
        Spawner sp = MakeSpawner(Wave(0.5f), Wave(0.5f));
        sp.gameObject.SetActive(true);
        yield return null;
        float prev = sp.RemainingTime;
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            Assert.LessOrEqual(sp.RemainingTime, prev);
            prev = sp.RemainingTime;
        }
        Assert.Less(prev, 1f);
    }

    [UnityTest]
    public IEnumerator Spawner_TimeScaleZero_RemainingFrozen()
    {
        Spawner sp = MakeSpawner(Wave(2f));
        sp.gameObject.SetActive(true);
        yield return null;
        yield return null;

        Time.timeScale = 0f;
        yield return null;
        float frozen = sp.RemainingTime;
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.AreEqual(frozen, sp.RemainingTime, 0.0001f);
        Assert.IsFalse(sp.IsFinished);

        Time.timeScale = 1f;
        yield return new WaitForSeconds(0.2f);
        Assert.Less(sp.RemainingTime, frozen);
    }
    #endregion

    #region WaveTimerUI
    [UnityTest]
    public IEnumerator Timer_BeforeStart_ShowsTotal_MmSs()
    {
        Spawner sp = MakeSpawner(Wave(60f), Wave(15f), null);
        WaveTimerUI ui = MakeTimer(out TextMeshProUGUI text, sp);
        yield return null;

        Assert.IsFalse(sp.IsStarted);
        Assert.AreEqual(75, ui.DisplayedSeconds);
        Assert.AreEqual("01:15", text.text);
        Assert.AreEqual(75f, ui.GetRemainingTime(), 0.001f);
    }

    [UnityTest]
    public IEnumerator Timer_Format_Boundaries()
    {
        Spawner zero = MakeSpawner(Wave(0f));
        WaveTimerUI uz = MakeTimer(out TextMeshProUGUI tz, zero);
        Spawner s59 = MakeSpawner(Wave(59f));
        MakeTimer(out TextMeshProUGUI t59, s59);
        Spawner s60 = MakeSpawner(Wave(60f));
        MakeTimer(out TextMeshProUGUI t60, s60);
        Spawner big = MakeSpawner(Wave(6000f));
        MakeTimer(out TextMeshProUGUI tbig, big);
        Spawner frac = MakeSpawner(Wave(0.2f));
        MakeTimer(out TextMeshProUGUI tfrac, frac);
        yield return null;

        Assert.AreEqual("00:00", tz.text);
        Assert.AreEqual(0, uz.DisplayedSeconds);
        Assert.AreEqual("00:59", t59.text);
        Assert.AreEqual("01:00", t60.text);
        Assert.AreEqual("100:00", tbig.text);
        Assert.AreEqual("00:01", tfrac.text);
    }

    [UnityTest]
    public IEnumerator Timer_ShowsZeroWhenWavesEnd_AndStaysZero()
    {
        Spawner sp = MakeSpawner(Wave(0.3f), Wave(0.3f));
        WaveTimerUI ui = MakeTimer(out TextMeshProUGUI text, sp);
        sp.gameObject.SetActive(true);
        yield return null;
        Assert.AreEqual("00:01", text.text);

        yield return MonsterTestFactory.WaitUntil(() => sp.IsFinished, 3f);
        Assert.IsTrue(sp.IsFinished);
        yield return null;
        Assert.AreEqual("00:00", text.text);
        Assert.AreEqual(0, ui.DisplayedSeconds);

        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual("00:00", text.text);
        Assert.AreEqual(0, ui.DisplayedSeconds);
    }

    [UnityTest]
    public IEnumerator Timer_TwoSpawners_UsesMaximum()
    {
        Spawner shortSp = MakeSpawner(Wave(0.5f));
        Spawner longSp = MakeSpawner(Wave(1.5f), Wave(10f));
        WaveTimerUI ui = MakeTimer(out TextMeshProUGUI text, shortSp, longSp);
        shortSp.gameObject.SetActive(true);
        longSp.gameObject.SetActive(true);
        yield return null;

        Assert.AreEqual(Mathf.Max(shortSp.RemainingTime, longSp.RemainingTime), ui.GetRemainingTime(), 0.0001f);
        Assert.AreEqual(longSp.RemainingTime, ui.GetRemainingTime(), 0.0001f);

        yield return MonsterTestFactory.WaitUntil(() => shortSp.IsFinished, 3f);
        Assert.AreEqual(0f, shortSp.RemainingTime);
        Assert.Greater(ui.GetRemainingTime(), 5f);
        Assert.AreEqual(longSp.RemainingTime, ui.GetRemainingTime(), 0.0001f);
    }

    [UnityTest]
    public IEnumerator Timer_NoSpawner_ShowsZeroWithoutException()
    {
        LogAssert.Expect(LogType.Warning, new Regex(@"\[WaveTimerUI\]"));
        WaveTimerUI ui = MakeTimer(out TextMeshProUGUI text, new Spawner[] { null });
        yield return null;
        Assert.AreEqual("00:00", text.text);
        Assert.AreEqual(0f, ui.GetRemainingTime());
    }
    #endregion

    #region Weapon HUD
    private Weapon WeaponPrefab(string _name)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(false);
        created.Add(go);
        return go.AddComponent<Weapon>();
    }

    private PlayerStats MakePlayer(PlayerSide _side)
    {
        GameObject go = new GameObject("HudPlayer" + _side);
        go.SetActive(false);
        created.Add(go);
        PlayerStats ps = go.AddComponent<PlayerStats>();
        SetField(ps, "side", _side);
        go.SetActive(true);
        return ps;
    }

    private WeaponSlotUI MakeSlot(string _name, out Image _icon, out TextMeshProUGUI _level, out TextMeshProUGUI _nameText)
    {
        GameObject go = new GameObject(_name);
        go.SetActive(false);
        created.Add(go);
        WeaponSlotUI slot = go.AddComponent<WeaponSlotUI>();
        _icon = go.AddComponent<Image>();
        GameObject lv = new GameObject("Level");
        lv.transform.SetParent(go.transform, false);
        _level = lv.AddComponent<TextMeshProUGUI>();
        GameObject nm = new GameObject("Name");
        nm.transform.SetParent(go.transform, false);
        _nameText = nm.AddComponent<TextMeshProUGUI>();
        SetField(slot, "iconImage", _icon);
        SetField(slot, "levelText", _level);
        SetField(slot, "nameText", _nameText);
        go.SetActive(true);
        return slot;
    }

    private RewardData MakeWeaponReward(Weapon _prefab, string _rewardName, Sprite _image)
    {
        WeaponRewardEffect effect = ScriptableObject.CreateInstance<WeaponRewardEffect>();
        SetField(effect, "weaponPrefab", _prefab);
        RewardData reward = ScriptableObject.CreateInstance<RewardData>();
        SetField(reward, "effect", effect);
        SetField(reward, "rewardName", _rewardName);
        SetField(reward, "image", _image);
        created.Add(effect);
        created.Add(reward);
        return reward;
    }

    private Sprite MakeSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        created.Add(tex);
        created.Add(s);
        return s;
    }

    private WeaponHUD MakeHud(PlayerSide _side, PlayerStats _player, WeaponSlotUI[] _slots, RewardData[] _rewards)
    {
        GameObject go = new GameObject("HudWeapon");
        go.SetActive(false);
        created.Add(go);
        WeaponHUD hud = go.AddComponent<WeaponHUD>();
        SetField(hud, "side", _side);
        SetField(hud, "player", _player);
        SetField(hud, "slots", _slots);
        SetField(hud, "fallbackRewards", _rewards);
        go.SetActive(true);
        return hud;
    }

    [UnityTest]
    public IEnumerator Slot_SetWeapon_ShowsIconAndInitialLevelZero_UpgradeRefreshes()
    {
        WeaponSlotUI slot = MakeSlot("S", out Image icon, out TextMeshProUGUI level, out TextMeshProUGUI nameText);
        Weapon w = WeaponPrefab("Bow");
        Sprite sprite = MakeSprite();

        slot.SetWeapon(w, sprite, "Bow");
        Assert.IsTrue(slot.HasWeapon);
        Assert.AreSame(w, slot.Weapon);
        Assert.AreEqual(0, slot.DisplayedLevel);
        Assert.AreEqual("0", level.text);
        Assert.IsTrue(icon.enabled);
        Assert.AreSame(sprite, icon.sprite);
        Assert.IsFalse(nameText.gameObject.activeSelf);

        w.ApplyUpgrade(new WeaponUpgradeStep("t", "d"));
        Assert.AreEqual("0", level.text);
        slot.RefreshLevel();
        Assert.AreEqual(1, slot.DisplayedLevel);
        Assert.AreEqual("1", level.text);

        w.ApplyUpgrade(new WeaponUpgradeStep("t", "d"));
        slot.RefreshLevel();
        Assert.AreEqual("2", level.text);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Slot_NoIcon_ShowsFallbackName_AndClearHides()
    {
        WeaponSlotUI slot = MakeSlot("S", out Image icon, out TextMeshProUGUI level, out TextMeshProUGUI nameText);
        Weapon w = WeaponPrefab("Axe");

        slot.SetWeapon(w, null, "Axe");
        Assert.IsFalse(icon.enabled);
        Assert.IsTrue(nameText.gameObject.activeSelf);
        Assert.AreEqual("Axe", nameText.text);

        slot.SetWeapon(w, null, null);
        Assert.AreEqual(string.Empty, nameText.text);

        slot.Clear();
        Assert.IsFalse(slot.HasWeapon);
        Assert.IsNull(slot.Weapon);
        Assert.AreEqual(-1, slot.DisplayedLevel);
        Assert.IsFalse(slot.gameObject.activeSelf);

        slot.RefreshLevel();
        Assert.AreEqual(-1, slot.DisplayedLevel);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Hud_FillsSlotsInAcquisitionOrder_ClearsExtra_AndTracksLevel()
    {
        PlayerStats ps = MakePlayer(PlayerSide.Left);
        Weapon a = WeaponPrefab("A");
        Weapon b = WeaponPrefab("B");
        Weapon c = WeaponPrefab("C");
        Sprite spriteA = MakeSprite();
        Sprite spriteB = MakeSprite();
        Sprite spriteC = MakeSprite();
        RewardData[] rewards =
        {
            MakeWeaponReward(a, "RA", spriteA),
            MakeWeaponReward(b, "RB", spriteB),
            MakeWeaponReward(c, "RC", spriteC),
        };
        WeaponSlotUI s0 = MakeSlot("S0", out Image i0, out TextMeshProUGUI l0, out _);
        WeaponSlotUI s1 = MakeSlot("S1", out Image i1, out TextMeshProUGUI l1, out _);
        WeaponSlotUI s2 = MakeSlot("S2", out Image i2, out _, out _);
        WeaponSlotUI s3 = MakeSlot("S3", out _, out _, out _);

        // 시작 무기 한 개를 가진 상태에서 HUD 가 켜진다
        Assert.IsTrue(ps.AddWeapon(c));
        WeaponHUD hud = MakeHud(PlayerSide.Left, ps, new[] { s0, s1, s2, s3 }, rewards);
        yield return null;

        Assert.AreSame(ps, hud.BoundPlayer);
        Assert.AreSame(spriteC, i0.sprite);
        Assert.AreSame(ps.AcquiredWeapons[0], s0.Weapon);
        Assert.IsFalse(s1.gameObject.activeSelf);

        // 이후 획득 순서대로 이어서 채운다
        ps.AddWeapon(a);
        ps.AddWeapon(b);
        Assert.AreSame(spriteC, i0.sprite);
        Assert.AreSame(spriteA, i1.sprite);
        Assert.AreSame(spriteB, i2.sprite);
        Assert.AreSame(ps.AcquiredWeapons[1], s1.Weapon);
        Assert.AreSame(ps.AcquiredWeapons[2], s2.Weapon);
        Assert.IsFalse(s3.gameObject.activeSelf);
        Assert.AreEqual("0", l0.text);
        Assert.AreEqual("0", l1.text);

        // 업그레이드 후 Update 에서 레벨 갱신
        ps.AcquiredWeapons[1].ApplyUpgrade(new WeaponUpgradeStep("t", "d"));
        ps.AcquiredWeapons[1].ApplyUpgrade(new WeaponUpgradeStep("t", "d"));
        yield return null;
        Assert.AreEqual("0", l0.text);
        Assert.AreEqual("2", l1.text);
    }

    [UnityTest]
    public IEnumerator Hud_IconNotFound_FallsBackToWeaponName()
    {
        PlayerStats ps = MakePlayer(PlayerSide.Left);
        Weapon a = WeaponPrefab("NoReward");
        Weapon b = WeaponPrefab("NoImage");
        RewardData[] rewards = { MakeWeaponReward(b, "NoImageName", null) };
        WeaponSlotUI s0 = MakeSlot("S0", out Image i0, out _, out TextMeshProUGUI n0);
        WeaponSlotUI s1 = MakeSlot("S1", out Image i1, out _, out TextMeshProUGUI n1);
        ps.AddWeapon(a);
        ps.AddWeapon(b);

        MakeHud(PlayerSide.Left, ps, new[] { s0, s1 }, rewards);
        yield return null;

        Assert.IsFalse(i0.enabled);
        Assert.AreEqual("NoReward", n0.text);
        Assert.IsFalse(i1.enabled);
        Assert.AreEqual("NoImageName", n1.text);
    }

    [UnityTest]
    public IEnumerator Hud_MoreWeaponsThanSlots_ShowsOnlyFirstSlots()
    {
        PlayerStats ps = MakePlayer(PlayerSide.Left);
        WeaponSlotUI s0 = MakeSlot("S0", out _, out _, out TextMeshProUGUI n0);
        WeaponSlotUI s1 = MakeSlot("S1", out _, out _, out TextMeshProUGUI n1);
        ps.AddWeapon(WeaponPrefab("A"));
        ps.AddWeapon(WeaponPrefab("B"));
        ps.AddWeapon(WeaponPrefab("C"));

        MakeHud(PlayerSide.Left, ps, new[] { s0, s1 }, null);
        yield return null;

        Assert.AreEqual("A", n0.text);
        Assert.AreEqual("B", n1.text);
    }

    [UnityTest]
    public IEnumerator Hud_LateBinding_FindsPlayerBySideInUpdate()
    {
        WeaponSlotUI s0 = MakeSlot("S0", out _, out _, out TextMeshProUGUI n0);
        WeaponHUD hud = MakeHud(PlayerSide.Right, null, new[] { s0 }, null);
        yield return null;
        Assert.IsNull(hud.BoundPlayer);
        Assert.IsFalse(s0.gameObject.activeSelf);

        PlayerStats ps = MakePlayer(PlayerSide.Right);
        ps.AddWeapon(WeaponPrefab("Late"));
        yield return null;
        yield return null;

        Assert.AreSame(ps, hud.BoundPlayer);
        Assert.IsTrue(s0.HasWeapon);
        Assert.AreEqual("Late", n0.text);
    }

    [UnityTest]
    public IEnumerator Hud_Disable_UnsubscribesFromPlayer()
    {
        PlayerStats ps = MakePlayer(PlayerSide.Left);
        WeaponSlotUI s0 = MakeSlot("S0", out _, out _, out _);
        WeaponHUD hud = MakeHud(PlayerSide.Left, ps, new[] { s0 }, null);
        yield return null;
        Assert.AreSame(ps, hud.BoundPlayer);

        hud.gameObject.SetActive(false);
        Assert.IsNull(hud.BoundPlayer);
        ps.AddWeapon(WeaponPrefab("X"));
        Assert.IsFalse(s0.HasWeapon);
    }
    #endregion
}
