using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 중간 보스 순수 로직 (BossMath / BossRewardRules / DropManager.CalculateGemExpRate)
/// + HealthSystem 즉사 면역·받는 데미지 배율 + PlayerStats.ApplyTimedSlow 겹침
/// </summary>
public class BossEditTests
{
    private GameObject go;

    [TearDown]
    public void TearDown()
    {
        if (null != go)
        {
            Object.DestroyImmediate(go);
            go = null;
        }
    }

    #region BossMath.IsRingDanger
    [Test]
    public void IsRingDanger_SingleRing_InsideAndOutside()
    {
        Assert.IsTrue(BossMath.IsRingDanger(2f, 5f, 0f, true));
        Assert.IsFalse(BossMath.IsRingDanger(8f, 5f, 0f, true));
        Assert.IsFalse(BossMath.IsRingDanger(2f, 5f, 0f, false));
        Assert.IsTrue(BossMath.IsRingDanger(8f, 5f, 0f, false));
    }

    [Test]
    public void IsRingDanger_SingleRing_OnBoundary_IsOutside()
    {
        Assert.IsFalse(BossMath.IsRingDanger(5f, 5f, 0f, true));
        Assert.IsTrue(BossMath.IsRingDanger(5f, 5f, 0f, false));
    }

    [Test]
    public void IsRingDanger_Band_InsideAndOutside()
    {
        Assert.IsTrue(BossMath.IsRingDanger(5.5f, 5f, 1f, true), "띠 안");
        Assert.IsTrue(BossMath.IsRingDanger(4.2f, 5f, 1f, true), "띠 안(안쪽 가장자리 근처)");
        Assert.IsFalse(BossMath.IsRingDanger(2f, 5f, 1f, true), "띠 안쪽 바깥");
        Assert.IsFalse(BossMath.IsRingDanger(9f, 5f, 1f, true), "띠 바깥");
        Assert.IsTrue(BossMath.IsRingDanger(2f, 5f, 1f, false), "띠 밖이 위험");
        Assert.IsFalse(BossMath.IsRingDanger(5f, 5f, 1f, false), "띠 한가운데는 안전");
    }

    [Test]
    public void IsRingDanger_Band_ExactEdge_IsOutside()
    {
        Assert.IsFalse(BossMath.IsRingDanger(6f, 5f, 1f, true));
        Assert.IsFalse(BossMath.IsRingDanger(4f, 5f, 1f, true));
    }

    [Test]
    public void IsRingDanger_NegativeBand_TreatedAsSingleRing()
    {
        Assert.IsTrue(BossMath.IsRingDanger(1f, 5f, -1f, true));
    }
    #endregion

    #region BossMath.PickRingSide
    [Test]
    public void PickRingSide_BelowStreak_UsesRandom()
    {
        Assert.IsTrue(BossMath.PickRingSide(true, 1, 2, 0.2f));
        Assert.IsFalse(BossMath.PickRingSide(true, 1, 2, 0.8f));
        Assert.IsFalse(BossMath.PickRingSide(true, 1, 2, 0.5f), "0.5 는 바깥");
    }

    [Test]
    public void PickRingSide_AtMaxStreak_FlipsRegardlessOfRandom()
    {
        Assert.IsFalse(BossMath.PickRingSide(true, 2, 2, 0.0f));
        Assert.IsTrue(BossMath.PickRingSide(false, 2, 2, 0.99f));
        Assert.IsFalse(BossMath.PickRingSide(true, 5, 2, 0.0f), "초과해도 반대쪽");
    }

    [Test]
    public void PickRingSide_MaxStreakZero_NeverForced()
    {
        Assert.IsTrue(BossMath.PickRingSide(true, 99, 0, 0.1f));
    }

    [Test]
    public void PickRingSide_SimulatedLoop_NeverThreeInARow()
    {
        // RingJudgeKeyword.OnActionStart 와 같은 방식으로 연속 횟수를 센다
        foreach (float forced in new[] { 0.1f, 0.9f })
        {
            bool last = false;
            bool has = false;
            int streak = 0;
            int run = 0;
            int maxRun = 0;
            for (int i = 0; i < 40; ++i)
            {
                bool pick = BossMath.PickRingSide(last, has ? streak : 0, 2, forced);
                if (has && pick == last)
                {
                    ++streak;
                }
                else
                {
                    streak = 1;
                }
                run = streak;
                maxRun = Mathf.Max(maxRun, run);
                last = pick;
                has = true;
            }
            Assert.LessOrEqual(maxRun, 2, $"random={forced}");
        }
    }
    #endregion

    #region BossMath.Bounce / Diagonal / etc
    private static readonly Rect Box = new Rect(-10f, -5f, 20f, 10f);

    [Test]
    public void Bounce_Inside_NoChange()
    {
        bool b = BossMath.Bounce(new Vector2(0f, 0f), new Vector2(1f, 1f), Box, out Vector2 d);
        Assert.IsFalse(b);
        Assert.AreEqual(new Vector2(1f, 1f), d);
    }

    [Test]
    public void Bounce_RightWall_FlipsX()
    {
        bool b = BossMath.Bounce(new Vector2(10f, 0f), new Vector2(1f, 1f), Box, out Vector2 d);
        Assert.IsTrue(b);
        Assert.AreEqual(new Vector2(-1f, 1f), d);
    }

    [Test]
    public void Bounce_Corner_FlipsBoth()
    {
        bool b = BossMath.Bounce(new Vector2(-11f, 6f), new Vector2(-1f, 1f), Box, out Vector2 d);
        Assert.IsTrue(b);
        Assert.AreEqual(new Vector2(1f, -1f), d);
    }

    [Test]
    public void Bounce_OutsideButMovingInward_DoesNotFlip()
    {
        bool b = BossMath.Bounce(new Vector2(12f, 0f), new Vector2(-1f, 0f), Box, out Vector2 d);
        Assert.IsFalse(b);
        Assert.AreEqual(new Vector2(-1f, 0f), d, "이미 안쪽으로 가면 유지 (떨림 방지)");
    }

    [Test]
    public void Bounce_AtWallMovingAlongIt_NoFlip()
    {
        bool b = BossMath.Bounce(new Vector2(10f, 0f), new Vector2(0f, 1f), Box, out Vector2 d);
        Assert.IsFalse(b);
    }

    [Test]
    public void ViewRect_InsetAndClamp()
    {
        Rect r = BossMath.ViewRect(new Vector2(1f, 2f), 5f, 2f, 1f);
        Assert.AreEqual(1f - 9f, r.xMin, 1e-4f);
        Assert.AreEqual(1f + 9f, r.xMax, 1e-4f);
        Assert.AreEqual(2f - 4f, r.yMin, 1e-4f);
        Assert.AreEqual(2f + 4f, r.yMax, 1e-4f);

        Rect tiny = BossMath.ViewRect(Vector2.zero, 1f, 1f, 5f);
        Assert.AreEqual(0f, tiny.width);
        Assert.AreEqual(0f, tiny.height);
    }

    [Test]
    public void DiagonalToward_QuadrantsAndTie()
    {
        Assert.AreEqual(new Vector2(1f, 1f).normalized, BossMath.DiagonalToward(Vector2.zero, new Vector2(3f, 1f)));
        Assert.AreEqual(new Vector2(-1f, -1f).normalized, BossMath.DiagonalToward(Vector2.zero, new Vector2(-3f, -1f)));
        Assert.AreEqual(new Vector2(1f, 1f).normalized, BossMath.DiagonalToward(Vector2.zero, Vector2.zero));
    }

    [Test]
    public void IsHPAtOrBelow_Boundary_AndZeroMax()
    {
        Assert.IsTrue(BossMath.IsHPAtOrBelow(50f, 100f, 0.5f));
        Assert.IsFalse(BossMath.IsHPAtOrBelow(51f, 100f, 0.5f));
        Assert.IsFalse(BossMath.IsHPAtOrBelow(0f, 0f, 0.5f));
        Assert.IsFalse(BossMath.IsHPAtOrBelow(0f, -5f, 0.5f));
    }

    [Test]
    public void ClampPullDistance_Cases()
    {
        Assert.AreEqual(3f, BossMath.ClampPullDistance(3f, 10f, 2f), 1e-5f);
        Assert.AreEqual(8f, BossMath.ClampPullDistance(20f, 10f, 2f), 1e-5f);
        Assert.AreEqual(0f, BossMath.ClampPullDistance(5f, 1f, 2f), 1e-5f, "이미 gap 안");
        Assert.AreEqual(0f, BossMath.ClampPullDistance(-3f, 10f, 2f), 1e-5f);
    }
    #endregion

    #region BossMath.ForwardRingPoint
    [Test]
    public void ForwardRingPoint_DistanceWithinRing_AndAngleWithinSpread()
    {
        Vector2 center = new Vector2(3f, -2f);
        Vector2 forward = new Vector2(0f, 1f);
        for (int i = 0; i <= 10; ++i)
        {
            for (int j = 0; j <= 10; ++j)
            {
                Vector2 p = BossMath.ForwardRingPoint(center, forward, 11f, 13f, 60f, i / 10f, j / 10f);
                Vector2 off = p - center;
                Assert.GreaterOrEqual(off.magnitude, 11f - 1e-3f);
                Assert.LessOrEqual(off.magnitude, 13f + 1e-3f);
                Assert.LessOrEqual(Vector2.Angle(forward, off), 30f + 1e-2f);
            }
        }
    }

    [Test]
    public void ForwardRingPoint_Center01_IsStraightAhead()
    {
        Vector2 p = BossMath.ForwardRingPoint(Vector2.zero, Vector2.right, 10f, 10f, 90f, 0.5f, 0.5f);
        Assert.AreEqual(10f, p.x, 1e-3f);
        Assert.AreEqual(0f, p.y, 1e-3f);
    }

    [Test]
    public void ForwardRingPoint_ZeroForward_UsesRight()
    {
        Vector2 p = BossMath.ForwardRingPoint(Vector2.zero, Vector2.zero, 5f, 5f, 0f, 0.3f, 0.7f);
        Assert.AreEqual(5f, p.x, 1e-3f);
        Assert.AreEqual(0f, p.y, 1e-3f);
    }

    [Test]
    public void ForwardRingPoint_SwappedRadiiAndOutOfRangeRandom_AreClamped()
    {
        Vector2 p = BossMath.ForwardRingPoint(Vector2.zero, Vector2.right, 13f, 11f, 60f, 5f, -3f);
        Assert.AreEqual(11f, p.magnitude, 1e-3f, "distance01 -3 -> 0 -> min(11)");
        Assert.LessOrEqual(Vector2.Angle(Vector2.right, p), 30f + 1e-2f);
    }

    [Test]
    public void ForwardRingPoint_NegativeSpread_TreatedAsZero()
    {
        Vector2 p = BossMath.ForwardRingPoint(Vector2.zero, Vector2.up, 4f, 4f, -90f, 0f, 0f);
        Assert.AreEqual(0f, p.x, 1e-3f);
        Assert.AreEqual(4f, p.y, 1e-3f);
    }
    #endregion

    #region BossRewardRules / DropManager
    [Test]
    public void GetPoolTypes_NotFull_WeaponAndUpgrade_NoBuffNerf()
    {
        List<RewardPoolType> list = new List<RewardPoolType> { RewardPoolType.PassiveNerf };
        BossRewardRules.GetPoolTypes(false, list);
        CollectionAssert.AreEqual(new[] { RewardPoolType.Weapon, RewardPoolType.WeaponUpgrade }, list);
    }

    [Test]
    public void GetPoolTypes_WeaponFull_ExcludesWeapon()
    {
        List<RewardPoolType> list = new List<RewardPoolType>();
        BossRewardRules.GetPoolTypes(true, list);
        CollectionAssert.AreEqual(new[] { RewardPoolType.WeaponUpgrade }, list);
    }

    [Test]
    public void ShouldFallback_EmptyPool_True_ElseFalse()
    {
        Assert.IsTrue(BossRewardRules.ShouldFallback(0));
        Assert.IsTrue(BossRewardRules.ShouldFallback(-1));
        Assert.IsFalse(BossRewardRules.ShouldFallback(1));
        Assert.IsFalse(BossRewardRules.ShouldFallback(3));
    }

    [Test]
    public void CalculateGemExpRate_SumsToTotal()
    {
        float rate = DropManager.CalculateGemExpRate(60f, 20, 1f);
        Assert.AreEqual(3f, rate, 1e-5f);
        Assert.AreEqual(60f, rate * 20 * 1f, 1e-4f);
        Assert.AreEqual(60f, DropManager.CalculateGemExpRate(60f, 20, 2f) * 20 * 2f, 1e-4f, "baseExp 2");
    }

    [Test]
    public void CalculateGemExpRate_EdgeCases()
    {
        Assert.AreEqual(0f, DropManager.CalculateGemExpRate(60f, 0, 1f));
        Assert.AreEqual(0f, DropManager.CalculateGemExpRate(60f, -2, 1f));
        Assert.AreEqual(0f, DropManager.CalculateGemExpRate(-10f, 5, 1f));
        Assert.AreEqual(6f, DropManager.CalculateGemExpRate(60f, 10, 0f), 1e-5f, "baseExp 0 -> 1");
        Assert.AreEqual(6f, DropManager.CalculateGemExpRate(60f, 10, -3f), 1e-5f);
    }
    #endregion

    #region HealthSystem
    private HealthSystem NewHealth(float _max)
    {
        go = new GameObject("BossHealthTest");
        go.SetActive(false);
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = _max;
        hs.currHP = _max;
        go.SetActive(true);
        return hs;
    }

    [Test]
    public void Execute_ExecuteImmune_ActsAsNormalDamage()
    {
        HealthSystem hs = NewHealth(100f);
        hs.IsExecuteImmune = true;
        int deaths = 0;
        hs.OnDeath += _ => ++deaths;

        DamageMassage msg = new DamageMassage();
        msg.damage = 10f;
        hs.Execute(ref msg);

        Assert.AreEqual(90f, hs.CurrHP, 1e-4f);
        Assert.IsFalse(hs.isDead);
        Assert.IsFalse(hs.IsExecuted);
        Assert.AreEqual(0, deaths);
    }

    [Test]
    public void Execute_ExecuteImmune_NormalDamageCanStillKill()
    {
        HealthSystem hs = NewHealth(100f);
        hs.IsExecuteImmune = true;
        DamageMassage msg = new DamageMassage();
        msg.damage = 100f;
        hs.Execute(ref msg);
        Assert.IsTrue(hs.isDead);
        Assert.IsFalse(hs.IsExecuted, "즉사가 아니라 일반 사망");
    }

    [Test]
    public void Execute_NotImmune_Kills()
    {
        HealthSystem hs = NewHealth(100f);
        DamageMassage msg = new DamageMassage();
        msg.damage = 1f;
        hs.Execute(ref msg);
        Assert.IsTrue(hs.isDead);
        Assert.IsTrue(hs.IsExecuted);
    }

    [Test]
    public void IncomingDamageMultiplier_ScalesDamage()
    {
        HealthSystem hs = NewHealth(100f);
        hs.IncomingDamageMultiplier = 1.5f;
        DamageMassage msg = new DamageMassage();
        msg.damage = 10f;
        hs.ProcessDamage(ref msg);
        Assert.AreEqual(85f, hs.CurrHP, 1e-4f);
    }

    [Test]
    public void IncomingDamageMultiplier_Zero_BlocksDamage_NegativeClampedToZero()
    {
        HealthSystem hs = NewHealth(100f);
        hs.IncomingDamageMultiplier = -2f;
        Assert.AreEqual(0f, hs.IncomingDamageMultiplier);
        DamageMassage msg = new DamageMassage();
        msg.damage = 999f;
        hs.ProcessDamage(ref msg);
        Assert.AreEqual(100f, hs.CurrHP);
        Assert.IsFalse(hs.isDead);
    }

    [Test]
    public void IncomingDamageMultiplier_DefaultOne()
    {
        HealthSystem hs = NewHealth(100f);
        Assert.AreEqual(1f, hs.IncomingDamageMultiplier);
        Assert.IsFalse(hs.IsExecuteImmune);
    }
    #endregion

    #region PlayerStats.ApplyTimedSlow
    private PlayerStats NewStats()
    {
        go = new GameObject("BossStatsTest");
        return go.AddComponent<PlayerStats>();
    }

    [Test]
    public void ApplyTimedSlow_AppliesMoveSpeedMultiply()
    {
        PlayerStats s = NewStats();
        s.ApplyTimedSlow(0.4f, 3f);
        Assert.AreEqual(-0.4f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
        Assert.AreEqual(0.4f, s.TimedSlowRate, 1e-5f);
    }

    [Test]
    public void ApplyTimedSlow_Overlap_TakesMaxNotSum()
    {
        PlayerStats s = NewStats();
        s.ApplyTimedSlow(0.3f, 3f);
        s.ApplyTimedSlow(0.5f, 3f);
        Assert.AreEqual(-0.5f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
        s.ApplyTimedSlow(0.2f, 3f);
        Assert.AreEqual(-0.5f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f, "약한 것은 무시");
        Assert.AreEqual(0.5f, s.TimedSlowRate, 1e-5f);
    }

    [Test]
    public void ApplyTimedSlow_LongerDuration_ExtendsEndTime_ShorterDoesNot()
    {
        PlayerStats s = NewStats();
        s.ApplyTimedSlow(0.3f, 5f);
        float end = s.TimedSlowEndTime;
        s.ApplyTimedSlow(0.3f, 1f);
        Assert.AreEqual(end, s.TimedSlowEndTime, 1e-4f);
        s.ApplyTimedSlow(0.3f, 9f);
        Assert.Greater(s.TimedSlowEndTime, end);
    }

    [Test]
    public void ApplyTimedSlow_InvalidArgs_NoEffect()
    {
        PlayerStats s = NewStats();
        s.ApplyTimedSlow(0f, 3f);
        s.ApplyTimedSlow(0.5f, 0f);
        s.ApplyTimedSlow(0.5f, -1f);
        Assert.AreEqual(0f, s.GetMultiply(ValueType.MoveSpeed));
        Assert.AreEqual(0f, s.TimedSlowRate);
    }

    [Test]
    public void ApplyTimedSlow_RateOverOne_ClampedToOne()
    {
        PlayerStats s = NewStats();
        s.ApplyTimedSlow(5f, 3f);
        Assert.AreEqual(-1f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
    }

    [Test]
    public void ClearTimedSlow_RestoresMoveSpeed_AndIsIdempotent()
    {
        PlayerStats s = NewStats();
        s.AddModifier(ValueType.MoveSpeed, StatModifierMode.Multiply, 0.1f);
        s.ApplyTimedSlow(0.4f, 3f);
        s.ClearTimedSlow();
        s.ClearTimedSlow();
        Assert.AreEqual(0.1f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
        Assert.AreEqual(0f, s.TimedSlowRate);
    }

    [Test]
    public void ResetModifiers_ClearsTimedSlowState()
    {
        PlayerStats s = NewStats();
        s.ApplyTimedSlow(0.4f, 3f);
        s.ResetModifiers();
        Assert.AreEqual(0f, s.TimedSlowRate);
        Assert.AreEqual(0f, s.GetMultiply(ValueType.MoveSpeed), 1e-5f);
    }
    #endregion
}
