using UnityEngine;

/// <summary>
/// Lifetime 의 남은 수명 비율에 따라 SpriteRenderer 의 알파를 줄인다 (폭발 등 연출이 흐려지며 사라짐)
/// BlackBoard 가 없는 연출(PooledEffect)에서도 동작하도록 Initializable 없이 Awake 에서 참조를 찾는다
/// </summary>
public class FadeOverLifetime
    : MonoBehaviour
{
    [Header("lifetime")]
    [Tooltip("비워두면 같은 오브젝트(또는 부모)의 Lifetime")]
    public Lifetime lifetime;

    [Header("renderer")]
    [Tooltip("알파를 바꿀 렌더러. 비워두면 같은 오브젝트의 SpriteRenderer")]
    public SpriteRenderer target;
    [Tooltip("x: 수명 진행률 (0 ~ 1), y: 알파 배율")]
    public AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private Color baseColor = Color.white;

    private void Awake()
    {
        if (null == target)
        {
            target = GetComponent<SpriteRenderer>();
        }
        if (null == lifetime)
        {
            lifetime = GetComponentInParent<Lifetime>();
        }

        if (null != target)
        {
            baseColor = target.color;
        }
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (null == target)
        {
            return;
        }

        float progress = null != lifetime ? 1f - lifetime.RemainRate : 1f;
        Color c = baseColor;
        c.a *= Mathf.Clamp01(alphaCurve.Evaluate(progress));
        target.color = c;
    }
}
