using UnityEngine;

/// <summary>
/// 키워드 런타임이 공통으로 쓰는 몬스터 개체 정보와 이동 헬퍼
/// MonsterBrain 이 소유하고 Initialize 마다 값을 갱신한다
/// </summary>
public class MonsterContext
{
    public Transform root;
    public HealthSystem health;
    public Knockback knockback;
    public SpriteRenderer spriteRenderer;
    public DamagePipeline pipeline;

    public Transform target;
    public float moveSpeedRate = 1f;
    public float damageRate = 1f;
    public Spawner spawner;
    public MonsterData data;
    public MonsterBrain brain;

    /// <summary>
    /// 스프라이트 원본이 왼쪽을 보고 있는지
    /// </summary>
    public bool isDefaultLeft = true;

    /// <summary>
    /// baseMoveSpeed × moveSpeedRate
    /// </summary>
    public float MoveSpeed => (null != data ? data.baseMoveSpeed : 0f) * moveSpeedRate;

    /// <summary>
    /// data.targetLayer 중 이 몬스터 레이어와 충돌하는 레이어만 (좌·우 진영이 서로의 플레이어를 때리지 않도록)
    /// </summary>
    public LayerMask TargetLayer
    {
        get
        {
            if (null == data)
            {
                return 0;
            }

            int mask = data.targetLayer;
            if (null != root)
            {
                mask &= Physics2D.GetLayerCollisionMask(root.gameObject.layer);
            }
            return mask;
        }
    }

    public bool HasTarget => null != target;

    public Vector2 Position => root.position;

    public Vector2 TargetPosition => null != target ? (Vector2)target.position : Position;

    public bool IsDead => null != health && health.isDead;

    public float DistanceToTarget()
    {
        if (null == target)
        {
            return float.MaxValue;
        }
        return Vector2.Distance(Position, target.position);
    }

    /// <summary>
    /// _delta 만큼 이동하고 이동 방향으로 스프라이트를 뒤집는다
    /// </summary>
    public void Move(Vector2 _delta)
    {
        root.position += new Vector3(_delta.x, _delta.y, 0f);
        Face(_delta.x);
    }

    /// <summary>
    /// _point 를 향해 _speed 로 이동한다 (넘어가지 않는다)
    /// </summary>
    public void MoveTowards(Vector2 _point, float _speed, float _dt)
    {
        Vector2 pos = Position;
        Vector2 next = Vector2.MoveTowards(pos, _point, _speed * _dt);
        Move(next - pos);
    }

    /// <summary>
    /// 즉시 위치를 옮긴다 (순간이동). z 는 유지한다
    /// </summary>
    public void SetPosition(Vector2 _position)
    {
        root.position = new Vector3(_position.x, _position.y, root.position.z);
    }

    /// <summary>
    /// x 방향으로 스프라이트를 돌린다. 0 이면 그대로 둔다
    /// </summary>
    public void Face(float _dirX)
    {
        if (null == spriteRenderer || Mathf.Abs(_dirX) <= Mathf.Epsilon)
        {
            return;
        }
        spriteRenderer.flipX = isDefaultLeft ^ (_dirX < 0f);
    }
}
