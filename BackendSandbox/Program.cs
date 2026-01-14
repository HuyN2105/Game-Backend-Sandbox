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
            RunHeadless();
    }

    static void Update(TimeSpan delta)
    {
        Console.WriteLine(delta);
    }

    static void RunHeadless()
    {
        Console.WriteLine("Headless mode");
        
        const int tickRate = 60;
        var tickTime = TimeSpan.FromSeconds(1.0 / tickRate);

        var stopWatch = Stopwatch.StartNew();
        var last = stopWatch.Elapsed;

        while (true)
        {
            var now = stopWatch.Elapsed;
            var delta = now - last;
            last = now;

            Update(delta);

            var sleep = tickTime - delta;
            if (sleep > TimeSpan.Zero)
            {
                Thread.Sleep(sleep);
            }
        }
    }

    static void RunVisual()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SandboxForm());
    }
}