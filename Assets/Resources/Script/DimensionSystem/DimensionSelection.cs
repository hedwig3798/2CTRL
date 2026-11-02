using UnityEngine;

/// <summary>
/// 선택 씬에서 고른 차원 (씬을 넘어 유지)
/// - 비어 있으면(GameScene 을 직접 실행하면) DimensionApplier 의 기본 차원을 쓴다
/// - 플레이 시작마다 비운다 (도메인 리로드를 꺼도 이전 플레이의 선택이 남지 않게)
/// </summary>
public static class DimensionSelection
{
    public static DimensionData Left { get; private set; }
    public static DimensionData Right { get; private set; }

    /// <summary>
    /// 양쪽 모두 골랐는지
    /// </summary>
    public static bool HasSelection => null != Left && null != Right;

    public static void Set(DimensionData _left, DimensionData _right)
    {
        Left = _left;
        Right = _right;
    }

    public static void Clear()
    {
        Left = null;
        Right = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Clear();
    }

    /// <summary>
    /// _side 진영이 고른 차원 (없으면 null)
    /// </summary>
    public static DimensionData Get(PlayerSide _side)
    {
        return PlayerSide.Left == _side ? Left : Right;
    }
}
