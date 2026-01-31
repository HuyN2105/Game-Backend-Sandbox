namespace BackendSandbox.Models;

public class LevelData
{
    public int LevelId { get; set; }
    public string Biome { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileSize { get; set; }
    public int RoomId { get; set; }

    // Connections
    public int LeftId { get; set; } = -1;
    public int RightId { get; set; } = -1;
    public int UpId { get; set; } = -1;
    public int DownId { get; set; } = -1;

    // The raw 1D array from the file
    public int[] Tiles { get; set; }

    // The raw spawns list
    public List<SpawnData> Spawns { get; set; }
}

public class SpawnData
{
    public string Type { get; set; } // "enemy", "player", etc.
    public int X { get; set; }
    public int Y { get; set; }
}