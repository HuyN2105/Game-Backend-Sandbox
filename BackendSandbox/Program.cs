using BackendSandbox.Core;
#if WINDOWS
using BackendSandbox.UI;
#endif

namespace BackendSandbox;

enum RunMode
{
    Headless,
    Visual
}

class Program
{
    static void Main(string[] args)
    {
        var mode = args.Contains("--visual") ? RunMode.Visual : RunMode.Headless;

        if (mode == RunMode.Visual)
        {
#if WINDOWS
            RunVisual();
#else
            Console.WriteLine("Visual mode is only supported on Windows.");
            RunHeadless(args);
#endif
        }
        else
        {
            RunHeadless(args);
        }
    }

    static void RunHeadless(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://0.0.0.0:5000");

        builder.Services.AddSingleton<GameLoopService>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<GameLoopService>());
        builder.Services.AddSingleton<GameWebSocketHandler>();

        var app = builder.Build();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        app.MapGet("/status", () => Results.Ok(new
        {
            message = "Game Server is Running!",
            websocket = "ws://localhost:5000/ws",
            httpState = "http://localhost:5000/state"
        }));

        app.MapGet("/state", (GameLoopService gameService) =>
        {
            var snapshot = gameService.CreateSnapshot();
            return Results.Ok(snapshot);
        });

        app.MapGet("/enemies", (GameLoopService gameService) =>
        {
            var snapshot = gameService.CreateSnapshot();
            return Results.Ok(snapshot.Enemies);
        });

        app.MapPost("/spawn", (GameLoopService gameService) =>
        {
            var enemy = gameService.SpawnEnemy(100, 100);
            return Results.Ok(new
            {
                message = "Enemy spawned.",
                enemyId = enemy.Id
            });
        });

        app.Map("/ws", async context =>
        {
            var handler = context.RequestServices.GetRequiredService<GameWebSocketHandler>();
            await handler.HandleAsync(context, context.RequestAborted);
        });

        Console.WriteLine("Server listening on http://localhost:5000");
        Console.WriteLine("WebSocket endpoint available at ws://localhost:5000/ws");
        app.Run();
    }
#if WINDOWS
    static void RunVisual()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SandboxForm());
    }
#endif
}