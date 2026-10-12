using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Select Panel 묶음. 그룹 안에서는 하나만 선택된다.
/// - 클릭된 패널만 선택하고 나머지는 해제하며, 같은 패널을 다시 눌러도 해제되지 않는다.
/// - 활성화될 때마다 선택을 초기화하므로 UI 를 열 때마다 선택 없는 상태로 시작한다.
/// </summary>
public class SelectPanelGroup
    : MonoBehaviour
{
    [Tooltip("이 그룹의 선택지 패널 (비어 있으면 자식에서 자동으로 찾음)")]
    [SerializeField]
    private List<SelectPanel> selectPanels = new List<SelectPanel>();

    // 현재 선택된 패널 (없으면 null)
    public SelectPanel SelectedPanel { get; private set; }

    public event Action<SelectPanel> OnSelectionChanged;

    private void Awake()
    {
        if (0 == selectPanels.Count)
        {
            // 하위에 다른 그룹이 있으면 그 그룹의 패널은 제외
            foreach (SelectPanel selectPanel in GetComponentsInChildren<SelectPanel>(true))
            {
                if (this == selectPanel.GetComponentInParent<SelectPanelGroup>(true))
                {
                    selectPanels.Add(selectPanel);
                }
            }
        }
    }

    private void OnEnable()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel)
            {
                selectPanel.OnClicked += HandlePanelClicked;
            }
        }

        ClearSelection();
    }

    private void OnDisable()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel)
            {
                selectPanel.OnClicked -= HandlePanelClicked;
            }
        }
    }

    private void HandlePanelClicked(SelectPanel _clicked)
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null == selectPanel)
            {
                continue;
            }

            selectPanel.IsSelected = (selectPanel == _clicked);
        }

        SelectedPanel = _clicked;
        OnSelectionChanged?.Invoke(_clicked);
    }

    public void ClearSelection()
    {
        foreach (SelectPanel selectPanel in selectPanels)
        {
            if (null != selectPanel)
            {
                selectPanel.IsSelected = false;
            }
        }

        SelectedPanel = null;
    }
}
