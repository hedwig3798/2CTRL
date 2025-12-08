using NUnit.Framework;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveDataList", menuName = "Wave/WaveDataList")]
public class WaveDataList : ScriptableObject
{
    public WaveData[] waveList;
}
