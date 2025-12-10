using UnityEngine;

[RequireComponent(typeof(ArrowMovemnt))]
[RequireComponent(typeof(DirectionalObject))]
[RequireComponent(typeof(AnimationClipSetter))]
[RequireComponent(typeof(CombatObject))]

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
    private CombatObject combatObject;

    private bool deadBool = false;

    private void Awake()
    {
        movement = GetComponent<ArrowMovemnt>();
        directionalObject = GetComponent<DirectionalObject>();
        animationClipSetter = GetComponent<AnimationClipSetter>();
        combatObject = GetComponent<CombatObject>();

        if (null == movement
            || null == directionalObject
            || null == animationClipSetter
            || null == combatObject
            )
        {
            Debug.LogError("some required component is missing.");
            gameObject.SetActive(false);
        }

        combatObject.DeadCallback = () => DeadCallback();
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
