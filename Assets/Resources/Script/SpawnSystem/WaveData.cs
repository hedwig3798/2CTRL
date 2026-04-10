using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "WaveData", menuName = "Scriptable Objects/WaveData")]

public class WaveData : ScriptableObject
{
    public string waveID;
    public SpawnData[] spwanArray;
    public float time;
}
