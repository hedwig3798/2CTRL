using UnityEngine;

public class ItemCollector 
    : MonoBehaviour
{
    [SerializeField]
    private ExpSystem expSystem;

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (expSystem != null 
            && _other.gameObject.TryGetComponent(out ExpItem item))
        {
            expSystem.GetExp(item.GetExp());
        }
    }
}
