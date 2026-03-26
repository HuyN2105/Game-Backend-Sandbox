using BackendSandbox.Models;
using System.Drawing; // For Brushes
using System.Numerics; // For Vector2

namespace BackendSandbox.Utils;

public static class RoomLoader
{
    public static Room InitialLoad()
    {
        return LoadRoomFromFile("level1-1.jsonc");
    }

    public static Room LoadLeft(Room currentRoom)
    {
        if (currentRoom.LeftId == -1) return null;
        return LoadRoomFromFile($"level{currentRoom.LevelId}-{currentRoom.LeftId}.jsonc");
    }

    public static Room LoadRight(Room currentRoom)
    {
        if (currentRoom.RightId == -1) return null;
        return LoadRoomFromFile($"level{currentRoom.LevelId}-{currentRoom.RightId}.jsonc");
    }

    public static Room LoadUp(Room currentRoom)
    {
        if (currentRoom.UpId == -1) return null;
        return LoadRoomFromFile($"level{currentRoom.LevelId}-{currentRoom.UpId}.jsonc");
    }

    public static Room LoadDown(Room currentRoom)
    {
        if (currentRoom.DownId == -1) return null;
        return LoadRoomFromFile($"level{currentRoom.LevelId}-{currentRoom.DownId}.jsonc");
    }

    public static Room LoadUpFloor(Room currentRoom)
    {
        if (currentRoom.FloorUpId == -1) return null;
        return LoadRoomFromFile($"level{currentRoom.FloorUpId}-1.jsonc");
    }

    public static Room LoadDownFloor(Room currentRoom)
    {
        if(currentRoom.FloorDownId == -1) return null;
        return LoadRoomFromFile($"level{currentRoom.FloorDownId}-1.jsonc");
    }

    private static Room LoadRoomFromFile(string filename)
    {
        // Deserialize into the Simple DTO
        var data = JsonLoader.LoadJsonc<LevelData>($"data/{filename}");

        if (data == null)
        {
            Logger.Error($"Failed to load level data from {filename}");
            return new Room(20, 12); // Fallback empty room
        }

        // Create the real Game Room
        var room = new Room(data.Width, data.Height);
        room.LevelId = data.LevelId;
        room.RoomId = data.RoomId;
        room.LeftId = data.LeftId;
        room.RightId = data.RightId;
        room.UpId = data.UpId;
        room.DownId = data.DownId;

        // Convert 1D Int Array -> 2D RoomTile Array
        // Expected format: data.Tiles is [Width * Height]
        for (int i = 0; i < data.Tiles.Length; i++)
        {
            int x = i % data.Width;
            int y = i / data.Width;

            int tileValue = data.Tiles[i];

            // MAP: 0=Wall, 1=Floor, 2=Door
            TileTypes type = tileValue switch
            {
                0 => TileTypes.Wall,
                1 => TileTypes.Floor,
                2 => TileTypes.Door,
                _ => TileTypes.Floor
            };

            room.SetTile(x, y, new RoomTile(type));
        }

        // 4. Handle Spawns
        if (data.Spawns != null)
        {
            foreach (var spawn in data.Spawns)
            {
                if (spawn.Type == "enemy")
                {
                    // Convert integer grid pos to pixel pos if needed, or raw pixels
                    // Assuming JSON X/Y are in Pixels based on your file
                    room.Enemies.Add(new Enemy(spawn.X, spawn.Y, 50, 50));
                }
            }
        }

        return room;
    }
}