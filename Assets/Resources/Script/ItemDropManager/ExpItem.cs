using UnityEngine;

public class ExpItem 
    : MonoBehaviour
    , Initializable
{
    [SerializeField]
    private float baseExp;

    private float expRate;

    public void Initialize(BlackBoard _data)
    {
        expRate = _data.GetFloat(DATA_TYPE.expRate);
    }

    public float GetExp()
    {
        return baseExp * expRate;
    }
}
