using System;

/// <summary>
/// 몬스터 상태 종류
/// </summary>
public enum MonsterStateId
{
    None,
    Move,
    Telegraph,
    Action,
    Downed,
    Dead,
}

/// <summary>
/// 상태머신 한 상태
/// </summary>
public interface IMonsterState
{
    MonsterStateId Id { get; }

    void Enter();

    void Tick(float _dt);

    void Exit();
}

/// <summary>
/// 범용 상태머신 (Enter / Tick / Exit)
/// </summary>
public class StateMachine
{
    private IMonsterState current;

    public IMonsterState Current => current;

    public MonsterStateId CurrentId => null != current ? current.Id : MonsterStateId.None;

    /// <summary>
    /// 상태가 바뀐 뒤 호출 (새 상태)
    /// </summary>
    public event Action<MonsterStateId> OnStateChanged;

    /// <summary>
    /// _next 로 바꾼다. 이전 상태 Exit → 현재 교체 → 새 상태 Enter 순서
    /// Exit/Enter 안에서 다시 ChangeState 를 부르면 그쪽이 최종 상태가 된다
    /// </summary>
    public void ChangeState(IMonsterState _next)
    {
        IMonsterState prev = current;
        current = _next;

        if (null != prev)
        {
            prev.Exit();
        }

        // Exit 중에 다른 상태로 바뀌었으면 그쪽을 따른다
        if (current != _next)
        {
            return;
        }

        if (null != _next)
        {
            _next.Enter();
        }

        if (current == _next)
        {
            OnStateChanged?.Invoke(CurrentId);
        }
    }

    /// <summary>
    /// Exit 없이 상태를 비운다 (풀 재사용 리셋용)
    /// </summary>
    public void Clear()
    {
        current = null;
    }

    public void Tick(float _dt)
    {
        if (null != current)
        {
            current.Tick(_dt);
        }
    }
}
