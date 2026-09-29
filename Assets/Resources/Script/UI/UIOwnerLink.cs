using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canvas로 옮겨진 UI의 원래 부모(owner)에 자동으로 붙는 컴포넌트
/// owner의 활성화 / 비활성화 / 파괴를 등록된 UI에 전달한다.
/// (CanvasUI 가 자동으로 추가하므로 직접 붙일 필요 없음)
/// </summary>
[DisallowMultipleComponent]
public class UIOwnerLink
    : MonoBehaviour
{
    private readonly List<CanvasUI> uis = new List<CanvasUI>();

    public void Register(CanvasUI _ui)
    {
        if (null == _ui || true == uis.Contains(_ui))
        {
            return;
        }

        uis.Add(_ui);
        _ui.gameObject.SetActive(isActiveAndEnabled);
    }

    public void Unregister(CanvasUI _ui)
    {
        uis.Remove(_ui);
    }

    private void OnEnable()
    {
        SetUIsActive(true);
    }

    private void OnDisable()
    {
        SetUIsActive(false);
    }

    private void OnDestroy()
    {
        // 순회 중 Unregister 로 리스트가 바뀌므로 복사본 사용
        CanvasUI[] targets = uis.ToArray();
        uis.Clear();

        foreach (CanvasUI ui in targets)
        {
            // 씬 언로드 등으로 이미 파괴된 UI는 건너뜀
            if (null == ui)
            {
                continue;
            }
            ui.OnOwnerDestroyed();
        }
    }

    private void SetUIsActive(bool _active)
    {
        foreach (CanvasUI ui in uis)
        {
            if (null == ui)
            {
                continue;
            }
            ui.gameObject.SetActive(_active);
        }
    }
}
