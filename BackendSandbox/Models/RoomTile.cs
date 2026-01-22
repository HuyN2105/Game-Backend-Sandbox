namespace BackendSandbox.Models;

public struct RoomTile
{
    public TileTypes TileType;
    public bool IsSolid => TileType == TileTypes.Wall;

    public RoomTile(TileTypes tileType)
    {
        TileType = tileType;
    }
}