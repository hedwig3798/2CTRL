using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬에 EventSystem 이 항상 하나만 존재하도록 보장
/// - 씬이 로드될 때마다 검사 (추가 로드 포함)
/// - 여러 개면 하나만 남기고 제거, 없으면 새로 생성
/// </summary>
public static class EventSystemGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    private static void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        Ensure();
    }

    /// <summary>
    /// EventSystem 을 하나로 맞추고 남은 EventSystem 반환
    /// </summary>
    public static EventSystem Ensure()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        if (0 == systems.Length)
        {
            return Create();
        }

        // 현재 사용 중인 EventSystem 우선, 없으면 첫 번째
        EventSystem keep = (null != EventSystem.current && 0 <= System.Array.IndexOf(systems, EventSystem.current))
            ? EventSystem.current
            : systems[0];

        foreach (EventSystem system in systems)
        {
            if (keep == system)
            {
                continue;
            }
            Remove(system);
        }

        return keep;
    }

    private static void Remove(EventSystem _system)
    {
        Debug.LogWarning($"[EventSystemGuard] 중복된 EventSystem 제거 : {_system.gameObject.name}");

        // Destroy 는 프레임 끝에 처리되므로 즉시 비활성화
        _system.enabled = false;

        // EventSystem 전용 오브젝트면 통째로 제거, 다른 컴포넌트나 자식이 있으면 비활성화만 유지
        GameObject go = _system.gameObject;
        foreach (Component component in go.GetComponents<Component>())
        {
            if (false == (component is Transform)
                && false == (component is EventSystem)
                && false == (component is BaseInputModule))
            {
                return;
            }
        }
        if (0 < go.transform.childCount)
        {
            return;
        }

        Object.Destroy(go);
    }

    private static EventSystem Create()
    {
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));

#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif

        return go.GetComponent<EventSystem>();
    }
}
