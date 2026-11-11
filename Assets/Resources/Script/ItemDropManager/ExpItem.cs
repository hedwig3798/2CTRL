using System.Collections.Generic;
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

    /// <summary>
    /// expRate 1 일 때의 획득 EXP (DropManager.DropExp 가 보석 하나의 배율을 계산할 때 쓴다)
    /// </summary>
    public float BaseExp => baseExp;

    private bool collected;

    // 땅에 있는(활성) EXP 아이템 전체 (최종 보스 진입 시 강제 흡수용)
    private static readonly HashSet<ExpItem> ActiveItems = new HashSet<ExpItem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveItems()
    {
        ActiveItems.Clear();
    }

    /// <summary>
    /// 활성이고 아직 수집되지 않은 EXP 아이템을 _result 에 담는다 (_result 는 먼저 비운다)
    /// </summary>
    public static void CollectActive(List<ExpItem> _result)
    {
        _result.Clear();
        foreach (ExpItem item in ActiveItems)
        {
            if (null != item && false == item.collected)
            {
                _result.Add(item);
            }
        }
    }

    private void OnEnable()
    {
        ActiveItems.Add(this);
    }

    private void OnDisable()
    {
        ActiveItems.Remove(this);
    }

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
