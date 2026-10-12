using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 레벨업 보상 선택지 하나를 표시하는 패널
/// - isSelected 가 true 이면 선택 배경을 표시한다.
/// - 클릭되면 OnClicked 로 알리기만 하고, 선택 여부는 LevelUpUI 가 결정한다.
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

    private void ApplySelected()
    {
        if (null != selectBackground)
        {
            selectBackground.SetActive(isSelected);
        }
    }
}
