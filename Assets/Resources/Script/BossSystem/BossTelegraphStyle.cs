using UnityEngine;

/// <summary>
/// 보스 예고 표시(BossTelegraph) 공용 외형 설정. 키워드에 비워 두면 기본값을 쓴다
/// </summary>
[CreateAssetMenu(fileName = "BossTelegraphStyle", menuName = "2CTRL/Boss/TelegraphStyle")]
public class BossTelegraphStyle
    : ScriptableObject
{
    [Tooltip("원 채움·링에 쓸 머티리얼 (비우면 URP 2D Sprite-Unlit-Default → Sprites/Default 순으로 찾는다)")]
    public Material material;

    [Tooltip("정렬 레이어 (Default 금지: BG 에 가려진다). 없으면 attack → item 순으로 쓴다")]
    public string sortingLayerName = "attack";

    [Tooltip("정렬 순서 (원 채움 = 이 값, 띠 = +1, 링 = +2)")]
    public int sortingOrder = -20;

    [Tooltip("링 선 두께")]
    [Min(0.01f)]
    public float ringWidth = 0.12f;

    [Tooltip("원·링 분할 수 (점선 칸 수)")]
    [Range(16, 128)]
    public int segments = 64;
}
