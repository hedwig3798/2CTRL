using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최종 보스 공유 HP 바 (화면 상단 전체 폭)
/// - OnFinalBossStart 전에는 숨긴다 (CanvasGroup 알파 0, 오브젝트는 켜 둔 채로 구독 유지)
/// - OnFinalBossHPChanged(cur, max)로 채움 갱신, 처치되면 숨긴다
/// </summary>
public class FinalBossHPBar
    : MonoBehaviour
{
    [SerializeField]
    private Image fill;

    [SerializeField]
    private CanvasGroup group;

    [SerializeField]
    private bool hideOnDefeated = true;

    private void Awake()
    {
        if (null == group)
        {
            group = GetComponent<CanvasGroup>();
        }
        if (null == group)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }
        SetVisible(false);
    }

    private void OnEnable()
    {
        EventManager events = EventManager.Instance;
        if (null == events)
        {
            return;
        }
        events.OnFinalBossStart -= HandleStart;
        events.OnFinalBossStart += HandleStart;
        events.OnFinalBossHPChanged -= HandleHPChanged;
        events.OnFinalBossHPChanged += HandleHPChanged;
        events.OnFinalBossDefeated -= HandleDefeated;
        events.OnFinalBossDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        if (false == EventManager.HasInstance)
        {
            return;
        }
        EventManager events = EventManager.Instance;
        events.OnFinalBossStart -= HandleStart;
        events.OnFinalBossHPChanged -= HandleHPChanged;
        events.OnFinalBossDefeated -= HandleDefeated;
    }

    private void SetVisible(bool _visible)
    {
        if (null == group)
        {
            return;
        }
        group.alpha = true == _visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void HandleStart()
    {
        SetVisible(true);
    }

    private void HandleHPChanged(float _cur, float _max)
    {
        SetVisible(true);
        if (null != fill)
        {
            fill.fillAmount = _max > 0f ? Mathf.Clamp01(_cur / _max) : 0f;
        }
    }

    private void HandleDefeated()
    {
        if (true == hideOnDefeated)
        {
            SetVisible(false);
        }
    }
}
