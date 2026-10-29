using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 몬스터 PlayMode 테스트용 오브젝트 조립 / 정리 도우미
/// 프리팹 없이 GameObject 와 ScriptableObject.CreateInstance 로 만든다
/// </summary>
public static class MonsterTestFactory
{
    /// <summary>플레이어(공격 대상) 더미가 쓰는 레이어. 몬스터는 기본 레이어(0)</summary>
    public const int TargetLayer = 30;
    public static LayerMask TargetMask => 1 << TargetLayer;

    /// <summary>몬스터 루트 레이어. 프로젝트 충돌 매트릭스에 의존하지 않도록 TargetLayer 와의 충돌을 직접 켠다</summary>
    public const int MonsterLayer = 27;
    private static bool savedIgnore;
    private static bool hasSaved;

    private static readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    public class Rig
    {
        public GameObject root;
        public GameObject body;
        public MonsterBrain brain;
        public HealthSystem health;
        public DamagePipeline pipeline;
        public Knockback knockback;
        public SpriteRenderer sprite;
        public ContactDamage contact;
        public BlackBoardHandler handler;
        public Spawnable spawnable;
        public MonsterData data;

        public Vector2 Pos => root.transform.position;
    }

    public class StateLog
    {
        public readonly List<MonsterStateId> States = new List<MonsterStateId>();
        public readonly List<float> Times = new List<float>();

        public StateLog(MonsterBrain _brain)
        {
            _brain.OnStateChanged += (id) =>
            {
                States.Add(id);
                Times.Add(Time.time);
            };
        }

        public int Count(MonsterStateId _id)
        {
            int n = 0;
            foreach (MonsterStateId s in States)
            {
                if (s == _id)
                {
                    ++n;
                }
            }
            return n;
        }
    }

    public static T So<T>() where T : ScriptableObject
    {
        T so = ScriptableObject.CreateInstance<T>();
        created.Add(so);
        return so;
    }

    public static MonsterData NewData(float _speed, params MonsterKeyword[] _keywords)
    {
        MonsterData d = So<MonsterData>();
        d.baseMoveSpeed = _speed;
        d.targetLayer = TargetMask;
        d.keywords = _keywords;
        return d;
    }

    /// <summary>
    /// 몬스터 하나를 조립한다. Initialize 는 호출하지 않는다 (Init 사용)
    /// </summary>
    public static Rig Create(
        MonsterData _data
        , Vector2 _pos
        , float _maxHP = 100f
        , bool _contact = true
        , bool _activate = true
        , bool _spawnable = false)
    {
        Rig r = new Rig();
        r.data = _data;

        if (false == hasSaved)
        {
            savedIgnore = Physics2D.GetIgnoreLayerCollision(MonsterLayer, TargetLayer);
            hasSaved = true;
        }
        Physics2D.IgnoreLayerCollision(MonsterLayer, TargetLayer, false);

        r.root = new GameObject("TestMonster");
        r.root.layer = MonsterLayer;
        r.root.SetActive(false);
        r.root.transform.position = _pos;
        created.Add(r.root);

        r.body = new GameObject("body");
        r.body.transform.SetParent(r.root.transform, false);
        r.sprite = r.body.AddComponent<SpriteRenderer>();
        CircleCollider2D col = r.body.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        r.health = r.body.AddComponent<HealthSystem>();
        r.health.maxHP = _maxHP;
        r.health.currHP = _maxHP;
        r.pipeline = r.body.AddComponent<DamagePipeline>();
        r.knockback = r.body.AddComponent<Knockback>();

        if (_contact)
        {
            GameObject atk = new GameObject("attack");
            atk.transform.SetParent(r.root.transform, false);
            r.contact = atk.AddComponent<ContactDamage>();
            r.contact.owner = r.pipeline;
        }

        r.handler = r.root.AddComponent<BlackBoardHandler>();
        if (_spawnable)
        {
            r.spawnable = r.root.AddComponent<Spawnable>();
            r.spawnable.blackBoardHandler = r.handler;
        }

        r.brain = r.root.AddComponent<MonsterBrain>();
        FieldInfo f = typeof(MonsterBrain).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
        f.SetValue(r.brain, _data);

        if (_activate)
        {
            r.root.SetActive(true);
        }
        return r;
    }

    public static BlackBoard MakeBoard(Transform _target, float _speedRate = 1f, float _damageRate = 1f, Spawner _spawner = null)
    {
        BlackBoard bb = new BlackBoard();
        if (null != _target)
        {
            bb.SetTransform(DATA_TYPE.moveTarget, _target);
        }
        bb.SetFloat(DATA_TYPE.moveSpeedRate, _speedRate);
        bb.SetFloat(DATA_TYPE.damageRate, _damageRate);
        bb.spawner = _spawner;
        return bb;
    }

    /// <summary>
    /// HealthSystem → MonsterBrain 순으로 Initialize 한다
    /// </summary>
    public static BlackBoard Init(Rig _r, Transform _target, float _speedRate = 1f, float _damageRate = 1f, Spawner _spawner = null)
    {
        BlackBoard bb = MakeBoard(_target, _speedRate, _damageRate, _spawner);
        _r.health.Initialize(bb);
        _r.brain.Initialize(bb);
        return bb;
    }

    /// <summary>
    /// 공격 대상 더미 (플레이어 레이어, 체력 + 파이프라인). _extraColliders 개의 콜라이더를 자식에 더 단다
    /// </summary>
    public static GameObject CreateTarget(Vector2 _pos, float _hp = 100f, int _extraColliders = 0)
    {
        GameObject go = new GameObject("TestTarget");
        go.SetActive(false);
        go.layer = TargetLayer;
        go.transform.position = _pos;
        created.Add(go);

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        HealthSystem hs = go.AddComponent<HealthSystem>();
        hs.maxHP = _hp;
        hs.currHP = _hp;
        go.AddComponent<DamagePipeline>();

        for (int i = 0; i < _extraColliders; ++i)
        {
            GameObject child = new GameObject("extra" + i);
            child.layer = TargetLayer;
            child.transform.SetParent(go.transform, false);
            child.AddComponent<CircleCollider2D>().radius = 0.2f;
        }

        go.SetActive(true);
        Physics2D.SyncTransforms();
        return go;
    }

    /// <summary>
    /// 비활성 상태의 Spawner. Awake/Start(웨이브 없음 에러)를 피하고 SpawnAt 만 쓴다
    /// </summary>
    public static Spawner CreateSpawner(Transform _target)
    {
        GameObject go = new GameObject("TestSpawner");
        go.SetActive(false);
        created.Add(go);
        Spawner sp = go.AddComponent<Spawner>();
        sp.target = _target;
        return sp;
    }

    public static Transform CreateMarker(Vector2 _pos)
    {
        GameObject go = new GameObject("Marker");
        go.transform.position = _pos;
        created.Add(go);
        return go.transform;
    }

    public static void Track(UnityEngine.Object _o)
    {
        created.Add(_o);
    }

    /// <summary>
    /// 조건이 참이 되거나 시간이 지날 때까지 프레임을 넘긴다. 호출 뒤 조건을 직접 단언한다
    /// </summary>
    public static IEnumerator WaitUntil(Func<bool> _cond, float _timeout = 3f)
    {
        float t = 0f;
        while (false == _cond() && t < _timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// _seconds 동안 프레임을 넘긴다 (Time.deltaTime 누적)
    /// </summary>
    public static IEnumerator Wait(float _seconds)
    {
        float t = 0f;
        while (t < _seconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    public static int CountAlive(MonsterData _data)
    {
        int n = 0;
        foreach (MonsterBrain b in UnityEngine.Object.FindObjectsByType<MonsterBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (b.Data == _data && null != b.Context && false == b.Context.IsDead)
            {
                ++n;
            }
        }
        return n;
    }

    public static List<MonsterBrain> FindActive(MonsterData _data)
    {
        List<MonsterBrain> list = new List<MonsterBrain>();
        foreach (MonsterBrain b in UnityEngine.Object.FindObjectsByType<MonsterBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (b.Data == _data)
            {
                list.Add(b);
            }
        }
        return list;
    }

    public static void Cleanup()
    {
        if (true == hasSaved)
        {
            Physics2D.IgnoreLayerCollision(MonsterLayer, TargetLayer, savedIgnore);
            hasSaved = false;
        }
        // 풀 반환 콜백이 파괴 중에 돌지 않게 먼저 표시한다
        foreach (Spawnable s in UnityEngine.Object.FindObjectsByType<Spawnable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            s.MarkReturningToPool();
        }
        foreach (HazardZone z in UnityEngine.Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            z.SetPool(null);
        }

        foreach (MonsterBrain b in UnityEngine.Object.FindObjectsByType<MonsterBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (null != b)
            {
                UnityEngine.Object.DestroyImmediate(b.gameObject);
            }
        }
        foreach (HazardZone z in UnityEngine.Object.FindObjectsByType<HazardZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (null != z)
            {
                UnityEngine.Object.DestroyImmediate(z.gameObject);
            }
        }

        foreach (UnityEngine.Object o in created)
        {
            if (null != o)
            {
                UnityEngine.Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }
}
