using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최종 보스 인트로 표시용 HUD 연출 (표시 전용, 게임 로직 없음)
/// - OnFinalBossStart 후 duration 동안 중앙선 균열 + WARNING/보스 이름 배너. unscaled 시간 사용
/// </summary>
public class FinalBossIntroUI
    : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform crack;
    [SerializeField] private RectTransform crackGlow;
    [SerializeField] private Image crackImage;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private RectTransform[] bands;
    [SerializeField] private float duration = 3f;

    private Coroutine routine;

    private void Awake()
    {
        if (null != group) { group.alpha = 0f; }
    }

    private void OnEnable()
    {
        EventManager events = EventManager.Instance;
        if (null == events) { return; }
        events.OnFinalBossStart -= HandleStart;
        events.OnFinalBossStart += HandleStart;
    }

    private void OnDisable()
    {
        if (false == EventManager.HasInstance) { return; }
        EventManager.Instance.OnFinalBossStart -= HandleStart;
    }

    private void HandleStart()
    {
        if (null != routine) { StopCoroutine(routine); }
        routine = StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        RectTransform self = transform as RectTransform;
        float t = 0f;
        float jitterTimer = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float h = null != self ? self.rect.height : 1080f;
            float fadeIn = Mathf.Clamp01(t / 0.2f);
            float fadeOut = Mathf.Clamp01((duration - t) / 0.5f);
            group.alpha = Mathf.Min(fadeIn, fadeOut);

            float grow = Mathf.Clamp01(t / 0.6f);
            jitterTimer -= Time.unscaledDeltaTime;
            float w = null != crack ? crack.sizeDelta.x : 6f;
            if (jitterTimer <= 0f)
            {
                jitterTimer = 0.05f;
                w = Random.Range(4f, 12f);
                if (null != crackImage) { crackImage.color = new Color(1f, Random.Range(0.75f, 1f), Random.Range(0.75f, 1f), Random.Range(0.7f, 1f)); }
            }
            if (null != crack) { crack.sizeDelta = new Vector2(w, h * grow); }
            if (null != crackGlow) { crackGlow.sizeDelta = new Vector2(w * 5f, h * grow); }

            if (null != warningText)
            {
                float punch = 1f + 0.5f * Mathf.Clamp01(1f - t / 0.3f);
                warningText.rectTransform.localScale = new Vector3(punch, punch, 1f);
                float blink = t < 2f ? (Mathf.Sin(t * 14f) > 0f ? 1f : 0.45f) : 1f;
                Color c = warningText.color;
                c.a = blink;
                warningText.color = c;
            }
            if (null != nameText)
            {
                Color c = nameText.color;
                c.a = Mathf.Clamp01((t - 0.5f) / 0.4f);
                nameText.color = c;
            }
            if (null != bands)
            {
                foreach (RectTransform b in bands)
                {
                    if (null != b) { b.localScale = new Vector3(Mathf.Clamp01(t / 0.4f), 1f, 1f); }
                }
            }
            yield return null;
        }
        group.alpha = 0f;
        routine = null;
    }
}
