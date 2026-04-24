using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace BackendSandbox.Core;

public class GameWebSocketHandler
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(100);
    private readonly GameLoopService _gameLoopService;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public GameWebSocketHandler(GameLoopService gameLoopService)
    {
        _gameLoopService = gameLoopService;
    }

    public async Task HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
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
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var payload = await ReceiveTextAsync(socket, cancellationToken);
            if (payload is null)
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
        var snapshot = _gameLoopService.CreateSnapshot();
        await SendJsonAsync(socket, sendLock, new
        {
            type = "state",
            playerId,
            serverTimeUtc = DateTimeOffset.UtcNow,
            snapshot
        }, cancellationToken);
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
                    var width = (int)MathF.Round(GetOptionalSingle(document.RootElement, "width") ?? 50f);
                    var height = (int)MathF.Round(GetOptionalSingle(document.RootElement, "height") ?? 50f);
                    _gameLoopService.SpawnEnemy(x, y, width, height);
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