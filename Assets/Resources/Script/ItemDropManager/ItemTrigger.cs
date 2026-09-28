using UnityEngine;

public class ItemTrigger : MonoBehaviour
{
    [SerializeField]
    private GameObject owner;

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (false == _other.CompareTag("item"))
        {
            return;
        }

        if (false == _other.TryGetComponent(out SlingshotMovement movement))
        {
            return;
        }

        Transform magnet = null != owner ? owner.transform : transform;
        movement.target = magnet;
        movement.isStop = false;
    }
}
