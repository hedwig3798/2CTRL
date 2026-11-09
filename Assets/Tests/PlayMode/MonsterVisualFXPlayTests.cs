using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MonsterVisualFX: 다운 상태에서 바로 사망해도 디졸브 동안 다운 색을 유지한다
/// </summary>
public class MonsterVisualFXPlayTests
{
    [TearDown]
    public void TearDown()
    {
        WeaponTestKit.Cleanup();
    }

    private static MonsterTestFactory.Rig NewRig(ReviveKeyword _revive)
    {
        MonsterTestFactory.Rig r = MonsterTestFactory.Create(MonsterTestFactory.NewData(0f, _revive), Vector2.zero, 100f, true, false);
        r.sprite.color = Color.white;
        r.root.AddComponent<MonsterVisualFX>();
        r.root.SetActive(true);
        MonsterTestFactory.Init(r, WeaponTestKit.Marker(new Vector2(500f, 500f)));
        return r;
    }

    [UnityTest]
    public IEnumerator Downed에서_Dead가_돼도_다운_색_회색_반투명을_유지한다()
    {
        ReviveKeyword rv = MonsterTestFactory.So<ReviveKeyword>();
        rv.reviveCount = 1;
        rv.reviveDelay = 5f;
        rv.hpRatio = 0.5f;
        MonsterTestFactory.Rig r = NewRig(rv);
        yield return null;
        Assert.AreEqual(1f, r.sprite.color.r, 0.01f, "평상시 원색");

        r.health.Kill();
        yield return null;
        yield return null;
        Assert.AreEqual(MonsterStateId.Downed, r.brain.CurrentState);
        Assert.AreEqual(0.5f, r.sprite.color.r, 0.01f);
        Assert.AreEqual(0.5f, r.sprite.color.a, 0.01f);

        DamageMassage msg = new DamageMassage();
        r.health.Execute(ref msg);
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        yield return null;
        yield return null;
        Assert.AreEqual(0.5f, r.sprite.color.r, 0.01f, "Dead 가 돼도 다운 색 유지");
        Assert.AreEqual(0.5f, r.sprite.color.a, 0.01f);
    }

    [UnityTest]
    public IEnumerator Downed_없이_바로_Dead면_원색이다()
    {
        ReviveKeyword rv = MonsterTestFactory.So<ReviveKeyword>();
        rv.reviveCount = 0;
        MonsterTestFactory.Rig r = NewRig(rv);
        yield return null;
        r.health.Kill();
        yield return null;
        yield return null;
        Assert.AreEqual(MonsterStateId.Dead, r.brain.CurrentState);
        Assert.AreEqual(1f, r.sprite.color.r, 0.01f);
    }
}
