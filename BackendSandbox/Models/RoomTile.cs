namespace BackendSandbox.Models;

public struct RoomTile(TileTypes tileType)
{
    public TileTypes TileType => tileType;
    public bool IsSolid => TileType == TileTypes.Wall || (TileType == TileTypes.Door && IsClosed);

    private bool _isClosed = false;
    public bool IsClosed
    {
        get => TileType == TileTypes.Door && _isClosed;
        set
        {
            if(TileType != TileTypes.Door) return;
            _isClosed = value;
        }
    }
}