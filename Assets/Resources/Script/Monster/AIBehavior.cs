using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public abstract class AIBehavior 
    : ScriptableObject
{
    // Start 에서 실행 할 함수
    protected virtual void CalculateDirection(Monster monster)
    {
        Vector3 monsterPositoin = monster.transform.position;
        Vector3 playerPosition = monster.GetTartgetTransform().position;
        Vector3 direction = (playerPosition - monsterPositoin).normalized;
        monster.SetDirection(direction);

        if (0 >= direction.x)
        {
            monster.SetIsRight(false);
        }
        else
        {
            monster.SetIsRight(true);
        }


    }

    // Update 에서 실행 할 함수
    public abstract void Excute(Monster monster);
    public abstract void Ready(Monster monster);
}
