using UnityEngine;

public class ExpItem
    : MonoBehaviour
    , Initializable
{
    [SerializeField]
    private GameObject owner;

    [SerializeField]
    private float baseExp;

    private float expRate;

    public void Initialize(BlackBoard _data)
    {
        expRate = _data.GetFloat(DATA_TYPE.expRate);
        if (expRate <= 0f)
        {
            expRate = 1f;
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.TryGetComponent(out ExpSystem expSystem))
        {
            expSystem.GetExp(baseExp * expRate);
            if (null != owner)
            {
                owner.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
