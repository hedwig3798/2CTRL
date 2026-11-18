using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// 차원 강화 창(TitleScene) + 결과 화면 차원력 줄(GameScene) 시각 정리. 멱등.
    /// ArtResourceBuilder 이후에 실행할 것 (버튼 스타일/폰트를 덮어쓰므로).
    /// </summary>
    public static class UpgradeUIVisual
    {
        private const string TITLE_SCENE = "Assets/Scenes/TitleScene.unity";
        private const string GAME_SCENE = "Assets/Scenes/GameScene.unity";

        private static readonly Color RowColor = new Color(0.17f, 0.17f, 0.25f, 1f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 1f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.88f);

        public static string Build()
        {
            var sb = new System.Text.StringBuilder();

            var title = EditorSceneManager.OpenScene(TITLE_SCENE, OpenSceneMode.Single);
            Image dim = FindAll<Image>(title).FirstOrDefault(i => i.name == "PermanentUpgradePanel");
            Transform row = FindAll<Transform>(title).FirstOrDefault(t => t.name == "RowTemplate");
            if (null != dim)
            {
                dim.color = Dim;
                EditorUtility.SetDirty(dim);
            }
            if (null != row)
            {
                var bg = row.GetComponent<Image>();
                bg.sprite = null;
                bg.type = Image.Type.Simple;
                bg.color = RowColor;
                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(16, 28, 8, 8);
                hlg.spacing = 14f;
                EditorUtility.SetDirty(bg);
                EditorUtility.SetDirty(hlg);

                Transform price = row.Find("PriceLabel");
                if (null != price)
                {
                    var t = price.GetComponent<TMP_Text>();
                    t.color = Gold;
                    EditorUtility.SetDirty(t);
                }
                Transform buy = row.Find("BuyButton");
                if (null != buy)
                {
                    var b = buy.GetComponent<Button>();
                    b.transition = Selectable.Transition.ColorTint;
                    b.colors = new ColorBlock
                    {
                        normalColor = Color.white,
                        highlightedColor = new Color(1f, 0.95f, 0.8f, 1f),
                        pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f),
                        selectedColor = Color.white,
                        disabledColor = new Color(0.35f, 0.35f, 0.4f, 0.75f),
                        colorMultiplier = 1f,
                        fadeDuration = 0.05f,
                    };
                    EditorUtility.SetDirty(b);
                }
                sb.AppendLine("RowTemplate styled");
            }
            EditorSceneManager.MarkSceneDirty(title);
            EditorSceneManager.SaveScene(title);

            var game = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Single);
            TMP_Text meta = FindAll<TMP_Text>(game).FirstOrDefault(t => t.name == "MetaRewardLabel");
            if (null != meta)
            {
                meta.color = Gold;
                EditorUtility.SetDirty(meta);
                sb.AppendLine("MetaRewardLabel styled");
            }
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            return sb.ToString();
        }

        private static T[] FindAll<T>(UnityEngine.SceneManagement.Scene _s) where T : Component
        {
            return _s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        }
    }
}
