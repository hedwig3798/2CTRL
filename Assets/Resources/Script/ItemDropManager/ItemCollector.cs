using UnityEngine;

public class ItemCollector 
    : MonoBehaviour
{
    [SerializeField]
    private ExpSystem expSystem;

    [SerializeField]
    private GameObject owner;

    private void Awake()
    {
        if (null == expSystem)
        {
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (false == _other.CompareTag("item"))
        {
            return;
        }
        
    }
}
