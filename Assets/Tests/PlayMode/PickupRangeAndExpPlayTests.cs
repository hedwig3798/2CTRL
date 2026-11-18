using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 아이템 획득 범위 패시브 · EXP 획득 배율 PlayMode 테스트
/// - ItemTrigger 원 반경이 PlayerStats PickupRange (버프 / 디버프 / 하한) 를 따라간다
/// - 반경이 커지면 원래 범위 밖의 보석도 끌어당긴다 (물리 트리거)
/// - ItemCollector.Absorb 가 ExpSystem.ExpGainMultiplier 를 곱해 EXP 와 OnExpGained 에 반영한다
/// </summary>
public class PickupRangeAndExpPlayTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; --i)
        {
            if (null != created[i])
            {
                Object.Destroy(created[i]);
            }
        }
        created.Clear();
    }

    private T Track<T>(T _o) where T : Object
    {
        created.Add(_o);
        return _o;
    }

    private static void SetField(object _t, string _f, object _v)
    {
        FieldInfo fi = _t.GetType().GetField(_f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(fi, "field not found " + _f);
        fi.SetValue(_t, _v);
    }

    private const float BASE_RADIUS = 1.43f;

    private (PlayerStats stats, ItemTrigger trigger, CircleCollider2D circle) MakePlayer(Vector2 _position)
    {
        GameObject root = Track(new GameObject("Player"));
        root.SetActive(false);
        root.transform.position = _position;
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        // 실제 Player 프리팹과 같이 Dynamic + 중력 0 (Kinematic 은 정적 콜라이더(보석) 와 트리거가 생기지 않는다)
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        PlayerStats stats = root.AddComponent<PlayerStats>();
        SetField(stats, "side", PlayerSide.Left);

        GameObject child = new GameObject("ItemTrigger");
        child.transform.SetParent(root.transform, false);
        // 실제 게임과 같은 레이어 (Default 끼리는 충돌 행렬에서 꺼져 있다)
        child.layer = LayerMask.NameToLayer("LPlayer");
        CircleCollider2D circle = child.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = BASE_RADIUS;
        ItemTrigger trigger = child.AddComponent<ItemTrigger>();
        SetField(trigger, "owner", root);

        root.SetActive(true);
        return (stats, trigger, circle);
    }

    [UnityTest]
    public IEnumerator Radius_FollowsPickupRangeModifiers()
    {
        var (stats, trigger, circle) = MakePlayer(new Vector2(500f, 500f));
        yield return null;

        Assert.AreEqual(BASE_RADIUS, circle.radius, 1e-4f, "버프 없으면 프리팹 반경");

        stats.AddModifier(ValueType.PickupRange, StatModifierMode.Multiply, 0.1f);
        Assert.AreEqual(BASE_RADIUS * 1.1f, circle.radius, 1e-4f, "+10%");

        stats.AddModifier(ValueType.PickupRange, StatModifierMode.Multiply, -0.06f);
        Assert.AreEqual(BASE_RADIUS * 1.04f, circle.radius, 1e-4f, "+10% -6%");

        for (int i = 0; i < 20; ++i)
        {
            stats.AddModifier(ValueType.PickupRange, StatModifierMode.Multiply, -0.06f);
        }
        Assert.AreEqual(BASE_RADIUS * stats.MinMultiplier, circle.radius, 1e-4f, "디버프 하한");
        Assert.AreEqual(BASE_RADIUS, trigger.BaseRadius, 1e-4f, "base 는 바뀌지 않는다");

        stats.ResetModifiers();
        Assert.AreEqual(BASE_RADIUS, circle.radius, 1e-4f, "초기화");
    }

    [UnityTest]
    public IEnumerator Buff_PullsGemOutsideBaseRadius()
    {
        Vector2 origin = new Vector2(600f, 600f);
        var (stats, _, _) = MakePlayer(origin);

        // 기본 반경(1.43) 밖, +10%(1.573) 안 (보석 반경 0.05)
        GameObject gem = Track(new GameObject("Gem"));
        gem.tag = "item";
        gem.layer = LayerMask.NameToLayer("LItem");
        gem.transform.position = origin + new Vector2(1.52f, 0f);
        gem.AddComponent<CircleCollider2D>().radius = 0.05f;
        SlingshotMovement movement = gem.AddComponent<SlingshotMovement>();

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsNull(movement.target, "기본 반경 밖이면 끌어당기지 않는다");

        stats.AddModifier(ValueType.PickupRange, StatModifierMode.Multiply, 0.1f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(stats.transform, movement.target, "버프로 커진 범위에 들어오면 끌어당긴다");
        Assert.IsFalse(movement.isStop);
    }

    [UnityTest]
    public IEnumerator Absorb_AppliesExpGainMultiplier()
    {
        LevelTable table = Track(ScriptableObject.CreateInstance<LevelTable>());
        table.maxLevel = 99;
        table.baseExp = 1000f;

        GameObject expGo = Track(new GameObject("ExpSystem"));
        ExpSystem expSystem = expGo.AddComponent<ExpSystem>();
        expSystem.levelTable = table;
        yield return null;

        Assert.AreEqual(1.5f, expSystem.ExpGainMultiplier, 1e-4f, "기본 EXP 획득 배율");

        GameObject gemGo = Track(new GameObject("Gem"));
        ExpItem item = gemGo.AddComponent<ExpItem>();
        SetField(item, "baseExp", 2f);
        BlackBoard data = new BlackBoard();
        data.SetFloat(DATA_TYPE.expRate, 3f);
        item.Initialize(data);

        float before = expSystem.currExp;
        float gained = ItemCollector.Absorb(item, null);
        Assert.AreEqual(2f * 3f * 1.5f, gained, 1e-4f);
        Assert.AreEqual(before + gained, expSystem.currExp, 1e-4f);
        Assert.AreEqual(0f, ItemCollector.Absorb(item, null), 1e-4f, "두 번 먹지 않는다");
    }
}
