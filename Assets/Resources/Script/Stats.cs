using UnityEngine;

// 몬스터, 플레이어의 기본 스텟
[System.Serializable]
public class Stats
{
    public float hp = 1;
    public float atk = 1;
    public float dmg = 1;
    public float atkDelay = 0.5f;
    public float speed = 1;
    public float coolTimeReduce = 0;
    public float exp = 0;
    public float level = 1;

    public bool isDead = false;
}
