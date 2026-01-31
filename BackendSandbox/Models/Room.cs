using System.Collections.Generic;
using System.Numerics;
using System.Drawing; // For Color
using BackendSandbox.Core;
using BackendSandbox.Utils; 

namespace BackendSandbox.Models;

public class Room
{
    public SpatialGrid Grid; 

    public int LevelId { get; set; }
    public int RoomId { get; set; }

    public int WidthInTiles { get; private set; }
    public int HeightInTiles { get; private set; }
    public int TileSize { get; private set; } = 64; 

    public RoomTile[,] Tiles { get; private set; }

    public List<Enemy> Enemies { get; } = new();
    public List<Player> Players { get; set; } = new();
    public List<Entity> OtherEntities { get; } = new();

    // Navigation IDs
    public int LeftId { get; set; } = -1;
    public int RightId { get; set; } = -1;
    public int UpId { get; set; } = -1;
    public int DownId { get; set; } = -1;

    // Cache for loaded rooms
    private Room? _left;
    private Room? _right;
    private Room? _up;
    private Room? _down;

    // Lazy Loaders
    public Room? Left => (LeftId != -1 && _left == null) ? (_left = RoomLoader.LoadLeft(this)) : _left;
    public Room? Right => (RightId != -1 && _right == null) ? (_right = RoomLoader.LoadRight(this)) : _right;
    public Room? Up => (UpId != -1 && _up == null) ? (_up = RoomLoader.LoadUp(this)) : _up;
    public Room? Down => (DownId != -1 && _down == null) ? (_down = RoomLoader.LoadDown(this)) : _down;

    public Room(int widthInTiles, int heightInTiles)
    {
        WidthInTiles = widthInTiles;
        HeightInTiles = heightInTiles;

        int pixelWidth = widthInTiles * TileSize;
        int pixelHeight = heightInTiles * TileSize;

        Grid = new SpatialGrid(pixelWidth, pixelHeight);
        Tiles = new RoomTile[widthInTiles, heightInTiles];

        // Default to floor, the Loader will overwrite this
        for (int x = 0; x < widthInTiles; x++)
        {
            for (int y = 0; y < heightInTiles; y++)
            {
                Tiles[x, y] = new RoomTile(TileTypes.Floor);
            }
        }
    }

    public void SetTile(int x, int y, RoomTile tile)
    {
        if (x >= 0 && x < WidthInTiles && y >= 0 && y < HeightInTiles)
        {
            Tiles[x, y] = tile;
        }
    }

    public RoomTile GetTileAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= WidthInTiles || y >= HeightInTiles)
            return new RoomTile(TileTypes.Wall); 
        return Tiles[x, y];
    }

    public bool IsOutOfBounds(Vector2 pos)
    {
        return pos.X < 0 || pos.X > WidthInTiles * TileSize ||
               pos.Y < 0 || pos.Y > HeightInTiles * TileSize;
    }

    public void GameProgress(float dt)
    {
        Grid.Clear();
        foreach (var enemy in Enemies) Grid.Insert(enemy);
        foreach (var player in Players) Grid.Insert(player);

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            if (Enemies[i].IsDead) Enemies.RemoveAt(i);
        }

        for (int i = OtherEntities.Count - 1; i >= 0; i--)
        {
            if (OtherEntities[i] is Bullet bullet)
            {
                var candidates = Grid.Retrieve(bullet);
                bullet.Move(dt, this, candidates);

                if (bullet.IsDead || IsOutOfBounds(bullet.Pos))
                {
                    OtherEntities.RemoveAt(i);
                }
            }
        }
    }
}