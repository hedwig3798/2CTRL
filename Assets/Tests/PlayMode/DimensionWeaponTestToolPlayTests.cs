#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

/// <summary>
/// 차원 무기 테스트 씬(DimensionWeaponTest.unity) 검증
/// </summary>
public class DimensionWeaponTestToolPlayTests
{
    private const string ScenePath = "Assets/Scenes/DimensionWeaponTest.unity";
    private DimensionWeaponTestController ctrl;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        ctrl = UnityEngine.Object.FindFirstObjectByType<DimensionWeaponTestController>();
        Assert.IsNotNull(ctrl, "controller");
        float t = 0f;
        while ((null == StageSpace.Instance || false == StageSpace.Instance.IsReady) && t < 5f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Assert.IsTrue(StageSpace.Instance.IsReady, "StageSpace ready");
        yield return null;
    }

    private static float Total(PlayerSide _s)
    {
        float sum = 0f;
        foreach (WeaponTestDamageMeter m in UnityEngine.Object.FindObjectsByType<WeaponTestDamageMeter>(FindObjectsInactive.Include))
        {
            int layer = m.gameObject.layer;
            bool left = layer == LayerMask.NameToLayer("LMonster");
            bool right = layer == LayerMask.NameToLayer("RMonster");
            if ((PlayerSide.Left == _s && left) || (PlayerSide.Right == _s && right))
            {
                sum += m.TotalDamage;
            }
        }
        return sum;
    }

    private static int MeterCount()
    {
        return UnityEngine.Object.FindObjectsByType<WeaponTestDamageMeter>(FindObjectsInactive.Include).Length;
    }

    private static float Signature(PlayerStats _p)
    {
        float s = 0f;
        foreach (ValueType v in Enum.GetValues(typeof(ValueType)))
        {
            s += Mathf.Abs(_p.GetLevelUpAdd(v)) + Mathf.Abs(_p.GetMultiply(v));
        }
        return s;
    }

    private static int StageLayerCount()
    {
        int stage = LayerMask.NameToLayer("Stage");
        int n = 0;
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go.layer == stage && null == go.GetComponentInParent<Camera>())
            {
                ++n;
            }
        }
        return n;
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator Weapons_AllSix_SpawnAndDealDamage()
    {
        Assert.AreEqual(6, ctrl.WeaponCount);
        StringBuilder table = new StringBuilder("\nIDX | weapon | created | L dmg | R dmg | time\n");
        int failures = 0;
        for (int i = 0; i < ctrl.WeaponCount; i++)
        {
            Assert.IsTrue(ctrl.SelectWeapon(i), "select " + i);
            yield return null;
            yield return null;
            bool created = null != ctrl.CurrentWeapon && false == ctrl.IsWeaponPending;
            float t = 0f;
            // 톱날(무작위 높이)·역병 지뢰(무작위 위치)는 배치에 따라 첫 타격이 늦어 넉넉히 기다린다
            while (t < 20f &&Total(PlayerSide.Left) + Total(PlayerSide.Right) <= 0f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            float l = Total(PlayerSide.Left);
            float r = Total(PlayerSide.Right);
            string wname = null != ctrl.CurrentWeapon ? ctrl.CurrentWeapon.name : "null";
            table.AppendLine($"{i} | {wname} | {created} | {l:0.#} | {r:0.#} | {t:0.0}s");
            if (false == created || l + r <= 0f)
            {
                ++failures;
            }
        }
        Debug.Log(table.ToString());
        System.IO.File.WriteAllText("E:/2CTRL/2CTRL/.claude/tmp/dimweapon_table.txt", table.ToString());
        Assert.AreEqual(0, failures, table.ToString());
    }

    [UnityTest]
    public IEnumerator SelectWeapon_Switch_RemovesPreviousAndLeavesNoStageResidue()
    {
        // 시작 무기(Start 에서 생성)의 Stage 오브젝트는 기준선에서 뺀다
        int baseline = StageLayerCount();
        if (null != ctrl.CurrentWeapon)
        {
            int stage = LayerMask.NameToLayer("Stage");
            foreach (Transform tr in ctrl.CurrentWeapon.GetComponentsInChildren<Transform>(true))
            {
                if (stage == tr.gameObject.layer)
                {
                    baseline--;
                }
            }
        }
        ctrl.SelectWeapon(0);
        yield return new WaitForSeconds(1f);
        DimensionWeapon first = ctrl.CurrentWeapon;
        Assert.IsNotNull(first);
        ctrl.SelectWeapon(1);
        yield return null;
        yield return null;
        Assert.IsTrue(null == first, "previous weapon destroyed");
        Assert.IsNotNull(ctrl.CurrentWeapon);
        Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<DimensionWeapon>(FindObjectsInactive.Include).Length, "one weapon only");

        for (int i = 0; i < ctrl.WeaponCount; i++)
        {
            ctrl.SelectWeapon(i);
            yield return new WaitForSeconds(0.5f);
        }
        Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<DimensionWeapon>(FindObjectsInactive.Include).Length);
        UnityEngine.Object.Destroy(ctrl.CurrentWeapon.gameObject);
        yield return null;
        yield return null;
        Assert.AreEqual(baseline, StageLayerCount(), "Stage layer residue after destroying last weapon (baseline " + baseline + ")");
    }

    [UnityTest]
    public IEnumerator Passive_LeftStack_AffectsOnlyLeft_ResetClears()
    {
        PlayerStats l = PlayerStats.Find(PlayerSide.Left);
        PlayerStats r = PlayerStats.Find(PlayerSide.Right);
        Assert.IsTrue(ctrl.PassiveCount > 1);
        float l0 = Signature(l);
        float r0 = Signature(r);
        Assert.IsTrue(ctrl.AddPassiveStack(PlayerSide.Left, 1, 1), "add");
        yield return null;
        Assert.AreEqual(1, ctrl.GetPassiveStack(PlayerSide.Left, 1));
        Assert.AreEqual(0, ctrl.GetPassiveStack(PlayerSide.Right, 1));
        Assert.Greater(Signature(l), l0, "left modifier");
        Assert.AreEqual(r0, Signature(r), 1e-5f, "right untouched");
        ctrl.ResetPassives();
        yield return null;
        Assert.AreEqual(0, ctrl.GetPassiveStack(PlayerSide.Left, 1));
        Assert.AreEqual(0f, Signature(l), 1e-5f, "left cleared");
        Assert.AreEqual(0f, Signature(r), 1e-5f);
    }

    [UnityTest]
    public IEnumerator Passive_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(ctrl.AddPassiveStack(PlayerSide.Left, -1, 1));
        Assert.IsFalse(ctrl.AddPassiveStack(PlayerSide.Left, ctrl.PassiveCount, 1));
        Assert.AreEqual(0, ctrl.GetPassiveStack(PlayerSide.Left, 999));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ResetAll_KeepsDummyCount_ResetsMeters_AndPassives()
    {
        int dummies = MeterCount();
        Assert.Greater(dummies, 0);
        ctrl.SelectWeapon(0);
        ctrl.AddPassiveStack(PlayerSide.Left, 1, 2);
        float t = 0f;
        while (t < 8f && Total(PlayerSide.Left) + Total(PlayerSide.Right) <= 0f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        Assert.Greater(Total(PlayerSide.Left) + Total(PlayerSide.Right), 0f, "damage before reset");
        ctrl.ResetAll();
        yield return null;
        Assert.AreEqual(dummies, MeterCount(), "dummy count after ResetAll");
        Assert.AreEqual(0, ctrl.GetPassiveStack(PlayerSide.Left, 1));
        Assert.AreEqual(0f, Signature(PlayerStats.Find(PlayerSide.Left)), 1e-5f);
        Assert.AreEqual(0f, Total(PlayerSide.Left) + Total(PlayerSide.Right), 1e-3f, "meters reset");
    }

    [UnityTest]
    public IEnumerator SelectWeapon_InvalidIndex_ReturnsFalse()
    {
        LogAssert.ignoreFailingMessages = true;
        Assert.IsFalse(ctrl.SelectWeapon(-1));
        Assert.IsFalse(ctrl.SelectWeapon(ctrl.WeaponCount));
        yield return null;
        LogAssert.ignoreFailingMessages = false;
    }
    /// <summary>
    /// 정령 편대 대상 규칙 (일반 정령과 같음): 정령마다 서로 다른 대상, 대상이 살아 있고 범위 안이면 바꾸지 않는다
    /// </summary>
    [UnityTest]
    public IEnumerator SpiritSquadron_SpiritsPickDistinctTargets_AndKeepThem()
    {
        int index = -1;
        for (int i = 0; i < ctrl.WeaponCount; i++)
        {
            ctrl.SelectWeapon(i);
            yield return null;
            yield return null;
            if (ctrl.CurrentWeapon is SpiritSquadron)
            {
                index = i;
                break;
            }
        }
        Assert.GreaterOrEqual(index, 0, "SpiritSquadron in registry");
        ctrl.SelectLayout(1); // 포위: 진영마다 여러 더미
        ctrl.SelectWeapon(index);

        SpiritSquadron squad = null;
        float t = 0f;
        while (t < 5f)
        {
            squad = ctrl.CurrentWeapon as SpiritSquadron;
            // 최대 수(FinalFireCount)까지 모두 나와 대상을 잡은 뒤 확인한다 (도중에 새 정령이 생기면 수가 바뀐다)
            if (null != squad && squad.SpiritCount >= squad.FinalFireCount && true == AllSpiritsTargeted(squad))
            {
                break;
            }
            t += Time.deltaTime;
            yield return null;
        }
        Assert.IsNotNull(squad);
        Assert.GreaterOrEqual(squad.SpiritCount, 2, "spirits spawned");
        Assert.AreEqual(squad.FinalFireCount, squad.SpiritCount, "all spirits spawned");

        int count = squad.SpiritCount;
        var first = new HealthSystem[count];
        for (int i = 0; i < count; i++)
        {
            first[i] = squad.GetSpiritTarget(i);
            Assert.IsNotNull(first[i], $"spirit {i} has target");
            for (int j = 0; j < i; j++)
            {
                Assert.AreNotSame(first[j], first[i], $"spirit {i} and {j} share a target");
            }
        }

        // 더미는 무적·고정이라 놓칠 일이 없다 → 대상 유지
        yield return new WaitForSeconds(0.6f);
        Assert.AreEqual(count, squad.SpiritCount, "no spirit expired during check");
        for (int i = 0; i < count; i++)
        {
            Assert.AreSame(first[i], squad.GetSpiritTarget(i), $"spirit {i} kept its target");
        }
    }
    private static bool AllSpiritsTargeted(SpiritSquadron _squad)
    {
        for (int i = 0; i < _squad.SpiritCount; i++)
        {
            if (null == _squad.GetSpiritTarget(i))
            {
                return false;
            }
        }
        return true;
    }
}
#endif
