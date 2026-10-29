/// <summary>
/// OneHitProjectile 과 같은 오브젝트에 붙어 타격마다 데미지 배율을 정한다 (부메랑 귀환 2배, 체인 증폭 등)
/// 여러 개가 붙어 있으면 배율을 모두 곱한다
/// </summary>
public interface IHitDamageModifier
{
    /// <param name="_projectile">타격하는 발사체</param>
    /// <param name="_hitIndex">이번 타격이 이번 발사의 몇 번째 타격인지 (0 부터)</param>
    /// <returns>데미지 배율 (1 이면 그대로)</returns>
    float GetHitDamageRate(OneHitProjectile _projectile, int _hitIndex);
}

/// <summary>
/// OneHitProjectile 과 같은 오브젝트에 붙어 타격 직후 알림을 받는다 (조각 재분열, 번개 표시 등)
/// 타격 수를 다 채워 발사체가 꺼지기 전에 호출된다
/// </summary>
public interface IProjectileHitListener
{
    /// <param name="_projectile">타격한 발사체</param>
    /// <param name="_target">타격한 대상</param>
    /// <param name="_hitIndex">이번 타격이 이번 발사의 몇 번째 타격인지 (0 부터)</param>
    void OnProjectileHit(OneHitProjectile _projectile, DamagePipeline _target, int _hitIndex);
}
