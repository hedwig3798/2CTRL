using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소환사: ctx.spawner.SpawnAt 으로 주변에 소환한다. 살아 있는 소환체가 maxAlive 이상이면 소환하지 않는다
/// keepDistance > 0 이면 타깃과 그 거리를 유지하도록 이동한다
/// </summary>
[CreateAssetMenu(fileName = "SummonerKeyword", menuName = "2CTRL/Monster/Keyword/Summoner")]
public class SummonerKeyword
    : MonsterKeyword
{
    public Spawnable minionPrefab;

    [Tooltip("한 번에 소환하는 수")]
    [Min(1)]
    public int count = 2;

    [Tooltip("동시에 살아 있을 수 있는 소환체 수")]
    [Min(1)]
    public int maxAlive = 6;

    [Tooltip("소환 위치 반경")]
    [Min(0f)]
    public float spawnRadius = 1.5f;

    [Min(0f)]
    public float telegraphTime = 0.8f;

    [Min(0f)]
    public float cooldown = 5f;

    [Tooltip("타깃과 유지할 거리. 0 이면 사용 안 함")]
    [Min(0f)]
    public float keepDistance = 0f;

    [Tooltip("keepDistance 허용 오차. 이 범위 안이면 멈춘다")]
    [Min(0f)]
    public float keepDistanceTolerance = 0.5f;

    public override MonsterKeywordCategory Category => MonsterKeywordCategory.Attack;

    public override KeywordRuntime CreateRuntime()
    {
        return new SummonerRuntime(this);
    }

    public override Spawnable SpawnTarget => minionPrefab;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (null == minionPrefab)
        {
            return;
        }

        if (null == minionPrefab.GetComponentInChildren<MonsterBrain>(true))
        {
            Debug.LogWarning($"[SummonerKeyword] {name} : 소환체 {minionPrefab.name} 에 MonsterBrain 이 없어 풀 재사용을 정확히 추적하지 못한다", this);
        }

        // 프리팹 그래프 순환 (자기 자신, A → B → A)
        MonsterSpawnGraph.WarnIfCycle(this, minionPrefab);
    }
#endif
}

public class SummonerRuntime
    : KeywordRuntime<SummonerKeyword>
{
    // 소환체는 MonsterBrain.OnDespawned(풀 반환)로 정확히 뺀다
    private readonly List<MonsterBrain> minions;
    // MonsterBrain 이 없는 소환체 (대체 경로: HealthSystem 상태로 매 프레임 정리)
    private readonly List<HealthSystem> untracked;
    private readonly Action<MonsterBrain> onMinionDespawned;
    private float readyTime;

    public SummonerRuntime(SummonerKeyword _data)
        : base(_data)
    {
        minions = new List<MonsterBrain>(Mathf.Max(1, _data.maxAlive));
        untracked = new List<HealthSystem>(Mathf.Max(1, _data.maxAlive));
        onMinionDespawned = HandleMinionDespawned;
    }

    /// <summary>
    /// 살아 있는 소환체 수. 다운(부활 대기)은 살아 있는 것으로, 최종 사망(디졸브 중)은 죽은 것으로 센다
    /// </summary>
    public int AliveCount
    {
        get
        {
            int alive = untracked.Count;
            for (int i = 0; i < minions.Count; ++i)
            {
                MonsterBrain b = minions[i];
                if (null == b)
                {
                    continue;
                }

                HealthSystem h = b.Context.health;
                if (null == h || false == IsFinallyDead(h))
                {
                    ++alive;
                }
            }
            return alive;
        }
    }

    public override void OnInitialize(MonsterContext _ctx)
    {
        ReleaseAll();
        readyTime = Time.time + data.cooldown;
    }

    public override void Tick(MonsterContext _ctx, float _dt)
    {
        for (int i = untracked.Count - 1; i >= 0; --i)
        {
            HealthSystem h = untracked[i];
            if (null == h || false == h.gameObject.activeInHierarchy || true == IsFinallyDead(h))
            {
                untracked.RemoveAt(i);
            }
        }
    }

    public override bool TryMove(MonsterContext _ctx, float _dt)
    {
        if (data.keepDistance <= 0f)
        {
            return false;
        }

        Vector2 targetPos = _ctx.TargetPosition;
        Vector2 offset = _ctx.Position - targetPos;
        float dist = offset.magnitude;

        if (Mathf.Abs(dist - data.keepDistance) <= data.keepDistanceTolerance)
        {
            // 거리 안이면 멈추고 타깃을 본다
            _ctx.Face(-offset.x);
            return true;
        }

        Vector2 dir = dist > Mathf.Epsilon ? offset / dist : Vector2.right;
        Vector2 desired = targetPos + dir * data.keepDistance;
        _ctx.MoveTowards(desired, _ctx.MoveSpeed, _dt);
        return true;
    }

    public override bool CanStartAction(MonsterContext _ctx)
    {
        if (Time.time < readyTime)
        {
            return false;
        }

        if (null == data.minionPrefab || null == _ctx.spawner)
        {
            return false;
        }

        return AliveCount < data.maxAlive;
    }

    public override float TelegraphTime => data.telegraphTime;

    public override void OnActionStart(MonsterContext _ctx)
    {
        if (null == data.minionPrefab || null == _ctx.spawner)
        {
            return;
        }

        int spawnCount = Mathf.Min(data.count, data.maxAlive - AliveCount);
        Vector2 center = _ctx.Position;
        for (int i = 0; i < spawnCount; ++i)
        {
            Vector2 pos = center + UnityEngine.Random.insideUnitCircle * data.spawnRadius;
            Spawnable s = _ctx.spawner.SpawnAt(data.minionPrefab, pos);
            if (null == s)
            {
                continue;
            }

            MonsterBrain b = s.GetComponentInChildren<MonsterBrain>();
            if (null != b)
            {
                b.OnDespawned -= onMinionDespawned;
                b.OnDespawned += onMinionDespawned;
                minions.Add(b);
                continue;
            }

            HealthSystem h = s.GetComponentInChildren<HealthSystem>();
            if (null != h)
            {
                untracked.Add(h);
            }
        }
    }

    public override void OnActionEnd(MonsterContext _ctx)
    {
        readyTime = Time.time + data.cooldown;
    }

    public override void OnDespawn(MonsterContext _ctx)
    {
        ReleaseAll();
    }

    private void HandleMinionDespawned(MonsterBrain _minion)
    {
        _minion.OnDespawned -= onMinionDespawned;
        minions.Remove(_minion);
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < minions.Count; ++i)
        {
            if (null != minions[i])
            {
                minions[i].OnDespawned -= onMinionDespawned;
            }
        }
        minions.Clear();
        untracked.Clear();
    }

    private static bool IsFinallyDead(HealthSystem _h)
    {
        return true == _h.isDead && false == _h.IsDowned;
    }
}
