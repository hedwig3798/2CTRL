using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public float maxHP;
    public float currHP;
    bool isDead;

    private Spawnable spawnable;

    public void AddCurrentHP(float _val)
    {
        currHP += _val;
    }

    private void Awake()
    {
        spawnable = GetComponent<Spawnable>();
        if (null != spawnable)
        {
            spawnable.OnSpawnEvent += ResetHP;
        }
        isDead = false;
    }

    private void ResetHP()
    {
        currHP = maxHP;
        isDead = false;
    }

    private void Update()
    {
        if (currHP <= 0)
        {
            isDead = true;
        }
    }
}
