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

        SlingshotMovement movement = _other.GetComponent<SlingshotMovement>();
        movement.target = owner.transform;
        movement.isStop = false;
    }
}
