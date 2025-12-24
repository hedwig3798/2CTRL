using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Object/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string id;
    public float baseDamage;
    public float baseCooltime;
    public float baseSize;
    public int baseCount;
    public float baseSpeed;

    // 이 무기의 발사체
    public WeaponInstance weaponObject;
}
