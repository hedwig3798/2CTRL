using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TileData
    : MonoBehaviour
{
    public float weight = 1;
    public TileData[] validUp;
    public TileData[] validDown;
    public TileData[] validLeft;
    public TileData[] validRight;

    public TileSocket upSocket;
    public TileSocket downSocket;
    public TileSocket leftSocket;
    public TileSocket rightSocket;

    public TileSet tileSet;

    private Sprite sprite;

    public Vector2 GetSpriteSize()
    {
        if (null == sprite)
        {
            sprite = GetComponent<SpriteRenderer>().sprite;
        }
        return sprite.bounds.size;
    }
}