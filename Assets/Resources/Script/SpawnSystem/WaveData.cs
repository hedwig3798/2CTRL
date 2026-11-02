using UnityEngine;

/// <summary>
/// 웨이브 하나 (time 초 동안 spwanArray 의 몬스터를 각자의 간격으로 스폰한다)
/// - hpRate / damageRate / speedRate : 이 웨이브 동안 스폰되는 몬스터의 기본 배율
///   Spawner 최종 배율 = 웨이브 배율 × 보상 보정 ((base + ΣAdd) × (1 + ΣMultiply))
/// </summary>
[CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/WaveData")]
public class WaveData : ScriptableObject
{
    public string waveID;
    public SpawnData[] spwanArray;
    public float time;

    [Header("Wave Rate")]
    [Tooltip("이 웨이브에서 스폰되는 몬스터의 HP 배율")]
    [Min(0.01f)]
    public float hpRate = 1f;

    [Tooltip("이 웨이브에서 스폰되는 몬스터의 데미지 배율")]
    [Min(0.01f)]
    public float damageRate = 1f;

    [Tooltip("이 웨이브에서 스폰되는 몬스터의 이동 속도 배율")]
    [Min(0.01f)]
    public float speedRate = 1f;
}
