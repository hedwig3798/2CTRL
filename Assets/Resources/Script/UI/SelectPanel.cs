using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 레벨업 보상 선택지 하나를 표시하는 패널
/// - isSelected 가 true 이면 선택 배경을 표시한다.
/// - 클릭되면 OnClicked 로 알리기만 하고, 선택 여부는 LevelUpUI 가 결정한다.
/// - SetData 로 RewardData 두 개를 받아 패널 하나를 표시하며, 비어 있는 항목의 UI 는 숨긴다.
///   - explainText1 = 첫 번째 RewardData 의 explain1, explainText2 = 두 번째 RewardData 의 explain1
///   - image / name 은 첫 번째 RewardData 를 우선하고, 없으면 두 번째 것을 사용한다
///   - RewardData 가 하나만 들어오면 들어온 하나의 정보만 표시한다
/// - key / value 는 따로 표시하지 않고 explain 에 포멧팅되어 표시되며, explain1 / explain2 별로 따로 저장한다.
/// </summary>
public class SelectPanel
    : MonoBehaviour
    , IPointerClickHandler
{
    public event Action<SelectPanel> OnClicked;

    [Tooltip("선택되었을 때 표시할 배경 (이 오브젝트의 자식, 다른 자식보다 위쪽 순서에 두어야 뒤에 그려짐)")]
    [SerializeField]
    private GameObject selectBackground;

    [Tooltip("현재 선택 여부")]
    [SerializeField]
    private bool isSelected = false;

    [Header("Reward Data 표시")]
    [Tooltip("RewardData 의 image 를 표시할 Image (첫 번째 데이터 우선)")]
    [SerializeField]
    private Image image;

    [Tooltip("RewardData 의 name 을 표시할 텍스트 (첫 번째 데이터 우선)")]
    [SerializeField]
    private TMP_Text nameText;

    [Tooltip("첫 번째 RewardData 의 explain1 을 key / value 로 포멧팅해서 표시할 텍스트")]
    [FormerlySerializedAs("explainText")]
    [SerializeField]
    private TMP_Text explainText1;

    [Tooltip("두 번째 RewardData 의 explain1 을 key / value 로 포멧팅해서 표시할 텍스트")]
    [SerializeField]
    private TMP_Text explainText2;

    // explainText1 / explainText2 에 들어간 데이터 (없으면 null)
    public RewardData Data1 { get; private set; }
    public RewardData Data2 { get; private set; }

    // explainText1 에 들어간 key / value (비어 있으면 Has* 가 false)
    public bool HasKey1 { get; private set; }
    public ValueType Key1 { get; private set; }
    public bool HasValue1 { get; private set; }
    public float Value1 { get; private set; }

    // explainText2 에 들어간 key / value (비어 있으면 Has* 가 false)
    public bool HasKey2 { get; private set; }
    public ValueType Key2 { get; private set; }
    public bool HasValue2 { get; private set; }
    public float Value2 { get; private set; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            isSelected = value;
            ApplySelected();
        }
    }

    private void Awake()
    {
        if (null == selectBackground)
        {
            Debug.LogWarning($"[SelectPanel] selectBackground 가 연결되지 않았습니다. ({name})", this);
        }

        if (null == image && null == nameText && null == explainText1 && null == explainText2)
        {
            Debug.LogWarning($"[SelectPanel] RewardData 를 표시할 UI 가 하나도 연결되지 않았습니다. ({name})", this);
        }

        ApplySelected();
    }

    // 인스펙터에서 isSelected 를 바꿨을 때도 바로 반영
    private void OnValidate()
    {
        ApplySelected();
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        OnClicked?.Invoke(this);
    }

    /// <summary>
    /// 데이터 두 개를 패널 하나에 표시한다. 비어 있는 항목(또는 데이터가 null)의 UI 는 숨긴다.
    /// </summary>
    /// <param name="_data1">explainText1 에 표시할 데이터 (image / name 우선)</param>
    /// <param name="_data2">explainText2 에 표시할 데이터 (없으면 null)</param>
    public void SetData(RewardData _data1, RewardData _data2 = null)
    {
        Data1 = _data1;
        Data2 = _data2;

        ReadKeyValue(_data1, out bool hasKey1, out ValueType key1, out bool hasValue1, out float value1);
        HasKey1 = hasKey1;
        Key1 = key1;
        HasValue1 = hasValue1;
        Value1 = value1;

        ReadKeyValue(_data2, out bool hasKey2, out ValueType key2, out bool hasValue2, out float value2);
        HasKey2 = hasKey2;
        Key2 = key2;
        HasValue2 = hasValue2;
        Value2 = value2;

        // image / name 은 첫 번째 데이터 우선 (UnityEngine.Object 이므로 ?? 대신 비교)
        RewardData main = null != _data1 ? _data1 : _data2;

        bool hasImage = null != main && main.HasImage;
        if (null != image)
        {
            image.sprite = hasImage ? main.GetImage() : null;
            image.gameObject.SetActive(hasImage);
        }

        bool hasName = null != main && main.HasName;
        ApplyText(nameText, hasName, hasName ? main.GetName() : string.Empty);

        bool hasExplain1 = null != _data1 && _data1.HasExplain1;
        ApplyText(explainText1, hasExplain1, hasExplain1 ? _data1.GetFormattedExplain1() : string.Empty);

        bool hasExplain2 = null != _data2 && _data2.HasExplain1;
        ApplyText(explainText2, hasExplain2, hasExplain2 ? _data2.GetFormattedExplain1() : string.Empty);
    }

    /// <summary>
    /// 데이터의 key / value 를 읽는다 (_data 가 null 이면 모두 비어 있음)
    /// </summary>
    private static void ReadKeyValue(RewardData _data, out bool _hasKey, out ValueType _key, out bool _hasValue, out float _value)
    {
        if (null == _data)
        {
            _hasKey = false;
            _key = default;
            _hasValue = false;
            _value = 0.0f;
            return;
        }

        _hasKey = _data.TryGetKey(out _key);
        _hasValue = _data.TryGetValue(out _value);
    }

    private static void ApplyText(TMP_Text _text, bool _visible, string _content)
    {
        if (null == _text)
        {
            return;
        }

        _text.text = _content;
        _text.gameObject.SetActive(_visible);
    }

    private void ApplySelected()
    {
        if (null != selectBackground)
        {
            selectBackground.SetActive(isSelected);
        }
    }
}
