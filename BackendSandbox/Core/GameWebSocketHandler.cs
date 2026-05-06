using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using BackendSandbox.Models;

namespace BackendSandbox.Models
{
    // Base class for routing WebSocket messages
    public class BaseWebSocketMessage
    {
        [JsonPropertyName("DataId")] public string DataId { get; set; }
    }

    // --- Live Data Models ---
    public class LiveDataMessage : BaseWebSocketMessage
    {
        [JsonPropertyName("PlayerX")] public int PlayerX { get; set; }
        
        [JsonPropertyName("PlayerY")] public int PlayerY { get; set; }
        [JsonPropertyName("CurrentPlayerHp")] public float CurrentPlayerHp { get; set; }

        [JsonPropertyName("Speed")] public float Speed { get; set; }

        [JsonPropertyName("Spawns")] public List<SpawnEntity> Spawns { get; set; }

        // Added the bullets array!
        [JsonPropertyName("Bullets")] public List<BulletEntity> Bullets { get; set; } = new List<BulletEntity>();
    }

    // You'll need a model for what a bullet looks like
    public class BulletEntity
    {
        [JsonPropertyName("X")] public int X { get; set; }

        [JsonPropertyName("Y")] public int Y { get; set; }
        
        [JsonPropertyName("DirectionX")] public int DirectionX { get; set; }
        
        [JsonPropertyName("DirectionY")] public int DirectionY { get; set; }
    }

    public class SpawnEntity
    {
        [JsonPropertyName("X")] public int X { get; set; }

        [JsonPropertyName("Y")] public int Y { get; set; }

        [JsonPropertyName("CurrentHp")] public float CurrentHp { get; set; }

        [JsonPropertyName("Speed")] public float Speed { get; set; }
    }

    // --- Room Switch Models ---
    public class RoomSwitchMessage : BaseWebSocketMessage
    {
        [JsonPropertyName("LevelId")] public int LevelId { get; set; }

        [JsonPropertyName("Biome")] public string Biome { get; set; }

        [JsonPropertyName("Width")] public int Width { get; set; }

        [JsonPropertyName("Height")] public int Height { get; set; }

        [JsonPropertyName("TileSize")] public int TileSize { get; set; }

        [JsonPropertyName("RoomId")] public int RoomId { get; set; }

        // Using nullable ints in case a room doesn't have an exit in a specific direction
        [JsonPropertyName("LeftId")] public int? LeftId { get; set; }

        [JsonPropertyName("RightId")] public int? RightId { get; set; }

        [JsonPropertyName("UpId")] public int? UpId { get; set; }

        [JsonPropertyName("DownId")] public int? DownId { get; set; }

        [JsonPropertyName("Tiles")] public List<int> Tiles { get; set; }
    }
}

namespace BackendSandbox.Core
{
    public class GameWebSocketHandler
    {
        private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(20);
        private readonly GameLoopService _gameLoopService;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public GameWebSocketHandler(GameLoopService gameLoopService)
        {
            _gameLoopService = gameLoopService;
        }

        public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
        {
            // if the async request was not a websocket request
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Expected a websocket request.", cancellationToken);
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var player = _gameLoopService.AddPlayer();
            using var sendLock = new SemaphoreSlim(1, 1);
            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.RequestAborted);

            try
            {
                await SendJsonAsync(socket, sendLock, new
                {
                    type = "welcome",
                    playerId = player.Id,
                    serverTimeUtc = DateTimeOffset.UtcNow,
                    supportedMessages = new[]
                    {
                        "move { x, y, dt? }",
                        "teleport { x, y }",
                        "look { x, y }",
                        "shoot",
                        "spawnEnemy { x, y, width?, height? }",
                        "snapshot",
                        "ping"
                    }
                }, linkedCts.Token);

                var receiveTask = ReceiveLoopAsync(socket, sendLock, player.Id, linkedCts.Token);
                var sendTask = SendSnapshotsAsync(socket, sendLock, player.Id, linkedCts.Token);

                await Task.WhenAny(receiveTask, sendTask);
                linkedCts.Cancel();

                await Task.WhenAll(
                    ObserveTaskAsync(receiveTask),
                    ObserveTaskAsync(sendTask));
            }
            finally
            {
                _gameLoopService.RemovePlayer(player.Id);
                await CloseSocketAsync(socket, CancellationToken.None);
            }
        }

        private async Task ReceiveLoopAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid playerId,
            CancellationToken cancellationToken)
        {
            // Keeps running as long as the socket is open
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var payload = await ReceiveTextAsync(socket, cancellationToken);
                if (payload is null) // disconnected
                {
                    return;
                }

                await ProcessMessageAsync(socket, sendLock, playerId, payload, cancellationToken);
            }
        }

        private async Task SendSnapshotsAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid playerId,
            CancellationToken cancellationToken)
        {
            await SendSnapshotAsync(socket, sendLock, playerId, cancellationToken);

            using var timer = new PeriodicTimer(SnapshotInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken) && socket.State == WebSocketState.Open)
            {
                await SendSnapshotAsync(socket, sendLock, playerId, cancellationToken);
            }
        }

        private async Task SendSnapshotAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid playerId,
            CancellationToken cancellationToken)
        {
            var snapshot = _gameLoopService.CreateSnapshot(playerId);

            if (snapshot == null) return;

            var currentPlayer = snapshot.Players.FirstOrDefault(p => p.Id == playerId);

            if (currentPlayer == null) return;

            var liveDataPayload = new LiveDataMessage
            {
                DataId = "LiveData",

                PlayerX = (int)currentPlayer.X,
                PlayerY = (int)currentPlayer.Y,
                CurrentPlayerHp = (float)currentPlayer.Health,
                Speed = (float)currentPlayer.Speed,

                Spawns = snapshot.Enemies.Select(e => new SpawnEntity
                {
                    X = (int)e.X,
                    Y = (int)e.Y,
                    CurrentHp = (float)e.Health,
                    Speed = (float)e.Speed
                }).ToList(),

                Bullets = snapshot.Bullets.Select(e => new BulletEntity
                {
                    X = (int)e.X,
                    Y = (int)e.Y,
                    DirectionX = (int)e.DirectionX,
                    DirectionY = (int)e.DirectionY
                }).ToList()
            };

            await SendJsonAsync(socket, sendLock, liveDataPayload, cancellationToken);
        }

        private async Task ProcessMessageAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid playerId,
            string payload,
            CancellationToken cancellationToken)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                if (!document.RootElement.TryGetProperty("type", out var typeElement))
                {
                    await SendErrorAsync(socket, sendLock, "Message is missing a type field.", cancellationToken);
                    return;
                }

                var messageType = typeElement.GetString();
                switch (messageType)
                {
                    case "move":
                    {
                        var direction = new Vector2(
                            GetRequiredSingle(document.RootElement, "x"),
                            GetRequiredSingle(document.RootElement, "y"));
                        var dt = Math.Clamp(GetOptionalSingle(document.RootElement, "dt") ?? (1f / 60f), 0.001f, 0.25f);
                        _gameLoopService.MovePlayer(playerId, direction, dt);
                        break;
                    }
                    case "teleport":
                    {
                        var position = new Vector2(
                            GetRequiredSingle(document.RootElement, "x"),
                            GetRequiredSingle(document.RootElement, "y"));
                        _gameLoopService.TeleportPlayer(playerId, position);
                        break;
                    }
                    case "shoot":
                    {
                        var shootDirection = new Vector2(
                            GetRequiredSingle(document.RootElement, "x"),
                            GetRequiredSingle(document.RootElement, "y"));
                        _gameLoopService.Shoot(playerId, shootDirection);
                        break;
                    }
                    case "spawnEnemy":
                    {
                        var x = GetRequiredSingle(document.RootElement, "x");
                        var y = GetRequiredSingle(document.RootElement, "y");
                        _gameLoopService.SpawnEnemy(playerId, x, y);
                        break;
                    }
                    case "snapshot":
                    {
                        await SendSnapshotAsync(socket, sendLock, playerId, cancellationToken);
                        break;
                    }
                    case "ping":
                    {
                        await SendJsonAsync(socket, sendLock, new
                        {
                            type = "pong",
                            serverTimeUtc = DateTimeOffset.UtcNow
                        }, cancellationToken);
                        break;
                    }
                    default:
                    {
                        await SendErrorAsync(socket, sendLock, $"Unsupported message type '{messageType}'.",
                            cancellationToken);
                        break;
                    }
                }
            }
            catch (JsonException)
            {
                await SendErrorAsync(socket, sendLock, "Invalid JSON payload.", cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                await SendErrorAsync(socket, sendLock, exception.Message, cancellationToken);
            }
        }

        private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4 * 1024];
            using var stream = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        private async Task SendErrorAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            string message,
            CancellationToken cancellationToken)
        {
            await SendJsonAsync(socket, sendLock, new
            {
                type = "error",
                message
            }, cancellationToken);
        }

        private async Task SendJsonAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            object payload,
            CancellationToken cancellationToken)
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await sendLock.WaitAsync(cancellationToken);
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        private static async Task CloseSocketAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed.", cancellationToken);
            }
        }

        private static async Task ObserveTaskAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
        }

        private static float GetRequiredSingle(JsonElement element, string propertyName)
        {
            var value = GetOptionalSingle(element, propertyName);
            if (value.HasValue)
            {
                return value.Value;
            }

            throw new InvalidOperationException($"Message is missing numeric field '{propertyName}'.");
        }

        private static float? GetOptionalSingle(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetSingle(out var value) => value,
                JsonValueKind.Number => property.GetInt32(),
                _ => throw new InvalidOperationException($"Field '{propertyName}' must be numeric.")
            };
        }
    }
}