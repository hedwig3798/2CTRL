/// <summary>
/// 무기 업그레이드가 바꾸는 값의 종류
/// - 0 ~ 99   : Weapon 공통값. 업그레이드하면 Weapon 필드에 바로 반영된다
/// - 100 ~ 199: 발사체 고유값. 기본값은 발사체 프리팹에 있고, 발사체가 weapon.GetParam(param, 기본값) 으로 읽는다
/// - 200 ~    : ★ 플래그. 누적값이 0 보다 크면 켜진다 (weapon.HasFlag(param))
/// 에셋에 숫자로 저장되므로 기존 값의 숫자는 바꾸지 않는다 (새 값은 각 구간 끝에 추가)
/// </summary>
public enum WeaponParam
{
    // Weapon 공통값
    Damage = 0,
    AttackSpeed = 1,
    Range = 2,
    FireCount = 3,
    HitCount = 4,
    AreaRadius = 5,
    Lifetime = 6,
    Knockback = 7,
    SpeedRate = 8,
    FireDelay = 9,
    ChainCount = 10,
    ChainRange = 11,

    // 발사체 고유값
    /// <summary>발사체 크기 배율 (기본 1). Boomerang</summary>
    ProjectileScale = 100,
    /// <summary>자식 무기 damage. Drone</summary>
    ChildDamage = 101,
    /// <summary>자식 무기 attackSpeed. Drone</summary>
    ChildAttackSpeed = 102,
    /// <summary>자식 무기 fireCount. Drone</summary>
    ChildFireCount = 103,
    /// <summary>자식 무기 hitCount. Drone</summary>
    ChildHitCount = 104,
    /// <summary>자식 무기 range. Drone</summary>
    ChildRange = 105,
    /// <summary>공전 반경. Drone (OrbitMovement.radius)</summary>
    OrbitRadius = 106,
    /// <summary>광선 두께. Laser</summary>
    LaserWidth = 107,
    /// <summary>부채꼴 광선 사이 각도. Laser</summary>
    FanAngle = 108,
    /// <summary>설치 후 활성화 지연. Mine</summary>
    ArmDelay = 109,
    /// <summary>장판 틱 간격. PoisonZone</summary>
    TickInterval = 110,
    /// <summary>조각 수. SplitShot</summary>
    FragmentCount = 111,
    /// <summary>조각 데미지 비율. SplitShot</summary>
    FragmentDamageRate = 112,
    /// <summary>조각 비행 거리. SplitShot (조각 Lifetime 도 같은 비율로 늘어난다)</summary>
    FragmentDistance = 113,
    /// <summary>칼날 길이. Sweep</summary>
    Reach = 114,

    // ★ 플래그
    /// <summary>돌아올 때 데미지 배율 적용. Boomerang</summary>
    FlagReturnDamage = 200,
    /// <summary>2차 폭발. Explosive</summary>
    FlagSecondaryExplosion = 201,
    /// <summary>연쇄 폭발. Mine</summary>
    FlagChainExplosion = 202,
    /// <summary>장판 위 적 둔화. PoisonZone</summary>
    FlagSlowZone = 203,
    /// <summary>수축 파동. Shockwave</summary>
    FlagContractWave = 204,
    /// <summary>조각 재분열. SplitShot</summary>
    FlagResplit = 205,
    /// <summary>검기. Sweep</summary>
    FlagSlashWave = 206,
    /// <summary>전이 증폭. Chain</summary>
    FlagChainAmplify = 207,
}

public static class WeaponParamUtils
{
    /// <summary>
    /// 발사체 고유값 구간 시작
    /// </summary>
    public const int ProjectileStart = 100;
    /// <summary>
    /// ★ 플래그 구간 시작
    /// </summary>
    public const int FlagStart = 200;

    /// <summary>
    /// 누적치를 담을 배열 크기 (가장 큰 값 + 1)
    /// </summary>
    public static readonly int SlotCount = GetSlotCount();

    /// <summary>
    /// 정의된 공통값 목록 (Weapon.ResetUpgrades 가 순회한다)
    /// </summary>
    public static readonly WeaponParam[] CommonParams = GetCommonParams();

    /// <summary>
    /// Weapon 필드에 바로 반영되는 공통값인지
    /// </summary>
    public static bool IsCommon(this WeaponParam _param)
    {
        int i = (int)_param;
        return 0 <= i && i < ProjectileStart;
    }

    /// <summary>
    /// ★ 플래그인지
    /// </summary>
    public static bool IsFlag(this WeaponParam _param)
    {
        return (int)_param >= FlagStart;
    }

    private static WeaponParam[] GetCommonParams()
    {
        System.Collections.Generic.List<WeaponParam> list = new System.Collections.Generic.List<WeaponParam>();
        foreach (WeaponParam p in System.Enum.GetValues(typeof(WeaponParam)))
        {
            if (true == p.IsCommon())
            {
                list.Add(p);
            }
        }
        return list.ToArray();
    }

    private static int GetSlotCount()
    {
        int max = 0;
        foreach (WeaponParam p in System.Enum.GetValues(typeof(WeaponParam)))
        {
            max = System.Math.Max(max, (int)p);
        }
        return max + 1;
    }
}
