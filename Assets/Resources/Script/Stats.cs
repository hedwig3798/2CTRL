using UnityEngine;

// 몬스터, 플레이어의 기본 스텟
[System.Serializable]
public class Stats
{
    public float maxHP = 1.0f;
    public float hp = 1;

    public float atk = 1;
    public float atkSpeed = 0.5f;

    public float coolTimeReduce = 0;

    public float speed = 1;

    public float exp = 0;

    public float level = 1;

    public float lifeSteal = 0;

    public float criticalRate = 0;
    public float criticalDMG = 1.5f;

    public float reflectRate = 0.0f;

    public bool isDead = false;
}
