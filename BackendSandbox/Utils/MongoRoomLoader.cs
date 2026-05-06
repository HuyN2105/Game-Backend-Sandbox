using System.Linq;
using MongoDB.Driver;
using BackendSandbox.Models;

namespace BackendSandbox.Utils;

public class MongoRoomLoader
{
    private readonly IMongoCollection<LevelData> _roomsCollection;

    public MongoRoomLoader(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        
        _roomsCollection = database.GetCollection<LevelData>("levels"); 
    }

    public Room LoadInitialRoom()
    {
        return LoadRoomFromDb(1, 1);
    }

    public Room LoadRoom(int levelId, int roomId)
    {
        if (roomId == -1) return null;
        return LoadRoomFromDb(levelId, roomId);
    }

    private Room LoadRoomFromDb(int levelId, int roomId)
    {
        // Fetch the data from MongoDB
        var data = _roomsCollection.Find(r => r.LevelId == levelId && r.RoomId == roomId).FirstOrDefault();

        if (data == null)
        {
            Logger.Error($"Failed to load level {levelId} room {roomId} from MongoDB");
            return new Room(20, 12); // Fallback empty room
        }

        // Map data to the Game Engine Room Object
        var room = new Room(data.Width, data.Height)
        {
            LevelId = data.LevelId,
            RoomId = data.RoomId,
            LeftId = data.LeftId,
            RightId = data.RightId,
            UpId = data.UpId,
            DownId = data.DownId
        };

        // Map Tiles
        for (int i = 0; i < data.Tiles.Length; i++)
        {
            int x = i % data.Width;
            int y = i / data.Width;

            TileTypes type = data.Tiles[i] switch
            {
                0 => TileTypes.Wall,
                1 => TileTypes.Floor,
                2 => TileTypes.Door,
                _ => TileTypes.Floor
            };

            room.SetTile(x, y, new RoomTile(type));
        }

        // Map Spawns
        if (data.Spawns != null)
        {
            foreach (var spawn in data.Spawns)
            {
                if (spawn.Type == "enemy")
                {
                    room.Enemies.Add(new Enemy(spawn.X, spawn.Y, 50, 50));
                }
            }
        }

        return room;
    }
}