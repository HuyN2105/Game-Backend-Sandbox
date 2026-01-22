using System.Collections.Generic;
using System.Numerics;
using System.Drawing; // For Color
using BackendSandbox.Core; // To use SpatialGrid

namespace BackendSandbox.Models;

public class Room
{
    public SpatialGrid Grid; // Optimization Grid

    public int WidthInTiles { get; private set; }
    public int HeightInTiles { get; private set; }
    public int TileSize { get; private set; } = 64; // Logical size

    public RoomTile[,] Tiles { get; private set; }

    public List<Enemy> Enemies { get; } = new();
    public List<Player> Players { get; } = new();
    public List<Entity> OtherEntities { get; } = new();

    public Room(int widthInTiles, int heightInTiles)
    {
        WidthInTiles = widthInTiles;
        HeightInTiles = heightInTiles;
        
        int pixelWidth = widthInTiles * TileSize;
        int pixelHeight = heightInTiles * TileSize;

        Grid = new SpatialGrid(pixelWidth, pixelHeight);
        Tiles = new RoomTile[widthInTiles, heightInTiles];

        for (int x = 0; x < widthInTiles; x++)
        {
            for (int y = 0; y < heightInTiles; y++)
            {
                Tiles[x, y] = new RoomTile(TileTypes.Floor);
            }
        }
        
        AddBorders();
    }

    private void AddBorders()
    {
        for (int x = 0; x < WidthInTiles; x++)
        {
            Tiles[x, 0] = new RoomTile(TileTypes.Wall);
            Tiles[x, HeightInTiles - 1] = new RoomTile(TileTypes.Wall);
        }
        for (int y = 0; y < HeightInTiles; y++)
        {
            Tiles[0, y] = new RoomTile(TileTypes.Wall);
            Tiles[WidthInTiles - 1, y] = new RoomTile(TileTypes.Wall);
        }
    }

    public RoomTile GetTileAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= WidthInTiles || y >= HeightInTiles)
            return new RoomTile(TileTypes.Wall); // Treat Out of Bounds as Wall
        return Tiles[x, y];
    }
    
    public bool IsOutOfBounds(Vector2 pos)
    {
        return pos.X < 0 || pos.X > WidthInTiles * TileSize ||
               pos.Y < 0 || pos.Y > HeightInTiles * TileSize;
    }

    public void GameProgress(float dt)
    {
        // Rebuild Spatial Grid
        Grid.Clear();
        foreach (var enemy in Enemies) Grid.Insert(enemy);
        foreach (var player in Players) Grid.Insert(player);

        // Process Enemies
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            if (Enemies[i].IsDead) Enemies.RemoveAt(i);
            // Enemy movement logic is called from GameLoop or Form Update
        }

        // 3. Process Bullets
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