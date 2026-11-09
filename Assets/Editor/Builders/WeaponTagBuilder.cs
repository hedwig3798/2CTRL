using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// 무기 프리팹에 태그(Weapon.statTags) 를 적용한다 (멱등)
    /// - 태그가 없는 수치는 플레이어 버프 / 디버프를 받지 않는다
    /// - 기본 = Damage | AttackSpeed | Range. 사거리를 쓰지 않는 무기는 Range 를 빼고, 지속형 / 다발 무기에 Duration / ProjectileCount 를 더한다
    /// - 무기 프리팹을 새로 만드는 빌더(WeaponUpgradeBuilder, ExtraWeaponBuilder 등) 를 다시 돌린 뒤에는 이것도 다시 실행한다
    /// </summary>
    public static class WeaponTagBuilder
    {
        private const string WEAPON_DIR = "Assets/prefab/weapon";

        private const WeaponStatTag BASE = WeaponStatTag.Damage | WeaponStatTag.AttackSpeed | WeaponStatTag.Range;

        // 사거리 0, 사거리를 쓰지 않는 무기
        private static readonly HashSet<string> NoRange = new HashSet<string> { "Saw", "Shockwave", "SpinBlade", "Tornado", "Vine" };

        // 공속 패시브를 받지 않는 무기 (SpinBlade 는 쿨타임 60초 고정, StormOrb 는 타격 간격이 발사체 고정값)
        private static readonly HashSet<string> NoAttackSpeed = new HashSet<string> { "SpinBlade", "StormOrb" };

        // 지속형 공격 (장판 수명, Flame 분사 시간, Tornado 수명 - Capped 라 수명이 늘면 재시전 간격도 늘어난다)
        private static readonly HashSet<string> Duration = new HashSet<string> { "Vine", "PoisonZone", "Root", "Flame", "Tornado" };

        // 한 번에 발사하는 발사체 수 (Drone 은 드론 수, Lightning 은 낙뢰 수)
        private static readonly HashSet<string> ProjectileCount = new HashSet<string>
        {
            "Boomerang", "Drone", "Explosive", "Laser", "Lightning", "Spirit", "SplitShot", "Tornado", "Root", "StormOrb",
        };

        /// <summary>
        /// 무기 이름(프리팹 이름) 의 태그
        /// </summary>
        public static WeaponStatTag GetTags(string _weaponName)
        {
            WeaponStatTag tags = BASE;
            if (NoRange.Contains(_weaponName))
            {
                tags &= ~WeaponStatTag.Range;
            }
            if (NoAttackSpeed.Contains(_weaponName))
            {
                tags &= ~WeaponStatTag.AttackSpeed;
            }
            if (Duration.Contains(_weaponName))
            {
                tags |= WeaponStatTag.Duration;
            }
            if (ProjectileCount.Contains(_weaponName))
            {
                tags |= WeaponStatTag.ProjectileCount;
            }
            return tags;
        }

        [MenuItem("Tools/Builders/WeaponTag")]
        public static string Build()
        {
            StringBuilder log = new StringBuilder();
            HashSet<string> found = new HashSet<string>();
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

                found.Add(prefab.name);
                WeaponStatTag tags = GetTags(prefab.name);
                log.AppendLine($"{prefab.name} : {tags}");
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

            foreach (HashSet<string> set in new[] { NoRange, NoAttackSpeed, Duration, ProjectileCount })
            {
                foreach (string name in set)
                {
                    if (false == found.Contains(name))
                    {
                        log.AppendLine($"[경고] 무기 프리팹을 찾지 못했습니다: {name}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            log.Insert(0, $"[WeaponTagBuilder] 변경 {changed}개\n");
            Debug.Log(log.ToString());
            return log.ToString();
        }
    }
}
