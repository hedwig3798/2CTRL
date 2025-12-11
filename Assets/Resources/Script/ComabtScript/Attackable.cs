using UnityEngine;

public class Attackable : MonoBehaviour
{
    public Stats stats;

    public float baseAtk;

    void Awake()
    {
        if (null == stats)
        {
            Debug.LogError($"{gameObject.name} has no stats but Attackable");
            enabled = false;
        }
    }
}
