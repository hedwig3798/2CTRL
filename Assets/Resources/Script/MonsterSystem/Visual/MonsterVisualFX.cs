using UnityEngine;

/// <summary>
/// 몬스터 상태/예고 연출 (색만 다룬다. 스케일은 건드리지 않는다)
/// - Telegraph: 키워드별 색으로 깜빡임 (자폭은 점점 빨라진다)
/// - Downed: 회색 + 반투명
/// - HP바(UI 자식): 루트 스케일을 상쇄해 크기가 커지지 않게 한다
/// 알파는 다른 곳(순간이동 페이드 등)이 쓰는 값을 존중한다: 외부에서 바뀐 알파를 읽어 곱해서 쓴다
/// </summary>
[DisallowMultipleComponent]
public class MonsterVisualFX
    : MonoBehaviour
{
    [Tooltip("색 연출 대상. 비우면 MonsterBrain 의 스프라이트")]
    [SerializeField]
    private SpriteRenderer target;

    [Tooltip("루트 스케일을 상쇄할 HP바 루트. 비우면 'UI' 자식")]
    [SerializeField]
    private Transform hpBar;

    [SerializeField]
    private Color telegraphColor = new Color(1f, 0.15f, 0.1f, 1f);

    [SerializeField]
    private Color downedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Range(0f, 1f)]
    [SerializeField]
    private float downedAlpha = 0.5f;

    [Min(0.1f)]
    [SerializeField]
    private float blinkHz = 6f;

    private MonsterBrain brain;
    private Vector3 baseRgb = Vector3.one;
    private float ownedAlpha = 1f;
    private float lastWrittenAlpha = 1f;
    private MonsterStateId state = MonsterStateId.Move;
    private float stateTime;
    private float blinkSpeedUp;
    private Color currTelegraph;
    private Vector3 hpBaseScale = Vector3.one;
    private bool hpCaptured;

    private void Awake()
    {
        brain = GetComponent<MonsterBrain>();
        if (null == target)
        {
            target = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (null != target)
        {
            Color c = target.color;
            baseRgb = new Vector3(c.r, c.g, c.b);
            ownedAlpha = c.a;
            lastWrittenAlpha = c.a;
        }
        if (null == hpBar)
        {
            hpBar = transform.Find("UI");
        }
        currTelegraph = telegraphColor;
    }

    private void OnEnable()
    {
        if (null != brain)
        {
            brain.OnStateChanged += HandleState;
            brain.OnActionTelegraph += HandleTelegraph;
        }
        state = MonsterStateId.Move;
        stateTime = 0f;
        FixHpBar();
        if (null != target)
        {
            lastWrittenAlpha = ownedAlpha = target.color.a;
        }
    }

    private void OnDisable()
    {
        if (null != brain)
        {
            brain.OnStateChanged -= HandleState;
            brain.OnActionTelegraph -= HandleTelegraph;
        }
        if (null != target)
        {
            Color c = target.color;
            target.color = new Color(baseRgb.x, baseRgb.y, baseRgb.z, c.a);
        }
    }

    private void HandleState(MonsterStateId _id)
    {
        state = _id;
        stateTime = 0f;
    }

    private void HandleTelegraph(KeywordRuntime _rt)
    {
        currTelegraph = telegraphColor;
        blinkSpeedUp = 0f;
        if (_rt is SelfDestructRuntime)
        {
            blinkSpeedUp = 2.5f;
        }
        else if (_rt is TeleportRuntime)
        {
            currTelegraph = new Color(0.85f, 0.5f, 1f, 1f);
        }
        else if (_rt is SummonerRuntime)
        {
            currTelegraph = new Color(0.4f, 0.9f, 1f, 1f);
        }
        else if (_rt is DashRuntime)
        {
            currTelegraph = new Color(1f, 0.85f, 0.2f, 1f);
        }
    }

    private void FixHpBar()
    {
        if (null == hpBar)
        {
            return;
        }
        if (false == hpCaptured)
        {
            hpBaseScale = hpBar.localScale;
            hpCaptured = true;
        }
        Vector3 s = transform.localScale;
        hpBar.localScale = new Vector3(
            hpBaseScale.x / Mathf.Max(0.0001f, Mathf.Abs(s.x))
            , hpBaseScale.y / Mathf.Max(0.0001f, Mathf.Abs(s.y))
            , hpBaseScale.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
    }

    private void LateUpdate()
    {
        if (null == target)
        {
            return;
        }

        stateTime += Time.deltaTime;

        // 다른 코드가 알파를 바꿨으면 그 값을 새 기준으로 삼는다
        Color cur = target.color;
        if (Mathf.Abs(cur.a - lastWrittenAlpha) > 0.0001f)
        {
            ownedAlpha = cur.a;
        }

        Vector3 rgb = baseRgb;
        float alphaMul = 1f;

        if (MonsterStateId.Telegraph == state)
        {
            float hz = blinkHz * (1f + blinkSpeedUp * Mathf.Clamp01(stateTime / 0.8f));
            float k = 0.5f + 0.5f * Mathf.Sin(stateTime * hz * Mathf.PI * 2f);
            Vector3 t = new Vector3(currTelegraph.r, currTelegraph.g, currTelegraph.b);
            rgb = Vector3.Lerp(baseRgb, t, k);
        }
        else if (MonsterStateId.Downed == state)
        {
            rgb = new Vector3(downedColor.r, downedColor.g, downedColor.b);
            alphaMul = downedAlpha;
        }

        float a = ownedAlpha * alphaMul;
        target.color = new Color(rgb.x, rgb.y, rgb.z, a);
        lastWrittenAlpha = a;
    }
}
