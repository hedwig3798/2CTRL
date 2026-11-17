using UnityEngine;

/// <summary>
/// 데미지 메시지의 knockbackForce 만큼 타격 지점 반대쪽으로 밀려난다
/// 밀려나는 속도는 deceleration 으로 줄어들며, 밀려나는 동안 IsKnockedBack 이 true (ChasingMovement 가 멈춘다)
/// DamagePipeline 과 같은 오브젝트에 있어야 파이프라인에 등록된다 (HEALTH 다음 단계라 죽은 대상은 밀리지 않는다)
/// </summary>
public class Knockback
    : MonoBehaviour
    , IDamageable
    , Initializable
{
    [Header("knockback")]
    [Tooltip("넉백 저항 (0 ~ 1). 1 이면 밀리지 않는다")]
    [Range(0f, 1f)]
    public float resistance;
    [Tooltip("밀려나는 속도의 초당 감소량")]
    [Min(0.01f)]
    public float deceleration = 40f;
    [Tooltip("실제로 밀려날 대상. 비워두면 부모의 Rigidbody2D 가 붙은 오브젝트, 없으면 최상위 오브젝트 (DamagePipeline 이 자식에 있어도 몸 전체가 밀리도록)")]
    public Transform moveRoot;

    // 이 속도 미만이면 넉백이 끝난 것으로 본다
    private const float StopSpeed = 0.05f;

    private Vector2 velocity;
    private HealthSystem healthSystem;
    private int generation;

    public bool IsKnockedBack => velocity.sqrMagnitude > StopSpeed * StopSpeed;

    /// <summary>
    /// 실제로 밀려날 대상. moveRoot 가 있으면 그것, 없으면 부모의 Rigidbody2D 가 붙은 오브젝트, 그것도 없으면 최상위 오브젝트
    /// (Awake 전에 불러도 같은 규칙으로 찾는다)
    /// </summary>
    public Transform ResolvedRoot => null != moveRoot ? moveRoot : FindDefaultRoot();

    /// <summary>
    /// 활성 세대. 꺼지거나(풀 반환) 다시 초기화될 때마다 바뀐다 (외부에서 붙잡은 대상이 재사용됐는지 확인용)
    /// </summary>
    public int Generation => generation;

    public void ProcessDamage(ref DamageMassage _msg)
    {
        if (_msg.knockbackForce <= 0f || resistance >= 1f)
        {
            return;
        }

        if (null != healthSystem && true == healthSystem.isDead)
        {
            return;
        }

        Vector2 dir = (Vector2)transform.position - _msg.hitPoint;
        dir = dir.sqrMagnitude < 0.0001f ? Random.insideUnitCircle.normalized : dir.normalized;

        // 더 약한 넉백은 진행 중인 넉백을 덮어쓰지 않는다
        Vector2 next = dir * _msg.knockbackForce * (1f - resistance);
        if (next.sqrMagnitude >= velocity.sqrMagnitude)
        {
            velocity = next;
        }
    }

    public void Initialize(BlackBoard _data)
    {
        velocity = Vector2.zero;
        ++generation;
    }

    private void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();

        if (null == moveRoot)
        {
            moveRoot = FindDefaultRoot();
        }
    }

    private Transform FindDefaultRoot()
    {
        Rigidbody2D body = GetComponentInParent<Rigidbody2D>();
        return null != body ? body.transform : transform.root;
    }

    private void OnDisable()
    {
        velocity = Vector2.zero;
        ++generation;
    }

    private void Update()
    {
        if (false == IsKnockedBack)
        {
            velocity = Vector2.zero;
            return;
        }

        moveRoot.position += (Vector3)(velocity * Time.deltaTime);
        velocity = Vector2.MoveTowards(velocity, Vector2.zero, deceleration * Time.deltaTime);
    }
}
