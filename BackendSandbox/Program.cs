using BackendSandbox.UI;
using BackendSandbox.Core;
using BackendSandbox.Models;

namespace BackendSandbox;

using System.Diagnostics;

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
            RunVisual();
        }
        else
            RunHeadless(args);
    }

    static void Update(TimeSpan delta)
    {
        Console.WriteLine(delta);
    }

    static void RunHeadless(string[] args)
    {
        // --- HEADLESS / SERVER MODE ---

        var builder = WebApplication.CreateBuilder(args);

        // 1. Register Game Loop as a Singleton (One instance shared by everyone)
        builder.Services.AddSingleton<GameLoopService>();

        // 2. Add 'HostedService' wrapper so .NET knows to run it in the background
        builder.Services.AddHostedService(provider => provider.GetRequiredService<GameLoopService>());

        var app = builder.Build();

        // --- API ENDPOINTS ---

        // GET
        app.MapGet("/status", () => "Game Server is Running!");

        // GET
        app.MapGet("/enemies", (GameLoopService gameService) =>
        {
            var room = gameService.GameRoom;
            lock (room.Enemies)
            {
                return room.Enemies.Select(e => new
                {
                    X = e.Pos.X,
                    Y = e.Pos.Y,
                    Health = e.Health
                });
            }
        });

        // POST /spawn
        app.MapPost("/spawn", (GameLoopService gameService) =>
        {
            var newEnemy = new Enemy(100, 100, 50, 50);
            gameService.GameRoom.Enemies.Add(newEnemy);
            return Results.Ok("Enemy Spawned");
        });

        Console.WriteLine("Server listening on http://localhost:5000");
        app.Run();
    }

    static void RunVisual()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SandboxForm());
    }
}