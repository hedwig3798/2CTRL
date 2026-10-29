using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders.Visual
{
    /// <summary>ChainLightning / SweepSlash 시각 적용 (멱등)</summary>
    public static class WeaponChainSweepVisual
    {
        private const string CHAIN = "Assets/prefab/weapon/Chain/ChainLightning.prefab";
        private const string SLASH = "Assets/prefab/weapon/Sweep/SweepSlash.prefab";
        private const string CRESCENT = "Assets/Resources/Textuer/SweepCrescent.png";
        private const string UNLIT = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        public static string Build()
        {
            var sb = new StringBuilder();
            Sprite crescent = EnsureCrescent();
            sb.AppendLine("crescent: " + (crescent != null));
            sb.AppendLine("chain: " + ApplyChain());
            sb.AppendLine("slash: " + ApplySlash(crescent));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }

        private static Sprite EnsureCrescent()
        {
            if (!File.Exists(CRESCENT))
            {
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        // 바깥 원 - 뒤(-x)로 치우친 안쪽 원 = 앞(+x)으로 볼록한 초승달
                        float px = (x + 0.5f) / N * 2f - 1f;
                        float py = (y + 0.5f) / N * 2f - 1f;
                        float outer = 1f - Mathf.Sqrt(px * px + py * py);
                        float ix = px + 0.45f;
                        float inner = Mathf.Sqrt(ix * ix + py * py) - 0.95f;
                        float a = Mathf.Clamp01(Mathf.Min(outer, inner) * 20f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                File.WriteAllBytes(CRESCENT, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(CRESCENT);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(CRESCENT);
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != 128))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spritePixelsPerUnit = 128;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(CRESCENT);
        }

        private static string ApplyChain()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CHAIN);
            try
            {
                var sb = new StringBuilder();
                Shader sh = Shader.Find(UNLIT);
                Transform line = root.transform.Find("line");
                if (line != null && line.TryGetComponent(out LineRenderer lr))
                {
                    sb.Append("prevMat=" + (lr.sharedMaterial != null ? lr.sharedMaterial.name : "null") + " ");
                    if (lr.sharedMaterial == null || lr.sharedMaterial.shader != sh)
                    {
                        var mat = new Material(sh) { name = "ChainLightningLine" };
                        string mp = "Assets/prefab/weapon/Chain/ChainLightningLine.mat";
                        var existing = AssetDatabase.LoadAssetAtPath<Material>(mp);
                        if (existing == null) { AssetDatabase.CreateAsset(mat, mp); existing = mat; }
                        lr.sharedMaterial = existing;
                    }
                    lr.widthMultiplier = 0.12f;
                    lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f));
                    var g = new Gradient();
                    g.SetKeys(
                        new[] { new GradientColorKey(new Color(0.35f, 0.8f, 1f), 0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(new Color(0.35f, 0.8f, 1f), 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                    lr.colorGradient = g;
                    lr.numCapVertices = 4;
                    lr.numCornerVertices = 2;
                    lr.sortingOrder = 6;
                    sb.Append("line ok ");
                }
                else sb.Append("line missing ");
                Transform vis = root.transform.Find("visual");
                if (vis != null && vis.TryGetComponent(out SpriteRenderer sr))
                {
                    sr.color = new Color(0.7f, 0.92f, 1f, 1f);
                    vis.localScale = new Vector3(0.3f, 0.3f, 1f);
                    sr.sortingOrder = 7;
                    sb.Append("visual ok(sprite=" + (sr.sprite != null ? sr.sprite.name : "null") + ")");
                }
                else sb.Append("visual missing");
                PrefabUtility.SaveAsPrefabAsset(root, CHAIN);
                return sb.ToString();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string ApplySlash(Sprite crescent)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SLASH);
            try
            {
                Transform vis = root.transform.Find("visual");
                if (vis == null || !vis.TryGetComponent(out SpriteRenderer sr)) return "visual missing";
                if (crescent != null) sr.sprite = crescent;
                sr.color = new Color(0.8f, 0.95f, 1f, 0.85f);
                vis.localScale = new Vector3(0.5f, 1.4f, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, SLASH);
                return "ok";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
