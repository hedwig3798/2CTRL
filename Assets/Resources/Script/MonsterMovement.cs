using UnityEngine;
using System;
using Unity.VisualScripting.FullSerializer;
using System.ComponentModel;
public class MonsterMovement 
    : DirectionalObject
{
    public Transform target;
    public Action move;
    public Action ready;

    public Vector3 direction;

    // public Action damaged;
    // public Action acttack;

    void OnEnable()
    {
        if(null != ready)
        {
            ready();
        }
    }

    void Update()
    {
        if (null == move)
        {
            Debug.LogError("move action is null in monster");
        }
        else
        {
            move();
        }

        CheckRight();
    }
}
