using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 편집 모드에서만 루트 Canvas 의 렌더링을 꺼서 Game 뷰에 UI 가 보이지 않게 하는 에디터 전용 토글
/// - GameObject 는 활성 상태 그대로 두고 Canvas.enabled 만 끄므로 스크립트 / 이벤트 구독에 영향이 없다.
/// - 플레이 진입, 씬 저장, 빌드, 스크립트 리로드 직전에 항상 원래 값으로 복원하므로 직렬화 데이터와 인게임 동작은 바뀌지 않는다.
/// - Hierarchy 에서 선택한 오브젝트가 속한 Canvas 는 편집할 수 있도록 숨기지 않는다.
/// </summary>
[InitializeOnLoad]
public static class EditModeUIHider
{
    private const string MenuPath = "Tools/UI/Hide UI In Edit Mode";
    private const string PrefsKey = "EditModeUIHider.Enabled";

    // 숨긴 Canvas 와 숨기기 전 원래 enabled 값
    private static readonly Dictionary<Canvas, bool> hiddenCanvases = new Dictionary<Canvas, bool>();

    // 저장 / 빌드 / 플레이 진입 중에는 다시 숨기지 않도록 막음
    private static bool isSuspended = false;

    // 우리가 바꾼 값으로 인해 이벤트가 다시 들어와 재귀되는 것을 막음
    private static bool isApplying = false;

    private static bool IsOn
    {
        get => EditorPrefs.GetBool(PrefsKey, false);
        set => EditorPrefs.SetBool(PrefsKey, value);
    }

    static EditModeUIHider()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.hierarchyChanged += Apply;
        EditorApplication.quitting += RestoreAll;
        Selection.selectionChanged += Apply;

        EditorSceneManager.sceneSaving += HandleSceneSaving;
        EditorSceneManager.sceneSaved += HandleSceneSaved;
        EditorSceneManager.sceneOpened += HandleSceneOpened;

        AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;

        // 도메인 리로드 직후에는 씬이 아직 준비되지 않았을 수 있으므로 한 프레임 뒤 적용
        EditorApplication.delayCall += Apply;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        IsOn = !IsOn;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, IsOn);
        return true;
    }

    /// <summary>
    /// 현재 상태(토글 / 플레이 모드 / 선택)에 맞게 Canvas 를 숨기거나 복원
    /// </summary>
    private static void Apply()
    {
        if (true == isApplying)
        {
            return;
        }

        if (false == IsOn
            || true == isSuspended
            || true == EditorApplication.isPlayingOrWillChangePlaymode)
        {
            RestoreAll();
            return;
        }

        isApplying = true;

        RemoveDestroyedCanvases();

        foreach (Canvas canvas in FindRootCanvasesInLoadedScenes())
        {
            bool shouldHide = false == IsSelected(canvas);
            bool isHidden = hiddenCanvases.ContainsKey(canvas);

            if (true == shouldHide && false == isHidden)
            {
                hiddenCanvases.Add(canvas, canvas.enabled);
                canvas.enabled = false;
            }
            else if (false == shouldHide && true == isHidden)
            {
                canvas.enabled = hiddenCanvases[canvas];
                hiddenCanvases.Remove(canvas);
            }
        }

        isApplying = false;
    }

    /// <summary>
    /// 숨긴 모든 Canvas 를 원래 enabled 값으로 되돌림
    /// </summary>
    private static void RestoreAll()
    {
        if (0 == hiddenCanvases.Count)
        {
            return;
        }

        isApplying = true;

        foreach (KeyValuePair<Canvas, bool> pair in hiddenCanvases)
        {
            if (null != pair.Key)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        hiddenCanvases.Clear();

        isApplying = false;
    }

    private static void RemoveDestroyedCanvases()
    {
        List<Canvas> destroyed = null;

        foreach (Canvas canvas in hiddenCanvases.Keys)
        {
            if (null == canvas)
            {
                destroyed ??= new List<Canvas>();
                destroyed.Add(canvas);
            }
        }

        if (null == destroyed)
        {
            return;
        }

        foreach (Canvas canvas in destroyed)
        {
            hiddenCanvases.Remove(canvas);
        }
    }

    /// <summary>
    /// 일반 씬(프리뷰 / 프리팹 스테이지 제외)에 있는 루트 Canvas 만 반환
    /// </summary>
    private static IEnumerable<Canvas> FindRootCanvasesInLoadedScenes()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Canvas canvas in canvases)
        {
            Scene scene = canvas.gameObject.scene;
            if (false == scene.IsValid()
                || false == scene.isLoaded
                || true == EditorSceneManager.IsPreviewScene(scene))
            {
                continue;
            }

            Transform parent = canvas.transform.parent;
            if (null != parent && null != parent.GetComponentInParent<Canvas>(true))
            {
                continue;
            }

            yield return canvas;
        }
    }

    private static bool IsSelected(Canvas _canvas)
    {
        foreach (Transform selected in Selection.transforms)
        {
            if (true == selected.IsChildOf(_canvas.transform))
            {
                return true;
            }
        }

        return false;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange _state)
    {
        switch (_state)
        {
            // 플레이 진입 전 씬 백업보다 먼저 복원해 인게임에는 원래 값이 들어가도록 함
            case PlayModeStateChange.ExitingEditMode:
                RestoreAll();
                break;

            case PlayModeStateChange.EnteredEditMode:
                Apply();
                break;
        }
    }

    private static void HandleSceneSaving(Scene _scene, string _path)
    {
        isSuspended = true;
        RestoreAll();
    }

    private static void HandleSceneSaved(Scene _scene)
    {
        isSuspended = false;

        // 여러 씬을 연속 저장하는 경우를 위해 저장이 모두 끝난 뒤 다시 숨김
        EditorApplication.delayCall -= Apply;
        EditorApplication.delayCall += Apply;
    }

    private static void HandleSceneOpened(Scene _scene, OpenSceneMode _mode)
    {
        Apply();
    }

    /// <summary>
    /// 빌드 직전 복원. 빌드가 끝나면 다음 에디터 업데이트에서 다시 숨김
    /// </summary>
    private class BuildPreprocessor
        : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport _report)
        {
            RestoreAll();
            EditorApplication.delayCall -= Apply;
            EditorApplication.delayCall += Apply;
        }
    }
}
