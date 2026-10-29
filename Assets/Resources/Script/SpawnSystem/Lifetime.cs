using UnityEngine;

/// <summary>
/// 활성화된 뒤 일정 시간이 지나면 오브젝트를 비활성화한다.
/// 비활성화되면 Spawnable / Projectile 의 OnDisable 에서 풀로 반환된다.
/// 수명은 BlackBoard 의 lifetime 으로 덮어쓸 수 있다 (0 이하면 인스펙터 값)
/// </summary>
public class Lifetime
    : MonoBehaviour
    , Initializable
{
    [Header("lifetime")]
    [Tooltip("활성화 후 비활성화까지의 시간 (초). 0 이하면 사라지지 않는다")]
    public float lifetime = 1f;

    private float baseLifetime;
    private float elapsed;

    /// <summary>
    /// 남은 수명 비율 (1 -> 0). 수명이 없으면 1
    /// </summary>
    public float RemainRate => lifetime > 0f ? Mathf.Clamp01(1f - elapsed / lifetime) : 1f;

    /// <summary>
    /// 수명만 갱신한다 (경과 시간은 OnEnable 에서만 초기화)
    /// 유지형 무기가 발사체 수가 바뀔 때 다시 초기화해도 수명이 늘어나지 않도록
    /// </summary>
    public void Initialize(BlackBoard _data)
    {
        float value = _data.GetFloat(DATA_TYPE.lifetime);
        lifetime = value > 0f ? value : baseLifetime;
    }

    private void Awake()
    {
        baseLifetime = lifetime;
    }

    private void OnEnable()
    {
        elapsed = 0f;
    }

    private void Update()
    {
        if (lifetime <= 0f)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            gameObject.SetActive(false);
        }
    }
}
