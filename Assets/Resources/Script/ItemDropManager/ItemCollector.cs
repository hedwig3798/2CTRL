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

        float exp = expItem.Collect();
        if (exp <= 0f)
        {
            return;
        }

        ExpSystem expSystem = ExpSystem.Instance;
        if (null == expSystem)
        {
            Debug.LogWarning("[ItemCollector] 씬에 공용 ExpSystem 이 없습니다.");
            return;
        }

        expSystem.GetExp(exp);
    }
}
