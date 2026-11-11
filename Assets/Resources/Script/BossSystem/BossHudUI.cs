using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 중간 보스 HUD 표시 (표시 전용, 게임플레이 로직 없음)
/// - OnBossWarning : 해당 진영 화면 상단에 배너를 bannerDuration 초 동안 표시
/// - OnBossSpawned : 보스가 진영 화면 밖에 있으면 화면 가장자리에 방향 화살표 (사망·화면 안이면 숨김)
/// - LevelUpUI.OnRoundShown : 보스 보상 라운드면 그 진영 패널 위 "보스 보상" 제목 표시
/// </summary>
public class BossHudUI
    : MonoBehaviour
{
    [Serializable]
    public class SideView
    {
        public PlayerSide side;
        public CanvasGroup banner;
        public TMP_Text bannerText;
        public GameObject rewardTitle;
    }

    [SerializeField]
    private SideView[] views;

    [Tooltip("화살표 원본 (비활성 템플릿, 보스마다 복제)")]
    [SerializeField]
    private RectTransform arrowTemplate;

    [SerializeField]
    private float bannerDuration = 3f;

    [Tooltip("화살표가 진영 화면 가장자리에서 떨어지는 정도 (뷰포트 비율)")]
    [SerializeField]
    private float arrowEdgeInset = 0.07f;

    private class Tracked
    {
        public MidBoss boss;
        public RectTransform arrow;
    }

    private readonly List<Tracked> tracked = new List<Tracked>();
    private readonly Dictionary<PlayerSide, float> bannerEnd = new Dictionary<PlayerSide, float>();
    private LevelUpUI levelUp;
    private bool subscribed;

    private void OnEnable()
    {
        Subscribe();
        HideAllTitles();
        foreach (SideView v in views)
        {
            SetBanner(v, 0f);
        }
    }

    private void OnDisable()
    {
        if (true == subscribed && true == EventManager.HasInstance)
        {
            EventManager.Instance.OnBossWarning -= HandleWarning;
            EventManager.Instance.OnBossSpawned -= HandleSpawned;
            EventManager.Instance.OnBossDefeated -= HandleDefeated;
            EventManager.Instance.OnBossWarningCancelled -= HandleWarningCancelled;
            EventManager.Instance.OnFinalBossStart -= HandleFinalBossStart;
        }
        subscribed = false;
        if (null != levelUp)
        {
            levelUp.OnRoundShown -= HandleRoundShown;
            levelUp = null;
        }
    }

    private void Subscribe()
    {
        if (false == subscribed)
        {
            EventManager.Instance.OnBossWarning += HandleWarning;
            EventManager.Instance.OnBossSpawned += HandleSpawned;
            EventManager.Instance.OnBossDefeated += HandleDefeated;
            EventManager.Instance.OnBossWarningCancelled += HandleWarningCancelled;
            EventManager.Instance.OnFinalBossStart += HandleFinalBossStart;
            subscribed = true;
        }
    }

    // 출현 취소: 그 진영 경고 배너를 바로 숨긴다
    private void HandleWarningCancelled(PlayerSide _side)
    {
        bannerEnd.Remove(_side);
        SideView v = View(_side);
        if (null != v)
        {
            SetBanner(v, 0f);
        }
    }

    // 최종 보스 시작: 모든 경고 배너와 화면 밖 화살표를 숨긴다
    private void HandleFinalBossStart()
    {
        bannerEnd.Clear();
        foreach (SideView v in views)
        {
            SetBanner(v, 0f);
        }
        for (int i = tracked.Count - 1; i >= 0; --i)
        {
            Remove(i);
        }
    }

    private SideView View(PlayerSide _side)
    {
        foreach (SideView v in views)
        {
            if (_side == v.side)
            {
                return v;
            }
        }
        return null;
    }

    private void HandleWarning(PlayerSide _side, MidBoss _boss)
    {
        SideView v = View(_side);
        if (null == v || null == v.bannerText)
        {
            return;
        }
        string bossName = null != _boss ? StringTable.Resolve(_boss.DisplayNameKey) : string.Empty;
        v.bannerText.text = StringTable.FormatText(StringTable.Get("ui.boss.warning"), bossName);
        bannerEnd[_side] = Time.unscaledTime + bannerDuration;
        SetBanner(v, 1f);
    }

    private void HandleSpawned(PlayerSide _side, MidBoss _boss)
    {
        if (null == _boss || null == arrowTemplate)
        {
            return;
        }
        RectTransform arrow = Instantiate(arrowTemplate, arrowTemplate.parent);
        arrow.name = "BossArrow_" + _boss.name;
        arrow.gameObject.SetActive(false);
        tracked.Add(new Tracked { boss = _boss, arrow = arrow });
    }

    private void HandleDefeated(PlayerSide _side, MidBoss _boss)
    {
        for (int i = tracked.Count - 1; i >= 0; --i)
        {
            if (_boss == tracked[i].boss)
            {
                Remove(i);
            }
        }
    }

    private void Remove(int _index)
    {
        if (null != tracked[_index].arrow)
        {
            Destroy(tracked[_index].arrow.gameObject);
        }
        tracked.RemoveAt(_index);
    }

    private void HandleRoundShown(bool _isBoss, PlayerSide _side)
    {
        foreach (SideView v in views)
        {
            if (null != v.rewardTitle)
            {
                v.rewardTitle.SetActive(true == _isBoss && _side == v.side);
            }
        }
    }

    private void HideAllTitles()
    {
        foreach (SideView v in views)
        {
            if (null != v.rewardTitle)
            {
                v.rewardTitle.SetActive(false);
            }
        }
    }

    private static void SetBanner(SideView _v, float _alpha)
    {
        if (null != _v.banner)
        {
            _v.banner.alpha = _alpha;
        }
    }

    private void Update()
    {
        if (null == levelUp)
        {
            levelUp = FindFirstObjectByType<LevelUpUI>();
            if (null != levelUp)
            {
                levelUp.OnRoundShown += HandleRoundShown;
            }
        }
        else if (false == levelUp.IsOpen)
        {
            HideAllTitles();
        }

        foreach (SideView v in views)
        {
            if (true == bannerEnd.TryGetValue(v.side, out float end))
            {
                float remain = end - Time.unscaledTime;
                SetBanner(v, remain <= 0f ? 0f : Mathf.Clamp01(remain / 0.4f));
                if (remain <= 0f)
                {
                    bannerEnd.Remove(v.side);
                }
            }
        }

        for (int i = tracked.Count - 1; i >= 0; --i)
        {
            Tracked t = tracked[i];
            if (null == t.boss || true == t.boss.Health.isDead)
            {
                Remove(i);
                continue;
            }
            UpdateArrow(t);
        }
    }

    private void UpdateArrow(Tracked _t)
    {
        Camera cam = _t.boss.SideCamera;
        if (null == cam || null == _t.arrow)
        {
            return;
        }

        Vector3 vp = cam.WorldToViewportPoint(_t.boss.transform.position);
        bool inside = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
        if (true == inside)
        {
            _t.arrow.gameObject.SetActive(false);
            return;
        }

        Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f)
        {
            d = -d;
        }
        float half = 0.5f - arrowEdgeInset;
        float k = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), 0.0001f);
        Vector2 p = new Vector2(0.5f, 0.5f) + d * (half / k);

        Rect r = cam.rect;
        Vector2 anchor = new Vector2(r.x + p.x * r.width, r.y + p.y * r.height);
        _t.arrow.anchorMin = anchor;
        _t.arrow.anchorMax = anchor;
        _t.arrow.anchoredPosition = Vector2.zero;
        _t.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        _t.arrow.gameObject.SetActive(true);
    }
}
