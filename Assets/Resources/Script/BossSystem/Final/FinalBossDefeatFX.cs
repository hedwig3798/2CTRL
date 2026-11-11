using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최종 보스 처치 연출 (표시 전용): 화면 플래시 + 보스 위치 폭발 원. unscaled 시간 사용
/// </summary>
public class FinalBossDefeatFX
    : MonoBehaviour
{
    [SerializeField] private Image flash;
    [SerializeField] private Sprite circleSprite;
    [SerializeField] private Material circleMaterial;
    [SerializeField] private string sortingLayer = "attack";
    [SerializeField] private int burstCount = 10;
    [SerializeField] private float duration = 0.9f;

    private void Awake()
    {
        if (null != flash) { flash.color = new Color(1f, 1f, 1f, 0f); }
    }

    private void OnEnable()
    {
        EventManager events = EventManager.Instance;
        if (null == events) { return; }
        events.OnFinalBossDefeated -= HandleDefeated;
        events.OnFinalBossDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        if (false == EventManager.HasInstance) { return; }
        EventManager.Instance.OnFinalBossDefeated -= HandleDefeated;
    }

    private void HandleDefeated()
    {
        FinalBoss boss = FindFirstObjectByType<FinalBoss>();
        Vector3 pos = null != boss ? boss.transform.position : Vector3.zero;
        int layer = null != boss ? boss.gameObject.layer : 0;
        StartCoroutine(Play(pos, layer));
    }

    private IEnumerator Play(Vector3 _pos, int _layer)
    {
        Transform[] bursts = new Transform[burstCount];
        SpriteRenderer[] srs = new SpriteRenderer[burstCount];
        float[] delay = new float[burstCount];
        Vector3[] offs = new Vector3[burstCount];
        float[] maxScale = new float[burstCount];
        if (null != circleSprite)
        {
            for (int i = 0; i < burstCount; ++i)
            {
                var go = new GameObject("DefeatBurst");
                go.layer = _layer;
                go.transform.position = _pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = circleSprite;
                if (null != circleMaterial) { sr.sharedMaterial = circleMaterial; }
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = 40;
                sr.enabled = false;
                bursts[i] = go.transform;
                srs[i] = sr;
                delay[i] = i * 0.05f;
                offs[i] = (Vector3)(Random.insideUnitCircle * 1.6f);
                maxScale[i] = Random.Range(2.5f, 5f);
            }
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (null != flash)
            {
                float a = t < 0.08f ? t / 0.08f : Mathf.Clamp01(1f - (t - 0.08f) / (duration - 0.08f));
                flash.color = new Color(1f, 0.95f, 0.85f, a);
            }
            for (int i = 0; i < burstCount; ++i)
            {
                if (null == bursts[i]) { continue; }
                float k = Mathf.Clamp01((t - delay[i]) / 0.5f);
                srs[i].enabled = t >= delay[i] && k < 1f;
                bursts[i].position = _pos + offs[i];
                float s = maxScale[i] * Mathf.Sqrt(k);
                bursts[i].localScale = new Vector3(s, s, 1f);
                srs[i].color = Color.Lerp(new Color(1f, 0.95f, 0.7f, 0.9f), new Color(1f, 0.3f, 0.1f, 0f), k);
            }
            yield return null;
        }
        if (null != flash) { flash.color = new Color(1f, 1f, 1f, 0f); }
        foreach (Transform b in bursts)
        {
            if (null != b) { Destroy(b.gameObject); }
        }
    }
}
