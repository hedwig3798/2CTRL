using UnityEngine;

/// <summary>
/// 차원 무기 합성법 (재료 2종 + 결과 프리팹 + 표시 정보)
/// - 재료는 양쪽 플레이어가 하나씩 최대 단계로 보유해야 한다 (어느 쪽이 어느 재료를 가졌는지는 따지지 않는다)
/// - 재료의 최대 단계는 차원의 무기 보상(WeaponRewardEffect.UpgradeTable.StepCount) 에서 찾고, 못 찾으면 fallbackRequiredLevel 을 쓴다
/// - 수치(데미지 / 공격 속도 등) 는 결과 프리팹(DimensionWeapon) 에 직렬화한다
/// </summary>
[CreateAssetMenu(fileName = "DimensionWeaponData", menuName = "Scriptable Objects/Dimension Weapon Data")]
public class DimensionWeaponData
    : ScriptableObject
{
    [Header("display")]
    [Tooltip("이름 StringTable 키")]
    [SerializeField]
    private string nameKey;

    [Tooltip("설명 StringTable 키")]
    [SerializeField]
    private string explainKey;

    [Tooltip("보상 카드 / HUD 아이콘")]
    [SerializeField]
    private Sprite icon;

    [Header("recipe")]
    [Tooltip("재료 무기 프리팹 A (예: 숲 무기)")]
    [SerializeField]
    private Weapon materialA;

    [Tooltip("재료 무기 프리팹 B (예: 공장 무기)")]
    [SerializeField]
    private Weapon materialB;

    [Tooltip("재료 업그레이드 테이블을 찾지 못했을 때 요구하는 업그레이드 단계")]
    [Min(1)]
    [SerializeField]
    private int fallbackRequiredLevel = 10;

    [Header("result")]
    [Tooltip("합성 결과 차원 무기 프리팹 (화면 공간에 1개 생성)")]
    [SerializeField]
    private DimensionWeapon resultPrefab;

    public string NameKey => nameKey;
    public string ExplainKey => explainKey;
    public Sprite Icon => icon;
    public Weapon MaterialA => materialA;
    public Weapon MaterialB => materialB;
    public int FallbackRequiredLevel => fallbackRequiredLevel;
    public DimensionWeapon ResultPrefab => resultPrefab;

    /// <summary>
    /// 재료 2종과 결과 프리팹이 모두 연결되어 있고 재료가 서로 다른지
    /// </summary>
    public bool IsValid => null != materialA && null != materialB && materialA != materialB && null != resultPrefab;

    /// <summary>
    /// 이름 / 설명 키 (없으면 에셋 이름)
    /// </summary>
    public string DisplayNameKey => true == string.IsNullOrEmpty(nameKey) ? name : nameKey;

    /// <summary>
    /// 합성법을 지정한다 (빌더 / 테스트용)
    /// </summary>
    public void SetRecipe(Weapon _materialA, Weapon _materialB, DimensionWeapon _resultPrefab)
    {
        materialA = _materialA;
        materialB = _materialB;
        resultPrefab = _resultPrefab;
    }

    /// <summary>
    /// 표시 정보를 지정한다 (빌더 / 테스트용)
    /// </summary>
    public void SetDisplay(string _nameKey, string _explainKey, Sprite _icon)
    {
        nameKey = _nameKey;
        explainKey = _explainKey;
        icon = _icon;
    }
}
