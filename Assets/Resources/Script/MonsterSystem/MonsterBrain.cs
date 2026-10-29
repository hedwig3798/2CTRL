using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 루트에 붙어 키워드 조합으로 행동을 구동한다 (StraightMovement 대체)
/// 키워드 런타임은 Awake 에서 한 번 만들고 Initialize 마다 리셋한다
/// Move → (행동 가능) Telegraph → Action → Move, 사망 가로채기 시 Downed, 최종 사망 시 Dead
/// </summary>
[DisallowMultipleComponent]
public class MonsterBrain
    : MonoBehaviour
    , Initializable
{
    [SerializeField]
    private MonsterData data;

    [Tooltip("이동 방향으로 뒤집을 스프라이트. 비우면 자식에서 찾는다")]
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [Tooltip("스프라이트 원본이 왼쪽을 보고 있는지")]
    [SerializeField]
    private bool isDefaultLeft = true;

    /// <summary>
    /// 상태가 바뀐 뒤 호출 (애니메이터/색 연출용)
    /// </summary>
    public event Action<MonsterStateId> OnStateChanged;

    /// <summary>
    /// 행동 예고가 시작될 때 호출 (예고하는 키워드)
    /// </summary>
    public event Action<KeywordRuntime> OnActionTelegraph;

    /// <summary>
    /// 비활성화(풀 반환) 시 호출. 소환사가 소환체를 정확히 추적하는 데 쓴다
    /// </summary>
    public event Action<MonsterBrain> OnDespawned;

    private MonsterContext ctx;
    private KeywordRuntime[] runtimes;
    private StateMachine machine;

    private MonsterMoveState moveState;
    private MonsterTelegraphState telegraphState;
    private MonsterActionState actionState;
    private MonsterDownedState downedState;
    private MonsterDeadState deadState;

    private KeywordRuntime activeAction;
    private KeywordRuntime downedBy;
    private ContactDamage contactDamage;
    private bool initialized;

    public MonsterData Data => data;
    public MonsterContext Context => ctx;
    public MonsterStateId CurrentState => null != machine ? machine.CurrentId : MonsterStateId.None;
    public KeywordRuntime ActiveAction => activeAction;
    public KeywordRuntime DownedBy => downedBy;
    public KeywordRuntime[] Runtimes => runtimes;
    public StateMachine Machine => machine;

    private void Awake()
    {
        ctx = new MonsterContext();
        ctx.root = transform;
        ctx.brain = this;
        ctx.data = data;
        ctx.isDefaultLeft = isDefaultLeft;

        ctx.health = GetComponentInChildren<HealthSystem>(true);
        ctx.knockback = GetComponentInChildren<Knockback>(true);

        // 공격자 표시용 파이프라인은 HealthSystem 과 같은 오브젝트에 있다
        if (null == ctx.health || false == ctx.health.TryGetComponent(out ctx.pipeline))
        {
            ctx.pipeline = GetComponentInChildren<DamagePipeline>(true);
        }

        if (null == spriteRenderer)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
        ctx.spriteRenderer = spriteRenderer;

        // 접촉 데미지 누락은 프리팹 구성 문제이므로 생성 시 한 번만 알린다
        contactDamage = GetComponentInChildren<ContactDamage>(true);
        if (null == contactDamage)
        {
            Debug.LogError($"[MonsterBrain] {name} has no ContactDamage (attack 자식 필요)", this);
        }

        CreateRuntimes();

        machine = new StateMachine();
        machine.OnStateChanged += HandleStateChanged;
        moveState = new MonsterMoveState(this);
        telegraphState = new MonsterTelegraphState(this);
        actionState = new MonsterActionState(this);
        downedState = new MonsterDownedState(this);
        deadState = new MonsterDeadState(this);

        if (null == data)
        {
            Debug.LogError($"[MonsterBrain] {name} has no MonsterData", this);
        }

        if (null == ctx.health)
        {
            Debug.LogError($"[MonsterBrain] {name} has no HealthSystem", this);
            return;
        }

        if (null != ctx.health.DeathInterceptor)
        {
            Debug.LogWarning($"[MonsterBrain] {name} : HealthSystem.DeathInterceptor 를 덮어쓴다", this);
        }
        ctx.health.DeathInterceptor = HandleDeathIntercept;
        ctx.health.OnDeath += HandleDeath;
    }

    private void CreateRuntimes()
    {
        List<KeywordRuntime> list = new List<KeywordRuntime>();
        if (null != data && null != data.keywords)
        {
            foreach (MonsterKeyword k in data.keywords)
            {
                if (null == k)
                {
                    continue;
                }

                KeywordRuntime rt = k.CreateRuntime();
                if (null != rt)
                {
                    list.Add(rt);
                }
            }
        }
        runtimes = list.ToArray();
    }

    public void Initialize(BlackBoard _data)
    {
        ctx.target = null;
        ctx.moveSpeedRate = 1f;
        ctx.damageRate = 1f;
        ctx.spawner = null;

        if (null != _data)
        {
            ctx.target = _data.GetTransform(DATA_TYPE.moveTarget);
            ctx.spawner = _data.spawner;

            float speedRate = _data.GetFloat(DATA_TYPE.moveSpeedRate);
            ctx.moveSpeedRate = speedRate <= 0f ? 1f : speedRate;

            float damageRate = _data.GetFloat(DATA_TYPE.damageRate);
            ctx.damageRate = damageRate <= 0f ? 1f : damageRate;
        }

        activeAction = null;
        downedBy = null;
        machine.Clear();

        for (int i = 0; i < runtimes.Length; ++i)
        {
            runtimes[i].OnInitialize(ctx);
        }

        initialized = true;
        machine.ChangeState(moveState);
    }

    private void Update()
    {
        if (false == initialized)
        {
            return;
        }

        float dt = Time.deltaTime;

        // 패시브는 살아 있는 동안만
        if (false == ctx.IsDead)
        {
            for (int i = 0; i < runtimes.Length; ++i)
            {
                runtimes[i].Tick(ctx, dt);
            }
        }

        machine.Tick(dt);
    }

    private void OnDisable()
    {
        OnDespawned?.Invoke(this);

        if (false == initialized)
        {
            return;
        }

        EndActiveAction();
        for (int i = 0; i < runtimes.Length; ++i)
        {
            runtimes[i].OnDespawn(ctx);
        }

        downedBy = null;
        initialized = false;
        machine.Clear();
    }

    private void OnDestroy()
    {
        if (null == ctx || null == ctx.health)
        {
            return;
        }

        if (ctx.health.DeathInterceptor == HandleDeathIntercept)
        {
            ctx.health.DeathInterceptor = null;
        }
        ctx.health.OnDeath -= HandleDeath;
    }

    #region State
    public void StartAction(KeywordRuntime _rt)
    {
        activeAction = _rt;
        if (_rt.TelegraphTime > 0f)
        {
            machine.ChangeState(telegraphState);
        }
        else
        {
            machine.ChangeState(actionState);
        }
    }

    public void ChangeToMove()
    {
        machine.ChangeState(moveState);
    }

    public void ChangeToAction()
    {
        machine.ChangeState(actionState);
    }

    /// <summary>
    /// 진행 중인 행동을 끝낸다 (OnActionEnd 호출). 없으면 아무것도 안 한다
    /// </summary>
    public void EndActiveAction()
    {
        if (null == activeAction)
        {
            return;
        }

        KeywordRuntime rt = activeAction;
        activeAction = null;
        rt.OnActionEnd(ctx);
    }

    public void RaiseActionTelegraph(KeywordRuntime _rt)
    {
        OnActionTelegraph?.Invoke(_rt);
    }

    /// <summary>
    /// 다운 상태에서 되살리고 Move 로 돌아간다
    /// </summary>
    public void ReviveFromDowned()
    {
        float ratio = null != downedBy ? downedBy.ReviveHPRatio : 1f;
        downedBy = null;

        if (null != ctx.health)
        {
            ctx.health.Revive(ratio);
        }

        for (int i = 0; i < runtimes.Length; ++i)
        {
            runtimes[i].OnRevive(ctx);
        }

        machine.ChangeState(moveState);
    }

    private void HandleStateChanged(MonsterStateId _id)
    {
        OnStateChanged?.Invoke(_id);
    }
    #endregion

    #region Death
    /// <summary>
    /// HealthSystem.DeathInterceptor. 키워드 하나라도 가로채면 Downed 로 간다
    /// </summary>
    private bool HandleDeathIntercept()
    {
        if (false == initialized)
        {
            return false;
        }

        for (int i = 0; i < runtimes.Length; ++i)
        {
            if (true == runtimes[i].InterceptDeath(ctx))
            {
                downedBy = runtimes[i];
                machine.ChangeState(downedState);
                return true;
            }
        }
        return false;
    }

    private void HandleDeath(GameObject _object)
    {
        if (false == initialized)
        {
            return;
        }

        machine.ChangeState(deadState);
    }
    #endregion
}
