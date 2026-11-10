using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 한 번 재생하고 사라지는 연출 (폭발, 착탄 등)
/// EffectPool.Spawn 으로 꺼내고, 비활성화되면 풀로 돌아간다
/// 보통 Lifetime 과 함께 붙여 수명이 끝나면 꺼지게 한다
/// </summary>
public class PooledEffect
    : MonoBehaviour
{
    [Header("scale")]
    [Tooltip("크기 1 일 때의 반지름. EffectPool.Spawn 에 반지름을 주면 크기 = 반지름 / unitRadius")]
    [Min(0.01f)]
    public float unitRadius = 0.5f;

    private IObjectPool<PooledEffect> pool;
    private bool returningToPool;
    private Vector3 baseScale = Vector3.one;

    public void SetPool(IObjectPool<PooledEffect> _pool)
    {
        pool = _pool;
    }

    /// <summary>
    /// 반지름에 맞춰 크기를 정한다. 0 이하면 프리팹 크기 그대로
    /// </summary>
    public void SetRadius(float _radius)
    {
        transform.localScale = _radius > 0f ? baseScale * (_radius / unitRadius) : baseScale;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        returningToPool = false;
    }

    private void OnDisable()
    {
        if (returningToPool || null == pool)
        {
            return;
        }

        returningToPool = true;
        pool.Release(this);
    }
}

/// <summary>
/// PooledEffect 프리팹별 오브젝트 풀
/// </summary>
public static class EffectPool
{
    private static Dictionary<PooledEffect, IObjectPool<PooledEffect>> pools = new Dictionary<PooledEffect, IObjectPool<PooledEffect>>();

    /// <summary>
    /// 연출을 꺼냈을 때 (프리팹, 위치, 레이어). 연출 소리 재생용 (CombatSoundBinder 가 구독)
    /// </summary>
    public static event System.Action<PooledEffect, Vector3, int> Spawned;

    /// <summary>
    /// _prefab 연출을 _position 에 꺼낸다. _radius 가 0 보다 크면 그 반지름에 맞춰 크기를 바꾼다
    /// _layer 로 자식까지 레이어를 맞춘다. 분할 카메라는 Default 레이어를 그리지 않으므로 반드시 연출을 낸 쪽의 레이어를 넘긴다
    /// _prefab 이 없으면 null
    /// </summary>
    public static PooledEffect Spawn(PooledEffect _prefab, Vector3 _position, float _radius, int _layer)
    {
        if (null == _prefab)
        {
            return null;
        }

        if (false == pools.TryGetValue(_prefab, out IObjectPool<PooledEffect> pool))
        {
            pool = CreatePool(_prefab);
            pools.Add(_prefab, pool);
        }

        // 씬이 바뀌어 파괴된 오브젝트가 풀에 남아 있으면 건너뛴다
        PooledEffect effect = pool.Get();
        while (null == effect)
        {
            effect = pool.Get();
        }

        effect.transform.position = _position;
        effect.SetRadius(_radius);
        if (effect.gameObject.layer != _layer)
        {
            LayerUtils.SetLayer(effect.gameObject, _layer);
        }
        effect.gameObject.SetActive(true);
        Spawned?.Invoke(_prefab, _position, _layer);
        return effect;
    }

    // Enter Play Mode 옵션으로 도메인 리로드를 끈 경우를 위해 static 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Spawned = null;
    }

    private static IObjectPool<PooledEffect> CreatePool(PooledEffect _prefab)
    {
        IObjectPool<PooledEffect> pool = null;
        pool = new ObjectPool<PooledEffect>
            (
                createFunc: () =>
                {
                    PooledEffect created = Object.Instantiate(_prefab);
                    created.gameObject.SetActive(false);
                    created.SetPool(pool);
                    return created;
                }
                , actionOnGet: null
                , actionOnRelease: null
                , actionOnDestroy: (e) =>
                {
                    if (null != e)
                    {
                        Object.Destroy(e.gameObject);
                    }
                }
                , collectionCheck: false
                , defaultCapacity: 16
                , maxSize: 100
            );
        return pool;
    }
}
