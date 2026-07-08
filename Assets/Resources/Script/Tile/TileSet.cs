using UnityEngine;

[CreateAssetMenu(fileName = "TileSet", menuName = "Tile/TileSet")]
public class TileSet 
    : ScriptableObject
{
    public string tileSetName;

    public TileSocket[] sockets;
}
