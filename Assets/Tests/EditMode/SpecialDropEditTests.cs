using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 특수 드랍 아이템 순수 로직 EditMode 테스트
/// - DropManager.RollSpecialItem (확률 경계 / 균등 선택 / count 0)
/// - InvincibleBuff.TickRemain / Activate / ProcessDamage (Awake·Update 없이 확인 가능한 부분)
/// - DAMAGE_PIPELINE.INVINCIBLE 단계 = InvincibleBuff 연결
/// </summary>
public class SpecialDropEditTests
{
    // ---------- RollSpecialItem ----------

    [Test]
    public void Roll_ChanceBoundary_0_001()
    {
        const float chance = 0.001f;
        Assert.AreEqual(0, DropManager.RollSpecialItem(chance, 2, 0f, 0f), "roll 0 은 나온다");
        Assert.AreEqual(0, DropManager.RollSpecialItem(chance, 2, 0.0009f, 0f), "chance 바로 아래는 나온다");
        Assert.AreEqual(-1, DropManager.RollSpecialItem(chance, 2, 0.001f, 0f), "roll == chance 는 안 나온다 (>=)");
        Assert.AreEqual(-1, DropManager.RollSpecialItem(chance, 2, 0.0011f, 0f));
        Assert.AreEqual(-1, DropManager.RollSpecialItem(chance, 2, 0.5f, 0f));
        Assert.AreEqual(-1, DropManager.RollSpecialItem(chance, 2, 0.999f, 0f));
    }

    [Test]
    public void Roll_ChanceBoundary_Statistics()
    {
        // roll 을 0..1 로 균등하게 훑으면 약 0.1% 만 나온다
        const int steps = 100000;
        int hits = 0;
        for (int i = 0; i < steps; ++i)
        {
            if (DropManager.RollSpecialItem(0.001f, 2, i / (float)steps, 0f) >= 0)
            {
                ++hits;
            }
        }
        Assert.That(hits, Is.InRange(99, 101));
    }

    [Test]
    public void Roll_ZeroChance_NeverDrops()
    {
        Assert.AreEqual(-1, DropManager.RollSpecialItem(0f, 2, 0f, 0.5f));
        Assert.AreEqual(-1, DropManager.RollSpecialItem(-0.5f, 2, 0f, 0.5f));
    }

    [Test]
    public void Roll_FullChance_AlwaysDrops()
    {
        Assert.AreEqual(0, DropManager.RollSpecialItem(1f, 2, 0.999999f, 0f));
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 2, 1f, 0.9f));
        Assert.AreEqual(0, DropManager.RollSpecialItem(1.5f, 2, 0.9f, 0f));
    }

    [Test]
    public void Roll_CountZeroOrNegative_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, DropManager.RollSpecialItem(1f, 0, 0f, 0.5f));
        Assert.AreEqual(-1, DropManager.RollSpecialItem(1f, -3, 0f, 0.5f));
    }

    [Test]
    public void Roll_Pick_SplitsEvenly()
    {
        Assert.AreEqual(0, DropManager.RollSpecialItem(1f, 2, 0f, 0f));
        Assert.AreEqual(0, DropManager.RollSpecialItem(1f, 2, 0f, 0.49f));
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 2, 0f, 0.5f));
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 2, 0f, 0.99f));

        Assert.AreEqual(0, DropManager.RollSpecialItem(1f, 3, 0f, 0.33f));
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 3, 0f, 0.34f));
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 3, 0f, 0.66f));
        Assert.AreEqual(2, DropManager.RollSpecialItem(1f, 3, 0f, 0.67f));
    }

    [Test]
    public void Roll_Pick_OutOfRange_IsClamped()
    {
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 2, 0f, 1f), "pick 1.0 은 마지막 인덱스 (범위 밖 인덱스 금지)");
        Assert.AreEqual(1, DropManager.RollSpecialItem(1f, 2, 0f, 7f));
        Assert.AreEqual(0, DropManager.RollSpecialItem(1f, 2, 0f, -3f));
    }

    [Test]
    public void Roll_SingleItem_AlwaysIndexZero()
    {
        for (int i = 0; i <= 10; ++i)
        {
            Assert.AreEqual(0, DropManager.RollSpecialItem(1f, 1, 0f, i / 10f));
        }
    }

    [Test]
    public void Roll_Pick_IsUniform()
    {
        const int steps = 3000;
        int[] counts = new int[3];
        for (int i = 0; i < steps; ++i)
        {
            int idx = DropManager.RollSpecialItem(1f, 3, 0f, i / (float)steps);
            Assert.That(idx, Is.InRange(0, 2));
            ++counts[idx];
        }
        foreach (int c in counts)
        {
            Assert.That(c, Is.InRange(steps / 3 - 3, steps / 3 + 3));
        }
    }

    // ---------- InvincibleBuff (순수 부분) ----------

    [TestCase(5f, 1f, 4f)]
    [TestCase(1f, 5f, 0f)]
    [TestCase(1f, 1f, 0f)]
    [TestCase(0f, 1f, 0f)]
    [TestCase(5f, 0f, 5f)]
    [TestCase(5f, -2f, 5f)]
    public void TickRemain_Cases(float _remain, float _dt, float _expected)
    {
        Assert.AreEqual(_expected, InvincibleBuff.TickRemain(_remain, _dt), 1e-5f);
    }

    private static InvincibleBuff MakeBuff(out GameObject _go)
    {
        _go = new GameObject("InvincibleBuffEdit");
        return _go.AddComponent<InvincibleBuff>();
    }

    [Test]
    public void ProcessDamage_InactiveBuff_DoesNotChangeMessage()
    {
        InvincibleBuff buff = MakeBuff(out GameObject go);
        try
        {
            DamageMassage msg = new DamageMassage { damage = 10f, knockbackForce = 20f };
            buff.ProcessDamage(ref msg);
            Assert.AreEqual(10f, msg.damage);
            Assert.AreEqual(20f, msg.knockbackForce);
            Assert.IsFalse(buff.IsActive);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ProcessDamage_ActiveBuff_ZeroesDamageAndKnockback()
    {
        InvincibleBuff buff = MakeBuff(out GameObject go);
        try
        {
            buff.Activate(5f);
            Assert.IsTrue(buff.IsActive);
            Assert.AreEqual(5f, buff.RemainTime, 1e-5f);

            DamageMassage msg = new DamageMassage { damage = 10f, knockbackForce = 20f };
            buff.ProcessDamage(ref msg);
            Assert.AreEqual(0f, msg.damage);
            Assert.AreEqual(0f, msg.knockbackForce);

            buff.Deactivate();
            Assert.IsFalse(buff.IsActive);
            msg = new DamageMassage { damage = 10f, knockbackForce = 20f };
            buff.ProcessDamage(ref msg);
            Assert.AreEqual(10f, msg.damage, "Deactivate 후엔 정상 피격");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    public void Activate_NonPositiveDuration_IsIgnored(float _duration)
    {
        InvincibleBuff buff = MakeBuff(out GameObject go);
        try
        {
            buff.Activate(_duration);
            Assert.IsFalse(buff.IsActive);
            Assert.AreEqual(0f, buff.RemainTime);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Activate_Twice_RefreshesNotAccumulates()
    {
        InvincibleBuff buff = MakeBuff(out GameObject go);
        try
        {
            buff.Activate(5f);
            buff.Activate(5f);
            Assert.AreEqual(5f, buff.RemainTime, 1e-5f, "누적(10) 이 아니라 5 로 갱신");

            buff.Activate(2f);
            Assert.AreEqual(2f, buff.RemainTime, 1e-5f, "갱신은 새 값으로 덮어쓴다");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // ---------- 파이프라인 연결 ----------

    [Test]
    public void DamagePipeline_InvincibleStage_IsFirstAndMapsToInvincibleBuff()
    {
        Assert.AreEqual(0, (int)DAMAGE_PIPELINE.INVINCIBLE, "무적은 첫 단계(HEALTH 이전)");
        Assert.Less((int)DAMAGE_PIPELINE.INVINCIBLE, (int)DAMAGE_PIPELINE.HEALTH);
        Assert.Less((int)DAMAGE_PIPELINE.INVINCIBLE, (int)DAMAGE_PIPELINE.KNOCKBACK);

        var field = typeof(DAMAGE_PIPELINE).GetField(DAMAGE_PIPELINE.INVINCIBLE.ToString());
        PipelineAttribute attr = (PipelineAttribute)Attribute.GetCustomAttribute(field, typeof(PipelineAttribute));
        Assert.NotNull(attr);
        Assert.AreEqual(typeof(InvincibleBuff), attr.targetType);
        Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(InvincibleBuff)));
    }
}
