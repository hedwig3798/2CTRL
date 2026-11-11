using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 레이어의 굵은 직선(레이저 발사/돌진 화살표)에 바깥 글로우 라인을 덧그린다 (표시 전용)
/// </summary>
public class FinalBossLineGlow
    : MonoBehaviour
{
    [SerializeField] private float minWidth = 0.3f;
    [SerializeField] private float glowScale = 2.4f;
    [SerializeField] private float glowAlpha = 0.35f;
    [SerializeField] private float scanInterval = 0.1f;

    private class Pair
    {
        public LineRenderer src;
        public LineRenderer glow;
    }

    private readonly List<Pair> pairs = new List<Pair>();
    private readonly HashSet<LineRenderer> known = new HashSet<LineRenderer>();
    private float scanTimer;
    private int bossLayer = -1;

    private void Awake()
    {
        bossLayer = LayerMask.NameToLayer("Boss");
    }

    private void LateUpdate()
    {
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            Scan();
        }
        for (int i = pairs.Count - 1; i >= 0; --i)
        {
            Pair p = pairs[i];
            if (null == p.src || null == p.glow)
            {
                if (null != p.glow) { Destroy(p.glow.gameObject); }
                pairs.RemoveAt(i);
                continue;
            }
            bool on = p.src.enabled && p.src.positionCount >= 2 && p.src.widthMultiplier >= minWidth;
            p.glow.enabled = on;
            if (false == on) { continue; }
            p.glow.SetPosition(0, p.src.GetPosition(0));
            p.glow.SetPosition(1, p.src.GetPosition(1));
            p.glow.widthMultiplier = p.src.widthMultiplier * glowScale;
            Color c = p.src.startColor;
            c.a *= glowAlpha;
            p.glow.startColor = c;
            p.glow.endColor = c;
        }
    }

    private void Scan()
    {
        foreach (LineRenderer lr in FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude))
        {
            if (lr.gameObject.layer != bossLayer || lr.positionCount != 2 || "LineGlow" == lr.name || known.Contains(lr))
            {
                continue;
            }
            known.Add(lr);
            var go = new GameObject("LineGlow");
            go.layer = lr.gameObject.layer;
            go.transform.SetParent(lr.transform, false);
            var g = go.AddComponent<LineRenderer>();
            g.useWorldSpace = true;
            g.positionCount = 2;
            g.sharedMaterial = lr.sharedMaterial;
            g.sortingLayerID = lr.sortingLayerID;
            g.sortingOrder = lr.sortingOrder - 1;
            g.numCapVertices = lr.numCapVertices;
            g.textureMode = lr.textureMode;
            g.enabled = false;
            pairs.Add(new Pair { src = lr, glow = g });
        }
    }
}
