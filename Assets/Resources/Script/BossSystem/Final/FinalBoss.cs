using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 최종 보스 본체 (보스 월드, "Boss" 레이어)
/// - 자체 HP (HealthSystem 을 쓰지 않는다: OnEnemyDeath·드롭·통계 부작용 방지). 피해는 BossHitProxy 가 ApplyDamage 로 넘긴다
/// - 패턴: 운석 / 돌진 / 레이저, P3 조합(레이저+운석, 돌진 후 운석). 같은 패턴 연속 금지, 격노 시 간격 단축
/// - 모든 시간은 Time.deltaTime 기반 (일시정지·timeScale 0 동안 멈춘다)
/// - 연출은 이벤트 훅으로만 노출한다 (애니메이션·이펙트는 visual-designer 몫)
/// </summary>
public class FinalBoss
    : MonoBehaviour
{
    [Tooltip("운석 본체 시각 (선택, 보스 레이어로 바뀐다)")]
    [SerializeField]
    private GameObject meteorVisualPrefab;

    [Tooltip("운석 그림자 시각 (선택)")]
    [SerializeField]
    private GameObject meteorShadowPrefab;

    /// <summary>패턴 시작 (조합 패턴 포함)</summary>
    public event Action<FinalBossPattern> OnPatternStarted;
    /// <summary>운석 팔 들기 시작</summary>
    public event Action OnMeteorWindup;
    /// <summary>운석 착탄 (위치, 반경)</summary>
    public event Action<Vector2, float> OnMeteorImpact;
    /// <summary>돌진 시작 (방향)</summary>
    public event Action<Vector2> OnDashStarted;
    public event Action OnDashEnded;
    /// <summary>레이저 발사</summary>
    public event Action OnLaserFired;
    /// <summary>등장 완료 (전투 시작)</summary>
    public event Action OnEntranceFinished;
    /// <summary>피해 (양)</summary>
    public event Action<float> OnDamaged;
    public event Action OnDefeated;

    private class Meteor
    {
        public Vector2 start;
        public Vector2 target;
        public float duration;
        public float elapsed;
        public bool leavesFire;
        public BossTelegraph telegraph;
        public GameObject visual;
        public GameObject shadow;
    }

    private class FireZone
    {
        public Vector2 center;
        public float remaining;
        public float tickTimer;
        public BossTelegraph telegraph;
    }

    private FinalBossDirector director;
    private FinalBossData data;
    private int bossLayer;

    private float maxHP;
    private float currHP;
    private bool isDead;
    private bool isFighting;
    private bool isVulnerable;
    private bool isDashing;

    // 일시정지를 뺀 전투 경과 시간
    private float fightClock;
    private float fightStartClock;
    private readonly float[] contactReadyAt = new float[2];

    private FinalBossPattern lastPattern = FinalBossPattern.None;
    private PlayerSide nextDashSide = PlayerSide.Left;
    private PlayerSide nextIdleSide = PlayerSide.Right;
    private System.Random rng;

    private readonly List<Meteor> meteors = new List<Meteor>();
    private readonly List<FireZone> fires = new List<FireZone>();
    private readonly HashSet<BossTelegraph> telegraphs = new HashSet<BossTelegraph>();
    private readonly List<LaserLine> laserLines = new List<LaserLine>();
    private readonly List<BossTelegraph> laserTelegraphs = new List<BossTelegraph>();
    private ObjectPool<GameObject> meteorPool;
    private ObjectPool<GameObject> shadowPool;

    public Vector2 Position => transform.position;
    public float BodyRadius => null != data ? data.bodyRadius : 0f;
    public float MaxHP => maxHP;
    public float CurrHP => currHP;
    public bool IsDead => isDead;
    public bool IsVulnerable => isVulnerable;
    public bool IsDashing => isDashing;
    public bool IsEnraged => true == isFighting && null != data && fightClock - fightStartClock >= data.enrageTime;
    public int Phase => null == data
        ? 0
        : FinalBossPatterns.PhaseFor(maxHP > 0f ? currHP / maxHP : 0f, data.phase2Ratio, data.phase3Ratio);
    public FinalBossPattern LastPattern => lastPattern;

    private BossSpace Space => director.Space;

    #region 시작·HP
    /// <summary>
    /// 전투를 시작한다 (등장 → 패턴 반복). 보스 레이어는 director 가 맞춘다
    /// </summary>
    public void Begin(FinalBossDirector _director, FinalBossData _data, int _bossLayer)
    {
        director = _director;
        data = _data;
        bossLayer = _bossLayer;
        rng = new System.Random(Environment.TickCount);

        maxHP = Mathf.Max(1f, data.maxHP);
        currHP = maxHP;
        isDead = false;
        isFighting = false;
        isVulnerable = false;
        isDashing = false;
        lastPattern = FinalBossPattern.None;
        nextDashSide = PlayerSide.Left;
        fightClock = 0f;
        contactReadyAt[0] = 0f;
        contactReadyAt[1] = 0f;
        PublishHP();

        StopAllCoroutines();
        ClearAttacks();
        StartCoroutine(EntranceRoutine());
    }

    /// <summary>
    /// 피해를 받는다 (등장 중·사망 후 무시)
    /// </summary>
    public void ApplyDamage(float _amount)
    {
        if (true == isDead || false == isVulnerable || _amount <= 0f)
        {
            return;
        }

        currHP = Mathf.Max(0f, currHP - _amount);
        PublishHP();
        OnDamaged?.Invoke(_amount);

        if (currHP <= 0f)
        {
            Die();
        }
    }

    private void PublishHP()
    {
        EventManager events = EventManager.Instance;
        if (null != events)
        {
            events.OnFinalBossHPChanged?.Invoke(currHP, maxHP);
        }
    }

    private void Die()
    {
        if (true == isDead)
        {
            return;
        }
        isDead = true;
        isVulnerable = false;
        isDashing = false;
        isFighting = false;
        StopAllCoroutines();
        ClearAttacks();
        OnDefeated?.Invoke();
        EventManager events = EventManager.Instance;
        if (null != events)
        {
            events.OnFinalBossDefeated?.Invoke();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 디버그: HP 를 최대치의 _ratio 로 맞춘다 (0 이하면 처치)
    /// </summary>
    public void DebugSetHPRatio(float _ratio)
    {
        if (true == isDead)
        {
            return;
        }
        currHP = Mathf.Clamp01(_ratio) * maxHP;
        PublishHP();
        if (currHP <= 0f)
        {
            Die();
        }
    }
#endif
    #endregion

    #region Update
    private void Update()
    {
        if (false == isFighting || true == isDead || null == director || null == Space)
        {
            return;
        }
        if (true == director.IsGameOver)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        fightClock += dt;
        UpdateMeteors(dt);
        UpdateFires(dt);
        UpdateContact();
    }

    private void UpdateContact()
    {
        if (false == isVulnerable)
        {
            return;
        }

        float damage = true == isDashing ? data.dashDamage : data.contactDamage;
        for (int i = 0; i < 2; ++i)
        {
            if (fightClock < contactReadyAt[i])
            {
                continue;
            }

            PlayerSide side = (PlayerSide)i;
            if (false == director.TryGetPlayerBossPos(side, out Vector2 player))
            {
                continue;
            }
            if (true == BossAttackResolver.CircleOverlapsCircle(Position, data.bodyRadius, player, data.playerHitRadius))
            {
                director.DamagePlayer(side, damage, Position);
                contactReadyAt[i] = fightClock + data.contactCooldown;
            }
        }
    }

    /// <summary>
    /// 원 범위 안 플레이어(양 진영)에게 데미지
    /// </summary>
    private void HitPlayersInCircle(Vector2 _center, float _radius, float _damage)
    {
        for (int i = 0; i < 2; ++i)
        {
            PlayerSide side = (PlayerSide)i;
            if (true == director.TryGetPlayerBossPos(side, out Vector2 player)
                && true == BossAttackResolver.CircleContainsPoint(_center, _radius, player, data.playerHitRadius))
            {
                director.DamagePlayer(side, _damage, _center);
            }
        }
    }
    #endregion

    #region 흐름
    private IEnumerator Wait(float _seconds)
    {
        float t = 0f;
        while (t < _seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator EntranceRoutine()
    {
        Rect stage = Space.StageRect;
        Vector2 from = new Vector2(stage.center.x, stage.yMax + data.bodyRadius + data.entryStartAbove);
        Vector2 to = new Vector2(stage.center.x, stage.yMax - data.entryStopFromTop);
        SetPosition(from);

        float t = 0f;
        float duration = Mathf.Max(0.01f, data.entryDuration);
        while (t < duration)
        {
            t += Time.deltaTime;
            SetPosition(Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetPosition(to);

        isFighting = true;
        isVulnerable = true;
        fightStartClock = fightClock;
        OnEntranceFinished?.Invoke();
        StartCoroutine(PatternLoop());
    }

    private IEnumerator PatternLoop()
    {
        while (false == isDead)
        {
            float interval = true == IsEnraged
                ? data.enrageInterval
                : FinalBossData.PhaseValue(data.patternInterval, Phase, 2.5f);
            yield return IdleFor(interval);

            FinalBossPattern pattern = FinalBossPatterns.PickPattern(lastPattern, Phase, data.p3ComboChance, Rand01(), Rand01());
            lastPattern = pattern;
            OnPatternStarted?.Invoke(pattern);

            switch (pattern)
            {
                case FinalBossPattern.Meteor:
                    yield return MeteorRoutine();
                    break;
                case FinalBossPattern.Dash:
                    yield return DashRoutine(true);
                    break;
                case FinalBossPattern.Laser:
                    yield return LaserRoutine(true);
                    break;
                case FinalBossPattern.LaserMeteor:
                    {
                        Coroutine meteor = StartCoroutine(MeteorRoutine());
                        yield return LaserRoutine(false);
                        yield return meteor;
                    }
                    break;
                case FinalBossPattern.DashMeteor:
                    yield return DashRoutine(false);
                    yield return MeteorRoutine();
                    break;
            }
        }
    }

    /// <summary>
    /// 대기: 좌우 진영을 번갈아 목표로 천천히 움직인다 (중앙선을 자주 넘게)
    /// </summary>
    private IEnumerator IdleFor(float _seconds)
    {
        Rect area = BossSpace.Inset(Space.SideRect(nextIdleSide), data.idleInset);
        nextIdleSide = nextIdleSide.Opposite();
        Vector2 target = new Vector2(
            Mathf.Lerp(area.xMin, area.xMax, Rand01()),
            Mathf.Lerp(area.center.y, area.yMax, Rand01()));

        float t = 0f;
        while (t < _seconds)
        {
            float dt = Time.deltaTime;
            t += dt;
            SetPosition(Vector2.MoveTowards(Position, target, data.idleMoveSpeed * dt));
            yield return null;
        }
    }

    private float Rand01()
    {
        return (float)rng.NextDouble();
    }

    private void SetPosition(Vector2 _pos)
    {
        transform.position = new Vector3(_pos.x, _pos.y, transform.position.z);
    }
    #endregion

    #region 운석
    private IEnumerator MeteorRoutine()
    {
        OnMeteorWindup?.Invoke();
        yield return Wait(data.meteorWindup);

        int phase = Phase;
        int count = FinalBossData.PhaseValue(data.meteorCountPerSide, phase, 3);
        float flight = data.Telegraph(data.meteorFlightTime, phase);
        bool fire = 2 <= phase;

        // P1 은 왼쪽 → 오른쪽 엇갈림, P2 부터 동시
        LaunchVolley(PlayerSide.Left, count, flight, fire);
        if (0 == phase && data.p1SideStagger > 0f)
        {
            yield return Wait(data.p1SideStagger);
        }
        LaunchVolley(PlayerSide.Right, count, flight, fire);
        yield return Wait(flight);
    }

    private void LaunchVolley(PlayerSide _side, int _count, float _flight, bool _fire)
    {
        Rect area = BossSpace.Inset(Space.SideRect(_side), data.meteorSideInset);
        for (int i = 0; i < _count; ++i)
        {
            Vector2 target;
            if (0 == i && true == director.TryGetPlayerBossPos(_side, out Vector2 player))
            {
                target = PlayerScreenClamp.Clamp(player, area);
            }
            else
            {
                target = new Vector2(Mathf.Lerp(area.xMin, area.xMax, Rand01()), Mathf.Lerp(area.yMin, area.yMax, Rand01()));
            }
            LaunchMeteor(target, _flight, _fire);
        }
    }

    private void LaunchMeteor(Vector2 _target, float _flight, bool _fire)
    {
        Meteor m = new Meteor();
        m.start = Position;
        m.target = _target;
        m.duration = Mathf.Max(0.01f, _flight);
        m.elapsed = 0f;
        m.leavesFire = _fire;

        // 발사 즉시 착탄 경고 원 (비행 동안 채워짐)
        m.telegraph = SpawnTelegraph(_target);
        m.telegraph.ShowRing(0, data.meteorRadius, data.warnColor, false);
        m.telegraph.ShowDisc(0f, data.fillColor);

        m.visual = GetVisual(ref meteorPool, meteorVisualPrefab);
        m.shadow = GetVisual(ref shadowPool, meteorShadowPrefab);
        meteors.Add(m);
        PlaceMeteor(m, 0f);
    }

    private void UpdateMeteors(float _dt)
    {
        for (int i = meteors.Count - 1; i >= 0; --i)
        {
            Meteor m = meteors[i];
            m.elapsed += _dt;
            float t = Mathf.Clamp01(m.elapsed / m.duration);
            m.telegraph.ShowDisc(data.meteorRadius * t, data.fillColor);
            PlaceMeteor(m, t);

            if (t < 1f)
            {
                continue;
            }

            meteors.RemoveAt(i);
            ReleaseMeteor(m);
            HitPlayersInCircle(m.target, data.meteorRadius, data.meteorDamage);
            OnMeteorImpact?.Invoke(m.target, data.meteorRadius);
            if (true == m.leavesFire)
            {
                AddFire(m.target);
            }
        }
    }

    // 지면 궤적(그림자)은 직선, 본체는 포물선 높이를 더한다
    private void PlaceMeteor(Meteor _m, float _t)
    {
        Vector2 ground = Vector2.Lerp(_m.start, _m.target, _t);
        if (null != _m.shadow)
        {
            _m.shadow.transform.position = ground;
        }
        if (null != _m.visual)
        {
            float height = data.meteorArcHeight * 4f * _t * (1f - _t);
            _m.visual.transform.position = ground + Vector2.up * height;
        }
    }

    private void ReleaseMeteor(Meteor _m)
    {
        ReleaseTelegraph(_m.telegraph);
        _m.telegraph = null;
        ReleaseVisual(meteorPool, _m.visual);
        ReleaseVisual(shadowPool, _m.shadow);
        _m.visual = null;
        _m.shadow = null;
    }

    private void AddFire(Vector2 _center)
    {
        FireZone f = new FireZone();
        f.center = _center;
        f.remaining = data.fireDuration;
        f.tickTimer = 0f;
        f.telegraph = SpawnTelegraph(_center);
        f.telegraph.ShowDisc(data.fireRadius, data.fireColor);
        fires.Add(f);
    }

    private void UpdateFires(float _dt)
    {
        for (int i = fires.Count - 1; i >= 0; --i)
        {
            FireZone f = fires[i];
            f.remaining -= _dt;
            f.tickTimer -= _dt;
            if (f.tickTimer <= 0f)
            {
                f.tickTimer = Mathf.Max(0.05f, data.fireTickInterval);
                HitPlayersInCircle(f.center, data.fireRadius, data.fireDamage);
            }

            if (f.remaining <= 0f)
            {
                ReleaseTelegraph(f.telegraph);
                fires.RemoveAt(i);
            }
        }
    }
    #endregion

    #region 돌진
    private IEnumerator DashRoutine(bool _rest)
    {
        PlayerSide side = nextDashSide;
        nextDashSide = side.Opposite();

        Vector2 target;
        if (false == director.TryGetPlayerBossPos(side, out target))
        {
            target = Space.SideRect(side).center;
        }
        Vector2 dir = target - Position;
        dir = dir.sqrMagnitude < 0.0001f ? Vector2.down : dir.normalized;

        // 예고: 보스에서 대상 쪽 화살표(선)
        BossTelegraph arrow = SpawnTelegraph(Position);
        arrow.ShowLine(Position, Position + dir * data.dashArrowLength, data.dashArrowWidth, data.arrowColor);
        yield return Wait(data.Telegraph(data.dashTelegraph, Phase));
        ReleaseTelegraph(arrow);

        isDashing = true;
        OnDashStarted?.Invoke(dir);

        Rect bounds = BossSpace.Inset(Space.StageRect, data.bodyRadius);
        float t = 0f;
        int bounces = 0;
        while (t < data.dashMaxDuration && bounces < data.dashMaxBounces)
        {
            float dt = Time.deltaTime;
            t += dt;
            Vector2 pos = Position + dir * data.dashSpeed * dt;
            if (true == BossMath.Bounce(pos, dir, bounds, out Vector2 newDir))
            {
                dir = newDir;
                ++bounces;
                pos = PlayerScreenClamp.Clamp(pos, bounds);
            }
            SetPosition(pos);
            yield return null;
        }

        isDashing = false;
        OnDashEnded?.Invoke();

        if (true == _rest)
        {
            yield return Wait(data.dashRest);
        }
    }
    #endregion

    #region 레이저
    /// <summary>
    /// 레이저. 배치에 실패하면(0줄) 경고 후 _fallback 이면 바로 돌진으로 대체한다 (조합 패턴은 운석이 이미 진행 중이라 대체 없음)
    /// </summary>
    private IEnumerator LaserRoutine(bool _fallback)
    {
        int phase = Phase;
        int count = FinalBossData.PhaseValue(data.laserCount, phase, 3);
        int made = FinalBossPatterns.GenerateLasers(laserLines, count, Space.StageRect,
            Space.SideRect(PlayerSide.Left), Space.SideRect(PlayerSide.Right),
            data.laserWidth, data.laserSafeGap, data.laserMaxAttempts, data.laserGapSampleStep, rng, data.laserCoarseStep);
        if (made <= 0)
        {
            Debug.LogWarning("[FinalBoss] 안전 틈을 보장하는 레이저 배치를 만들지 못했습니다. 다른 패턴으로 대체합니다.");
            if (true == _fallback)
            {
                lastPattern = FinalBossPattern.Dash;
                OnPatternStarted?.Invoke(FinalBossPattern.Dash);
                yield return DashRoutine(true);
            }
            yield break;
        }

        laserTelegraphs.Clear();
        for (int i = 0; i < laserLines.Count; ++i)
        {
            BossTelegraph line = SpawnTelegraph(Position);
            laserTelegraphs.Add(line);
        }

        // 경고선
        SetLasers(data.laserWarnWidth, data.warnColor, true);
        yield return Wait(data.Telegraph(data.laserWarn, phase));

        // 깜빡임
        float t = 0f;
        float interval = Mathf.Max(0.01f, data.laserBlinkInterval);
        while (t < data.laserBlink)
        {
            t += Time.deltaTime;
            SetLasers(data.laserWarnWidth, data.warnColor, 0 == ((int)(t / interval)) % 2);
            yield return null;
        }

        // 발사 (진영당 한 번만 맞는다)
        SetLasers(data.laserWidth, data.laserColor, true);
        OnLaserFired?.Invoke();
        bool hitLeft = false;
        bool hitRight = false;
        t = 0f;
        while (t < data.laserFire)
        {
            t += Time.deltaTime;
            hitLeft = hitLeft || TryLaserHit(PlayerSide.Left);
            hitRight = hitRight || TryLaserHit(PlayerSide.Right);
            yield return null;
        }

        foreach (BossTelegraph line in laserTelegraphs)
        {
            ReleaseTelegraph(line);
        }
        laserTelegraphs.Clear();
    }

    private void SetLasers(float _width, Color _color, bool _visible)
    {
        for (int i = 0; i < laserTelegraphs.Count && i < laserLines.Count; ++i)
        {
            if (true == _visible)
            {
                laserTelegraphs[i].ShowLine(laserLines[i].a, laserLines[i].b, _width, _color);
            }
            else
            {
                laserTelegraphs[i].HideLine();
            }
        }
    }

    private bool TryLaserHit(PlayerSide _side)
    {
        if (false == director.TryGetPlayerBossPos(_side, out Vector2 player))
        {
            return false;
        }
        for (int i = 0; i < laserLines.Count; ++i)
        {
            if (true == BossAttackResolver.CapsuleContainsPoint(laserLines[i].a, laserLines[i].b, data.laserWidth, player, data.playerHitRadius))
            {
                director.DamagePlayer(_side, data.laserDamage, player);
                return true;
            }
        }
        return false;
    }
    #endregion

    #region 예고·시각 풀
    private BossTelegraph SpawnTelegraph(Vector2 _center)
    {
        BossTelegraph t = BossTelegraph.Spawn(data.telegraphStyle, bossLayer, _center);
        telegraphs.Add(t);
        return t;
    }

    private void ReleaseTelegraph(BossTelegraph _t)
    {
        if (null == _t)
        {
            return;
        }
        telegraphs.Remove(_t);
        _t.Release();
    }

    private GameObject GetVisual(ref ObjectPool<GameObject> _pool, GameObject _prefab)
    {
        if (null == _prefab)
        {
            return null;
        }
        if (null == _pool)
        {
            _pool = new ObjectPool<GameObject>(
                () =>
                {
                    GameObject go = Instantiate(_prefab);
                    LayerUtils.SetLayer(go, bossLayer);
                    return go;
                },
                (go) => go.SetActive(true),
                (go) => go.SetActive(false),
                (go) =>
                {
                    if (null != go)
                    {
                        Destroy(go);
                    }
                },
                false, 16, 64);
        }
        return _pool.Get();
    }

    private static void ReleaseVisual(ObjectPool<GameObject> _pool, GameObject _go)
    {
        if (null == _pool || null == _go)
        {
            return;
        }
        _pool.Release(_go);
    }

    /// <summary>
    /// 진행 중인 운석·불장판·예고를 모두 거둔다
    /// </summary>
    private void ClearAttacks()
    {
        foreach (Meteor m in meteors)
        {
            ReleaseMeteor(m);
        }
        meteors.Clear();

        foreach (FireZone f in fires)
        {
            ReleaseTelegraph(f.telegraph);
        }
        fires.Clear();

        laserTelegraphs.Clear();
        foreach (BossTelegraph t in telegraphs)
        {
            if (null != t)
            {
                t.Release();
            }
        }
        telegraphs.Clear();
        isDashing = false;
    }

    private void OnDisable()
    {
        ClearAttacks();
    }

    private void OnDestroy()
    {
        meteorPool?.Clear();
        shadowPool?.Clear();
    }
    #endregion
}
