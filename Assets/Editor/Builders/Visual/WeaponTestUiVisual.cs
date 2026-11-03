using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// WeaponTest 씬 무기 목록(18개)이 세로로 넘치지 않게 2열 격자로 바꾼다. 멱등
    /// </summary>
    public static class WeaponTestUiVisual
    {
        private const string SCENE = "Assets/Scenes/WeaponTest.unity";

        public static string Build()
        {
            EditorSceneManager.OpenScene(SCENE);
            GameObject panel = GameObject.Find("WeaponListPanel");
            if (null == panel)
            {
                return "WeaponListPanel not found";
            }
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            if (null != vlg)
            {
                Object.DestroyImmediate(vlg);
            }
            var grid = panel.GetComponent<GridLayoutGroup>();
            if (null == grid)
            {
                grid = panel.AddComponent<GridLayoutGroup>();
            }
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.cellSize = new Vector2(150f, 44f);
            grid.spacing = new Vector2(8f, 8f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            var rt = (RectTransform)panel.transform;
            // 좌상단 스탯 패널(IMGUI) 아래에서 시작
            rt.sizeDelta = new Vector2(344f, -320f);
            rt.anchoredPosition = new Vector2(20f, -140f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return "ok";
        }
    }
}
