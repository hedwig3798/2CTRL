using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영구 강화 창의 한 줄 (아이콘, 이름, Lv a/b, 다음 효과, 가격, 구매 버튼)
/// - PermanentUpgradePanel 이 템플릿을 복제해 Bind 한다
/// - 구매 버튼을 누르면 MetaProgress.TryPurchase 를 부르고, 표시는 MetaProgress.OnChanged 로 창이 Refresh 한다
/// - 최대 레벨이면 가격 대신 MAX 를 표시하고 버튼을 끈다. 차원력이 모자라면 버튼을 비활성화한다
/// - 표시 문자열은 모두 StringTable 키 (고정 라벨 "구매" 는 LocalizedText 로)
/// </summary>
public class PermanentUpgradeRow
    : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text levelLabel;
    [Tooltip("현재 효과 → 다음 효과 (최대 레벨이면 현재 효과만)")]
    [SerializeField] private TMP_Text effectLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private Button buyButton;

    [Header("text (StringTable 키)")]
    [Tooltip("{0} = 레벨, {1} = 최대 레벨")]
    [SerializeField] private string levelFormatKey = "meta.level";
    [Tooltip("{0} = 현재 효과, {1} = 다음 효과 (각각 아래 값 형식으로 만든 문자열)")]
    [SerializeField] private string nextEffectFormatKey = "meta.next_effect";
    [Tooltip("Add 강화 값 형식. {0} = 값")]
    [SerializeField] private string addValueFormatKey = "meta.value_add";
    [Tooltip("Multiply 강화 값 형식. {0} = 퍼센트 (2 = 2%)")]
    [SerializeField] private string mulValueFormatKey = "meta.value_mul";
    [Tooltip("{0} = 가격")]
    [SerializeField] private string priceFormatKey = "meta.price";
    [SerializeField] private string maxKey = "meta.max";

    private PermanentUpgradeData data;

    public PermanentUpgradeData Data => data;

    private void Awake()
    {
        if (null != buyButton)
        {
            buyButton.onClick.AddListener(OnClickBuy);
        }
    }

    private void OnDestroy()
    {
        if (null != buyButton)
        {
            buyButton.onClick.RemoveListener(OnClickBuy);
        }
    }

    /// <summary>
    /// 표시할 강화를 정하고 갱신한다
    /// </summary>
    public void Bind(PermanentUpgradeData _data)
    {
        data = _data;
        Refresh();
    }

    /// <summary>
    /// 현재 레벨·차원력·언어로 다시 채운다
    /// </summary>
    public void Refresh()
    {
        if (null == data)
        {
            return;
        }

        int level = MetaProgress.GetLevel(data);
        bool isMax = level >= data.MaxLevel;

        if (null != icon)
        {
            icon.sprite = data.Icon;
            icon.enabled = null != data.Icon;
        }
        if (null != nameLabel)
        {
            nameLabel.text = data.DisplayName;
        }
        if (null != levelLabel)
        {
            levelLabel.text = StringTable.Format(levelFormatKey, level, data.MaxLevel);
        }
        if (null != effectLabel)
        {
            string current = FormatValue(data.GetTotalValue(level));
            effectLabel.text = true == isMax
                ? current
                : StringTable.Format(nextEffectFormatKey, current, FormatValue(data.GetTotalValue(level + 1)));
        }
        if (null != priceLabel)
        {
            priceLabel.text = true == isMax
                ? StringTable.Get(maxKey)
                : StringTable.Format(priceFormatKey, MetaProgress.GetCost(data));
        }
        if (null != buyButton)
        {
            buyButton.interactable = MetaProgress.CanPurchase(data);
        }
    }

    public void OnClickBuy()
    {
        if (null == data)
        {
            return;
        }
        // 성공하면 MetaProgress.OnChanged 로 창 전체가 갱신된다
        MetaProgress.TryPurchase(data);
    }

    private string FormatValue(float _value)
    {
        return StatModifierMode.Add == data.Mode
            ? StringTable.Format(addValueFormatKey, _value)
            : StringTable.Format(mulValueFormatKey, _value * 100f);
    }
}
