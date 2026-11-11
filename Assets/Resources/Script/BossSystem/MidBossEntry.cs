using System;
using UnityEngine;

/// <summary>
/// 차원 하나의 중간 보스 출현 설정 (DimensionData.midBosses → 반대 진영 Spawner 의 MidBossScheduler)
/// </summary>
[Serializable]
public class MidBossEntry
{
    [Tooltip("보스 프리팹 (루트에 MidBoss 필요)")]
    public Spawnable prefab;

    [Tooltip("출현 시각 (스포너 경과 초, 10분 = 600)")]
    [Min(0f)]
    public float spawnTime = 600f;

    [Tooltip("출현 몇 초 전에 경고하는지")]
    [Min(0f)]
    public float warningLead = 3f;

    [Tooltip("보스 생존 중 일반 스폰 개체수 배율 (0.7 = ×0.7)")]
    [Range(0.1f, 1f)]
    public float spawnCountMultiplier = 0.7f;

    [Tooltip("처치 시 떨어뜨리는 EXP 합")]
    [Min(0f)]
    public float expTotal = 60f;

    [Tooltip("처치 시 흩뿌리는 보석 수")]
    [Min(0)]
    public int gemCount = 20;
}
