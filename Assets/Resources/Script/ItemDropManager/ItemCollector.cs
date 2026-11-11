using UnityEngine;

/// <summary>
/// 플레이어가 아이템을 수집하는 컴포넌트
/// EXP 아이템을 수집하면 획득한 EXP 를 공용 ExpSystem 에 전달한다.
/// </summary>
public class ItemCollector 
    : MonoBehaviour
{
    [SerializeField]
    private GameObject owner;

    // 통계용 EXP 획득 귀속 (owner 우선, 없으면 부모에서 찾는다)
    private PlayerStats ownerStats;

    private void Awake()
    {
        if (null != owner)
        {
            ownerStats = owner.GetComponentInParent<PlayerStats>();
        }
        if (null == ownerStats)
        {
            ownerStats = GetComponentInParent<PlayerStats>();
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (false == _other.CompareTag("item"))
        {
            return;
        }

        if (false == _other.TryGetComponent(out ExpItem expItem))
        {
            return;
        }

        Absorb(expItem, ownerStats);
    }

    /// <summary>
    /// EXP 아이템을 수집해 공용 ExpSystem 에 넣고 _owner 진영으로 OnExpGained 를 알린다 (최종 보스 진입 시 강제 흡수에도 쓴다)
    /// 획득한 EXP 를 돌려준다 (이미 수집됐으면 0)
    /// </summary>
    public static float Absorb(ExpItem _item, PlayerStats _owner)
    {
        if (null == _item)
        {
            return 0f;
        }

        float exp = _item.Collect();
        if (exp <= 0f)
        {
            return 0f;
        }

        ExpSystem expSystem = ExpSystem.Instance;
        if (null == expSystem)
        {
            Debug.LogWarning("[ItemCollector] 씬에 공용 ExpSystem 이 없습니다.");
            return 0f;
        }

        expSystem.GetExp(exp);

        if (null != _owner && true == EventManager.HasInstance)
        {
            EventManager.Instance.OnExpGained?.Invoke(_owner.Side, exp);
        }
        return exp;
    }
}
