using UnityEngine;


public class DamagePipeline : MonoBehaviour
{
    IDamageable[] pipeline = new IDamageable[(int)DAMAGE_PIPELINE.END];

    public void ProcessDamage(ref DamageMassage _msg)
    {
        foreach (IDamageable damageable in pipeline)
        {
            if (null == damageable)
            {
                continue;
            }
            damageable.ProcessDamage(ref _msg);
        }
    }
}
