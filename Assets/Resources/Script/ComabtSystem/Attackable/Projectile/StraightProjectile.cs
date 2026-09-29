using UnityEngine;

/// <summary>
/// 가장 가까운 적을 향해 직선으로 날아가 처음 부딪힌 적을 타격하는 발사체
/// 이동은 BlackBoard 로 초기화되는 이동 컴포넌트(Straight 등)가 담당한다
/// </summary>
public class StraightProjectile
    : Projectile
{
    public BlackBoardHandler blackBoardHandler;

    protected override bool OnLaunch()
    {
        if (null == blackBoardHandler)
        {
            return false;
        }

        Transform target = FindNearest(weapon.transform.position, weapon.range);
        if (null == target)
        {
            return false;
        }

        BlackBoard b = blackBoardHandler.GetBlackBoard();
        b.SetTransform(DATA_TYPE.moveTarget, target);
        b.SetTransform(DATA_TYPE.startPosition, weapon.transform);
        b.SetFloat(DATA_TYPE.moveSpeedRate, weapon.speedRate);
        b.SetFloat(DATA_TYPE.damageRate, weapon.damageRate);
        blackBoardHandler.Initialize();
        return true;
    }

    private void Awake()
    {
        if (null == blackBoardHandler)
        {
            blackBoardHandler = GetComponent<BlackBoardHandler>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == gameObject.layer)
        {
            return;
        }

        if (other.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            DamageMassage duplicateMassage = damageMassage;
            dp.ProcessDamage(ref duplicateMassage);
            gameObject.SetActive(false);
        }
    }
}
