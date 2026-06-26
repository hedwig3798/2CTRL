using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TileData
    : MonoBehaviour
{
    public float weight;
    public TileData[] validUp;
    public TileData[] validDown;
    public TileData[] validLeft;
    public TileData[] validRight;

    private Sprite sprite;

    private void Start()
    {
        sprite = GetComponent<SpriteRenderer>().sprite;
    }


    public Vector2 GetSpriteSize()
    {
        return sprite.bounds.size;
    }
}