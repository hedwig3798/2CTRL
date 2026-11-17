using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 무기 합성법 목록 (레벨업 라운드를 열 때 FindCandidate 로 중앙 합성 카드 후보를 찾는다)
/// </summary>
[CreateAssetMenu(fileName = "DimensionFusionRegistry", menuName = "Scriptable Objects/Dimension Fusion Registry")]
public class DimensionFusionRegistry
    : ScriptableObject
{
    [Tooltip("합성법 목록")]
    [SerializeField]
    private DimensionWeaponData[] recipes;

    public IReadOnlyList<DimensionWeaponData> Recipes => null != recipes ? recipes : System.Array.Empty<DimensionWeaponData>();

    /// <summary>
    /// 지금 합성 가능한 합성법 중 무작위 1개 (없으면 null)
    /// </summary>
    public DimensionWeaponData FindCandidate()
    {
        return DimensionFusion.FindCandidate(recipes);
    }

    /// <summary>
    /// 합성법 목록을 바꾼다 (빌더 / 테스트용)
    /// </summary>
    public void SetRecipes(DimensionWeaponData[] _recipes)
    {
        recipes = _recipes;
    }
}
