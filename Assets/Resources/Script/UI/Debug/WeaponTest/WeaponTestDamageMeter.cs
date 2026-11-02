using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 무기 테스트용 데미지 미터 표시 설정 (WeaponTestController 인스펙터에서 조정)
/// - 텍스트는 런타임에 기본 TMP 폰트로 만들어지므로, 한글을 쓰려면 font 에 한글 폰트를 연결한다
/// </summary>
[Serializable]
public class WeaponTestDamageMeterSettings
{
    [Tooltip("텍스트 폰트 (비어 있으면 TMP 기본 폰트)")]
    public TMP_FontAsset font;

    [Tooltip("월드 기준 글자 크기")]
    public float fontSize = 3f;

    [Tooltip("최근 데미지 텍스트 위치 (더미 기준 월드 오프셋)")]
    public Vector3 lastDamageOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("초당 데미지 텍스트 위치 (더미 기준 월드 오프셋)")]
    public Vector3 dpsOffset = new Vector3(0f, 1.2f, 0f);

    public Color lastDamageColor = Color.white;
    public Color dpsColor = new Color(1f, 0.85f, 0.3f);

    [Tooltip("최근 데미지 포맷 ({0} = 데미지)")]
    public string lastDamageFormat = "HIT {0:0.#}";

    [Tooltip("초당 데미지 포맷 ({0} = DPS)")]
    public string dpsFormat = "DPS {0:0.#}";

    [Tooltip("아직 맞지 않았을 때 표시")]
    public string emptyText = "-";

    [Tooltip("DPS 분모(경과 시간)의 최소값 (초). 첫 타격 직후 값이 튀는 것을 막는다")]
    public float minDpsWindow = 1f;

    [Tooltip("텍스트 갱신 간격 (초)")]
    public float refreshInterval = 0.1f;

    [Tooltip("텍스트 정렬 순서 (스프라이트 위에 그리기)")]
    public int sortingOrder = 1000;
}

/// <summary>
/// 무기 테스트용 : 더미 하나가 받은 데미지를 머리 위에 표시한다
/// - HealthSystem.OnDamaged 를 구독한다 (무적이어도 받은 데미지가 그대로 온다)
/// - 최근 데미지 = 마지막으로 받은 한 번의 데미지
/// - 초당 데미지 = 누적 평균 (첫 타격부터의 총 데미지 / max(경과 시간, minDpsWindow))
/// - ResetMeter 로 0 부터 다시 잰다 (무기 교체 / 강화 / 초기화 시 WeaponTestController 가 호출)
/// - 기존 DamageTextBinder(떠오르는 숫자) 와는 별개로 동작한다
/// </summary>
public class WeaponTestDamageMeter
    : MonoBehaviour
{
    private HealthSystem target;
    private WeaponTestDamageMeterSettings settings;

    private TextMeshPro lastDamageText;
    private TextMeshPro dpsText;

    private float lastDamage;
    private float totalDamage;
    private int hitCount;
    private float firstHitTime;
    private bool hasHit;

    private float nextRefreshTime;
    private bool isDirty;

    // 마지막으로 텍스트에 표시한 값
    private bool hasShownValues;
    private bool shownHasHit;
    private float shownLastDamage;
    private float shownDps;

    public HealthSystem Target => target;
    public float LastDamage => lastDamage;
    public float TotalDamage => totalDamage;
    public int HitCount => hitCount;

    /// <summary>
    /// 누적 평균 초당 데미지 (맞은 적이 없으면 0)
    /// </summary>
    public float Dps
    {
        get
        {
            if (false == hasHit)
            {
                return 0f;
            }

            float window = (null != settings) ? settings.minDpsWindow : 1f;
            float elapsed = Mathf.Max(Mathf.Max(Time.time - firstHitTime, window), 0.0001f);
            return totalDamage / elapsed;
        }
    }

    /// <summary>
    /// 측정 대상과 표시 설정을 지정하고 텍스트를 만든다 (다시 부르면 대상만 바꾼다)
    /// </summary>
    public void Bind(HealthSystem _target, WeaponTestDamageMeterSettings _settings)
    {
        Unbind();

        target = _target;
        settings = (null != _settings) ? _settings : new WeaponTestDamageMeterSettings();

        EnsureTexts();
        ResetMeter();

        if (true == isActiveAndEnabled)
        {
            Subscribe();
        }
    }

    /// <summary>
    /// 측정값을 지우고 처음부터 다시 잰다
    /// </summary>
    public void ResetMeter()
    {
        lastDamage = 0f;
        totalDamage = 0f;
        hitCount = 0;
        firstHitTime = 0f;
        hasHit = false;

        // 설정(포맷)이 바뀌었을 수 있으므로 표시값 캐시도 지운다
        hasShownValues = false;
        RefreshTexts();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void LateUpdate()
    {
        if (null == settings)
        {
            return;
        }

        // DPS 는 맞지 않아도 시간이 지나면 바뀌므로 주기적으로 갱신
        // 매 프레임 맞아도 refreshInterval 마다만 갱신한다 (리셋 직후 등 hasHit 이 없으면 바로 갱신)
        if (Time.time < nextRefreshTime && true == hasHit)
        {
            return;
        }
        if (true == isDirty || true == hasHit)
        {
            RefreshTexts();
        }
    }

    private void Subscribe()
    {
        if (null == target)
        {
            return;
        }

        target.OnDamaged -= HandleDamaged;
        target.OnDamaged += HandleDamaged;
    }

    private void Unbind()
    {
        if (null != target)
        {
            target.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(float _amount)
    {
        if (false == hasHit)
        {
            hasHit = true;
            firstHitTime = Time.time;
        }

        lastDamage = _amount;
        totalDamage += _amount;
        hitCount++;
        isDirty = true;
    }

    private void RefreshTexts()
    {
        isDirty = false;
        if (null == settings)
        {
            return;
        }
        nextRefreshTime = Time.time + Mathf.Max(0f, settings.refreshInterval);

        // 값이 그대로면 문자열을 새로 만들지 않는다 (TMP 메시 재생성 방지)
        float dps = Dps;
        if (true == hasShownValues && hasHit == shownHasHit && lastDamage == shownLastDamage && dps == shownDps)
        {
            return;
        }
        hasShownValues = true;
        shownHasHit = hasHit;
        shownLastDamage = lastDamage;
        shownDps = dps;

        if (null != lastDamageText)
        {
            lastDamageText.text = (true == hasHit) ? string.Format(settings.lastDamageFormat, lastDamage) : settings.emptyText;
        }

        if (null != dpsText)
        {
            dpsText.text = (true == hasHit) ? string.Format(settings.dpsFormat, dps) : settings.emptyText;
        }
    }

    #region Text
    private void EnsureTexts()
    {
        if (null == lastDamageText)
        {
            lastDamageText = CreateText("LastDamageText");
        }
        if (null == dpsText)
        {
            dpsText = CreateText("DpsText");
        }

        ApplyStyle(lastDamageText, settings.lastDamageOffset, settings.lastDamageColor);
        ApplyStyle(dpsText, settings.dpsOffset, settings.dpsColor);
    }

    private TextMeshPro CreateText(string _name)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(transform, false);

        TextMeshPro text = go.GetComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontStyle = FontStyles.Bold;
        return text;
    }

    private void ApplyStyle(TextMeshPro _text, Vector3 _worldOffset, Color _color)
    {
        // 비어 있으면 TMP 기본 폰트 유지
        if (null != settings.font)
        {
            _text.font = settings.font;
        }

        _text.fontSize = settings.fontSize;
        _text.color = _color;
        _text.sortingOrder = settings.sortingOrder;

        // 더미와 같은 레이어로 두어 그 쪽 카메라에 보이게 한다
        _text.gameObject.layer = gameObject.layer;

        // 부모 스케일과 무관하게 월드 기준 오프셋 / 크기를 유지한다
        Transform t = _text.transform;
        t.position = transform.position + _worldOffset;
        t.rotation = Quaternion.identity;

        Vector3 parentScale = transform.lossyScale;
        t.localScale = new Vector3(
            SafeInverse(parentScale.x)
            , SafeInverse(parentScale.y)
            , SafeInverse(parentScale.z));
    }

    private static float SafeInverse(float _value)
    {
        return (Mathf.Abs(_value) > 0.0001f) ? 1f / _value : 1f;
    }
    #endregion
}
