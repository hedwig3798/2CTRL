using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 버튼을 누르면 소리를 재생한다 (기본 UI_CLICK, 가운데)
/// - 같은 오브젝트의 SelectPanel 이 있으면 SelectPanel.OnClicked, 없으면 Button.onClick 을 듣는다. (둘 다 있으면 SelectPanel 만)
/// - 일시정지 중에도 들리려면 해당 SoundEvent 의 ignorePause 를 켠다 (UI_CLICK / LVL_SELECT).
/// - SoundManager 가 씬 로드 때 아직 없는 버튼 / SelectPanel 에 자동으로 붙이므로 빌더에서 따로 붙이지 않아도 된다.
///   씬 로드 뒤 템플릿 없이 새로 만든 버튼은 붙지 않으므로 직접 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonSound
    : MonoBehaviour
{
    [Tooltip("클릭 시 재생할 사운드")]
    [SerializeField]
    private SoundId soundId = SoundId.UI_CLICK;

    private Button button;
    private SelectPanel selectPanel;
    private UnityAction clickAction;
    private bool isCached;

    public SoundId SoundId => soundId;

    /// <summary>
    /// 재생할 사운드를 바꾼다 (자동 부착 시 SelectPanel 은 LVL_SELECT)
    /// </summary>
    public void SetSoundId(SoundId _id)
    {
        soundId = _id;
    }

    /// <summary>
    /// _button 에 UIButtonSound 를 붙이고(이미 있으면 그대로) 소리를 _id 로 바꾼다
    /// - SoundManager 의 자동 부착(UI_CLICK) 대신 다른 소리를 내거나, None 으로 클릭음을 끌 때 쓴다 (씬 로드 전 Awake 에서 호출)
    /// </summary>
    public static UIButtonSound Assign(Button _button, SoundId _id)
    {
        if (null == _button)
        {
            return null;
        }
        if (false == _button.TryGetComponent(out UIButtonSound sound))
        {
            sound = _button.gameObject.AddComponent<UIButtonSound>();
        }
        sound.SetSoundId(_id);
        return sound;
    }

    private void OnEnable()
    {
        // 비활성 오브젝트에 붙으면 Awake 가 늦게 불리므로 여기서 찾는다
        CacheTargets();

        if (null != selectPanel)
        {
            selectPanel.OnClicked += HandlePanelClicked;
        }
        else if (null != button)
        {
            button.onClick.AddListener(clickAction);
        }
    }

    private void OnDisable()
    {
        if (null != selectPanel)
        {
            selectPanel.OnClicked -= HandlePanelClicked;
        }
        else if (null != button)
        {
            button.onClick.RemoveListener(clickAction);
        }
    }

    private void CacheTargets()
    {
        if (true == isCached)
        {
            return;
        }
        isCached = true;

        selectPanel = GetComponent<SelectPanel>();
        button = GetComponent<Button>();
        clickAction = HandleClicked;
    }

    private void HandleClicked()
    {
        SoundManager.Play(soundId);
    }

    private void HandlePanelClicked(SelectPanel _panel)
    {
        SoundManager.Play(soundId);
    }
}
