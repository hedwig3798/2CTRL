using System;
using UnityEngine;

[RequireComponent(typeof(MonsterMovement))]
[RequireComponent(typeof(DirectionalObject))]
[RequireComponent(typeof(AnimationClipSetter))]
[RequireComponent(typeof(CombatObject))]

public class Monster : MonoBehaviour
{
    [SerializeField]
    public string id = "default";

    [SerializeField] 
    private MonsterMovement movement;

    [SerializeField] 
    private DirectionalObject directionalObject;

    [SerializeField] 
    private AnimationClipSetter animationClipSetter;

    [SerializeField] 
    private CombatObject combatObject;

    private void Awake()
    {
        movement = GetComponent<MonsterMovement>();
        directionalObject = GetComponent<DirectionalObject>();  
        animationClipSetter = GetComponent<AnimationClipSetter>();
        combatObject = GetComponent<CombatObject>();

        if (null == movement 
            || null == directionalObject
            || null == animationClipSetter
            || null == combatObject
            )
        {
            Debug.LogError("a required component is missing.");
            gameObject.SetActive(false);
        }
    }

    public Stats GetStats()
    {
        return combatObject.stats;
    }
    public void SetStats(Stats _stats)
    {
        combatObject.stats = _stats;
    }

    public Transform GetTartgetTransform()
    {
        return movement.target;
    }

    public Transform SetTartgetTransform(Transform _target)
    {
        return movement.target = _target;
    }

    public void SetExcuteAction(Action _ai)
    {
        movement.move = _ai;
    }

    public void SetReadyAction(Action _ai)
    {
        movement.ready = _ai;
    }

    public void SetAnimationClip(string _key, AnimationClip _clip)
    {
        if (null == _clip)
        {
            Debug.LogError("no clip error");
            return;
        }
        animationClipSetter.SetCilip( _key, _clip );
    }

    public void SetDirection(Vector3 _dir)
    {
        movement.direction = _dir;
    }

    public Vector3 GetDirection()
    {
        return movement.direction;
    }
}
