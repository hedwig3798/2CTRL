using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 시 화면 가장자리를 붉게 물들이는 비네트
/// - 시작 투명도 0, 피격(HealthSystem.OnDamaged) 마다 1 로 초기화한 뒤 fadeDuration 동안 0 으로 줄인다
/// - 붉은 정도는 텍스처(가장자리 그라데이션) 자체의 알파로 정한다
/// </summary>
[RequireComponent(typeof(Image))]
public class HitVignetteUI : MonoBehaviour
{
    [SerializeField]
    private HealthSystem target;

    [SerializeField]
    private float fadeDuration = 0.4f;

    private Image image;
    private float alpha;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;

        if (null == target)
        {
            PlayerStats owner = GetComponentInParent<PlayerStats>();
            if (null != owner)
            {
                target = owner.GetComponentInChildren<HealthSystem>(true);
            }
        }

        SetAlpha(0f);
    }

    private void OnEnable()
    {
        if (null != target)
        {
            target.OnDamaged -= HandleDamaged;
            target.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (null != target)
        {
            target.OnDamaged -= HandleDamaged;
        }

        SetAlpha(0f);
    }

    private void HandleDamaged(float _damage)
    {
        SetAlpha(1f);
    }

    private void Update()
    {
        if (alpha <= 0f)
        {
            return;
        }

        float step = (0f < fadeDuration) ? Time.deltaTime / fadeDuration : 1f;
        SetAlpha(Mathf.Max(0f, alpha - step));
    }

    private void SetAlpha(float _alpha)
    {
        alpha = _alpha;
        Color c = image.color;
        c.a = _alpha;
        image.color = c;
        image.enabled = 0f < _alpha;
    }
}
