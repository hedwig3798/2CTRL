#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 디버그 전용 : 각 플레이어의 스텟 / 무기 최종 수치와 몬스터 비율을 화면에 표시한다
/// - 에디터 / 개발 빌드(Development Build)에서만 컴파일되며, 씬 로드 시 자동으로 생성되므로 씬에 배치할 필요가 없다
/// - toggleKey(기본 F1) 로 켜고 끈다
/// - IMGUI(OnGUI) 로 그리므로 Canvas 가 필요 없고, 게임이 일시정지되어도 갱신된다
/// - 표시 형식 : 스텟 = 최종값 (base + ΣAdd) × (1 + ΣMul), 무기 = 최종값 (무기 + 플레이어)
/// </summary>
public class PlayerStatDebugUI
    : MonoBehaviour
{
    [Tooltip("표시를 켜고 끄는 키")]
    [SerializeField]
    private Key toggleKey = Key.F1;

    [Tooltip("시작 시 표시 여부 (HUD 를 가리므로 기본 꺼짐, toggleKey 로 켠다)")]
    [SerializeField]
    private bool isVisible = false;

    [Tooltip("무기 / 체력 컴포넌트를 다시 찾는 간격 (초, 일시정지 중에도 동작)")]
    [SerializeField]
    private float refreshInterval = 0.5f;

    private const float PanelWidth = 300f;
    private const float Margin = 10f;

    private static readonly ValueType[] StatTypes = (ValueType[])System.Enum.GetValues(typeof(ValueType));
    private static readonly SpawnModifierType[] SpawnTypes = (SpawnModifierType[])System.Enum.GetValues(typeof(SpawnModifierType));

    // 플레이어마다 찾아 둔 컴포넌트 (무기는 보상으로 늘어나므로 주기적으로 다시 찾는다)
    private readonly Dictionary<PlayerSide, List<Weapon>> weaponCache = new Dictionary<PlayerSide, List<Weapon>>();
    private readonly Dictionary<PlayerSide, HealthSystem> healthCache = new Dictionary<PlayerSide, HealthSystem>();
    private float nextRefreshTime = 0f;

    private Spawner spawner;

    private readonly StringBuilder builder = new StringBuilder();
    private GUIStyle boxStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstance()
    {
        if (null != FindAnyObjectByType<PlayerStatDebugUI>())
        {
            return;
        }

        GameObject go = new GameObject(nameof(PlayerStatDebugUI));
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerStatDebugUI>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (null != keyboard && Key.None != toggleKey && true == keyboard[toggleKey].wasPressedThisFrame)
        {
            isVisible = !isVisible;
        }
    }

    private void OnGUI()
    {
        if (false == isVisible)
        {
            return;
        }

        if (null == boxStyle)
        {
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = true,
                wordWrap = false,
            };
            boxStyle.padding = new RectOffset(8, 8, 6, 6);
        }

        // Time.timeScale 이 0 이어도 갱신되도록 realtime 사용
        if (Time.realtimeSinceStartup >= nextRefreshTime)
        {
            nextRefreshTime = Time.realtimeSinceStartup + refreshInterval;
            RefreshCache();
        }

        DrawPlayer(PlayerSide.Left, new Vector2(Margin, Margin));
        DrawPlayer(PlayerSide.Right, new Vector2(Screen.width - PanelWidth - Margin, Margin));
        DrawSpawner(new Vector2((Screen.width - PanelWidth) * 0.5f, Margin));
    }

    private void RefreshCache()
    {
        foreach (PlayerSide side in new[] { PlayerSide.Left, PlayerSide.Right })
        {
            PlayerStats stats = PlayerStats.Find(side);
            if (false == weaponCache.TryGetValue(side, out List<Weapon> weapons))
            {
                weapons = new List<Weapon>();
                weaponCache.Add(side, weapons);
            }

            weapons.Clear();
            healthCache[side] = null;
            if (null == stats)
            {
                continue;
            }

            stats.GetComponentsInChildren(true, weapons);
            healthCache[side] = stats.GetComponentInChildren<HealthSystem>(true);
        }

        if (null == spawner)
        {
            spawner = FindAnyObjectByType<Spawner>();
        }
    }

    private void DrawPlayer(PlayerSide _side, Vector2 _position)
    {
        PlayerStats stats = PlayerStats.Find(_side);

        builder.Clear();
        if (null == stats)
        {
            builder.Append($"<b>{_side}</b> : 플레이어 없음");
            DrawBox(_position);
            return;
        }

        builder.Append($"<b>{_side} : {stats.name}</b>  <color=#999999>[{toggleKey}]</color>\n");

        if (healthCache.TryGetValue(_side, out HealthSystem health) && null != health)
        {
            builder.Append($"현재 HP  {health.CurrHP:0.##} / {health.MaxHP:0.##}\n");
        }

        builder.Append("\n<b>플레이어 스텟</b>\n");
        foreach (ValueType type in StatTypes)
        {
            float add = stats.GetAdd(type);
            builder.Append($"{type.ToDisplayName()}  <b>{stats.Get(type):0.##}</b>");
            builder.Append($"  <color=#999999>({stats.GetBase(type):0.##} {add:+0.##;-0.##;+0} × {stats.GetMultiplier(type):0.##})</color>\n");
        }

        if (weaponCache.TryGetValue(_side, out List<Weapon> weapons))
        {
            foreach (Weapon weapon in weapons)
            {
                if (null == weapon)
                {
                    continue;
                }

                builder.Append($"\n<b>무기 : {weapon.name}</b>{(weapon.isActiveAndEnabled ? string.Empty : " <color=#ff6666>(꺼짐)</color>")}\n");
                builder.Append($"데미지  <b>{weapon.FinalDamage:0.##}</b>  <color=#999999>({FormatWeaponStat(weapon, stats, ValueType.Damage, weapon.damage)})</color>\n");
                builder.Append($"공격 속도  <b>{weapon.FinalAttackSpeed:0.##}</b>/s  <color=#999999>({FormatWeaponStat(weapon, stats, ValueType.AttackSpeed, weapon.attackSpeed)}, 간격 {weapon.FinalCoolTime:0.##}s)</color>\n");
                builder.Append($"범위  <b>{weapon.FinalRange:0.##}</b>  <color=#999999>({FormatWeaponStat(weapon, stats, ValueType.Range, weapon.range)})</color>\n");
                builder.Append($"발사 수  <b>{weapon.FinalFireCount}</b>  <color=#999999>({FormatWeaponStat(weapon, stats, ValueType.ProjectileCount, weapon.fireCount)})</color>\n");
                if (weapon.lifetime > 0f)
                {
                    builder.Append($"지속 시간  <b>{weapon.FinalLifetime:0.##}</b>s  <color=#999999>({FormatWeaponStat(weapon, stats, ValueType.Duration, weapon.lifetime)})</color>\n");
                }
                if (weapon.fireDelay > 0f)
                {
                    builder.Append($"발사 딜레이  <b>{weapon.FinalFireDelay:0.###}</b>s  <color=#999999>(기본 {weapon.fireDelay:0.###}s)</color>\n");
                }
                if (weapon.spreadRange > 0f)
                {
                    builder.Append($"랜덤 보정  <b>{weapon.spreadRange:0.##}</b>\n");
                }
            }
        }

        DrawBox(_position);
    }

    // 무기 수치 계산식. 태그가 없으면 플레이어 스텟을 무시하므로 그 사실을 표시한다
    private static string FormatWeaponStat(Weapon _weapon, PlayerStats _stats, ValueType _type, float _weaponValue)
    {
        if (false == _weapon.IsAffectedBy(_type))
        {
            return $"{_weaponValue:0.##}, 태그 없음: 버프 무시";
        }
        return $"({_weaponValue:0.##} {_stats.GetAdd(_type):+0.##;-0.##;+0}) × {_stats.GetMultiplier(_type):0.##}";
    }

    private void DrawSpawner(Vector2 _position)
    {
        builder.Clear();
        if (null == spawner)
        {
            return;
        }

        builder.Append("<b>몬스터 비율</b>\n");
        foreach (SpawnModifierType type in SpawnTypes)
        {
            builder.Append($"{type}  <b>{spawner.GetRate(type):0.##}</b>\n");
        }

        DrawBox(_position);
    }

    private void DrawBox(Vector2 _position)
    {
        GUIContent content = new GUIContent(builder.ToString().TrimEnd('\n'));
        float height = boxStyle.CalcHeight(content, PanelWidth);
        GUI.Box(new Rect(_position.x, _position.y, PanelWidth, height), content, boxStyle);
    }
}
#endif
