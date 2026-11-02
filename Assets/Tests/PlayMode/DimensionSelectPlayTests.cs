using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Rig = MonsterTestFactory.Rig;

/// <summary>
/// 차원 선택 PlayMode 테스트
/// DimensionSelectController (커서 / 확정 / 취소), Spawner 몬스터 외형, DimensionApplier
/// </summary>
public class DimensionSelectPlayTests
{
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        DimensionSelection.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        DimensionSelection.Clear();
        MonsterTestFactory.Cleanup();
        for (int i = created.Count - 1; i >= 0; --i)
        {
            if (null != created[i])
            {
                Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    #region Helpers
    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private static void SetField(object _target, string _field, object _value)
    {
        FieldInfo f = _target.GetType().GetField(_field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(f, $"field {_field} not found on {_target.GetType().Name}");
        f.SetValue(_target, _value);
    }

    private static object GetField(object _target, string _field)
    {
        FieldInfo f = _target.GetType().GetField(_field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(f, $"field {_field} not found on {_target.GetType().Name}");
        return f.GetValue(_target);
    }

    private DimensionData NewDim(string _name)
    {
        DimensionData d = Track(ScriptableObject.CreateInstance<DimensionData>());
        d.name = _name;
        return d;
    }

    private Sprite NewSprite(string _name)
    {
        Texture2D tex = Track(new Texture2D(4, 4));
        Sprite s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        s.name = _name;
        return Track(s);
    }

    private DimensionSelectController NewController(int _count, float _loadDelay = 1000f, string _scene = "")
    {
        GameObject go = Track(new GameObject("Controller"));
        go.SetActive(false);
        DimensionSelectController c = go.AddComponent<DimensionSelectController>();
        DimensionData[] dims = new DimensionData[_count];
        for (int i = 0; i < _count; ++i)
        {
            dims[i] = NewDim("Dim" + i);
        }
        SetField(c, "dimensions", dims);
        SetField(c, "loadDelay", _loadDelay);
        SetField(c, "gameSceneName", _scene);
        go.SetActive(true);
        return c;
    }
    #endregion

    #region Controller cursor
    [Test]
    public void Controller_InitialCursors_AreLeft0Right1()
    {
        DimensionSelectController c = NewController(3);
        Assert.AreEqual(0, c.GetCursor(PlayerSide.Left));
        Assert.AreEqual(1, c.GetCursor(PlayerSide.Right));
        Assert.AreSame(c.Dimensions[0], c.GetCurrent(PlayerSide.Left));
    }

    [Test]
    public void Controller_MoveCursor_WrapsBothDirections()
    {
        // 오른쪽 커서(1)가 확정 전이라 건너뛰기 없음. 왼쪽만 이동
        DimensionSelectController c = NewController(3);
        // Left 0 -> prev -> 2 (wrap)
        Assert.IsTrue(c.MoveCursor(PlayerSide.Left, -1));
        Assert.AreEqual(2, c.GetCursor(PlayerSide.Left));
        // 2 -> next -> 0 (wrap)
        Assert.IsTrue(c.MoveCursor(PlayerSide.Left, 1));
        Assert.AreEqual(0, c.GetCursor(PlayerSide.Left));
        // 0 -> next -> 1 (오른쪽이 아직 확정 안 했으므로 겹쳐도 이동)
        Assert.IsTrue(c.MoveCursor(PlayerSide.Left, 1));
        Assert.AreEqual(1, c.GetCursor(PlayerSide.Left));
    }

    [Test]
    public void Controller_MoveCursor_FiresEvent_AndZeroDirectionIgnored()
    {
        DimensionSelectController c = NewController(3);
        int calls = 0;
        PlayerSide lastSide = PlayerSide.Right;
        int lastIndex = -1;
        c.OnCursorChanged += (s, i) => { ++calls; lastSide = s; lastIndex = i; };

        Assert.IsFalse(c.MoveCursor(PlayerSide.Left, 0));
        Assert.AreEqual(0, calls);

        c.MoveCursor(PlayerSide.Left, 1);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(PlayerSide.Left, lastSide);
        Assert.AreEqual(1, lastIndex);
    }

    [Test]
    public void Controller_MoveCursor_SkipsDimensionConfirmedByOpponent()
    {
        DimensionSelectController c = NewController(3);
        // 오른쪽이 1번 확정
        Assert.IsTrue(c.ToggleConfirm(PlayerSide.Right));
        // 왼쪽 0 -> next: 1 은 건너뛰고 2
        Assert.IsTrue(c.MoveCursor(PlayerSide.Left, 1));
        Assert.AreEqual(2, c.GetCursor(PlayerSide.Left));
        // 2 -> prev : 1 건너뛰고 0
        Assert.IsTrue(c.MoveCursor(PlayerSide.Left, -1));
        Assert.AreEqual(0, c.GetCursor(PlayerSide.Left));
    }

    [Test]
    public void Controller_MoveCursor_WhileConfirmed_Blocked()
    {
        DimensionSelectController c = NewController(3);
        c.ToggleConfirm(PlayerSide.Left);
        Assert.IsFalse(c.MoveCursor(PlayerSide.Left, 1));
        Assert.AreEqual(0, c.GetCursor(PlayerSide.Left));
    }

    [Test]
    public void Controller_TwoDimensions_OpponentConfirmed_NoMovePossible()
    {
        DimensionSelectController c = NewController(2);
        c.ToggleConfirm(PlayerSide.Right); // 1 확정
        Assert.IsFalse(c.MoveCursor(PlayerSide.Left, 1), "갈 수 있는 차원이 없다");
        Assert.AreEqual(0, c.GetCursor(PlayerSide.Left));
    }
    #endregion

    #region Controller confirm
    [Test]
    public void Controller_Confirm_TogglesAndFiresEvent()
    {
        DimensionSelectController c = NewController(3);
        List<bool> states = new List<bool>();
        c.OnConfirmChanged += (s, b) => states.Add(b);

        Assert.IsTrue(c.ToggleConfirm(PlayerSide.Left));
        Assert.IsTrue(c.IsConfirmed(PlayerSide.Left));
        Assert.IsTrue(c.ToggleConfirm(PlayerSide.Left));
        Assert.IsFalse(c.IsConfirmed(PlayerSide.Left));
        CollectionAssert.AreEqual(new[] { true, false }, states);
    }

    [Test]
    public void Controller_CannotConfirmDimensionConfirmedByOpponent()
    {
        DimensionSelectController c = NewController(3);
        c.MoveCursor(PlayerSide.Left, 1); // 겹치게 (Left=1, Right=1)
        Assert.AreEqual(1, c.GetCursor(PlayerSide.Left));
        Assert.IsTrue(c.ToggleConfirm(PlayerSide.Right)); // 오른쪽 먼저 확정

        Assert.IsFalse(c.CanConfirm(PlayerSide.Left, 1));
        Assert.IsFalse(c.ToggleConfirm(PlayerSide.Left), "상대가 확정한 차원은 확정 불가");
        Assert.IsFalse(c.IsConfirmed(PlayerSide.Left));
        Assert.IsFalse(c.IsBothConfirmed);
    }

    [Test]
    public void Controller_CanConfirm_OutOfRange_False()
    {
        DimensionSelectController c = NewController(3);
        Assert.IsFalse(c.CanConfirm(PlayerSide.Left, -1));
        Assert.IsFalse(c.CanConfirm(PlayerSide.Left, 3));
        Assert.IsTrue(c.CanConfirm(PlayerSide.Left, 2));
    }

    [Test]
    public void Controller_CancelAfterOpponentConfirmed_ReleasesDimension()
    {
        DimensionSelectController c = NewController(3);
        c.MoveCursor(PlayerSide.Left, 1);
        c.ToggleConfirm(PlayerSide.Right);
        Assert.IsFalse(c.CanConfirm(PlayerSide.Left, 1));

        c.ToggleConfirm(PlayerSide.Right); // 취소
        Assert.IsTrue(c.CanConfirm(PlayerSide.Left, 1));
        Assert.IsTrue(c.ToggleConfirm(PlayerSide.Left));
    }

    [Test]
    public void Controller_BothConfirmed_FiresOnBothConfirmed_AndSetsSelectionAfterDelay()
    {
        // loadDelay 가 길어서 씬 로드는 일어나지 않는다
        DimensionSelectController c = NewController(3, 1000f);
        int both = 0;
        c.OnBothConfirmed += () => ++both;

        c.ToggleConfirm(PlayerSide.Left);
        Assert.AreEqual(0, both);
        c.ToggleConfirm(PlayerSide.Right);

        Assert.AreEqual(1, both);
        Assert.IsTrue(c.IsBothConfirmed);
        Assert.IsFalse(DimensionSelection.HasSelection, "대기 중에는 아직 세팅하지 않는다");
    }

    [UnityTest]
    public IEnumerator Controller_CancelDuringLoadDelay_NeverSetsSelection()
    {
        DimensionSelectController c = NewController(3, 0.2f, "NoSuchScene_ForTest");
        c.ToggleConfirm(PlayerSide.Left);
        c.ToggleConfirm(PlayerSide.Right);
        Assert.IsTrue(c.IsBothConfirmed);

        c.ToggleConfirm(PlayerSide.Left); // 취소
        yield return new WaitForSecondsRealtime(0.5f);

        Assert.IsFalse(DimensionSelection.HasSelection);
        Assert.IsNull(DimensionSelection.Left);
    }

    [UnityTest]
    public IEnumerator Controller_BothConfirmed_AfterDelay_SetsSelectionToCursors()
    {
        // 존재하지 않는 씬 이름 : 로드는 실패(로그)하지만 그 전에 DimensionSelection 이 세팅된다
        LogAssert.ignoreFailingMessages = true;
        DimensionSelectController c = NewController(3, 0.1f, "NoSuchScene_ForTest");
        c.MoveCursor(PlayerSide.Left, -1); // Left = 2, Right = 1
        c.ToggleConfirm(PlayerSide.Left);
        c.ToggleConfirm(PlayerSide.Right);

        yield return new WaitForSecondsRealtime(0.4f);

        Assert.IsTrue(DimensionSelection.HasSelection);
        Assert.AreSame(c.Dimensions[2], DimensionSelection.Left);
        Assert.AreSame(c.Dimensions[1], DimensionSelection.Right);
    }
    #endregion

    #region Spawner skins
    private class SkinRig
    {
        public Rig rig;
        public MonsterData data;
        public Sprite original;
    }

    private SkinRig NewSkinTemplate(string _name)
    {
        SkinRig s = new SkinRig();
        s.data = MonsterTestFactory.NewData(0f);
        s.data.name = _name;
        s.rig = MonsterTestFactory.Create(s.data, new Vector2(500f, 500f), 50f, true, false, true);
        s.original = NewSprite(_name + "_orig");
        s.rig.sprite.sprite = s.original;
        return s;
    }

    private static SpriteRenderer SpriteOf(Spawnable _s)
    {
        return _s.GetComponentInChildren<SpriteRenderer>(true);
    }

    [Test]
    public void Spawner_Skin_AppliesSpriteForMatchingMonster()
    {
        SkinRig a = NewSkinTemplate("A");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Sprite skinSprite = NewSprite("skin");
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data, sprite = skinSprite } });

        Spawnable s = sp.SpawnAt(a.rig.spawnable, Vector2.zero);

        Assert.IsNotNull(s);
        Assert.AreSame(skinSprite, SpriteOf(s).sprite);
    }

    [Test]
    public void Spawner_Skin_OtherMonsterKeepsPrefabDefault()
    {
        SkinRig a = NewSkinTemplate("A");
        SkinRig b = NewSkinTemplate("B");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data, sprite = NewSprite("skin") } });

        Spawnable s = sp.SpawnAt(b.rig.spawnable, Vector2.zero);

        Assert.AreSame(b.original, SpriteOf(s).sprite);
    }

    [Test]
    public void Spawner_Skin_EmptySpriteField_KeepsPrefabDefault()
    {
        SkinRig a = NewSkinTemplate("A");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data } });

        Spawnable s = sp.SpawnAt(a.rig.spawnable, Vector2.zero);

        Assert.AreSame(a.original, SpriteOf(s).sprite);
    }

    [Test]
    public void Spawner_Skin_PoolReuse_RestoresDefaultWhenSkinRemoved()
    {
        SkinRig a = NewSkinTemplate("A");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data, sprite = NewSprite("skin") } });
        Spawnable first = sp.SpawnAt(a.rig.spawnable, Vector2.zero);
        first.ReturnToPool();

        sp.SetMonsterSkins(null);
        Spawnable second = sp.SpawnAt(a.rig.spawnable, Vector2.zero);

        Assert.AreSame(first, second, "풀 재사용");
        Assert.AreSame(a.original, SpriteOf(second).sprite, "외형이 없으면 프리팹 기본값으로 복원");
    }

    [Test]
    public void Spawner_Skin_PoolReuse_RestoresDefaultWhenSwitchedToOtherMonsterSkin()
    {
        SkinRig a = NewSkinTemplate("A");
        SkinRig b = NewSkinTemplate("B");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data, sprite = NewSprite("skinA") } });
        Spawnable first = sp.SpawnAt(a.rig.spawnable, Vector2.zero);
        first.ReturnToPool();

        // 다른 몬스터 외형만 있는 목록으로 교체 : A 는 기본값
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = b.data, sprite = NewSprite("skinB") } });
        Spawnable second = sp.SpawnAt(a.rig.spawnable, Vector2.zero);

        Assert.AreSame(first, second);
        Assert.AreSame(a.original, SpriteOf(second).sprite);
    }

    [Test]
    public void Spawner_Skin_PoolReuse_ChangesToNewSkin()
    {
        SkinRig a = NewSkinTemplate("A");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Sprite skin1 = NewSprite("skin1");
        Sprite skin2 = NewSprite("skin2");
        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data, sprite = skin1 } });
        Spawnable first = sp.SpawnAt(a.rig.spawnable, Vector2.zero);
        first.ReturnToPool();

        sp.SetMonsterSkins(new[] { new MonsterSkin { monster = a.data, sprite = skin2 } });
        Spawnable second = sp.SpawnAt(a.rig.spawnable, Vector2.zero);

        Assert.AreSame(first, second);
        Assert.AreSame(skin2, SpriteOf(second).sprite);
    }

    [Test]
    public void Spawner_SetMonsterSkins_NullAndNullMonsterEntries_Safe()
    {
        SkinRig a = NewSkinTemplate("A");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Assert.DoesNotThrow(() => sp.SetMonsterSkins(null));
        Assert.DoesNotThrow(() => sp.SetMonsterSkins(new[] { new MonsterSkin { monster = null, sprite = NewSprite("x") } }));

        Spawnable s = sp.SpawnAt(a.rig.spawnable, Vector2.zero);
        Assert.AreSame(a.original, SpriteOf(s).sprite);
    }

    [Test]
    public void Spawner_SetMonsterSkins_Duplicate_LastWinsWithWarning()
    {
        SkinRig a = NewSkinTemplate("A");
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        Sprite s1 = NewSprite("s1");
        Sprite s2 = NewSprite("s2");

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("중복"));
        sp.SetMonsterSkins(new[]
        {
            new MonsterSkin { monster = a.data, sprite = s1 },
            new MonsterSkin { monster = a.data, sprite = s2 },
        });

        Spawnable s = sp.SpawnAt(a.rig.spawnable, Vector2.zero);
        Assert.AreSame(s2, SpriteOf(s).sprite);
    }

    [Test]
    public void Spawner_SetWaves_ReplacesWaveArray()
    {
        Spawner sp = MonsterTestFactory.CreateSpawner(null);
        WaveData w = Track(ScriptableObject.CreateInstance<WaveData>());
        w.spwanArray = new SpawnData[0];
        WaveData[] waves = { w };

        sp.SetWaves(waves);

        Assert.AreSame(waves, GetField(sp, "waveArray"));
    }
    #endregion

    #region DimensionApplier
    private class ApplierRig
    {
        public GameObject root;
        public PlayerStats player;
        public SpriteRenderer playerRenderer;
        public Spawner leftSpawner;   // 왼쪽 진영의 몬스터가 나오는 스포너 (오른쪽 차원이 채움)
        public Spawner rightSpawner;
        public RewardManager manager;
    }

    private ApplierRig NewRig()
    {
        ApplierRig r = new ApplierRig();
        r.root = Track(new GameObject("ApplierRig"));
        r.manager = r.root.AddComponent<RewardManager>();

        GameObject pl = Track(new GameObject("PlayerL"));
        r.player = pl.AddComponent<PlayerStats>();
        SetField(r.player, "side", PlayerSide.Left);
        GameObject body = new GameObject("body");
        body.transform.SetParent(pl.transform, false);
        r.playerRenderer = body.AddComponent<SpriteRenderer>();

        r.leftSpawner = MonsterTestFactory.CreateSpawner(null);
        r.rightSpawner = MonsterTestFactory.CreateSpawner(null);
        return r;
    }

    private DimensionApplier NewApplier(ApplierRig _r, PlayerSide _side, DimensionData _default, Spawner _opponent)
    {
        GameObject go = Track(new GameObject("Applier_" + _side));
        go.SetActive(false);
        DimensionApplier a = go.AddComponent<DimensionApplier>();
        SetField(a, "side", _side);
        SetField(a, "defaultDimension", _default);
        SetField(a, "opponentSpawner", _opponent);
        SetField(a, "rewardManager", _r.manager);
        if (PlayerSide.Left == _side)
        {
            SetField(a, "player", _r.player);
        }
        return a;
    }

    private Weapon NewWeapon(string _name)
    {
        GameObject go = Track(new GameObject(_name));
        go.SetActive(false);
        Weapon w = go.AddComponent<Weapon>();
        w.damage = 10f;
        w.attackSpeed = 1f;
        w.range = 5f;
        w.fireCount = 1;
        w.hitCount = 1;
        w.areaRadius = 2f;
        return w;
    }

    private RewardData NewWeaponReward(string _name, Weapon _weapon)
    {
        WeaponRewardEffect e = Track(ScriptableObject.CreateInstance<WeaponRewardEffect>());
        SetField(e, "weaponPrefab", _weapon);
        WeaponUpgradeTable t = Track(ScriptableObject.CreateInstance<WeaponUpgradeTable>());
        t.SetSteps(new[]
        {
            new WeaponUpgradeStep("S1", "D1", new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f)),
            new WeaponUpgradeStep("S2", "D2", new WeaponModifier(WeaponParam.Damage, StatModifierMode.Multiply, 0.2f)),
        }, 1f);
        SetField(e, "upgradeTable", t);
        RewardData r = Track(ScriptableObject.CreateInstance<RewardData>());
        r.name = _name;
        r.SetName(_name);
        r.SetEffect(e);
        return r;
    }

    private DimensionData NewFullDim(string _name, Weapon _start, RewardData[] _rewards, WaveData[] _waves, MonsterSkin[] _skins, Sprite _playerSprite)
    {
        DimensionData d = NewDim(_name);
        SetField(d, "startWeapon", _start);
        SetField(d, "weaponRewards", _rewards);
        SetField(d, "waves", _waves);
        SetField(d, "monsterSkins", _skins);
        SetField(d, "playerSprite", _playerSprite);
        return d;
    }

    [UnityTest]
    public IEnumerator Applier_PutsWavesAndSkinsOnOpponentSpawnerOnly()
    {
        SkinRig mon = NewSkinTemplate("Mon");
        ApplierRig r = NewRig();
        WaveData wave = Track(ScriptableObject.CreateInstance<WaveData>());
        wave.spwanArray = new SpawnData[0];
        WaveData[] waves = { wave };
        Sprite skin = NewSprite("dimSkin");
        DimensionData dim = NewFullDim("DimL", null, null, waves, new[] { new MonsterSkin { monster = mon.data, sprite = skin } }, null);
        DimensionSelection.Set(dim, NewDim("DimR"));

        // 왼쪽 차원 -> 반대 진영(오른쪽) 스포너
        DimensionApplier a = NewApplier(r, PlayerSide.Left, null, r.rightSpawner);
        a.gameObject.SetActive(true);
        yield return null;

        Assert.AreSame(waves, GetField(r.rightSpawner, "waveArray"), "반대 진영 스포너에 웨이브");
        Assert.IsNull(GetField(r.leftSpawner, "waveArray"), "자기 진영 스포너는 그대로");
        Spawnable s = r.rightSpawner.SpawnAt(mon.rig.spawnable, Vector2.zero);
        Assert.AreSame(skin, SpriteOf(s).sprite, "반대 진영 스포너에 외형");
        Spawnable s2 = r.leftSpawner.SpawnAt(mon.rig.spawnable, Vector2.zero);
        Assert.AreSame(mon.original, SpriteOf(s2).sprite, "자기 진영 스포너는 외형 없음");
        Assert.AreSame(dim, a.Dimension);
    }

    [UnityTest]
    public IEnumerator Applier_UsesDefaultDimension_WhenSelectionEmpty()
    {
        ApplierRig r = NewRig();
        WaveData wave = Track(ScriptableObject.CreateInstance<WaveData>());
        wave.spwanArray = new SpawnData[0];
        WaveData[] waves = { wave };
        DimensionData def = NewFullDim("Def", null, null, waves, null, null);

        DimensionApplier a = NewApplier(r, PlayerSide.Left, def, r.rightSpawner);
        a.gameObject.SetActive(true);
        yield return null;

        Assert.AreSame(def, a.Dimension);
        Assert.AreSame(waves, GetField(r.rightSpawner, "waveArray"));
    }

    [UnityTest]
    public IEnumerator Applier_NoDimension_LogsErrorAndAppliesNothing()
    {
        ApplierRig r = NewRig();
        DimensionApplier a = NewApplier(r, PlayerSide.Left, null, r.rightSpawner);

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("적용할 차원이 없습니다"));
        a.gameObject.SetActive(true);
        yield return null;

        Assert.IsNull(a.Dimension);
        Assert.IsFalse(r.manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.Weapon));
    }

    [UnityTest]
    public IEnumerator Applier_SetsWeaponPool_StartWeaponApplied_RewardHiddenAndLevel1Upgrade()
    {
        ApplierRig r = NewRig();
        Weapon startW = NewWeapon("StartW");
        Weapon otherW = NewWeapon("OtherW");
        RewardData startR = NewWeaponReward("StartR", startW);
        RewardData otherR = NewWeaponReward("OtherR", otherW);
        RewardData shared = Track(ScriptableObject.CreateInstance<RewardData>());
        r.manager.Add(RewardPoolType.Weapon, shared, 1f);
        DimensionData dim = NewFullDim("D", startW, new[] { startR, otherR }, null, null, null);
        DimensionSelection.Set(dim, NewDim("R"));

        LogAssert.ignoreFailingMessages = true; // 맵/레이어/스포너 경고 무시
        DimensionApplier a = NewApplier(r, PlayerSide.Left, null, r.rightSpawner);
        a.gameObject.SetActive(true);
        yield return null;

        Assert.IsTrue(r.manager.IsPoolOverriddenFor(PlayerSide.Left, RewardPoolType.Weapon));
        Assert.IsFalse(r.manager.IsPoolOverriddenFor(PlayerSide.Right, RewardPoolType.Weapon));
        Assert.IsTrue(r.player.HasWeapon(startW), "시작 무기 지급");
        Assert.IsTrue(r.manager.IsExcludedFor(PlayerSide.Left, startR));

        List<RewardData> l = new List<RewardData>();
        r.manager.GetRandomUnique(100, new[] { RewardPoolType.Weapon }, PlayerSide.Left, l);
        CollectionAssert.AreEquivalent(new[] { otherR }, l, "차원 무기만, 시작 무기 보상 제외");

        List<RewardData> rr = new List<RewardData>();
        r.manager.GetRandomUnique(100, new[] { RewardPoolType.Weapon }, PlayerSide.Right, rr);
        CollectionAssert.AreEquivalent(new[] { shared }, rr, "다른 진영은 공유 풀");

        List<RewardData> up = new List<RewardData>();
        r.manager.GetRandomUnique(100, new[] { RewardPoolType.WeaponUpgrade }, PlayerSide.Left, up);
        Assert.AreEqual(1, up.Count, "1단계 업그레이드 추가");
    }

    [UnityTest]
    public IEnumerator Applier_StartWeaponNotInPool_GivesWeaponWithoutUpgrade()
    {
        ApplierRig r = NewRig();
        Weapon startW = NewWeapon("StartW");
        RewardData otherR = NewWeaponReward("OtherR", NewWeapon("OtherW"));
        DimensionData dim = NewFullDim("D", startW, new[] { otherR }, null, null, null);
        DimensionSelection.Set(dim, NewDim("R"));

        LogAssert.ignoreFailingMessages = true;
        DimensionApplier a = NewApplier(r, PlayerSide.Left, null, r.rightSpawner);
        a.gameObject.SetActive(true);
        yield return null;

        Assert.IsTrue(r.player.HasWeapon(startW));
        List<RewardData> up = new List<RewardData>();
        r.manager.GetRandomUnique(100, new[] { RewardPoolType.WeaponUpgrade }, PlayerSide.Left, up);
        Assert.AreEqual(0, up.Count);
    }

    [UnityTest]
    public IEnumerator Applier_AppliesPlayerSprite()
    {
        ApplierRig r = NewRig();
        Sprite ps = NewSprite("player");
        DimensionData dim = NewFullDim("D", null, null, null, null, ps);
        DimensionSelection.Set(dim, NewDim("R"));

        LogAssert.ignoreFailingMessages = true;
        DimensionApplier a = NewApplier(r, PlayerSide.Left, null, r.rightSpawner);
        a.gameObject.SetActive(true);
        yield return null;

        Assert.AreSame(ps, r.playerRenderer.sprite);
    }
    #endregion
}
