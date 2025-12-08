using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(fileName = "Straight", menuName = "Monster/Straight")]
public class Straight
    : AIBehavior
{
    public override void Ready(Monster monster)
    {
        CalculateDirection(monster);
    }
    public override void Excute(Monster monster)
    {
        float movement = monster.GetStats().speed * Time.deltaTime;
        monster.transform.position = monster.transform.position + (monster.GetDirection() * movement);
    }
}