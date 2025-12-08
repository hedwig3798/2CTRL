using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;


public class PlayerCombat 
    : MonoBehaviour
{
    public HashSet<string> enemies;

    public Player player;

    private Dictionary<MonsterMovement, float> hitCoolDownDict = new Dictionary<MonsterMovement, float>(); 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hitCoolDownDict.Clear();
    }

    private void Update()
    {
        foreach (var key in hitCoolDownDict.Keys.ToList())
        {
            hitCoolDownDict[key] -= Time.deltaTime;
            if (hitCoolDownDict[key] <= 0)
            {
                hitCoolDownDict[key] = 0;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (true == enemies.Contains(collision.gameObject.tag))
        {
            MonsterMovement monster = collision.gameObject.GetComponent<MonsterMovement>();
            if (monster != null)
            {
                return;
            }

            // hitCoolDownDict.Add(monster, monster.atkDelay);
            // player.hp -= monster.atk;
        }
    }
}
