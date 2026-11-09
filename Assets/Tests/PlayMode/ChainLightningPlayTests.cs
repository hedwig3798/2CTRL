using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 체인 번개 즉시 타격 / 선 페이드 검증. 프리팹 없이 코드로 구성
/// </summary>
public class ChainLightningPlayTests
{
    private const float HP = 1000f;
    private const float DAMAGE = 10f;

    [TearDown]
    public void TearDown()
    {
        WeaponTestKit.Cleanup();
    }

    private static Weapon NewChain(int _chainCount, float _chainRange = 3f, ChainLightningProjectile _template = null)
    {
        ChainLightningProjectile template = null != _template ? _template : WeaponTestKit.Template<ChainLightningProjectile>();
        Weapon w = WeaponTestKit.Make(WEAPON_TYPE.Repeat, template, WeaponTestKit.Marker(Vector2.zero), DAMAGE, 0f, 5f);
        w.chainCount = _chainCount;
        w.chainRange = _chainRange;
        w.hitCount = 99;
        return w;
    }

    private static GameObject Target(float _x)
    {
        return MonsterTestFactory.CreateTarget(new Vector2(_x, 0f), HP);
    }

    private static float Lost(GameObject _go)
    {
        return HP - WeaponTestKit.Hp(_go);
    }

    [Test]
    public void 발사한_프레임에_전이_대상_전부를_타격한다()
    {
        GameObject[] targets = { Target(2f), Target(4f), Target(6f), Target(8f) };
        Weapon w = NewChain(3);

        int frame = Time.frameCount;
        w.Fire();

        Assert.AreEqual(frame, Time.frameCount);
        foreach (GameObject t in targets)
        {
            Assert.AreEqual(DAMAGE, Lost(t), 0.01f);
        }
        Assert.AreEqual(4, WeaponTestKit.FirstActive<ChainLightningProjectile>().StruckCount);
    }

    [Test]
    public void 전이_횟수를_넘는_적은_맞지_않는다()
    {
        GameObject a = Target(2f);
        GameObject b = Target(4f);
        GameObject c = Target(6f);
        Weapon w = NewChain(1);

        w.Fire();

        Assert.AreEqual(DAMAGE, Lost(a), 0.01f);
        Assert.AreEqual(DAMAGE, Lost(b), 0.01f);
        Assert.AreEqual(0f, Lost(c), 0.01f);
    }

    [Test]
    public void 전이_범위_밖의_적으로는_전이하지_않는다()
    {
        GameObject near = Target(2f);
        GameObject far = Target(4.9f);
        Weapon w = NewChain(3, 2f);

        w.Fire();

        Assert.AreEqual(DAMAGE, Lost(near), 0.01f);
        Assert.AreEqual(0f, Lost(far), 0.01f);
    }

    [Test]
    public void 전이_횟수가_남아도_같은_적을_다시_맞히지_않는다()
    {
        GameObject a = Target(2f);
        GameObject b = Target(3f);
        Weapon w = NewChain(5);

        w.Fire();

        Assert.AreEqual(DAMAGE, Lost(a), 0.01f);
        Assert.AreEqual(DAMAGE, Lost(b), 0.01f);
    }

    [Test]
    public void 사거리_안에_적이_없으면_발사하지_않는다()
    {
        Target(20f);
        Weapon w = NewChain(3);

        w.Fire();

        Assert.AreEqual(0, WeaponTestKit.CountActive<ChainLightningProjectile>());
    }

    [Test]
    public void 증폭_플래그가_있으면_전이마다_데미지가_누적된다()
    {
        GameObject a = Target(2f);
        GameObject b = Target(4f);
        GameObject c = Target(6f);
        Weapon w = NewChain(2);
        w.AddModifier(WeaponParam.FlagChainAmplify, StatModifierMode.Add, 1f);

        w.Fire();

        Assert.AreEqual(DAMAGE, Lost(a), 0.01f);
        Assert.AreEqual(DAMAGE * 1.15f, Lost(b), 0.01f);
        Assert.AreEqual(DAMAGE * 1.3f, Lost(c), 0.01f);
    }

    [UnityTest]
    public IEnumerator 선은_유지_뒤_서서히_사라지고_풀로_돌아간다()
    {
        Target(2f);
        Target(4f);

        ChainLightningProjectile template = WeaponTestKit.Template<ChainLightningProjectile>();
        GameObject lineGo = new GameObject("line");
        lineGo.transform.SetParent(template.transform, false);
        lineGo.AddComponent<LineRenderer>();

        Weapon w = NewChain(1, 3f, template);
        w.Fire();

        ChainLightningProjectile bolt = WeaponTestKit.FirstActive<ChainLightningProjectile>();
        Assert.IsNotNull(bolt);
        LineRenderer line = bolt.GetComponentInChildren<LineRenderer>();
        Assert.Greater(line.positionCount, 0, "타격 직후 선이 그려져야 함");
        Assert.AreEqual(1f, line.colorGradient.Evaluate(0.5f).a, 0.001f, "유지 구간은 불투명");

        // holdDuration 0.1 + fadeDuration 0.3 의 중간
        yield return MonsterTestFactory.Wait(0.22f);
        Assert.IsTrue(bolt.gameObject.activeSelf);
        float mid = line.colorGradient.Evaluate(0.5f).a;
        Assert.Less(mid, 0.9f, "페이드 중에는 알파가 줄어야 함");
        Assert.Greater(mid, 0.1f, "페이드 중간에 이미 사라지면 안 됨");

        yield return MonsterTestFactory.WaitUntil(() => false == bolt.gameObject.activeSelf, 1f);
        Assert.IsFalse(bolt.gameObject.activeSelf, "페이드가 끝나면 풀로 돌아가야 함");
        Assert.AreEqual(0, line.positionCount);

        // 재사용 시 알파가 복구된다
        w.Fire();
        Assert.AreEqual(1f, line.colorGradient.Evaluate(0.5f).a, 0.001f);
    }
}
