using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

// 몬스터, 플레이어의 기본 스텟
[System.Serializable]
public class Stats
    : MonoBehaviour
{
    public float maxHP = 1.0f;
    public float hp = 1;
    public float hpRegen = 0.1f;
    public float hpRegenTick = 1.0f;

    public float atkSpeed = 0.5f;
    public float dmgIncreasePercent = 1.0f;
    public float coolTimeReduce = 0;

    public float speed = 1;

    public float exp = 0;

    public float level = 1;

    public float lifeSteal = 0;

    public float criticalRate = 0;
    public float criticalDMG = 1.5f;

    public float reflectRate = 0.0f;

    public bool isDead = false;

    private float hpRegenTimer = 0.0f;

    // 나중에 Attack 정보를 받아와야됨
    public float GetFinalDamage()
    {
        // 최종 데미지 계산
        float finalDmg = 1 * dmgIncreasePercent;

        // 크리티컬 확률 계산
        float crit = UnityEngine.Random.value;
        // 크리티컬인 경우 데미지 계산
        if (crit <= criticalRate)
        {
            finalDmg += 1 * criticalDMG;
        }

        return finalDmg;
    }

    public void AddHP(float _val)
    {
        hp += _val;

        if (hp >= maxHP)
        {
            hp = maxHP;
        }
    }

    private void LateUpdate()
    {
        // Update 에서 데미지 계산 후 최종 hp가 0 이하면 죽는다
        if (0 >= hp)
        {
            isDead = true;
            hpRegenTimer = 0.0f;
            return;
        }

        // 죽지 않았으면 HP 리젠
        hpRegenTimer += Time.deltaTime;
        while (hpRegenTimer > hpRegenTick)
        {
            hpRegenTimer -= hpRegenTick;
            AddHP(hpRegen);
        }
    }
}
