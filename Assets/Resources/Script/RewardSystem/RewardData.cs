using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 레벨업 보상 선택지 데이터 (이미지, 이름, 키, 값, 설명)
/// - 모든 항목은 선택적이며, 비어 있는 항목은 SelectPanel 에서 숨긴다
///   - image : null 이면 비어 있음
///   - name / explain1 / explain2 : 빈 문자열이면 비어 있음
///   - key / value : hasKey / hasValue 가 false 면 비어 있음
/// - key / value 는 직접 표시하지 않고 explain1 / explain2 에 포멧팅해서 표시한다
/// - explain1 / explain2 는 string.Format 포멧 문자열이다 ({0} = key 의 한글 이름, {1} = value, {2} = name)
///   - 예) "{0} 이(가) {1} 증가합니다", "{0} +{1:0.#}", "{0} {1:P0} 증가"
///   - 비어 있는 key / value 자리는 빈 문자열로 채운다
/// - RewardManager 의 풀에 넣고 Get / GetAny 로 꺼내 쓴다
/// - 골랐을 때 실제 동작은 effect(RewardEffect) 가 담당하며, RewardManager.ApplyReward 로 실행한다
/// - 런타임에 Set / Clear 로 에셋을 바꾸면 에디터에서는 에셋에 그대로 남으므로,
///   런타임용 데이터는 ScriptableObject.CreateInstance&lt;RewardData&gt;() 로 만들어 쓴다
/// </summary>
[CreateAssetMenu(fileName = "RewardData", menuName = "Scriptable Objects/RewardData")]
public class RewardData
    : ScriptableObject
{
    [SerializeField]
    private Sprite image;

    [SerializeField]
    private string rewardName;

    [Tooltip("체크를 해제하면 key 를 표시하지 않음")]
    [SerializeField]
    private bool hasKey = false;

    [SerializeField]
    private ValueType key;

    [Tooltip("체크를 해제하면 value 를 표시하지 않음")]
    [SerializeField]
    private bool hasValue = false;

    [SerializeField]
    private float value;

    [Tooltip("SelectPanel 의 Explain Text 1 에 표시할 포멧 문자열 ({0} = key, {1} = value, {2} = name)")]
    [TextArea]
    [FormerlySerializedAs("explain")]
    [SerializeField]
    private string explain1 = "{0}";

    [Tooltip("SelectPanel 의 Explain Text 2 에 표시할 포멧 문자열 ({0} = key, {1} = value, {2} = name)")]
    [TextArea]
    [SerializeField]
    private string explain2 = "+{1:0.##}";

    [Tooltip("이 보상을 골랐을 때 실행할 효과 (비어 있으면 표시만 하고 아무것도 하지 않음)")]
    [SerializeField]
    private RewardEffect effect;

    public bool HasEffect => null != effect;

    /// <summary>
    /// 보상 효과를 실행한다 (effect 가 없으면 아무것도 하지 않는다)
    /// </summary>
    public void Apply(RewardContext _context)
    {
        if (null == effect)
        {
            return;
        }

        effect.Apply(_context, this);
    }

    #region Getter
    public RewardEffect GetEffect()
    {
        return effect;
    }

    public bool HasImage => null != image;
    public bool HasName => false == string.IsNullOrEmpty(rewardName);
    public bool HasKey => hasKey;
    public bool HasValue => hasValue;
    public bool HasExplain1 => false == string.IsNullOrEmpty(explain1);
    public bool HasExplain2 => false == string.IsNullOrEmpty(explain2);

    public Sprite GetImage()
    {
        return image;
    }

    public string GetName()
    {
        return null != rewardName ? rewardName : string.Empty;
    }

    /// <summary>
    /// key 를 가져온다 (HasKey 가 false 여도 저장된 값을 그대로 반환)
    /// </summary>
    public ValueType GetKey()
    {
        return key;
    }

    /// <summary>
    /// value 를 가져온다 (HasValue 가 false 면 0)
    /// </summary>
    public float GetValue()
    {
        return hasValue ? value : 0.0f;
    }

    /// <summary>
    /// 포멧팅 전의 explain1 원본 문자열
    /// </summary>
    public string GetExplain1Format()
    {
        return null != explain1 ? explain1 : string.Empty;
    }

    /// <summary>
    /// 포멧팅 전의 explain2 원본 문자열
    /// </summary>
    public string GetExplain2Format()
    {
        return null != explain2 ? explain2 : string.Empty;
    }

    /// <summary>
    /// explain1 의 포멧 자리에 key / value / name 을 넣은 문자열
    /// </summary>
    public string GetFormattedExplain1()
    {
        return FormatExplain(explain1);
    }

    /// <summary>
    /// explain2 의 포멧 자리에 key / value / name 을 넣은 문자열
    /// </summary>
    public string GetFormattedExplain2()
    {
        return FormatExplain(explain2);
    }

    /// <summary>
    /// 포멧 문자열의 자리에 key / value / name 을 넣는다
    /// 포멧 문자열이 잘못되었으면 경고 후 원본을 그대로 반환한다
    /// </summary>
    private string FormatExplain(string _format)
    {
        if (true == string.IsNullOrEmpty(_format))
        {
            return string.Empty;
        }

        object keyArg = hasKey ? key.ToDisplayName() : string.Empty;
        object valueArg = hasValue ? value : (object)string.Empty;

        try
        {
            return string.Format(_format, keyArg, valueArg, GetName());
        }
        catch (System.FormatException)
        {
            Debug.LogWarning($"[RewardData] explain 포멧 문자열이 잘못되었습니다. ({name}) : {_format}", this);
            return _format;
        }
    }

    /// <returns>key 가 비어 있으면 false</returns>
    public bool TryGetKey(out ValueType _key)
    {
        _key = key;
        return hasKey;
    }

    /// <returns>value 가 비어 있으면 false</returns>
    public bool TryGetValue(out float _value)
    {
        _value = hasValue ? value : 0.0f;
        return hasValue;
    }
    #endregion

    #region Setter
    public void SetImage(Sprite _image)
    {
        image = _image;
    }

    public void SetName(string _name)
    {
        rewardName = _name;
    }

    public void SetKey(ValueType _key)
    {
        key = _key;
        hasKey = true;
    }

    public void SetValue(float _value)
    {
        value = _value;
        hasValue = true;
    }

    public void SetExplain1(string _explain)
    {
        explain1 = _explain;
    }

    public void SetExplain2(string _explain)
    {
        explain2 = _explain;
    }

    /// <summary>
    /// 골랐을 때 실행할 효과를 바꾼다 (런타임에 만든 보상 데이터용)
    /// </summary>
    public void SetEffect(RewardEffect _effect)
    {
        effect = _effect;
    }

    public void ClearImage()
    {
        image = null;
    }

    public void ClearName()
    {
        rewardName = string.Empty;
    }

    public void ClearExplain1()
    {
        explain1 = string.Empty;
    }

    public void ClearExplain2()
    {
        explain2 = string.Empty;
    }

    public void ClearKey()
    {
        hasKey = false;
    }

    public void ClearValue()
    {
        hasValue = false;
    }
    #endregion
}
