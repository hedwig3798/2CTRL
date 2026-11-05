using TMPro;
using UnityEngine;

/// <summary>
/// 고정 라벨용 : 같은 오브젝트의 TMP_Text 에 StringTable 의 key 문자열을 넣는다
/// - OnEnable 과 언어가 바뀔 때 적용, OnDisable 에서 구독 해제
/// - 코드에서 직접 text 를 바꾸는 라벨에는 붙이지 않는다 (덮어쓰기 충돌)
///
/// 사용 예)
///   GetComponent&lt;LocalizedText&gt;().Key = "ui.pause.title";
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText
    : MonoBehaviour
{
    [Tooltip("StringTable.csv 의 key")]
    [SerializeField]
    private string key;

    private TMP_Text target;

    public string Key
    {
        get => key;
        set
        {
            if (value == key)
            {
                return;
            }
            key = value;
            if (true == isActiveAndEnabled)
            {
                Apply();
            }
        }
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += HandleLanguageChanged;
        Apply();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
    }

    /// <summary>
    /// 현재 언어로 다시 적용
    /// </summary>
    public void Apply()
    {
        if (null == target)
        {
            target = GetComponent<TMP_Text>();
        }
        if (null == target || true == string.IsNullOrEmpty(key))
        {
            return;
        }

        target.text = StringTable.Get(key);
    }

    private void HandleLanguageChanged(Language _language)
    {
        Apply();
    }
}
