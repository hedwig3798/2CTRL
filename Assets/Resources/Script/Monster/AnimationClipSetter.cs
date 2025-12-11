using System.Collections.Generic;
using UnityEngine;

public class AnimationClipSetter : MonoBehaviour
{
    private AnimatorOverrideController animatorOverrideController;

    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        RuntimeAnimatorController srcController = animator.runtimeAnimatorController;

        if (null == animator)
        {
            Debug.LogError("no animator in monster");
            enabled = false;
        }

        if (srcController is AnimatorOverrideController overriedCtlrl)
        {
            animatorOverrideController = new AnimatorOverrideController(overriedCtlrl.runtimeAnimatorController);
        }
        else
        {
            animatorOverrideController = new AnimatorOverrideController(srcController);
        }

        animator.runtimeAnimatorController = animatorOverrideController;
    }

    public bool SetCilip(string _key, AnimationClip _clip)
    {
        if (null == animatorOverrideController)
        {
            Debug.LogError("no animator controller in component");
            return false;
        }

        if (null == animatorOverrideController[_key])
        {
            Debug.LogError($"no {_key} in animation controller");
            return false;
        }

        animatorOverrideController[_key] = _clip;

        return true;
    }
}
