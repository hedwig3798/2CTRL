using NUnit.Framework.Constraints;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(ArrowMovemnt))]
[RequireComponent(typeof(DirectionalObject))]
[RequireComponent(typeof(AnimationClipSetter))]
[RequireComponent(typeof(Damagable))]
[RequireComponent(typeof(Stats))]

public class Player : MonoBehaviour
{
    [SerializeField]
    public string id = "default";

    [SerializeField]
    private ArrowMovemnt movement;

    [SerializeField]
    private DirectionalObject directionalObject;

    [SerializeField]
    private AnimationClipSetter animationClipSetter;

    [SerializeField]
    private Damagable damagable;

    [SerializeField]
    private Stats stats;

    private bool deadBool = false;

    // public List<AttackComponent> attacks = new();

    private void Awake()
    {
        movement = GetComponent<ArrowMovemnt>();
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

    public void DeadCallback()
    {
        if (false == deadBool)
        {
            Debug.Log($"{gameObject.name} dead");
            deadBool = true;
        }
    }
}
