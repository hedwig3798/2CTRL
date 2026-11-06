using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Builders.Visual
{
    /// <summary>
    /// 피격 비네트 (멱등)
    /// 1. 가장자리 붉은 그라데이션 텍스처 생성 (Textuer/Hud, Art 폴더의 Point 필터 설정을 피한다)
    /// 2. Player 프리팹 ScreenSpaceUI 아래에 HitVignette(Image + HitVignetteUI) 배치
    /// </summary>
    public static class HitVignetteBuilder
    {
        private const string TEX_PATH = "Assets/Resources/Textuer/Hud/HitVignette.png";
        private const string PLAYER_PREFAB = "Assets/prefab/Player/Player.prefab";
        private const string NODE_NAME = "HitVignette";

        private const int TEX_SIZE = 256;
        private static readonly Color EDGE_COLOR = new Color(0.85f, 0.05f, 0.05f);
        private const float EDGE_MAX_ALPHA = 0.4f;   // 가장 바깥 픽셀의 진하기 ("약간" 붉게)
        private const float EDGE_WIDTH = 0.15f;       // 가장자리에서 안쪽으로 번지는 폭 (텍스처 비율)

        public static string Build()
        {
            Sprite sprite = BuildTexture();
            BuildPrefab(sprite);
            return "HitVignette 완료: " + TEX_PATH + " / " + PLAYER_PREFAB;
        }

        private static Sprite BuildTexture()
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color[TEX_SIZE * TEX_SIZE];
            for (int y = 0; y < TEX_SIZE; ++y)
            {
                for (int x = 0; x < TEX_SIZE; ++x)
                {
                    float u = (x + 0.5f) / TEX_SIZE;
                    float v = (y + 0.5f) / TEX_SIZE;

                    // 가장 가까운 변까지의 거리 → 0(가장자리) ~ 1(안쪽)
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float t = 1f - Mathf.Clamp01(edge / EDGE_WIDTH);

                    // 모서리는 두 변이 겹쳐 조금 더 진하게
                    float cx = Mathf.Abs(u - 0.5f) * 2f;
                    float cy = Mathf.Abs(v - 0.5f) * 2f;
                    float corner = Mathf.Clamp01((cx * cx + cy * cy - 1f) * 0.5f);

                    float a = Mathf.Clamp01(t * t + corner * 0.3f) * EDGE_MAX_ALPHA;
                    pixels[y * TEX_SIZE + x] = new Color(EDGE_COLOR.r, EDGE_COLOR.g, EDGE_COLOR.b, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(TEX_PATH, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TEX_PATH, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TEX_PATH);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(TEX_PATH);
        }

        private static void BuildPrefab(Sprite _sprite)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB);
            try
            {
                Transform canvas = root.transform.Find("ScreenSpaceUI");
                if (null == canvas)
                {
                    throw new System.Exception("Player 프리팹에 ScreenSpaceUI 가 없다");
                }

                Transform node = canvas.Find(NODE_NAME);
                if (null == node)
                {
                    var go = new GameObject(NODE_NAME, typeof(RectTransform));
                    go.layer = canvas.gameObject.layer;
                    node = go.transform;
                    node.SetParent(canvas, false);
                }
                node.SetAsLastSibling();

                var rt = (RectTransform)node;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;

                Image img = node.GetComponent<Image>();
                if (null == img)
                {
                    img = node.gameObject.AddComponent<Image>();
                }
                img.sprite = _sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0f);
                img.enabled = false;

                if (null == node.GetComponent<HitVignetteUI>())
                {
                    node.gameObject.AddComponent<HitVignetteUI>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PLAYER_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
