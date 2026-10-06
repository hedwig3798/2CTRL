using UnityEngine;

/// <summary>
/// ExpSystem 의 EXP 변동을 SliderUI 에 반영하는 연결 컴포넌트
/// SliderUI 는 EXP 를 모르고, ExpSystem 은 UI 를 모른다. 둘 사이의 연결만 담당한다.
/// - target 이 비어있으면 부모에서 ExpSystem 을 찾는다.
/// - slider 가 비어있으면 자기 자신에서 SliderUI 를 찾는다.
///
/// 런타임에 대상이 정해지는 경우 (플레이어 스폰 후 HUD 연결 등)
///   binder.SetTarget(player.GetComponent<ExpSystem>());
/// </summary>
public class ExpSliderBinder
    : MonoBehaviour
{
    [Tooltip("EXP 를 표시할 대상 (비어있으면 부모에서 탐색)")]
    [SerializeField]
    private ExpSystem target;

    [Tooltip("표시할 슬라이더 (비어있으면 자기 자신에서 탐색)")]
    [SerializeField]
    private SliderUI slider;

    public ExpSystem Target => target;

    /// <summary>
    /// 표시 대상 변경 (null 이면 연결 해제)
    /// </summary>
    public void SetTarget(ExpSystem _target)
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
            target = GetComponentInParent<ExpSystem>();
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

        target.OnExpChanged -= HandleExpChanged;
        target.OnExpChanged += HandleExpChanged;

        // 구독 전에 바뀐 값은 이벤트로 오지 않으므로 즉시 동기화
        slider.SetValue(target.currExp, target.maxExp, true);
    }

    private void Unbind()
    {
        if (null != target)
        {
            target.OnExpChanged -= HandleExpChanged;
        }
    }

    private void HandleExpChanged(float _curr, float _max)
    {
        slider.SetValue(_curr, _max);
    }
}
