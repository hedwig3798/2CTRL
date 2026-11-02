using UnityEngine;

public struct DamageMassage
{
    public bool isReflected;
    public float damage;
    public DamagePipeline attacker;

    /// <summary>
    /// 밀어내는 힘 (밀려나는 초기 속도). 0 이면 넉백 없음
    /// </summary>
    public float knockbackForce;
    /// <summary>
    /// 타격이 일어난 지점. 넉백은 이 지점에서 대상 쪽으로 밀어낸다
    /// </summary>
    public Vector2 hitPoint;

    /// <summary>
    /// 이 데미지를 만든 무기 (통계 귀속용). 무기가 아닌 데미지(접촉, 몬스터 장판 등)는 null
    /// </summary>
    public Weapon weapon;
}
