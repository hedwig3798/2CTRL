using UnityEngine;

/// <summary>
/// 대상(target)이 움직였는지를 Animator bool 파라미터로 넘긴다 (idle / walk 전환)
/// - 이동 스크립트를 건드리지 않고 위치 변화만 본다
/// - 컨트롤러에 파라미터가 없으면 아무것도 하지 않는다 (차원 오버라이드가 바뀌어도 경고 없음)
/// </summary>
[RequireComponent(typeof(Animator))]
public class MoveAnimator
    : MonoBehaviour
{
    [Tooltip("움직임을 볼 대상 (비우면 최상위 부모)")]
    [SerializeField]
    private Transform target;

    [SerializeField]
    private string movingParam = "IsMoving";

    [Tooltip("한 프레임 이동 거리가 이보다 크면 걷는 중")]
    [Min(0f)]
    [SerializeField]
    private float threshold = 0.0005f;

    private Animator animator;
    private int paramHash;
    private Vector3 lastPosition;

    // 파라미터 존재 여부는 컨트롤러가 바뀔 때만 다시 확인한다
    private RuntimeAnimatorController checkedController;
    private bool hasParam;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        paramHash = Animator.StringToHash(movingParam);
        if (null == target)
        {
            target = transform.root;
        }
    }

    private void OnEnable()
    {
        lastPosition = target.position;
    }

    private void LateUpdate()
    {
        Vector3 position = target.position;
        bool moving = (position - lastPosition).sqrMagnitude > threshold * threshold;
        lastPosition = position;

        if (checkedController != animator.runtimeAnimatorController)
        {
            checkedController = animator.runtimeAnimatorController;
            hasParam = false;
            if (null != checkedController)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.nameHash == paramHash && AnimatorControllerParameterType.Bool == p.type)
                    {
                        hasParam = true;
                        break;
                    }
                }
            }
        }

        if (true == hasParam)
        {
            animator.SetBool(paramHash, moving);
        }
    }
}
