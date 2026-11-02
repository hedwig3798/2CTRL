using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 몬스터 보정 종류 (레벨업 보상으로 Spawner 의 비율을 바꿀 때 사용)
/// - HP / 데미지 / 속도 비율은 스폰될 때 BlackBoard 로 전달하므로, 보상으로 바뀐 비율은 이후 스폰되는 몬스터부터 적용된다
/// </summary>
public enum SpawnModifierType
{
    /// <summary>
    /// 개체수 비율 (스폰 간격 = spawnInterval / 비율)
    /// </summary>
    Count,
    HP,
    Damage,
    Speed,
}

/// <summary>
/// spawable ��ü�� �����ϴ� ������
/// </summary>
public class Spawner
    : MonoBehaviour
{
    [SerializeField]
    [Header("Managers")]
    private DropManager dropManager;

    [SerializeField]
    [Header("Spawn Setting")]
    private Transform spawnLocation;

    [SerializeField]
    private WaveData[] waveArray;

    private Dictionary<Spawnable, IObjectPool<Spawnable>> poolDict
        = new Dictionary<Spawnable, IObjectPool<Spawnable>>();

    /// <summary>
    /// 생성한 몬스터의 외형 대상과 프리팹 기본 외형 (외형을 되돌릴 때 사용)
    /// </summary>
    private class SkinTarget
    {
        public MonsterData data;
        public Animator animator;
        public SpriteRenderer spriteRenderer;
        public RuntimeAnimatorController originalController;
        public Sprite originalSprite;
        public bool isSkinned;
    }

    // MonsterData -> 이 스포너에서 쓸 외형 (SetMonsterSkins)
    private readonly Dictionary<MonsterData, MonsterSkin> skinMap = new Dictionary<MonsterData, MonsterSkin>();
    private readonly Dictionary<Spawnable, SkinTarget> skinTargets = new Dictionary<Spawnable, SkinTarget>();

    //
    [Header("Spawn Data")]
    public Transform target;
    [Tooltip("기본 이동 속도 비율 (보상으로 보정됨)")]
    public float speedRate;
    [Tooltip("기본 HP 비율 (보상으로 보정됨)")]
    public float HPRate;
    [Tooltip("기본 데미지 비율 (보상으로 보정됨)")]
    public float damagerate;

    private static readonly int ModifierCount = System.Enum.GetValues(typeof(SpawnModifierType)).Length;

    // 보상으로 누적된 보정값. 최종값 = (base + ΣAdd) × (1 + ΣMultiply)
    private readonly float[] addRates = new float[ModifierCount];
    private readonly float[] mulRates = new float[ModifierCount];

    // 개체수 비율이 이 값보다 작아지지 않게 한다 (스폰 간격이 무한히 길어지지 않도록)
    private const float MinCountRate = 0.1f;

    /// <summary>
    /// 보상으로 몬스터 비율을 보정한다
    /// </summary>
    public void AddRate(SpawnModifierType _type, StatModifierMode _mode, float _value)
    {
        int i = (int)_type;
        switch (_mode)
        {
            case StatModifierMode.Add:
                addRates[i] += _value;
                break;
            case StatModifierMode.Multiply:
                mulRates[i] += _value;
                break;
        }

        Debug.Log($"[Spawner] {_type} {_mode} {_value:+0.##;-0.##} → {GetRate(_type):0.##}", this);
    }

    /// <summary>
    /// 보정이 적용된 최종 비율 (Count 의 base 는 1)
    /// </summary>
    public float GetRate(SpawnModifierType _type)
    {
        int i = (int)_type;
        float rate = (GetBaseRate(_type) + addRates[i]) * (1f + mulRates[i]);
        return SpawnModifierType.Count == _type ? Mathf.Max(MinCountRate, rate) : Mathf.Max(0f, rate);
    }

    private float GetBaseRate(SpawnModifierType _type)
    {
        switch (_type)
        {
            case SpawnModifierType.HP:
                return HPRate;
            case SpawnModifierType.Damage:
                return damagerate;
            case SpawnModifierType.Speed:
                return speedRate;
            default:
                return 1f;
        }
    }

    private void Awake()
    {
        PreparePools();
    }

    /// <summary>
    /// 웨이브를 바꾼다 (Start 전에 호출해야 첫 웨이브부터 적용된다)
    /// - 비어 있으면 기존 웨이브를 유지한다
    /// </summary>
    public void SetWaves(WaveData[] _waves)
    {
        if (null == _waves || 0 == _waves.Length)
        {
            Debug.LogWarning($"[Spawner] 넣을 웨이브가 비어 있어 기존 웨이브를 유지합니다. ({name})", this);
            return;
        }

        waveArray = _waves;
        PreparePools();
    }

    /// <summary>
    /// 이 스포너에서 나오는 몬스터의 외형을 바꾼다 (MonsterData 기준, 이후 스폰부터 적용)
    /// - 목록에 없는 몬스터는 프리팹 기본 외형을 쓴다
    /// </summary>
    public void SetMonsterSkins(MonsterSkin[] _skins)
    {
        skinMap.Clear();
        if (null == _skins)
        {
            return;
        }

        foreach (MonsterSkin skin in _skins)
        {
            if (null == skin.monster)
            {
                continue;
            }

            if (true == skinMap.ContainsKey(skin.monster))
            {
                Debug.LogWarning($"[Spawner] {skin.monster.name} 의 외형이 중복되어 뒤의 값을 씁니다.", this);
            }
            skinMap[skin.monster] = skin;
        }
    }

    /// <summary>
    /// 웨이브에 있는 프리팹의 풀을 미리 만든다
    /// </summary>
    private void PreparePools()
    {
        if (null == waveArray)
        {
            return;
        }

        foreach (var wd in waveArray)
        {
            if (null == wd || null == wd.spwanArray)
            {
                continue;
            }

            foreach (var sd in wd.spwanArray)
            {
                if (null == sd.spawnObject)
                {
                    continue;
                }

                GetPool(sd.spawnObject);
            }
        }
    }

    /// <summary>
    /// 프리팹의 풀. 없으면 새로 만든다 (웨이브에 없는 소환/분열 몬스터용)
    /// </summary>
    private IObjectPool<Spawnable> GetPool(Spawnable _prefab)
    {
        if (true == poolDict.TryGetValue(_prefab, out IObjectPool<Spawnable> pool))
        {
            return pool;
        }

        pool = new ObjectPool<Spawnable>
            (
                createFunc: () => CreateObject(_prefab)
                , OnSpawn
                , OnRelease
                , OnDespawn
                , true
                , 100
                , 200
            );
        poolDict[_prefab] = pool;
        return pool;
    }

    /// <summary>
    /// 지정한 위치에 몬스터를 스폰하고 현재 보정값으로 초기화한다
    /// 실패하면 null
    /// </summary>
    public Spawnable SpawnAt(Spawnable _prefab, Vector2 _position)
    {
        if (null == _prefab)
        {
            return null;
        }

        IObjectPool<Spawnable> pool = GetPool(_prefab);
        Spawnable sa = pool.Get();
        if (null == sa)
        {
            return null;
        }

        sa.transform.position = _position;
        ApplySkin(sa);

        if (null == sa.blackBoardHandler)
        {
            Debug.LogError("it has no BlackBoardHandler");
            pool.Release(sa);
            return null;
        }

        BlackBoard data = sa.blackBoardHandler.GetBlackBoard();
        if (null == data)
        {
            Debug.LogError("BlackBoardHandler has no data");
            pool.Release(sa);
            return null;
        }

        data.dropManager = dropManager;
        data.spawner = this;
        data.SetFloat(DATA_TYPE.HPRate, GetRate(SpawnModifierType.HP));
        data.SetFloat(DATA_TYPE.moveSpeedRate, GetRate(SpawnModifierType.Speed));
        data.SetFloat(DATA_TYPE.damageRate, GetRate(SpawnModifierType.Damage));
        data.SetTransform(DATA_TYPE.moveTarget, target);
        sa.blackBoardHandler.Initialize();

        return sa;
    }

    /// <summary>
    /// MonsterData 에 맞는 외형을 적용한다. 외형이 없으면 프리팹 기본값으로 되돌린다
    /// - 본체(SpriteRenderer 오브젝트) 에 Animator 가 있으면 컨트롤러만 바꾸고 스프라이트는 건드리지 않는다 (애니메이션이 덮어쓰므로)
    /// - Animator 가 없으면 스프라이트를 바꾼다
    /// </summary>
    private void ApplySkin(Spawnable _object)
    {
        if (false == skinTargets.TryGetValue(_object, out SkinTarget target))
        {
            return;
        }

        bool hasSkin = skinMap.TryGetValue(target.data, out MonsterSkin skin);
        if (false == hasSkin && false == target.isSkinned)
        {
            return;
        }
        target.isSkinned = hasSkin;

        if (null != target.animator)
        {
            RuntimeAnimatorController controller = null != skin.animator ? skin.animator : target.originalController;
            // 같은 컨트롤러를 다시 넣으면 애니메이터 상태가 초기화되므로 바뀔 때만 넣는다
            if (controller != target.animator.runtimeAnimatorController)
            {
                target.animator.runtimeAnimatorController = controller;
            }
        }
        else if (null != target.spriteRenderer)
        {
            target.spriteRenderer.sprite = null != skin.sprite ? skin.sprite : target.originalSprite;
        }
    }

    /// <summary>
    /// 생성한 몬스터의 외형 대상과 기본 외형을 기억한다 (MonsterData 가 없으면 외형을 바꾸지 않는다)
    /// </summary>
    private void RegisterSkinTarget(Spawnable _object)
    {
        MonsterBrain brain = _object.GetComponentInChildren<MonsterBrain>(true);
        if (null == brain || null == brain.Data)
        {
            return;
        }

        SkinTarget target = new SkinTarget();
        target.data = brain.Data;
        target.spriteRenderer = null != brain.Context ? brain.Context.spriteRenderer : null;
        if (null == target.spriteRenderer)
        {
            target.spriteRenderer = _object.GetComponentInChildren<SpriteRenderer>(true);
        }
        // 다른 자식의 Animator 를 잡지 않도록 본체(SpriteRenderer) 오브젝트의 Animator 만 쓴다
        target.animator = null != target.spriteRenderer ? target.spriteRenderer.GetComponent<Animator>() : null;
        target.originalController = null != target.animator ? target.animator.runtimeAnimatorController : null;
        target.originalSprite = null != target.spriteRenderer ? target.spriteRenderer.sprite : null;
        skinTargets[_object] = target;
    }

    private void Start()
    {
        if (null == waveArray || 0 == waveArray.Length)
        {
            Debug.LogError("[Spawner] WaveData�� �����ϴ�.");
            return;
        }

        StartCoroutine(Wave());
    }

    private Spawnable CreateObject(Spawnable _sa)
    {
        Spawnable sa = Instantiate(_sa);
        sa.SetPool(GetPool(_sa));

        Transform[] transforms = sa.gameObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in transforms)
        {
            t.gameObject.layer = gameObject.layer;
        }

        if (null != sa.blackBoardHandler)
        {
            BlackBoard data = sa.blackBoardHandler.GetBlackBoard();
            data.dropManager = dropManager;
        }

        RegisterSkinTarget(sa);

        return sa;
    }

    private void OnSpawn(Spawnable _object)
    {
        _object.gameObject.SetActive(true);
    }

    private void OnRelease(Spawnable _object)
    {
        _object.MarkReturningToPool();
        if (_object.gameObject.activeSelf)
        {
            _object.gameObject.SetActive(false);
        }
    }

    private void OnDespawn(Spawnable _object)
    {
        skinTargets.Remove(_object);
        Destroy(_object.gameObject);
    }

    IEnumerator Spawn(SpawnData _data)
    {
        if (null == _data.spawnObject)
        {
            yield break;
        }

        Transform origin = null != spawnLocation ? spawnLocation : transform;
        float interval = -1f;
        WaitForSeconds flag = null;
        while (true)
        {
            // 개체수 보상으로 비율이 바뀌었을 때만 대기 시간을 다시 만든다
            float newInterval = Mathf.Max(0.01f, _data.spawnInterval / GetRate(SpawnModifierType.Count));
            if (false == Mathf.Approximately(interval, newInterval))
            {
                interval = newInterval;
                flag = new WaitForSeconds(interval);
            }

            float dis = Random.Range(_data.spawnRange.x, _data.spawnRange.y);
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }

            Vector2 spawnPos = origin.position;
            spawnPos += dir * dis;

            SpawnAt(_data.spawnObject, spawnPos);

            yield return flag;
        }
    }

    IEnumerator Wave()
    {
        List<Coroutine> spawCoroutine = new List<Coroutine>();

        for (int i = 0; i < waveArray.Length; i++)
        {
            if (null == waveArray[i] || null == waveArray[i].spwanArray)
            {
                continue;
            }

            foreach (Coroutine c in spawCoroutine)
            {
                if (null != c)
                {
                    StopCoroutine(c);
                }
            }
            spawCoroutine.Clear();

            for (int j = 0; j < waveArray[i].spwanArray.Length; j++)
            {
                spawCoroutine.Add(StartCoroutine(Spawn(waveArray[i].spwanArray[j])));
            }

            yield return new WaitForSeconds(waveArray[i].time);
        }
    }
}
