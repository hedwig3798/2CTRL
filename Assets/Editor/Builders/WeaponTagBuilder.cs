using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 무기 프리팹에 태그(Weapon.statTags) 를 적용한다 (멱등)
    /// - 태그가 없는 수치는 플레이어 버프 / 디버프를 받지 않는다
    /// - 표에 없는 무기는 All
    /// - 무기 프리팹을 새로 만드는 빌더(WeaponUpgradeBuilder, ExtraWeaponBuilder 등) 를 다시 돌린 뒤에는 이것도 다시 실행한다
    /// </summary>
    public static class WeaponTagBuilder
    {
        private const string WEAPON_DIR = "Assets/prefab/weapon";

        private const WeaponStatTag NO_RANGE = WeaponStatTag.Damage | WeaponStatTag.AttackSpeed;

        // 무기 프리팹 경로 (WEAPON_DIR 기준) -> 태그. 드론 자식 무기는 실행 중 Drone 의 태그를 복사한다
        private static readonly Dictionary<string, WeaponStatTag> Tags = new Dictionary<string, WeaponStatTag>
        {
            // 사거리 0, 사거리를 쓰지 않는 무기
            { "Saw/Saw.prefab", NO_RANGE },
            { "Shockwave/Shockwave.prefab", NO_RANGE },
            { "Tornado/Tornado.prefab", NO_RANGE },
            { "Vine/Vine.prefab", NO_RANGE },
        };

        [MenuItem("Tools/Builders/WeaponTag")]
        public static string Build()
        {
            StringBuilder log = new StringBuilder();
            int changed = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WEAPON_DIR }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Weapon weapon = null != prefab ? prefab.GetComponent<Weapon>() : null;
                if (null == weapon)
                {
                    continue;
                }

                string key = path.Substring(WEAPON_DIR.Length + 1);
                WeaponStatTag tags = Tags.TryGetValue(key, out WeaponStatTag found) ? found : WeaponStatTag.All;

                log.AppendLine($"{key} : {tags}");
                if (tags == weapon.statTags)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(weapon);
                so.FindProperty("statTags").intValue = (int)tags;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
                changed++;
            }

            foreach (string key in Tags.Keys)
            {
                if (null == AssetDatabase.LoadAssetAtPath<Weapon>($"{WEAPON_DIR}/{key}"))
                {
                    log.AppendLine($"[경고] 무기 프리팹을 찾지 못했습니다: {key}");
                }
            }

            AssetDatabase.SaveAssets();
            log.Insert(0, $"[WeaponTagBuilder] 변경 {changed}개\n");
            Debug.Log(log.ToString());
            return log.ToString();
        }
    }
}
