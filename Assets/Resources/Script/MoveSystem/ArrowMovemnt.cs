using NUnit.Framework.Constraints;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using static UnityEngine.GraphicsBuffer;

public class ArrowMovemnt 
    : MonoBehaviour
{
    [Header("movement value")]
    public float speed = 5.0f;

    [Header("key code bind")]
    public KeyCode right;
    public KeyCode left;
    public KeyCode up;
    public KeyCode down;

    [Header("sprite value")]
    [SerializeField]
    private SPRITE_ROTATE_MODE rotateMode;
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    [SerializeField]
    private bool isDefaultLeft = true;
    [SerializeField]
    [Range(0f, 360f)]
    private float rotateOffset;
    
    private bool isLeft = true;

    private void FlipSprite()
    {
        if (null == spriteRenderer)
        {
            return;
        }

        spriteRenderer.flipX = isDefaultLeft ^ isLeft;
    }

    void Update()
    {
        if (Input.GetKey(right))
        {
            transform.Translate(Vector2.right * speed * Time.deltaTime);
            isLeft = false;
        }

        if (Input.GetKey(left))
        {
            transform.Translate(Vector2.left * speed * Time.deltaTime);
            isLeft = true;
        }

        if (Input.GetKey(up))
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        if (Input.GetKey(down))
        {
            transform.Translate(Vector2.down * speed * Time.deltaTime);
        }

        FlipSprite();
    }
}
