using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 빌더에서 문자열 테이블(Assets/Resources/Localization/StringTable.csv)을 쓰기 위한 헬퍼
    /// - Bind : 고정 라벨에 LocalizedText(key) 를 붙이고, 에디터 미리보기 글자는 한국어로 넣는다
    /// - Unbind : 코드가 글자를 바꾸는 라벨에서 LocalizedText 를 뗀다 (덮어쓰기 충돌 방지)
    /// - Ko : 키의 한국어 문자열 (빌더가 초기 글자를 넣을 때)
    /// - KeyOfKorean : 한국어 원문 → 키 역조회 (업그레이드 단계 표처럼 원문으로 적힌 빌더 표를 키로 바꿀 때)
    /// - script run 에서도 쓸 수 있도록 public
    /// </summary>
    public static class LocalizationBuilderUtil
    {
        private static Dictionary<string, string> koreanToKey;

        /// <summary>
        /// CSV 를 다시 읽는다 (빌더 시작 시 호출하면 CSV 수정이 바로 반영된다)
        /// </summary>
        public static void Reload()
        {
            AssetDatabase.ImportAsset("Assets/Resources/" + StringTable.RESOURCE_PATH + ".csv");
            StringTable.Reload();
            koreanToKey = null;
        }

        public static string Ko(string _key)
        {
            return StringTable.Get(_key, Language.Korean);
        }

        /// <summary>
        /// 무기 보상 이름 키 (예: PoisonZone → reward.weapon.poison_zone.name)
        /// </summary>
        public static string WeaponRewardNameKey(string _weaponId)
        {
            return "reward.weapon." + ToSnake(_weaponId) + ".name";
        }

        /// <summary>
        /// 무기 보상 설명(explain1) 키 (예: PoisonZone → reward.weapon.poison_zone.explain)
        /// </summary>
        public static string WeaponRewardExplainKey(string _weaponId)
        {
            return "reward.weapon." + ToSnake(_weaponId) + ".explain";
        }

        /// <summary>
        /// PascalCase → snake_case (PoisonZone → poison_zone)
        /// </summary>
        public static string ToSnake(string _id)
        {
            if (true == string.IsNullOrEmpty(_id))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(_id.Length + 4);
            for (int i = 0; i < _id.Length; ++i)
            {
                char c = _id[i];
                if (true == char.IsUpper(c))
                {
                    if (0 < i)
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 고정 라벨에 LocalizedText 를 붙이고 key 를 넣는다 (멱등)
        /// </summary>
        public static T Bind<T>(T _text, string _key) where T : TMP_Text
        {
            if (null == _text)
            {
                return null;
            }

            LocalizedText localized = _text.GetComponent<LocalizedText>();
            if (null == localized)
            {
                localized = _text.gameObject.AddComponent<LocalizedText>();
            }

            var so = new SerializedObject(localized);
            so.FindProperty("key").stringValue = _key;
            so.ApplyModifiedPropertiesWithoutUndo();

            _text.text = Ko(_key);
            EditorUtility.SetDirty(_text);
            return _text;
        }

        /// <summary>
        /// 버튼 아래 라벨에 Bind
        /// </summary>
        public static void BindChildLabel(Component _root, string _key)
        {
            if (null == _root)
            {
                return;
            }
            Bind(_root.GetComponentInChildren<TMP_Text>(true), _key);
        }

        /// <summary>
        /// 코드가 글자를 넣는 라벨에서 LocalizedText 를 뗀다
        /// </summary>
        public static void Unbind(TMP_Text _text)
        {
            if (null == _text)
            {
                return;
            }
            LocalizedText localized = _text.GetComponent<LocalizedText>();
            if (null != localized)
            {
                Object.DestroyImmediate(localized, true);
            }
        }

        /// <summary>
        /// 한국어 원문의 키 (없으면 경고 후 원문 그대로 → 런타임에는 원문이 그대로 표시된다)
        /// - 같은 한국어가 여러 키에 있으면 CSV 에서 먼저 나온 키
        /// </summary>
        public static string KeyOfKorean(string _korean)
        {
            if (true == string.IsNullOrEmpty(_korean))
            {
                return string.Empty;
            }

            if (null == koreanToKey)
            {
                koreanToKey = new Dictionary<string, string>();
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/" + StringTable.RESOURCE_PATH + ".csv");
                if (null != asset)
                {
                    // 헤더의 열 이름으로 언어 열을 찾는 StringTable.Parse 결과를 쓴다 (열 순서 무관)
                    int korean = (int)Language.Korean;
                    foreach (KeyValuePair<string, string[]> pair in StringTable.Parse(asset.text))
                    {
                        string ko = korean < pair.Value.Length ? pair.Value[korean] : null;
                        if (false == string.IsNullOrEmpty(ko) && false == koreanToKey.ContainsKey(ko))
                        {
                            koreanToKey.Add(ko, pair.Key);
                        }
                    }
                }
            }

            if (true == koreanToKey.TryGetValue(_korean, out string key))
            {
                return key;
            }

            Debug.LogWarning($"[LocalizationBuilderUtil] StringTable 에 없는 원문 : \"{_korean}\" (원문 그대로 기록)");
            return _korean;
        }
    }
}
