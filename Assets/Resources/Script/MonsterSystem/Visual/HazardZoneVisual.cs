using UnityEngine;

/// <summary>
/// 독 장판 연출: 수명(RemainRate)에 따라 알파가 줄고, 끝에서 빠르게 페이드한다
/// 크기는 HazardZone 이 정하므로 알파/색만 다룬다
/// </summary>
[RequireComponent(typeof(HazardZone))]
public class HazardZoneVisual
    : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer outer;

    [SerializeField]
    private SpriteRenderer core;

    [Tooltip("x: 남은 수명 비율 (0 ~ 1), y: 알파 배율")]
    [SerializeField]
    private AnimationCurve alphaByRemain = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.25f, 0.8f), new Keyframe(1f, 1f));

    [Range(0f, 0.5f)]
    [SerializeField]
    private float pulse = 0.12f;

    [Min(0.1f)]
    [SerializeField]
    private float pulseHz = 1.2f;

    private HazardZone zone;
    private Color outerBase = Color.white;
    private Color coreBase = Color.white;

    private void Awake()
    {
        zone = GetComponent<HazardZone>();
        if (null != outer)
        {
            outerBase = outer.color;
        }
        if (null != core)
        {
            coreBase = core.color;
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
        float fade = alphaByRemain.Evaluate(zone.RemainRate);
        float p = 1f + pulse * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);
        if (null != outer)
        {
            Color c = outerBase;
            c.a *= fade * p;
            outer.color = c;
        }
        if (null != core)
        {
            Color c = coreBase;
            c.a *= fade * (2f - p);
            core.color = c;
        }
    }
}
