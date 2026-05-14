using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class ContactDamage
    : MonoBehaviour
    , Initializable
{
    [Header("attacker data")]
    public DamagePipeline owner;

    [Header("attack data")]
    public float baseDamage;
    public float coolTime;

    private float damageRate;

    private float timer;

    private DamagePipeline target;

    private DamageMassage damageMassage;

    public void Initialize(BlackBoard _data)
    {
        timer = coolTime;
        damageRate = _data.GetFloat(DATA_TYPE.damageRate);
    }

    private void Awake()
    {
        timer = coolTime;
        damageMassage = new DamageMassage();
        damageMassage.attacker = target;
    }

    private void Update()
    {
        if (null == target)
        {
            return;
        }

        if (0 < timer)
        {
            timer -= Time.deltaTime;
        }
        else
        {
            timer = coolTime;

            if (null != target)
            {
                damageMassage.damage = baseDamage * damageRate;
                target.ProcessDamage(ref damageMassage);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D _collision)
    {
        if (gameObject.layer == _collision.collider.gameObject.layer)
        {
            return;
        }

        if (_collision.collider.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            target = dp;
        }
    }

    private void OnCollisionExit2D(Collision2D _collision)
    {
        if (target == _collision.collider.gameObject.TryGetComponent(out DamagePipeline dp))
        {
            target = null;
        }
    }
}
