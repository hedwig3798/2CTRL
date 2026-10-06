using UnityEngine;

/// <summary>
/// HealthSystem 의 HP 변동을 SliderUI 에 반영하는 연결 컴포넌트
/// SliderUI 는 HP 를 모르고, HealthSystem 은 UI 를 모른다. 둘 사이의 연결만 담당한다.
/// - target 이 비어있으면 부모에서 HealthSystem 을 찾는다. (몬스터 머리 위 바 용)
/// - slider 가 비어있으면 자기 자신에서 SliderUI 를 찾는다.
///
/// 런타임에 대상이 정해지는 경우 (플레이어 스폰 후 HUD 연결 등)
///   binder.SetTarget(player.GetComponent<HealthSystem>());
/// </summary>
public class HPSliderBinder
    : MonoBehaviour
{
    [Tooltip("HP 를 표시할 대상 (비어있으면 부모에서 탐색)")]
    [SerializeField]
    private HealthSystem target;

    [Tooltip("표시할 슬라이더 (비어있으면 자기 자신에서 탐색)")]
    [SerializeField]
    private SliderUI slider;

    public HealthSystem Target => target;

    /// <summary>
    /// 표시 대상 변경 (null 이면 연결 해제)
    /// </summary>
    public void SetTarget(HealthSystem _target)
    {
        if (target == _target)
        {
            return;
        }

        Unbind();
        target = _target;

        if (true == isActiveAndEnabled)
        {
            Bind();
        }
    }

    private void Awake()
    {
        if (null == target)
        {
            target = GetComponentInParent<HealthSystem>();
        }
        if (null == slider)
        {
            slider = GetComponent<SliderUI>();
        }
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Bind()
    {
        if (null == target || null == slider)
        {
            return;
        }

        target.OnHPChanged -= HandleHPChanged;
        target.OnHPChanged += HandleHPChanged;

        // 구독 전에 바뀐 값은 이벤트로 오지 않으므로 즉시 동기화
        slider.SetValue(target.CurrHP, target.MaxHP, true);
    }

    private void Unbind()
    {
        if (null != target)
        {
            target.OnHPChanged -= HandleHPChanged;
        }
    }

    private void HandleHPChanged(float _curr, float _max)
    {
        slider.SetValue(_curr, _max);
    }
}
