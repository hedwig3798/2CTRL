using UnityEngine;

public class DirectionalObject 
    : MonoBehaviour
{
    [SerializeField]
    public SpriteRenderer spriteRenderer;

    [HideInInspector]
    public bool isRight;

    protected void CheckRight()
    {
        if (null == spriteRenderer)
        {
            Debug.LogError("no sprite renderer in monster");
            return;
        }

        spriteRenderer.flipX = !isRight;
    }
}
