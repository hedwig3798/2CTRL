using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창의 볼륨 한 줄 (슬라이더 + 음소거 토글 버튼)
/// - 슬라이더 값(0~1)은 GameSettings.SetVolume, 버튼은 GameSettings.SetMuted 를 뒤집는다.
/// - 켜질 때마다 GameSettings 값을 다시 읽고, 켜져 있는 동안은 변경 이벤트로 표시를 맞춘다.
/// - 음소거 버튼 글자는 StringTable 키. 언어가 바뀌면 다시 표시한다.
/// </summary>
public class VolumeSettingRow
    : MonoBehaviour
{
    [SerializeField] private AudioChannel channel = AudioChannel.Master;
    [SerializeField] private Slider slider;
    [SerializeField] private Button muteButton;

    [Tooltip("음소거 버튼 글자 (비워도 됨)")]
    [SerializeField] private TMP_Text muteLabel;
    [Tooltip("볼륨 % 표시 (비워도 됨)")]
    [SerializeField] private TMP_Text valueLabel;

    [Header("text")]
    [Tooltip("켜짐 상태 버튼 글자 StringTable 키")]
    [SerializeField] private string unmutedKey = "ui.settings.volume.on";
    [Tooltip("음소거 상태 버튼 글자 StringTable 키")]
    [SerializeField] private string mutedKey = "ui.settings.volume.muted";
    [Tooltip("볼륨 % 숫자 포맷 (0~100 정수)")]
    [SerializeField] private string valueNumberFormat = "0";
    [SerializeField] private string valueSuffix = "%";

    [Header("color")]
    [SerializeField] private Color unmutedColor = Color.white;
    [SerializeField] private Color mutedColor = new Color(1f, 0.45f, 0.45f, 1f);

    public AudioChannel Channel => channel;

    private void OnEnable()
    {
        if (null != slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(HandleSliderChanged);
        }
        if (null != muteButton)
        {
            muteButton.onClick.AddListener(HandleMuteClicked);
        }
        GameSettings.OnVolumeChanged += HandleVolumeChanged;
        StringTable.OnLanguageChanged += HandleLanguageChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (null != slider)
        {
            slider.onValueChanged.RemoveListener(HandleSliderChanged);
        }
        if (null != muteButton)
        {
            muteButton.onClick.RemoveListener(HandleMuteClicked);
        }
        GameSettings.OnVolumeChanged -= HandleVolumeChanged;
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
    }

    /// <summary>
    /// GameSettings 값으로 표시 갱신
    /// </summary>
    public void Refresh()
    {
        float volume = GameSettings.GetVolume(channel);
        bool muted = GameSettings.IsMuted(channel);

        if (null != slider)
        {
            slider.SetValueWithoutNotify(volume);
        }
        if (null != valueLabel)
        {
            // string.Format(object) 박싱을 피해 숫자를 직접 문자열로
            valueLabel.text = Mathf.RoundToInt(volume * 100f).ToString(valueNumberFormat) + valueSuffix;
        }
        if (null != muteLabel)
        {
            muteLabel.text = StringTable.Get(true == muted ? mutedKey : unmutedKey);
            muteLabel.color = true == muted ? mutedColor : unmutedColor;
        }
    }

    private void HandleSliderChanged(float _value)
    {
        GameSettings.SetVolume(channel, _value);
        // 같은 값이라 이벤트가 안 와도 라벨은 맞춘다
        Refresh();
    }

    private void HandleMuteClicked()
    {
        GameSettings.SetMuted(channel, false == GameSettings.IsMuted(channel));
        Refresh();
    }

    private void HandleLanguageChanged(Language _language)
    {
        Refresh();
    }

    private void HandleVolumeChanged(AudioChannel _channel)
    {
        if (_channel == channel)
        {
            Refresh();
        }
    }
}
