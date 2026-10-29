using UnityEngine;

/// <summary>
/// Lifetime 의 진행률(0 -> 1)에 따라 크기를 바꾼다 (충격파 확장, 장판 등장 / 사라짐 등)
/// 크기 = 기본 크기 * curve(진행률) * maxScale
/// useAreaRadius 가 켜져 있으면 maxScale 대신 BlackBoard 의 areaRadius / unitRadius 를 쓴다 (콜라이더도 같이 커진다)
/// </summary>
public class ScaleOverLifetime
    : MonoBehaviour
    , Initializable
{
    [Header("lifetime")]
    [Tooltip("비워두면 같은 오브젝트(또는 부모)의 Lifetime")]
    public Lifetime lifetime;
    [Tooltip("크기를 바꿀 대상. 비워두면 이 오브젝트")]
    public Transform target;

    [Header("scale")]
    [Tooltip("x: 수명 진행률 (0 ~ 1), y: 크기 배율")]
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Min(0f)]
    public float maxScale = 1f;

    [Header("area radius")]
    [Tooltip("켜면 maxScale 을 BlackBoard 의 areaRadius / unitRadius 로 정한다 (areaRadius 가 0 이하면 maxScale 그대로)")]
    public bool useAreaRadius;
    [Tooltip("크기 1 일 때의 반지름")]
    [Min(0.01f)]
    public float unitRadius = 0.5f;

    private Vector3 baseScale = Vector3.one;
    private float baseMaxScale;
    private float currMaxScale;

    public void Initialize(BlackBoard _data)
    {
        currMaxScale = baseMaxScale;
        if (true == useAreaRadius)
        {
            float radius = _data.GetFloat(DATA_TYPE.areaRadius);
            if (radius > 0f)
            {
                currMaxScale = radius / unitRadius;
            }
        }
        Apply();
    }

    private void Awake()
    {
        if (null == target)
        {
            target = transform;
        }
        if (null == lifetime)
        {
            lifetime = GetComponentInParent<Lifetime>();
        }

        baseScale = target.localScale;
        baseMaxScale = maxScale;
        currMaxScale = maxScale;
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
        float progress = null != lifetime ? 1f - lifetime.RemainRate : 1f;
        target.localScale = baseScale * (curve.Evaluate(progress) * currMaxScale);
    }
}
