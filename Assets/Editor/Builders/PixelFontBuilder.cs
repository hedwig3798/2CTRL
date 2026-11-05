using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Builders
{
    /// <summary>
    /// 갈무리(Galmuri11) 픽셀 폰트용 TMP_FontAsset 을 만들고, 데미지 텍스트(DamageTextBinder)에 연결한다 (멱등)
    /// - 선명하게 : RASTER_HINTED(SDF 아님) + 아틀라스 Point 필터. 글자 크기는 12 의 배수에서 가장 깨끗하다.
    /// - 숫자 · 한글은 Dynamic 아틀라스로 필요할 때 채운다. (다중 아틀라스 허용)
    /// - Regular 경로는 ArtResourceBuilder 와 같다 (Galmuri11 Pixel.asset). 이미 있으면 그대로 쓴다.
    /// - Bold 에셋을 Regular 의 굵게(700) 서체로 연결해, Bold 스타일에서 가짜 굵게 대신 진짜 Bold 글리프를 쓴다.
    /// - 갈무리에 없는 글자는 NotoSansKR SDF 로 대체 (있을 때만)
    /// </summary>
    public static class PixelFontBuilder
    {
        public const string REGULAR_TTF = "Assets/Fonts/Galmuri/Galmuri11.ttf";
        public const string REGULAR_ASSET = "Assets/Fonts/Galmuri/Galmuri11 Pixel.asset";
        public const string BOLD_TTF = "Assets/Fonts/Galmuri/Galmuri11-Bold.ttf";
        public const string BOLD_ASSET = "Assets/Fonts/Galmuri/Galmuri11-Bold Pixel.asset";
        private const string FALLBACK_SEARCH = "NotoSansKR-Regular SDF t:TMP_FontAsset";
        private const string PREFAB_ROOT = "Assets/prefab";

        // 갈무리11 은 12px 격자. 2배로 래스터해 12 / 24 / 36 ... 크기에서 깨끗하게
        private const int SAMPLING_POINT_SIZE = 24;
        private const int ATLAS_PADDING = 2;
        private const int ATLAS_SIZE = 1024;
        private const int BOLD_WEIGHT_INDEX = 7; // fontWeightTable[7] = 700 (Bold)

        // 데미지 텍스트 설정
        private const string DAMAGE_SORTING_LAYER = "Front UI";

        [MenuItem("Tools/Builders/PixelFont")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PixelFontBuilder]");

            TMP_FontAsset regular = GetOrCreateRegular();
            TMP_FontAsset bold = GetOrCreateBold();
            sb.AppendLine("Regular: " + (null != regular ? REGULAR_ASSET : "실패 " + REGULAR_TTF));
            sb.AppendLine("Bold: " + (null != bold ? BOLD_ASSET : "실패 " + BOLD_TTF));

            sb.AppendLine(AssignDamageTextFont());

            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>
        /// UI 용 Regular 픽셀 폰트 (Bold 를 굵게 서체로 연결)
        /// </summary>
        public static TMP_FontAsset GetOrCreateRegular()
        {
            TMP_FontAsset regular = GetOrCreate(REGULAR_TTF, REGULAR_ASSET, "Galmuri11 Pixel");
            TMP_FontAsset bold = GetOrCreate(BOLD_TTF, BOLD_ASSET, "Galmuri11-Bold Pixel");
            if (null != regular && null != bold)
            {
                TMP_FontWeightPair[] table = regular.fontWeightTable;
                if (null != table && BOLD_WEIGHT_INDEX < table.Length && table[BOLD_WEIGHT_INDEX].regularTypeface != bold)
                {
                    // 배열 요소(struct)를 직접 수정 (getter 가 내부 배열을 그대로 돌려준다)
                    table[BOLD_WEIGHT_INDEX].regularTypeface = bold;
                    EditorUtility.SetDirty(regular);
                }
            }
            return regular;
        }

        /// <summary>
        /// 데미지 숫자 등 강조용 Bold 픽셀 폰트
        /// </summary>
        public static TMP_FontAsset GetOrCreateBold()
        {
            return GetOrCreate(BOLD_TTF, BOLD_ASSET, "Galmuri11-Bold Pixel");
        }

        /// <summary>
        /// ttf 로 래스터(픽셀) Dynamic TMP_FontAsset 을 만들거나 기존 것을 불러와 설정을 맞춘다
        /// </summary>
        public static TMP_FontAsset GetOrCreate(string _ttfPath, string _assetPath, string _name)
        {
            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_assetPath);
            if (null == fa)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(_ttfPath);
                if (null == font)
                {
                    Debug.LogWarning("[PixelFontBuilder] ttf 없음 " + _ttfPath);
                    return null;
                }

                fa = TMP_FontAsset.CreateFontAsset(font, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.RASTER_HINTED,
                    ATLAS_SIZE, ATLAS_SIZE, AtlasPopulationMode.Dynamic, true);
                if (null == fa)
                {
                    Debug.LogWarning("[PixelFontBuilder] 폰트 에셋 생성 실패 " + _ttfPath);
                    return null;
                }

                fa.name = _name;
                AssetDatabase.CreateAsset(fa, _assetPath);
                fa.atlasTextures[0].name = _name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = _name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            // 픽셀이 번지지 않도록
            foreach (Texture2D atlas in fa.atlasTextures)
            {
                if (null != atlas && FilterMode.Point != atlas.filterMode)
                {
                    atlas.filterMode = FilterMode.Point;
                    EditorUtility.SetDirty(atlas);
                }
            }

            TMP_FontAsset fallback = AssetDatabase.FindAssets(FALLBACK_SEARCH)
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(f => null != f);
            if (null != fallback && (null == fa.fallbackFontAssetTable || false == fa.fallbackFontAssetTable.Contains(fallback)))
            {
                fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            }

            EditorUtility.SetDirty(fa);
            return fa;
        }

        /// <summary>
        /// Assets/prefab 아래 DamageTextBinder 가 있는 모든 프리팹에 Bold 픽셀 폰트 / 정렬 레이어를 연결
        /// </summary>
        public static string AssignDamageTextFont()
        {
            TMP_FontAsset bold = GetOrCreateBold();
            if (null == bold)
            {
                return "데미지 폰트: Bold 폰트 없음, 건너뜀";
            }

            var changed = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_ROOT }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (null == asset || 0 == asset.GetComponentsInChildren<DamageTextBinder>(true).Length)
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (DamageTextBinder binder in root.GetComponentsInChildren<DamageTextBinder>(true))
                    {
                        var so = new SerializedObject(binder);
                        so.FindProperty("font").objectReferenceValue = bold;
                        so.FindProperty("fontStyle").intValue = (int)FontStyles.Normal;
                        so.FindProperty("sortingLayerName").stringValue = DAMAGE_SORTING_LAYER;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add(path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return "데미지 폰트 연결 (" + changed.Count + "): " + string.Join(", ", changed);
        }
    }
}
