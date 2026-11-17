using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 쿼드트리 대상 수집 (StageSpace 가 매 프레임 1회 호출)
/// - HealthSystem.ActiveEnemies 중 살아 있는 적을 화면 좌표로 옮겨 넣는다. 자기 진영 화면(SideRect)과 겹치지 않는 적은 뺀다
/// - 진영: BossHitProxy 가 있으면 프록시 진영, 아니면 레이어(LMonster/RMonster). 둘 다 아니면 뺀다
/// - 최종 보스 프록시는 같은 FinalBoss 당 하나만 넣는다 (좌우 프록시가 둘 다 보여도 데미지가 한 번만 들어가게).
///   위치·반지름은 보스 본체(화면 공간 = 보스 월드) 기준
/// - 반지름: 콜라이더 bounds extents 최대값을 HealthSystem 별로 캐시 (스케일이 바뀌면 다시 잰다)
/// </summary>
public class StageTargets
{
    private struct Info
    {
        public Collider2D collider;
        public BossHitProxy proxy;
        public float radius;
        public float scale;
        public bool hasRadius;
    }

    private const int PRUNE_MIN = 256;
    private const int PRUNE_INTERVAL = 120;

    private readonly string leftLayerName;
    private readonly string rightLayerName;
    private readonly float defaultRadius;
    private int leftLayer = int.MinValue;
    private int rightLayer = int.MinValue;

    private readonly List<HealthSystem> aliveBuffer = new List<HealthSystem>();
    private readonly List<FinalBoss> bossSeen = new List<FinalBoss>(2);
    private readonly Dictionary<HealthSystem, Info> cache = new Dictionary<HealthSystem, Info>();
    private readonly List<HealthSystem> pruneBuffer = new List<HealthSystem>();
    private int rebuildCount;

    public StageTargets(string _leftLayerName, string _rightLayerName, float _defaultRadius)
    {
        leftLayerName = _leftLayerName;
        rightLayerName = _rightLayerName;
        defaultRadius = Mathf.Max(0f, _defaultRadius);
    }

    /// <summary>
    /// _tree 를 비우고 지금 화면에 보이는 적으로 다시 채운다
    /// </summary>
    public void Rebuild(StageQuadTree _tree, BossSpace _space)
    {
        if (null == _tree || null == _space)
        {
            return;
        }
        ResolveLayers();

        Rect leftRect = _space.SideRect(PlayerSide.Left);
        Rect rightRect = _space.SideRect(PlayerSide.Right);
        _tree.Clear(BossSpace.Union(leftRect, rightRect));
        bossSeen.Clear();

        HealthSystem.CollectAlive(~0, aliveBuffer);
        for (int i = 0; i < aliveBuffer.Count; ++i)
        {
            HealthSystem health = aliveBuffer[i];
            if (null == health || false == health.isActiveAndEnabled)
            {
                continue;
            }

            Info info = GetInfo(health);
            PlayerSide side;
            Vector2 stagePos;
            float radius;

            if (null != info.proxy)
            {
                side = info.proxy.Side;
                FinalBoss boss = info.proxy.Boss;
                if (null != boss)
                {
                    if (true == bossSeen.Contains(boss))
                    {
                        continue;
                    }
                    stagePos = boss.Position;
                    radius = boss.BodyRadius;
                }
                else
                {
                    stagePos = _space.FromSideWorld(side, health.transform.position);
                    radius = info.radius;
                }

                if (false == BossSpace.CircleOverlapsRect(stagePos, radius, PlayerSide.Left == side ? leftRect : rightRect))
                {
                    continue;
                }
                if (null != boss)
                {
                    bossSeen.Add(boss);
                }
            }
            else
            {
                int layer = health.gameObject.layer;
                if (layer == leftLayer)
                {
                    side = PlayerSide.Left;
                }
                else if (layer == rightLayer)
                {
                    side = PlayerSide.Right;
                }
                else
                {
                    continue;
                }

                stagePos = _space.FromSideWorld(side, health.transform.position);
                radius = info.radius;
                if (false == BossSpace.CircleOverlapsRect(stagePos, radius, PlayerSide.Left == side ? leftRect : rightRect))
                {
                    continue;
                }
            }

            _tree.Insert(new StageTarget(stagePos, radius, side, health));
        }

        aliveBuffer.Clear();
        PruneCache();
    }

    private void ResolveLayers()
    {
        if (int.MinValue == leftLayer)
        {
            leftLayer = LayerMask.NameToLayer(leftLayerName);
        }
        if (int.MinValue == rightLayer)
        {
            rightLayer = LayerMask.NameToLayer(rightLayerName);
        }
    }

    /// <summary>
    /// 캐시된 반지름·프록시 (처음 보거나 스케일이 바뀌었으면 다시 잰다)
    /// </summary>
    private Info GetInfo(HealthSystem _health)
    {
        float scale = Mathf.Abs(_health.transform.lossyScale.x);
        if (true == cache.TryGetValue(_health, out Info info)
            && true == info.hasRadius && true == Mathf.Approximately(info.scale, scale))
        {
            return info;
        }

        if (false == cache.ContainsKey(_health))
        {
            info.proxy = _health.GetComponentInParent<BossHitProxy>();
            info.collider = _health.GetComponent<Collider2D>();
            if (null == info.collider)
            {
                info.collider = _health.GetComponentInChildren<Collider2D>();
            }
        }

        info.scale = scale;
        info.radius = defaultRadius;
        info.hasRadius = false;
        if (null != info.collider && true == info.collider.enabled)
        {
            Vector3 extents = info.collider.bounds.extents;
            float r = Mathf.Max(extents.x, extents.y);
            if (r > 0f)
            {
                info.radius = r;
                info.hasRadius = true;
            }
        }
        cache[_health] = info;
        return info;
    }

    /// <summary>
    /// 파괴된 HealthSystem 캐시를 정리한다 (캐시가 클 때 PRUNE_INTERVAL 번 재구성마다 한 번)
    /// </summary>
    private void PruneCache()
    {
        ++rebuildCount;
        if (cache.Count < PRUNE_MIN || 0 != rebuildCount % PRUNE_INTERVAL)
        {
            return;
        }
        pruneBuffer.Clear();
        foreach (KeyValuePair<HealthSystem, Info> pair in cache)
        {
            if (null == pair.Key)
            {
                pruneBuffer.Add(pair.Key);
            }
        }
        for (int i = 0; i < pruneBuffer.Count; ++i)
        {
            cache.Remove(pruneBuffer[i]);
        }
        pruneBuffer.Clear();
    }
}
