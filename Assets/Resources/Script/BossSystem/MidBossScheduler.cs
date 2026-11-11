using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner 옆에서 중간 보스를 출현시킨다 (Spawner.SetMidBosses 가 없으면 붙인다)
/// - 경과 시간(Time.time - Spawner.WaveStartTime) 이 spawnTime - warningLead 면 OnBossWarning, spawnTime 이면 스폰
/// - 위치: 대상 플레이어 이동 방향 앞쪽 스폰 링
/// - 보스 생존 중 일반 스폰 개체수를 spawnCountMultiplier 로 줄이고, 사망(또는 비활성화) 시 같은 값만큼 되돌린다
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Spawner))]
public class MidBossScheduler
    : MonoBehaviour
{
    [Tooltip("출현 위치: 플레이어 이동 방향 앞쪽 링 (최소, 최대 거리)")]
    [SerializeField]
    private Vector2 spawnRing = new Vector2(11f, 13f);

    [Tooltip("출현 위치 부채꼴 각도 (이동 방향 기준 ±절반)")]
    [Range(0f, 360f)]
    [SerializeField]
    private float spawnSpread = 60f;

    private class ActiveBoss
    {
        public MidBoss boss;
        public float countMultiplier;
    }

    private struct DebugRequest
    {
        public int index;
        public float spawnAt;
    }

    private Spawner spawner;
    private MidBossEntry[] entries;
    private bool[] warned;
    private bool[] spawned;
    private readonly List<ActiveBoss> actives = new List<ActiveBoss>();
    private readonly List<DebugRequest> debugRequests = new List<DebugRequest>();

    public IReadOnlyList<MidBossEntry> Entries => entries;

    /// <summary>
    /// 살아 있는(아직 사망·비활성화 안 된) 보스 수
    /// </summary>
    public int AliveCount => actives.Count;

    private void Awake()
    {
        spawner = GetComponent<Spawner>();
    }

    /// <summary>
    /// 출현 목록을 넣는다 (출현·경고 기록을 지운다)
    /// </summary>
    public void SetEntries(MidBossEntry[] _entries)
    {
        entries = _entries;
        int count = null != entries ? entries.Length : 0;
        warned = new bool[count];
        spawned = new bool[count];
    }

    private void Update()
    {
        if (null == spawner)
        {
            return;
        }

        if (null != entries && true == spawner.IsStarted)
        {
            float elapsed = Time.time - spawner.WaveStartTime;
            for (int i = 0; i < entries.Length; ++i)
            {
                MidBossEntry entry = entries[i];
                if (null == entry || true == spawned[i])
                {
                    continue;
                }

                if (false == warned[i] && elapsed >= entry.spawnTime - entry.warningLead)
                {
                    warned[i] = true;
                    RaiseWarning(entry);
                }

                // 스폰에 실패하면 다음 프레임에 다시 시도한다 (경고는 한 번만)
                if (elapsed >= entry.spawnTime && null != SpawnBoss(entry))
                {
                    spawned[i] = true;
                }
            }
        }

        for (int i = debugRequests.Count - 1; i >= 0; --i)
        {
            DebugRequest req = debugRequests[i];
            if (Time.time < req.spawnAt)
            {
                continue;
            }

            debugRequests.RemoveAt(i);
            if (null != entries && req.index >= 0 && req.index < entries.Length)
            {
                SpawnBoss(entries[req.index]);
            }
        }
    }

    private PlayerSide GetSide()
    {
        if (null != spawner && null != spawner.target)
        {
            PlayerStats stats = spawner.target.GetComponentInParent<PlayerStats>();
            if (null != stats)
            {
                return stats.Side;
            }
        }
        return PlayerSide.Left;
    }

    private void RaiseWarning(MidBossEntry _entry)
    {
        if (null == _entry.prefab)
        {
            return;
        }

        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnBossWarning?.Invoke(GetSide(), _entry.prefab.GetComponent<MidBoss>());
        }
    }

    /// <summary>
    /// _entry 보스를 지금 스폰한다. 실패하면 null
    /// </summary>
    private MidBoss SpawnBoss(MidBossEntry _entry)
    {
        if (null == _entry || null == _entry.prefab)
        {
            Debug.LogWarning($"[MidBossScheduler] 보스 프리팹이 비어 있습니다. ({name})", this);
            return null;
        }

        Transform target = spawner.target;
        ArrowMovemnt movement = null;
        if (null != target)
        {
            movement = target.GetComponentInParent<ArrowMovemnt>();
            if (null == movement)
            {
                movement = target.GetComponentInChildren<ArrowMovemnt>();
            }
        }

        Vector2 pos = MidBoss.FindSpawnPoint(target, movement, spawnRing.x, spawnRing.y, spawnSpread);
        Spawnable sa = spawner.SpawnAt(_entry.prefab, pos);
        if (null == sa)
        {
            return null;
        }

        MidBoss boss = sa.GetComponent<MidBoss>();
        if (null == boss)
        {
            Debug.LogError($"[MidBossScheduler] {_entry.prefab.name} 루트에 MidBoss 가 없습니다.", sa);
            return null;
        }

        boss.Setup(_entry);

        ActiveBoss active = new ActiveBoss();
        active.boss = boss;
        active.countMultiplier = Mathf.Clamp(_entry.spawnCountMultiplier, 0.1f, 1f);
        spawner.AddBossCountMultiplier(active.countMultiplier);
        actives.Add(active);

        boss.OnDefeated += HandleBossGone;
        boss.OnDespawned += HandleBossGone;

        if (true == EventManager.HasInstance)
        {
            EventManager.Instance.OnBossSpawned?.Invoke(boss.Side, boss);
        }
        return boss;
    }

    /// <summary>
    /// 보스가 죽거나 비활성화되면 개체수 보정을 그 보스 몫만큼 되돌린다 (한 번만)
    /// </summary>
    private void HandleBossGone(MidBoss _boss)
    {
        for (int i = 0; i < actives.Count; ++i)
        {
            if (_boss != actives[i].boss)
            {
                continue;
            }

            ActiveBoss active = actives[i];
            actives.RemoveAt(i);
            _boss.OnDefeated -= HandleBossGone;
            _boss.OnDespawned -= HandleBossGone;

            if (null != spawner)
            {
                spawner.RemoveBossCountMultiplier(active.countMultiplier);
            }
            return;
        }
    }

    private void OnDestroy()
    {
        foreach (ActiveBoss active in actives)
        {
            if (null != active.boss)
            {
                active.boss.OnDefeated -= HandleBossGone;
                active.boss.OnDespawned -= HandleBossGone;
            }
        }
        actives.Clear();
    }

    private readonly List<MidBoss> stopBuffer = new List<MidBoss>();

    /// <summary>
    /// 스케줄을 멈추고(컴포넌트 비활성) 활성 보스를 조용히 제거한다 (최종 보스 진입)
    /// 보스는 비활성화되어 풀로 돌아가고 OnDespawned 로 개체수 보정이 되돌려진다 (처치 보상·EXP 없음)
    /// </summary>
    public void StopAndRemoveAll()
    {
        enabled = false;

        // 경고만 하고 아직 나오지 않은 보스가 있으면 출현 취소를 알린다 (경고 배너 숨김)
        bool pending = 0 < debugRequests.Count;
        if (null != warned && null != spawned)
        {
            for (int i = 0; i < warned.Length && i < spawned.Length; ++i)
            {
                if (true == warned[i] && false == spawned[i])
                {
                    pending = true;
                    break;
                }
            }
        }
        debugRequests.Clear();
        if (true == pending && true == EventManager.HasInstance)
        {
            EventManager.Instance.OnBossWarningCancelled?.Invoke(GetSide());
        }

        stopBuffer.Clear();
        foreach (ActiveBoss active in actives)
        {
            if (null != active.boss)
            {
                stopBuffer.Add(active.boss);
            }
        }
        foreach (MidBoss boss in stopBuffer)
        {
            Spawnable spawnable = boss.GetComponent<Spawnable>();
            GameObject target = null != spawnable ? spawnable.gameObject : boss.gameObject;
            target.SetActive(false);
        }
        stopBuffer.Clear();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 디버그 전용: _index 번 보스를 지금 경고하고 warningLead 초 뒤 출현시킨다. 목록에 없으면 false
    /// </summary>
    public bool DebugSpawnMidBoss(int _index)
    {
        if (null == entries || _index < 0 || _index >= entries.Length || null == entries[_index])
        {
            return false;
        }

        MidBossEntry entry = entries[_index];
        RaiseWarning(entry);

        DebugRequest req = new DebugRequest();
        req.index = _index;
        req.spawnAt = Time.time + Mathf.Max(0f, entry.warningLead);
        debugRequests.Add(req);
        return true;
    }
#endif
}
