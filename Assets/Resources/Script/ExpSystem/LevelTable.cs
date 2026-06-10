using UnityEngine;

[CreateAssetMenu(fileName = "LevelTable", menuName = "Scriptable Objects/LevelTable")]
public class LevelTable 
    : ScriptableObject
{
    [Header("need exp when level x to x+1")]
    public float[] expTable;

    [Header("max level")]
    public int maxLevel;
}
