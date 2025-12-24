using System.Collections.Generic;
using System.Threading;
using UnityEngine;


[CreateAssetMenu(fileName = "Chasing", menuName = "Monster/Chasing")]
public class Chasing 
    : AIBehavior
{
    public override void Ready(Monster monster)
    {
        
    }

    public override void Excute(Monster monster)
    {
        CalculateDirection(monster);

        float movement = monster.GetStats().speed * Time.deltaTime;
        monster.transform.Translate(monster.GetDirection() * movement);
    }
}
