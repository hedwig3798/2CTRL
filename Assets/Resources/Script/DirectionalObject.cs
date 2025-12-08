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
            return;
        }

        spriteRenderer.flipX = !isRight;
    }
}
