using System;
using UnityEngine;

[RequireComponent(typeof(MonsterMovement))]
[RequireComponent(typeof(DirectionalObject))]
[RequireComponent(typeof(AnimationClipSetter))]
[RequireComponent(typeof(Damagable))]
[RequireComponent(typeof(Stats))]

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
    private Damagable damagable;

    [SerializeField]
    private Stats stats;

    private MonsterGenerator monsterGenerator;

    private void Awake()
    {
        movement = GetComponent<MonsterMovement>();
        directionalObject = GetComponent<DirectionalObject>();  
        animationClipSetter = GetComponent<AnimationClipSetter>();
        damagable = GetComponent<Damagable>();
        stats = GetComponent<Stats>();

        if (null == movement 
            || null == directionalObject
            || null == animationClipSetter
            || null == damagable
            || null == stats
            )
        {
            Debug.LogError("some required component is missing.");
            gameObject.SetActive(false);
        }

        damagable.DeadCallback = () => DeadCallback();
    }

    private void Update()
    {
    }

    public void DeadCallback()
    {
        Debug.Log($"{gameObject.name} dead");
        monsterGenerator.ReturnMonster(this);
    }

    public Stats GetStats()
    {
        return damagable.stats;
    }
    public void SetStats(Stats _stats)
    {
        stats = _stats;
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

    public void SetIsRight(bool _isRight)
    {
        movement.isRight = _isRight;
    }

    public void SetLayerMask(LayerMask _mask)
    {
        damagable.dmgLayer = _mask;
    }
}
