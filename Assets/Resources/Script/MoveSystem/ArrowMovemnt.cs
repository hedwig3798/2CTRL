using NUnit.Framework.Constraints;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using static UnityEngine.GraphicsBuffer;

public class ArrowMovemnt 
    : MonoBehaviour
    , IMovementSystem
{
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    public float speed = 5.0f;

    public KeyCode right;
    public KeyCode left;
    public KeyCode up;
    public KeyCode down;

    public void Init(MovementInitData _initData)
    {
        speed = _initData.speed;
    }

    void Update()
    {
        if (Input.GetKey(right))
        {
            transform.Translate(Vector2.right * speed * Time.deltaTime);
        }

        if (Input.GetKey(left))
        {
            transform.Translate(Vector2.left * speed * Time.deltaTime);
        }

        if (Input.GetKey(up))
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        if (Input.GetKey(down))
        {
            transform.Translate(Vector2.down * speed * Time.deltaTime);
        }
    }
}
