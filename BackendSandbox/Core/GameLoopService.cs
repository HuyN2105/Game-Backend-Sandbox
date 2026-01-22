using BackendSandbox.Models;
using Microsoft.Extensions.Hosting; // For BackgroundService
using System.Diagnostics;

namespace BackendSandbox.Core;

public class GameLoopService : BackgroundService
{
    public Room GameRoom { get; private set; }

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

            GameRoom.GameProgress(dt);

            // Control Tick Rate (Sleep to save CPU)
            var sleepTime = tickRate - (stopwatch.Elapsed - now);
            if (sleepTime > TimeSpan.Zero)
            {
                await Task.Delay(sleepTime, stoppingToken);
            }
        }
    }
}