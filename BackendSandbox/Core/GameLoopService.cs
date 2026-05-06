using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using BackendSandbox.Models;
using BackendSandbox.Utils; // Needed for RoomLoader
using Microsoft.Extensions.Hosting;

namespace BackendSandbox.Core;

public class GameLoopService : BackgroundService
{
    // Dictionary of active rooms mapped by PlayerId
    private readonly ConcurrentDictionary<Guid, Room> _activeRooms = new();

    public GameLoopService()
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Headless Instanced Game Loop Started...");

        var stopwatch = Stopwatch.StartNew();
        var lastTime = stopwatch.Elapsed;
        var tickRate = TimeSpan.FromSeconds(1.0 / 60.0);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = stopwatch.Elapsed;
            var dt = (float)(now - lastTime).TotalSeconds;
            lastTime = now;

            // Tick EVERY active room independently
            foreach (var kvp in _activeRooms)
            {
                var room = kvp.Value;
                // Lock the individual room so we don't update it while someone is joining/leaving
                lock (room)
                {
                    room.GameProgress(dt);
                }
            }

            var sleepTime = tickRate - (stopwatch.Elapsed - now);
            if (sleepTime > TimeSpan.Zero)
            {
                await Task.Delay(sleepTime, stoppingToken);
            }
        }
    }

    public Player AddPlayer(float x = 228f, float y = 228f, int width = 48, int height = 48)
    {
        var player = new Player(x, y, width, height);

        // Create a brand new unique room for this new connection
        var room = RoomLoader.InitialLoad() ?? new Room(20, 12);

        // Optional: Add a test enemy to the new room
        room.Enemies.Add(new Enemy(400, 300, 50, 50));
        room.Players.Add(player);

        // Assign the room to this player's ID
        _activeRooms.TryAdd(player.Id, room);

        return player;
    }

    public void RemovePlayer(Guid playerId)
    {
        // Destroy the room when the player disconnects
        _activeRooms.TryRemove(playerId, out _);
    }

    // Helper to find a specific player's room
    private Room? GetRoomForPlayer(Guid playerId)
    {
        _activeRooms.TryGetValue(playerId, out var room);
        return room;
    }

    public bool MovePlayer(Guid playerId, Vector2 direction, float dt)
    {
        var room = GetRoomForPlayer(playerId);
        if (room is null) return false;

        lock (room)
        {
            var player = room.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null) return false;

            player.Move(direction, dt, room);
            return true;
        }
    }

    public bool TeleportPlayer(Guid playerId, Vector2 position)
    {
        var room = GetRoomForPlayer(playerId);
        if (room is null) return false;

        lock (room)
        {
            var player = room.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null) return false;

            player.Pos = position;
            return true;
        }
    }

    public bool Shoot(Guid playerId, Vector2 shootDirection)
    {
        var room = GetRoomForPlayer(playerId);
        if (room is null) return false;

        lock (room)
        {
            var player = room.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null) return false;

            player.Shoot(room, shootDirection);
            return true;
        }
    }

    public Enemy? SpawnEnemy(Guid playerId, float x, float y, int width = 50, int height = 50)
    {
        var room = GetRoomForPlayer(playerId);
        if (room is null) return null;

        lock (room)
        {
            var enemy = new Enemy(x, y, width, height);
            room.Enemies.Add(enemy);
            return enemy;
        }
    }

    // 4. Now requires a PlayerId to know WHICH world to snapshot
    public GameStateSnapshot? CreateSnapshot(Guid playerId)
    {
        var room = GetRoomForPlayer(playerId);
        if (room is null) return null;

        lock (room)
        {
            return new GameStateSnapshot(
                room.WidthInTiles * room.TileSize,
                room.HeightInTiles * room.TileSize,
                room.TileSize,
                room.Players.Select(player => new PlayerSnapshot(
                    player.Id,
                    player.Pos.X,
                    player.Pos.Y,
                    player.Speed,
                    player.Health)).ToArray(),
                room.Enemies.Select(enemy => new EnemySnapshot(
                    enemy.Id,
                    enemy.Pos.X,
                    enemy.Pos.Y,
                    enemy.Speed,
                    enemy.Health)).ToArray(),
                room.OtherEntities
                    .OfType<Bullet>()
                    .Select(bullet => new BulletSnapshot(
                        bullet.Pos.X,
                        bullet.Pos.Y,
                        bullet.MovingDirection.X,
                        bullet.MovingDirection.Y,
                        bullet.IsOwnedByPlayer))
                    .ToArray());
        }
    }
}

public sealed record GameStateSnapshot(
    int WorldWidth,
    int WorldHeight,
    int TileSize,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<EnemySnapshot> Enemies,
    IReadOnlyList<BulletSnapshot> Bullets);

public sealed record PlayerSnapshot(
    Guid Id,
    float X,
    float Y,
    float Speed,
    float Health);

public sealed record EnemySnapshot(
    Guid Id,
    float X,
    float Y,
    float Speed,
    float Health);

public sealed record BulletSnapshot(
    float X,
    float Y,
    float DirectionX,
    float DirectionY,
    bool IsOwnedByPlayer);