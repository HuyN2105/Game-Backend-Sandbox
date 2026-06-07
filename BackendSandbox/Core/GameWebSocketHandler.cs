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

    public class BulletEntity
    {
        [JsonPropertyName("Id")] public Guid Id { get; set; }

        [JsonPropertyName("X")] public int X { get; set; }

        [JsonPropertyName("Y")] public int Y { get; set; }
        
        [JsonPropertyName("DirectionX")] public float DirectionX { get; set; }
        
        [JsonPropertyName("DirectionY")] public float DirectionY { get; set; }

        [JsonPropertyName("IsOwnedByPlayer")] public bool IsOwnedByPlayer { get; set; }
    }

    public class SpawnEntity
    {
        [JsonPropertyName("Id")] public Guid Id { get; set; }

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
        private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(1.0 / 64.0);
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
                    type = "Welcome",
                    PlayerId = player.Id,
                    serverTimeUtc = DateTimeOffset.UtcNow,
                    supportedMessages = new[]
                    {
                        "move { X, Y, Dt? }",
                        "teleport { X, Y }",
                        "look { X, Y }",
                        "shoot",
                        "spawnEnemy { X, Y, Width?, Height? }",
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
            Guid PlayerId,
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

                await ProcessMessageAsync(socket, sendLock, PlayerId, payload, cancellationToken);
            }
        }

        private async Task SendSnapshotsAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid PlayerId,
            CancellationToken cancellationToken)
        {
            await SendSnapshotAsync(socket, sendLock, PlayerId, cancellationToken);

            using var timer = new PeriodicTimer(SnapshotInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken) && socket.State == WebSocketState.Open)
            {
                await SendSnapshotAsync(socket, sendLock, PlayerId, cancellationToken);
            }
        }

        private async Task SendSnapshotAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid PlayerId,
            CancellationToken cancellationToken)
        {
            var snapshot = _gameLoopService.CreateSnapshot(PlayerId);

            if (snapshot == null) return;

            var currentPlayer = snapshot.Players.FirstOrDefault(p => p.Id == PlayerId);

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
                    Id = e.Id,
                    X = (int)e.X,
                    Y = (int)e.Y,
                    CurrentHp = (float)e.Health,
                    Speed = (float)e.Speed
                }).ToList(),

                Bullets = snapshot.Bullets.Select(e => new BulletEntity
                {
                    Id = e.Id,
                    X = (int)e.X,
                    Y = (int)e.Y,
                    DirectionX = e.DirectionX,
                    DirectionY = e.DirectionY,
                    IsOwnedByPlayer = e.IsOwnedByPlayer
                }).ToList()
            };

            await SendJsonAsync(socket, sendLock, liveDataPayload, cancellationToken);
        }
        
        private async Task SendRoomSwitchAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Room room,
            CancellationToken cancellationToken)
        {
            var msg = new RoomSwitchMessage
            {
                DataId = "RoomSwitch",
                LevelId = room.LevelId,
                RoomId = room.RoomId,
                Width = room.WidthInTiles,
                Height = room.HeightInTiles,
                TileSize = room.TileSize,
                LeftId = room.LeftId == -1 ? null : room.LeftId,
                RightId = room.RightId == -1 ? null : room.RightId,
                UpId = room.UpId == -1 ? null : room.UpId,
                DownId = room.DownId == -1 ? null : room.DownId,
                Tiles = new List<int>()
            };

            // Map the 2D RoomTile array back to the flat 1D JSON list
            for (int y = 0; y < room.HeightInTiles; y++)
            {
                for (int x = 0; x < room.WidthInTiles; x++)
                {
                    var tile = room.GetTileAt(x, y);
                    
                    // Convert Enum back to the JSON map integers
                    int tileValue = tile.TileType switch
                    {
                        TileTypes.Wall => 0,
                        TileTypes.Floor => 1,
                        TileTypes.Door => 2,
                        _ => 1
                    };
                    msg.Tiles.Add(tileValue);
                }
            }

            await SendJsonAsync(socket, sendLock, msg, cancellationToken);
        }

        private async Task ProcessMessageAsync(
            WebSocket socket,
            SemaphoreSlim sendLock,
            Guid PlayerId,
            string payload,
            CancellationToken cancellationToken)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                
                
                if (!document.RootElement.TryGetProperty("EventId", out var typeElement))
                {
                    await SendErrorAsync(socket, sendLock, "Message is missing a type field.", cancellationToken);
                    return;
                }

                var eventType = typeElement.GetString();
                switch (eventType)
                {
                    case "Move":
                    {
                        var direction = new Vector2(
                            GetRequiredSingle(document.RootElement, "DirectionX"),
                            GetRequiredSingle(document.RootElement, "DirectionY"));
                        var dt = Math.Clamp(GetOptionalSingle(document.RootElement, "Dt") ?? (1f / 60f), 0.001f, 0.25f);

                        // Get the player's current room using the method we just made public
                        var currentRoom = _gameLoopService.GetRoomForPlayer(PlayerId);
                        var player = currentRoom?.Players.FirstOrDefault(p => p.Id == PlayerId);
                        
                        
                        if (player != null && currentRoom != null)
                        {
                            // Check if this movement pushes the player through a door
                            var nextRoom = GameLogic.TrySwitchRoom(player, direction * player.Speed * dt, currentRoom);
                            if (nextRoom != null)
                            {
                                // Send the NEW map data to the frontend
                                await SendRoomSwitchAsync(socket, sendLock, nextRoom, cancellationToken);
                                _gameLoopService.SwitchPlayerRoom(PlayerId, nextRoom);
                            }
                        }
                        
                        // Actually move the player in the physics engine
                        _gameLoopService.MovePlayer(PlayerId, direction, dt);
                        break;
                    }
                    case "Teleport":
                    {
                        var position = new Vector2(
                            GetRequiredSingle(document.RootElement, "X"),
                            GetRequiredSingle(document.RootElement, "Y"));  
                        _gameLoopService.TeleportPlayer(PlayerId, position);
                        break;
                    }
                    case "Shoot":
                    {
                        var shootDirection = new Vector2(
                            GetRequiredSingle(document.RootElement, "X"),
                            GetRequiredSingle(document.RootElement, "Y"));
                        var isSpecial = false;
                        if (document.RootElement.TryGetProperty("IsSpecial", out var isSpecialProp))
                        {
                            isSpecial = isSpecialProp.GetBoolean();
                        }
                        _gameLoopService.Shoot(PlayerId, shootDirection, isSpecial);
                        break;
                    }
                    case "SpawnEnemy":
                    {
                        var x = GetRequiredSingle(document.RootElement, "X");
                        var y = GetRequiredSingle(document.RootElement, "Y");
                        _gameLoopService.SpawnEnemy(PlayerId, x, y);
                        break;
                    }
                    case "Snapshot":
                    {
                        await SendSnapshotAsync(socket, sendLock, PlayerId, cancellationToken);
                        break;
                    }
                    case "Ping":
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
                        await SendErrorAsync(socket, sendLock, $"Unsupported message type '{eventType}'.",
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