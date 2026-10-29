using UnityEngine;

public sealed class ChasingMovement
    : MonoBehaviour
    , Initializable
{
    public Transform target;
    public float speed;
    Vector3 dir;

    private float baseSpeed;
    private HealthSystem healthSystem;
    private Knockback knockback;

    private void Awake()
    {
        baseSpeed = speed;
        healthSystem = GetComponentInParent<HealthSystem>();
        // Knockback 은 DamagePipeline 과 같은 (자식) 오브젝트에 붙는다
        knockback = GetComponentInParent<Knockback>();
        if (null == knockback)
        {
            knockback = GetComponentInChildren<Knockback>();
        }
    }

    public void Initialize(BlackBoard _data)
    {
        target = _data.GetTransform(DATA_TYPE.moveTarget);
        float rate = _data.GetFloat(DATA_TYPE.moveSpeedRate);
        if (rate <= 0f)
        {
            rate = 1f;
        }
        speed = baseSpeed * rate;
        dir = Vector3.zero;
    }

    private void Update()
    {
        if (null == target)
        {
            return;
        }

        // 사망 연출(디졸브) 중에는 멈춘다
        if (null != healthSystem && healthSystem.isDead)
        {
            return;
        }

        // 밀려나는 동안에는 추적하지 않는다
        if (null != knockback && knockback.IsKnockedBack)
        {
            return;
        }

        dir = MathUtils.GetDirection(transform, target);
        dir.z = 0;
        transform.Translate(dir * speed * Time.deltaTime);
    }
}
