using UnityEngine;

/// <summary>
/// 드랍되는 EXP 아이템
/// 스스로 EXP 를 지급하지 않고, ItemCollector 가 Collect 로 수집해 공용 ExpSystem 에 전달한다.
/// </summary>
public class ExpItem
    : MonoBehaviour
    , Initializable
{
    [SerializeField]
    private GameObject owner;

    [SerializeField]
    private float baseExp;

    private float expRate;

    private bool collected;

    public void Initialize(BlackBoard _data)
    {
        collected = false;

        expRate = _data.GetFloat(DATA_TYPE.expRate);
        if (expRate <= 0f)
        {
            expRate = 1f;
        }
    }

    /// <summary>
    /// 아이템을 수집하고 획득 EXP 를 반환 (이미 수집된 경우 0)
    /// </summary>
    public float Collect()
    {
        if (true == collected)
        {
            return 0f;
        }

        collected = true;

        if (null != owner)
        {
            owner.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        return baseExp * expRate;
    }
}
