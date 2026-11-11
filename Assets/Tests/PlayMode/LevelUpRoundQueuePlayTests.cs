using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// LevelUpUI 라운드 큐 (일반 레벨업 / 보스 보상). UI 계층은 테스트 안에서 최소 조립한다
/// SelectPanel 은 표시 UI 가 없어 경고를 내므로 로그 실패를 끈다
/// </summary>
public class LevelUpRoundQueuePlayTests
{
    private readonly List<Object> created = new List<Object>();
    private GameObject uiRoot;
    private GameObject content;
    private LevelUpUI ui;
    private SelectPanelGroup left;
    private SelectPanelGroup right;
    private Button resume;
    private RewardManager manager;
    private bool prevIgnore;

    [SetUp]
    public void SetUp()
    {
        prevIgnore = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        manager = Track(new GameObject("RM")).AddComponent<RewardManager>();
    }

    [TearDown]
    public void TearDown()
    {
        // LevelUpUI.OnDisable 이 일시정지를 풀도록 먼저 파괴한다
        if (null != uiRoot)
        {
            Object.DestroyImmediate(uiRoot);
        }
        for (int i = created.Count - 1; i >= 0; --i)
        {
            if (null != created[i])
            {
                Object.DestroyImmediate(created[i]);
            }
        }
        created.Clear();
        Time.timeScale = 1f;
        LogAssert.ignoreFailingMessages = prevIgnore;
    }

    private GameObject Track(GameObject _go)
    {
        created.Add(_go);
        return _go;
    }

    private static void SetField(object _target, string _field, object _value)
    {
        FieldInfo f = _target.GetType().GetField(_field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(f, _field);
        f.SetValue(_target, _value);
    }

    private SelectPanelGroup MakeGroup(string _name, PlayerSide _side, int _panels)
    {
        GameObject g = new GameObject(_name);
        g.transform.SetParent(content.transform, false);
        SelectPanelGroup group = g.AddComponent<SelectPanelGroup>();
        SetField(group, "side", _side);
        for (int i = 0; i < _panels; ++i)
        {
            GameObject p = new GameObject("Panel" + i);
            p.transform.SetParent(g.transform, false);
            p.AddComponent<SelectPanel>();
        }
        return group;
    }

    private void BuildUI()
    {
        uiRoot = new GameObject("LevelUpUI");
        uiRoot.SetActive(false);
        // 실제 프리팹처럼 그룹은 직계 자식(내용 패널) 아래에 둔다
        content = new GameObject("Content");
        content.transform.SetParent(uiRoot.transform, false);
        left = MakeGroup("L", PlayerSide.Left, 2);
        right = MakeGroup("R", PlayerSide.Right, 2);
        GameObject b = new GameObject("Resume");
        b.transform.SetParent(uiRoot.transform, false);
        resume = b.AddComponent<Button>();
        ui = uiRoot.AddComponent<LevelUpUI>();
        SetField(ui, "resumeButton", resume);
        uiRoot.SetActive(true);
    }

    private void AddRewards(RewardPoolType _type, int _count)
    {
        for (int i = 0; i < _count; ++i)
        {
            RewardData r = ScriptableObject.CreateInstance<RewardData>();
            created.Add(r);
            r.SetName(_type + "_" + i);
            manager.Add(_type, r, 1f);
        }
    }

    private void Resume()
    {
        resume.onClick.Invoke();
    }

    [Test]
    public void TwoLevelUps_QueueInOrder_BothSidesSelected_ThenClose()
    {
        AddRewards(RewardPoolType.PassiveBuff, 4);
        BuildUI();
        Assert.IsFalse(ui.IsOpen);

        EventManager.Instance.OnLevelUp?.Invoke(uiRoot, 2);

        Assert.IsTrue(ui.IsOpen);
        Assert.AreEqual(2, ui.PendingRoundCount);
        Assert.IsFalse(ui.IsBossRound);
        Assert.IsTrue(left.gameObject.activeSelf);
        Assert.IsTrue(right.gameObject.activeSelf);
        Assert.IsNotNull(left.SelectedPanel);
        Assert.IsNotNull(right.SelectedPanel);
        Assert.IsTrue(GamePause.IsPaused);

        Resume();
        Assert.IsTrue(ui.IsOpen, "다음 라운드가 남음");
        Assert.AreEqual(1, ui.PendingRoundCount);
        Assert.IsNotNull(left.SelectedPanel);
        Assert.IsNotNull(right.SelectedPanel);

        Resume();
        Assert.IsFalse(ui.IsOpen);
        Assert.AreEqual(0, ui.PendingRoundCount);
        Assert.IsFalse(GamePause.IsPaused);
    }

    [Test]
    public void LevelUp_ZeroAmount_DoesNotOpen()
    {
        AddRewards(RewardPoolType.PassiveBuff, 4);
        BuildUI();
        EventManager.Instance.OnLevelUp?.Invoke(uiRoot, 0);
        Assert.IsFalse(ui.IsOpen);
        Assert.AreEqual(0, ui.PendingRoundCount);
    }

    [Test]
    public void BossReward_ShowsOnlyThatSide_HidesOther_AndRestoresOnClose()
    {
        AddRewards(RewardPoolType.Weapon, 3);
        BuildUI();

        ui.OpenBossReward(PlayerSide.Right);

        Assert.IsTrue(ui.IsOpen);
        Assert.IsTrue(ui.IsBossRound);
        Assert.AreEqual(PlayerSide.Right, ui.BossRoundSide);
        Assert.IsTrue(right.gameObject.activeSelf);
        Assert.IsFalse(left.gameObject.activeSelf, "다른 진영은 숨김");
        Assert.Greater(right.CountVisiblePanels(), 0);
        Assert.IsTrue(resume.interactable, "숨긴 그룹은 재개 조건에서 빠짐");

        Resume();
        Assert.IsFalse(ui.IsOpen);
        Assert.IsTrue(left.gameObject.activeSelf, "닫을 때 숨긴 그룹 복원");
        Assert.IsTrue(right.gameObject.activeSelf);
    }

    [Test]
    public void BossReward_EmptyBossPools_FallsBackToNormalRewards()
    {
        // Weapon / WeaponUpgrade 풀은 비어 있고 일반 풀만 있다
        AddRewards(RewardPoolType.PassiveBuff, 3);
        BuildUI();

        ui.OpenBossReward(PlayerSide.Left);

        Assert.IsTrue(ui.IsBossRound);
        Assert.IsTrue(left.gameObject.activeSelf);
        Assert.IsFalse(right.gameObject.activeSelf);
        Assert.Greater(left.CountVisiblePanels(), 0, "폴백으로 일반 보상이 채워짐");
        Assert.IsNotNull(left.SelectedPanel);
        Assert.IsTrue(resume.interactable);
    }

    [Test]
    public void BossReward_BothPoolsEmpty_NoVisiblePanels_BossGroupOnly()
    {
        BuildUI();
        ui.OpenBossReward(PlayerSide.Left);
        Assert.IsTrue(ui.IsOpen);
        Assert.AreEqual(0, left.CountVisiblePanels());
        Assert.IsTrue(resume.interactable, "선택지가 없어도 재개 가능");
        Resume();
        Assert.IsFalse(ui.IsOpen);
    }

    [Test]
    public void LevelUpThenBossReward_AreProcessedInOrder()
    {
        AddRewards(RewardPoolType.PassiveBuff, 4);
        AddRewards(RewardPoolType.Weapon, 3);
        BuildUI();
        List<string> shown = new List<string>();
        ui.OnRoundShown += (boss, side) => shown.Add(boss ? "boss:" + side : "normal");

        EventManager.Instance.OnLevelUp?.Invoke(uiRoot, 1);
        ui.OpenBossReward(PlayerSide.Right);

        Assert.AreEqual(2, ui.PendingRoundCount);
        Assert.IsFalse(ui.IsBossRound, "먼저 들어온 일반 레벨업이 먼저");
        Assert.IsTrue(left.gameObject.activeSelf);
        Assert.IsTrue(right.gameObject.activeSelf);

        Resume();
        Assert.IsTrue(ui.IsOpen);
        Assert.IsTrue(ui.IsBossRound);
        Assert.AreEqual(PlayerSide.Right, ui.BossRoundSide);
        Assert.IsFalse(left.gameObject.activeSelf);

        Resume();
        Assert.IsFalse(ui.IsOpen);
        CollectionAssert.AreEqual(new[] { "normal", "boss:Right" }, shown);
    }

    [Test]
    public void BossRewardThenLevelUp_AreProcessedInOrder()
    {
        AddRewards(RewardPoolType.PassiveBuff, 4);
        AddRewards(RewardPoolType.Weapon, 3);
        BuildUI();

        ui.OpenBossReward(PlayerSide.Left);
        EventManager.Instance.OnLevelUp?.Invoke(uiRoot, 1);

        Assert.AreEqual(2, ui.PendingRoundCount);
        Assert.IsTrue(ui.IsBossRound);
        Assert.IsFalse(right.gameObject.activeSelf);

        Resume();
        Assert.IsTrue(ui.IsOpen);
        Assert.IsFalse(ui.IsBossRound);
        Assert.IsTrue(left.gameObject.activeSelf);
        Assert.IsTrue(right.gameObject.activeSelf, "일반 라운드에서 양쪽 다시 표시");
        Assert.IsNotNull(right.SelectedPanel);

        Resume();
        Assert.IsFalse(ui.IsOpen);
    }

    [Test]
    public void BossDefeatedEvent_OpensBossRewardForThatSide()
    {
        AddRewards(RewardPoolType.Weapon, 3);
        BuildUI();
        EventManager.Instance.OnBossDefeated?.Invoke(PlayerSide.Left, null);
        Assert.IsTrue(ui.IsOpen);
        Assert.IsTrue(ui.IsBossRound);
        Assert.AreEqual(PlayerSide.Left, ui.BossRoundSide);
    }

    [Test]
    public void DisableWhileOpen_ClearsQueueAndResumesGame()
    {
        AddRewards(RewardPoolType.PassiveBuff, 4);
        BuildUI();
        EventManager.Instance.OnLevelUp?.Invoke(uiRoot, 3);
        Assert.AreEqual(3, ui.PendingRoundCount);

        uiRoot.SetActive(false);
        Assert.IsFalse(ui.IsOpen);
        Assert.AreEqual(0, ui.PendingRoundCount);
        Assert.IsFalse(GamePause.IsPaused);
    }
}
