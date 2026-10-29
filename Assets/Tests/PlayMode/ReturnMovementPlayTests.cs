using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// ReturnMovement PlayMode 테스트
/// - 되돌아서는 순간 방향이 고정된다 (이후 주인이 움직여도 바뀌지 않음)
/// - 주인 위치에 도달해도 비활성화되지 않는다
/// </summary>
public class ReturnMovementPlayTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (null != o)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    private Transform MakeOwner(Vector3 _pos)
    {
        GameObject go = new GameObject("Owner");
        go.transform.position = _pos;
        created.Add(go);
        return go.transform;
    }

    private ReturnMovement MakeBoomerang(Transform _owner, Vector3 _targetPos)
    {
        GameObject targetGo = new GameObject("Target");
        targetGo.transform.position = _targetPos;
        created.Add(targetGo);

        GameObject go = new GameObject("Boomerang");
        created.Add(go);
        ReturnMovement rm = go.AddComponent<ReturnMovement>();
        rm.speed = 8f;

        BlackBoard b = new BlackBoard();
        b.SetTransform(DATA_TYPE.startPosition, _owner);
        b.SetTransform(DATA_TYPE.moveTarget, targetGo.transform);
        b.SetFloat(DATA_TYPE.moveSpeedRate, 1f);
        rm.Initialize(b);
        return rm;
    }

    private IEnumerator WaitUntilReturning(ReturnMovement _rm, float _timeout)
    {
        float end = Time.time + _timeout;
        while (false == _rm.IsReturning && Time.time < end)
        {
            yield return null;
        }
        Assert.IsTrue(_rm.IsReturning, "시간 안에 되돌아서지 않음");
    }

    [UnityTest]
    public IEnumerator Direction_FixedAtTurnaround_OwnerMovingAfterwardDoesNotSteer()
    {
        Transform owner = MakeOwner(Vector3.zero);
        ReturnMovement rm = MakeBoomerang(owner, new Vector3(5f, 0f, 0f));

        yield return WaitUntilReturning(rm, 10f);
        Assert.Greater(rm.transform.position.x, 4f, "목표 근처까지 날아간 뒤에 되돌아서야 한다");

        // 되돌아선 뒤 주인이 위로 멀리 이동
        owner.position = new Vector3(rm.transform.position.x, 20f, 0f);

        Vector3 turnPos = rm.transform.position;
        float prevX = turnPos.x;
        for (int i = 0; i < 30; ++i)
        {
            yield return null;
            Assert.LessOrEqual(rm.transform.position.x, prevX + 0.0001f, "원래 출발 방향(-x)으로 계속 날아가야 한다");
            prevX = rm.transform.position.x;
        }

        Assert.AreEqual(turnPos.y, rm.transform.position.y, 0.001f, "주인이 위로 이동해도 y 가 변하면 안 된다 (방향 고정)");
        Assert.Less(rm.transform.position.x, turnPos.x, "실제로 이동은 해야 한다");
    }

    [UnityTest]
    public IEnumerator Direction_FixedAtTurnaround_OwnerMovesFarSideways()
    {
        Transform owner = MakeOwner(new Vector3(0f, 0f, 0f));
        ReturnMovement rm = MakeBoomerang(owner, new Vector3(0f, 5f, 0f));

        yield return WaitUntilReturning(rm, 10f);

        // 주인을 오른쪽으로 크게 이동. 방향이 고정이면 x 는 변하지 않는다
        float x0 = rm.transform.position.x;
        owner.position = new Vector3(30f, rm.transform.position.y, 0f);

        for (int i = 0; i < 30; ++i)
        {
            yield return null;
        }
        Assert.AreEqual(x0, rm.transform.position.x, 0.001f);
    }

    [UnityTest]
    public IEnumerator ReachingOwner_DoesNotDeactivate_FliesPast()
    {
        Transform owner = MakeOwner(Vector3.zero);
        ReturnMovement rm = MakeBoomerang(owner, new Vector3(5f, 0f, 0f));

        yield return WaitUntilReturning(rm, 10f);

        // 주인 위치를 지나 반대편으로 갈 때까지 대기
        float end = Time.time + 15f;
        bool passed = false;
        while (Time.time < end)
        {
            yield return null;
            Assert.IsTrue(rm.gameObject.activeSelf, "주인에게 닿아도 비활성화되면 안 된다");
            if (rm.transform.position.x < -2f)
            {
                passed = true;
                break;
            }
        }
        Assert.IsTrue(passed, "주인을 지나쳐 계속 날아가야 한다");

        for (int i = 0; i < 10; ++i)
        {
            yield return null;
        }
        Assert.IsTrue(rm.gameObject.activeSelf);
        Assert.IsTrue(rm.IsReturning);
    }

    [UnityTest]
    public IEnumerator Initialize_ReactivatedBoomerang_ResetsReturningState()
    {
        Transform owner = MakeOwner(Vector3.zero);
        ReturnMovement rm = MakeBoomerang(owner, new Vector3(5f, 0f, 0f));
        yield return WaitUntilReturning(rm, 10f);

        // 풀 재사용: 비활성화 후 다시 켜고 Initialize
        rm.gameObject.SetActive(false);
        rm.gameObject.SetActive(true);
        Assert.IsFalse(rm.IsReturning);

        BlackBoard b = new BlackBoard();
        b.SetTransform(DATA_TYPE.startPosition, owner);
        b.SetTransform(DATA_TYPE.moveTarget, owner);
        rm.Initialize(b);
        Assert.IsFalse(rm.IsReturning);
        Assert.AreEqual(0f, rm.transform.position.x, 0.001f);
    }
}
