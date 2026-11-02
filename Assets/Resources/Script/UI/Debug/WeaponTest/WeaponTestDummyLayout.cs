using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 테스트 씬의 더미 배치 프리셋 (플레이어 위치 기준 오프셋 목록)
/// - WeaponTestController 의 인스펙터에서 이름 / 오프셋을 직접 고칠 수 있다
/// - CreateDefaults 는 컴포넌트를 처음 붙일 때 쓰는 기본 4종 (단일 / 포위 / 군집 / 일렬·산개)
/// </summary>
[Serializable]
public class WeaponTestDummyLayout
{
    [Tooltip("버튼에 표시할 이름")]
    public string displayName;

    [Tooltip("플레이어 위치 기준 더미 오프셋 (월드 단위)")]
    public Vector2[] offsets;

    public int Count => null != offsets ? offsets.Length : 0;

    public WeaponTestDummyLayout()
    {
        displayName = string.Empty;
        offsets = new Vector2[0];
    }

    public WeaponTestDummyLayout(string _name, Vector2[] _offsets)
    {
        displayName = _name;
        offsets = null != _offsets ? _offsets : new Vector2[0];
    }

    #region Default
    /// <summary>
    /// 기본 프리셋 4종 (컴포넌트를 처음 붙일 때의 초기값이며, 이후에는 인스펙터 값이 우선)
    /// </summary>
    public static List<WeaponTestDummyLayout> CreateDefaults()
    {
        List<WeaponTestDummyLayout> result = new List<WeaponTestDummyLayout>();

        // 단일 타겟 : 정면 1개
        result.Add(new WeaponTestDummyLayout("단일", new Vector2[] { new Vector2(3f, 0f) }));

        // 원형 포위 : 플레이어 주위 8개
        result.Add(new WeaponTestDummyLayout("포위", Ring(Vector2.zero, 2.5f, 8)));

        // 밀집 군집 : 정면 한 지점에 촘촘히 10개
        result.Add(new WeaponTestDummyLayout("군집", Cluster(new Vector2(5f, 0f), 0.35f, 10)));

        // 일렬 / 원거리 산개 : 가로 일렬 5개 + 사거리 끝까지 흩어진 6개
        List<Vector2> lineAndFar = new List<Vector2>();
        lineAndFar.AddRange(Line(new Vector2(2f, 0f), new Vector2(2f, 0f), 5));
        lineAndFar.Add(new Vector2(3f, 4f));
        lineAndFar.Add(new Vector2(7f, -4f));
        lineAndFar.Add(new Vector2(11f, 3f));
        lineAndFar.Add(new Vector2(-5f, 3f));
        lineAndFar.Add(new Vector2(-9f, -2f));
        lineAndFar.Add(new Vector2(0f, -7f));
        result.Add(new WeaponTestDummyLayout("일렬/산개", lineAndFar.ToArray()));

        return result;
    }

    /// <summary>
    /// _center 를 중심으로 반지름 _radius 원 위에 _count 개를 같은 간격으로 놓는다
    /// </summary>
    public static Vector2[] Ring(Vector2 _center, float _radius, int _count)
    {
        Vector2[] result = new Vector2[Mathf.Max(0, _count)];
        for (int i = 0; i < result.Length; i++)
        {
            float angle = (Mathf.PI * 2f) * i / result.Length;
            result[i] = _center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _radius;
        }
        return result;
    }

    /// <summary>
    /// _center 에 _count 개를 해바라기 씨앗 배치로 촘촘히 놓는다 (이웃 간격 약 _spacing)
    /// </summary>
    public static Vector2[] Cluster(Vector2 _center, float _spacing, int _count)
    {
        const float GoldenAngle = 137.508f * Mathf.Deg2Rad;

        Vector2[] result = new Vector2[Mathf.Max(0, _count)];
        for (int i = 0; i < result.Length; i++)
        {
            float radius = _spacing * Mathf.Sqrt(i);
            float angle = i * GoldenAngle;
            result[i] = _center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
        return result;
    }

    /// <summary>
    /// _start 부터 _step 간격으로 _count 개를 한 줄로 놓는다
    /// </summary>
    public static Vector2[] Line(Vector2 _start, Vector2 _step, int _count)
    {
        Vector2[] result = new Vector2[Mathf.Max(0, _count)];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = _start + _step * i;
        }
        return result;
    }
    #endregion
}
