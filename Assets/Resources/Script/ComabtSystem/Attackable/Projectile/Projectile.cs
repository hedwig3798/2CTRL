using UnityEngine;

public class Projectile 
    : MonoBehaviour
    , Initializable
{
    public BlackBoardHandler blackBoardHandler;

    public ProjectileWeapon owner;

    public float baseDamage;

    private DamageMassage damageMassage;

    public void Initialize(BlackBoard _data)
    {
        damageMassage.damage = _data.GetFloat(DATA_TYPE.damageRate) * baseDamage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            DamageMassage duplicateMassage = damageMassage;
            dp.ProcessDamage(ref duplicateMassage);
        }
    }
}
