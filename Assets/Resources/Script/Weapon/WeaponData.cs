using System;
using UnityEngine;

[System.Serializable]
public class WeaponData
{
    public string name;
    public float baseDamage;
    public float baseCooltime;
    public float baseSize;
    public int baseCount;

    public GameObject weaponObject;
}
