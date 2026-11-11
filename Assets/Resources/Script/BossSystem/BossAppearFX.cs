using UnityEngine;

/// <summary>
/// 보스 등장·재배치 연출 (표시 전용): 몸체가 작게 시작해 튀어 오르며 커지고, 바닥에 흰 링이 퍼진다
/// MidBoss.OnRelocated 와 활성화(최초 등장) 때 재생한다. 링은 보스와 같은 레이어, attack 정렬 레이어
/// </summary>
[RequireComponent(typeof(MidBoss))]
public class BossAppearFX
    : MonoBehaviour
{
    [SerializeField]
    private Transform body;

    [SerializeField]
    private BossTelegraphStyle style;

    [SerializeField]
    private float duration = 0.5f;

    [SerializeField]
    private float ringRadius = 3.5f;

    private MidBoss boss;
    private Vector3 bodyScale = Vector3.one;
    private float startTime = -1f;
    private BossTelegraph ring;

    private void Awake()
    {
        boss = GetComponent<MidBoss>();
        if (null != body)
        {
            bodyScale = body.localScale;
        }
    }

    private void OnEnable()
    {
        boss.OnRelocated += HandleRelocated;
        Play();
    }

    private void OnDisable()
    {
        boss.OnRelocated -= HandleRelocated;
        Finish();
    }

    private void HandleRelocated(MidBoss _boss, Vector2 _pos)
    {
        Play();
    }

    private void Play()
    {
        Finish();
        startTime = Time.time;
        ring = BossTelegraph.Spawn(style, gameObject.layer, transform.position);
        ApplyScale(0f);
    }

    private void Finish()
    {
        if (null != ring)
        {
            ring.Release();
            ring = null;
        }
        if (null != body)
        {
            body.localScale = bodyScale;
        }
        startTime = -1f;
    }

    private void ApplyScale(float _t)
    {
        if (null == body)
        {
            return;
        }
        // 0.4 → 1.12 → 1 (살짝 튀어 오름)
        float s = _t < 0.7f
            ? Mathf.Lerp(0.4f, 1.12f, _t / 0.7f)
            : Mathf.Lerp(1.12f, 1f, (_t - 0.7f) / 0.3f);
        body.localScale = bodyScale * s;
    }

    private void Update()
    {
        if (startTime < 0f)
        {
            return;
        }
        float t = Mathf.Clamp01((Time.time - startTime) / Mathf.Max(0.05f, duration));
        ApplyScale(t);
        if (null != ring)
        {
            ring.transform.position = transform.position;
            ring.ShowRing(0, Mathf.Lerp(0.5f, ringRadius, t), new Color(1f, 1f, 1f, 0.8f * (1f - t)), false);
        }
        if (t >= 1f)
        {
            Finish();
        }
    }
}
