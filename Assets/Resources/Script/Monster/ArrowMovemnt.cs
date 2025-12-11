using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ArrowMovemnt
    : DirectionalObject
{
    private enum Direction
    {
        Right = 0, 
        Left = 1, 
        Up = 2, 
        Down = 3,
    }

    public float speed = 5.0f;

    public KeyCode[] moveKeys =
    {
        KeyCode.RightArrow,
        KeyCode.LeftArrow,
        KeyCode.UpArrow,
        KeyCode.DownArrow
    };

    void Update()
    {
        if (Input.GetKey(moveKeys[(int)Direction.Right]))
        {
            isRight = true;
            transform.Translate(Vector2.right * speed * Time.deltaTime);
        }

        if (Input.GetKey(moveKeys[(int)Direction.Left]))
        {
            isRight = false;
            transform.Translate(Vector2.left * speed * Time.deltaTime);
        }

        if (Input.GetKey(moveKeys[(int)Direction.Up]))
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime);
        }

        if (Input.GetKey(moveKeys[(int)Direction.Down]))
        {
            transform.Translate(Vector2.down * speed * Time.deltaTime);
        }

        CheckRight();
    }
}
