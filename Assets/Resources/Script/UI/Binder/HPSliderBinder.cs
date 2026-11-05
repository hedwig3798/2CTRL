using UnityEngine;

/// <summary>
/// HealthSystem 의 HP 변동을 SliderUI 에 반영하는 연결 컴포넌트
/// SliderUI 는 HP 를 모르고, HealthSystem 은 UI 를 모른다. 둘 사이의 연결만 담당한다.
/// - target 이 비어있으면 부모에서 HealthSystem 을 찾는다. (몬스터 머리 위 바 용)
/// - slider 가 비어있으면 자기 자신에서 SliderUI 를 찾는다.
///
/// 런타임에 대상이 정해지는 경우 (플레이어 스폰 후 HUD 연결 등)
///   binder.SetTarget(player.GetComponent<HealthSystem>());
///
/// 적 체력바 표시 설정 (GameSettings.ShowEnemyHPBar)
/// - owner 가 Enemy 이거나, Auto 이면서 대상 / 자신의 부모에 MonsterBrain 이 있으면 적 체력바로 본다.
/// - 적 체력바는 설정이 꺼지면 체력바 Canvas(슬라이더의 가장 가까운 부모 Canvas)를 enabled = false 로 해서 숨긴다.
///   (오브젝트는 켜 둔 채라 HP 동기화는 계속됨. 그리기만 멈추므로 알파 방식보다 싸다)
/// - 플레이어 체력바(Player / Auto 이면서 MonsterBrain 없음)는 영향받지 않는다.
/// </summary>
public class HPSliderBinder
    : MonoBehaviour
{
    public enum HP_BAR_OWNER
    {
        Auto,
        Player,
        Enemy,
    }

    [Tooltip("HP 를 표시할 대상 (비어있으면 부모에서 탐색)")]
    [SerializeField]
    private HealthSystem target;

    [Tooltip("표시할 슬라이더 (비어있으면 자기 자신에서 탐색)")]
    [SerializeField]
    private SliderUI slider;

    [Tooltip("체력바 주인 (Auto : 부모에 MonsterBrain 이 있으면 적)\n적 체력바만 '적 체력바 표시' 설정을 따른다")]
    [SerializeField]
    private HP_BAR_OWNER owner = HP_BAR_OWNER.Auto;

    [Tooltip("적 체력바를 숨길 때 끌 Canvas (비어있으면 슬라이더의 가장 가까운 부모 Canvas)")]
    [SerializeField]
    private Canvas hpBarCanvas;

    private bool isEnemy;
    // 이 컴포넌트가 체력바 Canvas 를 끈 상태인지 (플레이어로 바뀌면 되돌리기용)
    private bool hiddenByThis;

    public HealthSystem Target => target;
    public bool IsEnemy => isEnemy;

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
        ResolveOwner();

        if (true == isActiveAndEnabled)
        {
            Bind();
            ApplyVisibility(GameSettings.ShowEnemyHPBar);
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
        ResolveOwner();
    }

    private void OnEnable()
    {
        Bind();

        // 꺼져 있는 동안(풀 대기 등) 바뀐 설정도 반영
        GameSettings.OnShowEnemyHPBarChanged -= ApplyVisibility;
        GameSettings.OnShowEnemyHPBarChanged += ApplyVisibility;
        ApplyVisibility(GameSettings.ShowEnemyHPBar);
    }

    private void OnDisable()
    {
        Unbind();
        GameSettings.OnShowEnemyHPBarChanged -= ApplyVisibility;
    }

    private void ResolveOwner()
    {
        switch (owner)
        {
            case HP_BAR_OWNER.Enemy:
                isEnemy = true;
                break;
            case HP_BAR_OWNER.Player:
                isEnemy = false;
                break;
            default:
                isEnemy = null != GetComponentInParent<MonsterBrain>(true)
                    || (null != target && null != target.GetComponentInParent<MonsterBrain>(true));
                break;
        }
    }

    private void ApplyVisibility(bool _showEnemyHPBar)
    {
        if (false == isEnemy)
        {
            // 주인이 바뀌어 적 → 플레이어가 된 경우, 이 컴포넌트가 끈 Canvas 만 되돌린다
            if (true == hiddenByThis && null != hpBarCanvas)
            {
                hpBarCanvas.enabled = true;
            }
            hiddenByThis = false;
            return;
        }

        if (null == hpBarCanvas)
        {
            if (null == slider)
            {
                return;
            }
            hpBarCanvas = slider.GetComponentInParent<Canvas>(true);
            if (null == hpBarCanvas)
            {
                return;
            }
        }

        bool show = true == _showEnemyHPBar;
        if (hpBarCanvas.enabled != show)
        {
            hpBarCanvas.enabled = show;
        }
        hiddenByThis = false == show;
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
