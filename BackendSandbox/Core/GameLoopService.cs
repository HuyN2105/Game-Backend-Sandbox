using System.Diagnostics;
using System.Numerics;
using BackendSandbox.Models;
using Microsoft.Extensions.Hosting;

namespace BackendSandbox.Core;

public class GameLoopService : BackgroundService
{
    public Room GameRoom { get; }
    public object SyncRoot { get; } = new();

    public GameLoopService()
    {
        GameRoom = new Room(1280, 720);
        GameRoom.Enemies.Add(new Enemy(400, 300, 50, 50));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Headless Game Loop Started...");

        var stopwatch = Stopwatch.StartNew();
        var lastTime = stopwatch.Elapsed;
        var tickRate = TimeSpan.FromSeconds(1.0 / 60.0);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = stopwatch.Elapsed;
            var dt = (float)(now - lastTime).TotalSeconds;
            lastTime = now;

            lock (SyncRoot)
            {
                GameRoom.GameProgress(dt);
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
        lock (SyncRoot)
        {
            var player = new Player(x, y, width, height);
            GameRoom.Players.Add(player);
            return player;
        }
    }

    public void RemovePlayer(Guid playerId)
    {
        lock (SyncRoot)
        {
            var index = GameRoom.Players.FindIndex(player => player.Id == playerId);
            if (index >= 0)
            {
                GameRoom.Players.RemoveAt(index);
            }
        }
    }

    public bool MovePlayer(Guid playerId, Vector2 direction, float dt)
    {
        lock (SyncRoot)
        {
            var player = FindPlayer(playerId);
            if (player is null)
            {
                return false;
            }

            player.Move(direction, dt, GameRoom);
            return true;
        }
    }

    public bool TeleportPlayer(Guid playerId, Vector2 position)
    {
        lock (SyncRoot)
        {
            var player = FindPlayer(playerId);
            if (player is null)
            {
                return false;
            }

            player.Pos = position;
            return true;
        }
    }

    public bool SetPlayerLook(Guid playerId, Vector2 worldTarget)
    {
        lock (SyncRoot)
        {
            var player = FindPlayer(playerId);
            if (player is null)
            {
                return false;
            }

            player.LookingDirection = worldTarget;
            return true;
        }
    }

    public bool Shoot(Guid playerId)
    {
        lock (SyncRoot)
        {
            var player = FindPlayer(playerId);
            if (player is null)
            {
                return false;
            }

            player.Shoot(GameRoom);
            return true;
        }
    }

    public Enemy SpawnEnemy(float x, float y, int width = 50, int height = 50)
    {
        lock (SyncRoot)
        {
            var enemy = new Enemy(x, y, width, height);
            GameRoom.Enemies.Add(enemy);
            return enemy;
        }
    }

    public GameStateSnapshot CreateSnapshot()
    {
        lock (SyncRoot)
        {
            return new GameStateSnapshot(
                GameRoom.WidthInTiles * GameRoom.TileSize,
                GameRoom.HeightInTiles * GameRoom.TileSize,
                GameRoom.TileSize,
                GameRoom.Players.Select(player => new PlayerSnapshot(
                    player.Id,
                    player.Pos.X,
                    player.Pos.Y,
                    player.Width,
                    player.Height,
                    player.Health)).ToArray(),
                GameRoom.Enemies.Select(enemy => new EnemySnapshot(
                    enemy.Id,
                    enemy.Pos.X,
                    enemy.Pos.Y,
                    enemy.Width,
                    enemy.Height,
                    enemy.Health)).ToArray(),
                GameRoom.OtherEntities
                    .OfType<Bullet>()
                    .Select(bullet => new BulletSnapshot(
                        bullet.Id,
                        bullet.Pos.X,
                        bullet.Pos.Y,
                        bullet.Width,
                        bullet.Height,
                        bullet.MovingDirection.X,
                        bullet.MovingDirection.Y,
                        bullet.IsOwnedByPlayer))
                    .ToArray());
        }
    }

    private Player? FindPlayer(Guid playerId)
    {
        return GameRoom.Players.FirstOrDefault(player => player.Id == playerId);
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
    int Width,
    int Height,
    float Health);

public sealed record EnemySnapshot(
    Guid Id,
    float X,
    float Y,
    int Width,
    int Height,
    float Health);

public sealed record BulletSnapshot(
    Guid Id,
    float X,
    float Y,
    int Width,
    int Height,
    float DirectionX,
    float DirectionY,
    bool IsOwnedByPlayer);
