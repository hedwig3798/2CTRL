using UnityEngine;

/// <summary>
/// 레이어 → 진영 판정 (사운드 패닝 / 위치 컬링용)
/// - L 로 시작하는 진영 레이어(LPlayer, LMonster, LAttack, LItem, LUI, LBG) = Left, R 쪽 = Right, 그 외 = null(공유)
/// - 이름은 ProjectSettings/TagManager 의 실제 레이어 이름. 처음 쓸 때 32 칸 표를 만든다 (이후 GC 0)
/// </summary>
public static class SoundLayerSide
{
    public const string LEFT_MONSTER_LAYER = "LMonster";
    public const string RIGHT_MONSTER_LAYER = "RMonster";

    private static readonly string[] LEFT_LAYERS = { "LPlayer", LEFT_MONSTER_LAYER, "LAttack", "LItem", "LUI", "LBG" };
    private static readonly string[] RIGHT_LAYERS = { "RPlayer", RIGHT_MONSTER_LAYER, "RAttack", "RItem", "RUI", "RBG" };

    private const sbyte NONE = -1;

    // 레이어 번호 → 진영 (-1 없음 / 0 Left / 1 Right)
    private static sbyte[] table;
    private static int leftMonsterLayer = -1;
    private static int rightMonsterLayer = -1;

    /// <summary>
    /// 레이어의 진영 (진영 레이어가 아니면 null)
    /// </summary>
    public static PlayerSide? GetSide(int _layer)
    {
        EnsureTable();
        if (_layer < 0 || table.Length <= _layer)
        {
            return null;
        }

        sbyte v = table[_layer];
        if (NONE == v)
        {
            return null;
        }
        return 0 == v ? PlayerSide.Left : PlayerSide.Right;
    }

    /// <summary>
    /// 진영의 몬스터 레이어 번호 (없으면 -1). 진영 카메라 판정에 쓴다
    /// </summary>
    public static int GetMonsterLayer(PlayerSide _side)
    {
        EnsureTable();
        return PlayerSide.Left == _side ? leftMonsterLayer : rightMonsterLayer;
    }

    private static void EnsureTable()
    {
        if (null != table)
        {
            return;
        }

        table = new sbyte[32];
        for (int i = 0; i < table.Length; ++i)
        {
            table[i] = NONE;
        }
        Fill(LEFT_LAYERS, 0);
        Fill(RIGHT_LAYERS, 1);
        leftMonsterLayer = LayerMask.NameToLayer(LEFT_MONSTER_LAYER);
        rightMonsterLayer = LayerMask.NameToLayer(RIGHT_MONSTER_LAYER);
    }

    private static void Fill(string[] _names, sbyte _value)
    {
        foreach (string n in _names)
        {
            int layer = LayerMask.NameToLayer(n);
            if (0 <= layer && layer < table.Length)
            {
                table[layer] = _value;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        table = null;
        leftMonsterLayer = -1;
        rightMonsterLayer = -1;
    }
}
