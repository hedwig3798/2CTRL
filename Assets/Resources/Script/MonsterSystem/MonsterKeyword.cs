using UnityEngine;

/// <summary>
/// 몬스터 키워드 분류
/// </summary>
public enum MonsterKeywordCategory
{
    Move,
    Attack,
    Death,
}

/// <summary>
/// 몬스터 행동 키워드 (수치만 가진다)
/// SO 는 여러 몬스터가 공유하므로 개체별 상태는 CreateRuntime 으로 만든 KeywordRuntime 에 둔다
/// </summary>
public abstract class MonsterKeyword
    : ScriptableObject
{
    public abstract MonsterKeywordCategory Category { get; }

    /// <summary>
    /// 몬스터 개체 하나가 쓸 런타임 상태를 만든다 (Brain 의 Awake 에서 한 번)
    /// </summary>
    public abstract KeywordRuntime CreateRuntime();

    /// <summary>
    /// 이 키워드가 스폰하는 프리팹 (분열/소환). 없으면 null. 무한 스폰 순환 검사에 쓴다
    /// </summary>
    public virtual Spawnable SpawnTarget => null;
}

/// <summary>
/// 키워드의 개체별 런타임 상태
/// 모든 훅은 기본적으로 아무것도 하지 않는다
/// </summary>
public abstract class KeywordRuntime
{
    /// <summary>
    /// 스폰 또는 재사용 시 리셋
    /// </summary>
    public virtual void OnInitialize(MonsterContext _ctx) { }

    /// <summary>
    /// 이번 프레임 이동을 직접 처리했으면 true. 리스트 순서상 첫 true 가 이긴다
    /// </summary>
    public virtual bool TryMove(MonsterContext _ctx, float _dt) { return false; }

    #region Action
    public virtual bool CanStartAction(MonsterContext _ctx) { return false; }

    /// <summary>
    /// 예고 시간. 0 이하면 예고 없이 바로 행동한다
    /// </summary>
    public virtual float TelegraphTime => 0f;

    public virtual void OnTelegraphStart(MonsterContext _ctx) { }

    public virtual void OnTelegraphTick(MonsterContext _ctx, float _dt) { }

    public virtual void OnActionStart(MonsterContext _ctx) { }

    /// <summary>
    /// 행동이 끝나면 true
    /// </summary>
    public virtual bool TickAction(MonsterContext _ctx, float _dt) { return true; }

    /// <summary>
    /// 행동이 끝나거나 (예고/행동 중) 다운·사망으로 끊겼을 때 호출. 쿨다운을 시작한다
    /// </summary>
    public virtual void OnActionEnd(MonsterContext _ctx) { }
    #endregion

    /// <summary>
    /// 살아 있는 동안 매 프레임 호출 (패시브)
    /// </summary>
    public virtual void Tick(MonsterContext _ctx, float _dt) { }

    #region Death
    /// <summary>
    /// true 면 사망을 가로챈다 (다운 → 부활)
    /// </summary>
    public virtual bool InterceptDeath(MonsterContext _ctx) { return false; }

    /// <summary>
    /// 사망을 가로챈 키워드의 다운 대기 시간
    /// </summary>
    public virtual float ReviveDelay => 0f;

    /// <summary>
    /// 사망을 가로챈 키워드의 부활 HP 비율
    /// </summary>
    public virtual float ReviveHPRatio => 1f;

    /// <summary>
    /// 다운 상태에서 부활한 직후 호출
    /// </summary>
    public virtual void OnRevive(MonsterContext _ctx) { }

    /// <summary>
    /// 최종 사망 시 호출
    /// </summary>
    public virtual void OnDeath(MonsterContext _ctx) { }
    #endregion

    /// <summary>
    /// 비활성화(풀 반환) 시 정리
    /// </summary>
    public virtual void OnDespawn(MonsterContext _ctx) { }
}

/// <summary>
/// 자기 키워드 SO 를 타입 그대로 들고 있는 런타임
/// </summary>
public abstract class KeywordRuntime<T>
    : KeywordRuntime
    where T : MonsterKeyword
{
    protected readonly T data;

    protected KeywordRuntime(T _data)
    {
        data = _data;
    }
}
