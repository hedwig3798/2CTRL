using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// WEAPON_TYPE.Capped / Wave 발사 방식 검증
/// </summary>
public class WeaponFireModePlayTests
{
    [SetUp]
    public void SetUp()
    {
        FireModeTestProjectile.ResetStatics();
    }

    [TearDown]
    public void TearDown()
    {
        WeaponTestKit.Cleanup();
    }

    private static Weapon NewWeapon(WEAPON_TYPE _type, float _attackSpeed, int _fireCount)
    {
        FireModeTestProjectile tpl = WeaponTestKit.Template<FireModeTestProjectile>();
        return WeaponTestKit.Make(_type, tpl, WeaponTestKit.Marker(Vector2.zero), 1f, _attackSpeed, 5f, _fireCount);
    }

    private static FireModeTestProjectile[] Alive()
    {
        return Object.FindObjectsByType<FireModeTestProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    #region Capped
    [UnityTest]
    public IEnumerator Capped_FireCount만큼만_발사하고_더_발사하지_않는다()
    {
        NewWeapon(WEAPON_TYPE.Capped, 4f, 2);

        yield return MonsterTestFactory.Wait(1.5f);

        Assert.AreEqual(2, FireModeTestProjectile.LaunchFrames.Count, "상한 도달 후 추가 발사");
        Assert.AreEqual(2, Alive().Length);
    }

    [UnityTest]
    public IEnumerator Capped_한_번에_하나씩_발사한다()
    {
        NewWeapon(WEAPON_TYPE.Capped, 2f, 3);

        yield return MonsterTestFactory.Wait(0.3f);
        Assert.AreEqual(0, FireModeTestProjectile.LaunchFrames.Count, "쿨타임 전에 발사");

        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 1, 2f);
        yield return null;
        Assert.AreEqual(1, FireModeTestProjectile.LaunchFrames.Count, "첫 쿨타임에 1개만 발사해야 함");
    }

    [UnityTest]
    public IEnumerator Capped_상한일_때_타이머가_멈춰_하나가_사라진_뒤_쿨타임을_다시_기다린다()
    {
        NewWeapon(WEAPON_TYPE.Capped, 2f, 2);

        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 2, 3f);
        Assert.AreEqual(2, FireModeTestProjectile.LaunchFrames.Count);

        // 쿨타임(0.5초)의 3배 동안 막혀 있어도 타이머가 쌓이면 안 된다
        yield return MonsterTestFactory.Wait(1.5f);
        Assert.AreEqual(2, FireModeTestProjectile.LaunchFrames.Count);

        Alive()[0].gameObject.SetActive(false);
        yield return MonsterTestFactory.Wait(0.25f);
        Assert.AreEqual(2, FireModeTestProjectile.LaunchFrames.Count, "타이머가 멈춰 있었다면 바로 발사하지 않는다");

        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 3, 2f);
        Assert.AreEqual(3, FireModeTestProjectile.LaunchFrames.Count);
        Assert.AreEqual(2, Alive().Length);
    }

    [UnityTest]
    public IEnumerator Capped_Launch가_false면_살아있는_수에_넣지_않고_허용되면_바로_발사한다()
    {
        FireModeTestProjectile.Allow = false;
        NewWeapon(WEAPON_TYPE.Capped, 4f, 1);

        yield return MonsterTestFactory.Wait(0.8f);
        Assert.Greater(FireModeTestProjectile.Attempts, 1, "실패 후 다음 프레임에 재시도해야 함");
        Assert.AreEqual(0, FireModeTestProjectile.LaunchFrames.Count);
        Assert.AreEqual(0, Alive().Length);

        FireModeTestProjectile.Allow = true;
        yield return MonsterTestFactory.Wait(0.1f);
        Assert.AreEqual(1, FireModeTestProjectile.LaunchFrames.Count, "허용되면 대기 없이 발사");
    }

    [UnityTest]
    public IEnumerator Capped_fireCount가_0이면_발사하지_않는다()
    {
        NewWeapon(WEAPON_TYPE.Capped, 10f, 0);

        yield return MonsterTestFactory.Wait(0.5f);
        Assert.AreEqual(0, FireModeTestProjectile.Attempts);
    }
    #endregion

    #region Wave
    [UnityTest]
    public IEnumerator Wave_쿨타임_후_fireCount개를_같은_프레임에_발사한다()
    {
        NewWeapon(WEAPON_TYPE.Wave, 2f, 3);

        yield return MonsterTestFactory.Wait(0.2f);
        Assert.AreEqual(0, FireModeTestProjectile.LaunchFrames.Count, "쿨타임 전에 발사");

        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count > 0, 2f);
        Assert.AreEqual(3, FireModeTestProjectile.LaunchFrames.Count);
        Assert.AreEqual(1, FireModeTestProjectile.LaunchFrames.Distinct().Count(), "동시 발사여야 함");
    }

    [UnityTest]
    public IEnumerator Wave_하나라도_살아있으면_다음_파도를_발사하지_않고_전부_사라진_뒤_쿨타임_후_발사한다()
    {
        NewWeapon(WEAPON_TYPE.Wave, 2f, 3);

        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 3, 3f);
        Assert.AreEqual(3, FireModeTestProjectile.LaunchFrames.Count);

        yield return MonsterTestFactory.Wait(1.2f);
        Assert.AreEqual(3, FireModeTestProjectile.LaunchFrames.Count, "살아 있는 동안 추가 발사");

        // 일부만 사라져도 다음 파도는 없다
        Alive()[0].gameObject.SetActive(false);
        yield return MonsterTestFactory.Wait(1.2f);
        Assert.AreEqual(3, FireModeTestProjectile.LaunchFrames.Count, "1개 남았는데 추가 발사");

        foreach (FireModeTestProjectile p in Alive())
        {
            p.gameObject.SetActive(false);
        }
        yield return MonsterTestFactory.Wait(0.2f);
        Assert.AreEqual(3, FireModeTestProjectile.LaunchFrames.Count, "전부 사라진 직후에는 쿨타임을 기다려야 함");

        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 6, 2f);
        Assert.AreEqual(6, FireModeTestProjectile.LaunchFrames.Count);
        Assert.AreEqual(3, Alive().Length);
    }

    [UnityTest]
    public IEnumerator Wave_Launch가_false면_알아있는_발사체_없이_다시_쿨타임을_기다린다()
    {
        FireModeTestProjectile.Allow = false;
        NewWeapon(WEAPON_TYPE.Wave, 4f, 2);

        yield return MonsterTestFactory.Wait(1.0f);
        Assert.Greater(FireModeTestProjectile.Attempts, 0);
        Assert.AreEqual(0, Alive().Length);
        Assert.AreEqual(0, FireModeTestProjectile.LaunchFrames.Count);

        FireModeTestProjectile.Allow = true;
        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 2, 2f);
        Assert.AreEqual(2, FireModeTestProjectile.LaunchFrames.Count);
    }
    #endregion

    [UnityTest]
    public IEnumerator 무기가_꺼지면_Capped_발사체도_풀로_돌아간다()
    {
        Weapon w = NewWeapon(WEAPON_TYPE.Capped, 10f, 2);
        yield return MonsterTestFactory.WaitUntil(() => FireModeTestProjectile.LaunchFrames.Count >= 2, 2f);
        Assert.AreEqual(2, Alive().Length);

        w.gameObject.SetActive(false);
        yield return null;
        Assert.AreEqual(0, Alive().Length);
    }
}
