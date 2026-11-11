using UnityEngine;

/// <summary>
/// 최종 보스 투명 히트박스 (진영당 1개, 진영 몬스터 레이어)
/// - 매 LateUpdate 보스 위치를 그 진영 월드로 옮긴다 (BossSpace.ToSideWorld). 무기·자동 조준이 수정 없이 보스를 맞힌다
/// - 보스 몸체 원이 그 진영 화면 절반(SideRect)과 겹칠 때만 콜라이더·HealthSystem 을 켠다 (켜진 동안 ActiveEnemies 에 들어간다)
/// - HealthSystem 은 무적(받은 데미지 표시 후 최대치로 복구) + 즉사 면역. OnDamaged 로 받은 양을 FinalBoss HP 로 넘긴다
/// - 프리팹에 Knockback 은 넣지 않는다. 데미지 텍스트는 DamageTextBinder 를 붙이면 그대로 뜬다
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(DamagePipeline))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossHitProxy
    : MonoBehaviour
{
    [SerializeField]
    private PlayerSide side = PlayerSide.Left;

    [Tooltip("프록시 HealthSystem 최대 HP (죽지 않게 큰 값)")]
    [SerializeField]
    private float proxyHP = 1000000f;

    [SerializeField]
    private string leftLayerName = "LMonster";

    [SerializeField]
    private string rightLayerName = "RMonster";

    private CircleCollider2D hitCollider;
    private HealthSystem health;
    private DamagePipeline pipeline;
    private Rigidbody2D body;
    private FinalBossDirector director;
    private FinalBoss boss;
    private bool isLive = true;

    public PlayerSide Side => side;
    public bool IsLive => isLive;
    public DamagePipeline Pipeline => pipeline;

    private void Awake()
    {
        hitCollider = GetComponent<CircleCollider2D>();
        hitCollider.isTrigger = true;
        health = GetComponent<HealthSystem>();
        health.IsInvincible = true;
        health.IsExecuteImmune = true;
        pipeline = GetComponent<DamagePipeline>();

        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;

        SetLive(false);
    }

    private void OnEnable()
    {
        if (null != health)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (null != health)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    /// <summary>
    /// 보스와 연결한다 (진영, 레이어, 프록시 HP 설정)
    /// </summary>
    public void Setup(FinalBossDirector _director, FinalBoss _boss, PlayerSide _side)
    {
        director = _director;
        boss = _boss;
        side = _side;

        int layer = LayerMask.NameToLayer(PlayerSide.Left == side ? leftLayerName : rightLayerName);
        if (layer >= 0)
        {
            LayerUtils.SetLayer(gameObject, layer);
        }
        else
        {
            Debug.LogWarning("[BossHitProxy] 몬스터 레이어를 찾지 못했습니다: " + side);
        }

        ResetHP();
    }

    /// <summary>
    /// 보스와 연결을 끊고 끈다
    /// </summary>
    public void Shutdown()
    {
        boss = null;
        SetLive(false);
    }

    private void ResetHP()
    {
        if (true == health.isDead)
        {
            health.Initialize(null);
        }
        health.SetMaxHP(proxyHP);
        health.CurrHP = proxyHP;
    }

    private void LateUpdate()
    {
        BossSpace space = null != director ? director.Space : null;
        if (null == boss || null == space || true == boss.IsDead || false == boss.IsVulnerable)
        {
            SetLive(false);
            return;
        }

        Vector2 bossPos = boss.Position;
        bool overlaps = BossSpace.CircleOverlapsRect(bossPos, boss.BodyRadius, space.SideRect(side));
        if (true == overlaps)
        {
            Vector2 world = space.ToSideWorld(side, bossPos);
            // 물리 위치로 옮긴다 (Transform 직접 이동은 Rigidbody2D 와 어긋난다). 같은 프레임 쿼리용으로 transform 도 맞춘다
            body.position = world;
            transform.position = new Vector3(world.x, world.y, transform.position.z);

            float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            hitCollider.radius = boss.BodyRadius * space.SideScale(side) / scale;

            // 무적이라 죽지 않지만, 다른 경로(Kill 등)로 죽었으면 되살린다
            if (true == health.isDead)
            {
                ResetHP();
            }
        }
        SetLive(overlaps);
    }

    private void SetLive(bool _live)
    {
        if (isLive == _live)
        {
            return;
        }
        isLive = _live;
        if (null != hitCollider)
        {
            hitCollider.enabled = _live;
        }
        if (null != health)
        {
            // 컴포넌트를 끄면 HealthSystem.OnDisable 이 ActiveEnemies 에서 뺀다
            health.enabled = _live;
        }
    }

    private void HandleDamaged(float _amount)
    {
        if (false == isLive || null == boss)
        {
            return;
        }
        boss.ApplyDamage(_amount);
    }
}
