// M1 of the BRUTAL evaluation (GuitarAlchemist/gaia#257): open one window through BRUTAL's GLFW binding, log keyboard
// input, and close cleanly on Esc or after a time limit (seconds, first argument, default 10). No graphics API: this
// proves the lifecycle, not rendering. Built on the upstream README's "Your first window" (ahwootech/BRUTAL a25cdf8).

using System.Diagnostics;
using Brutal.GlfwApi;
using Brutal.Numerics;

var limit = TimeSpan.FromSeconds(args.Length > 0 && double.TryParse(args[0], out var seconds) ? seconds : 10.0);
var clock = Stopwatch.StartNew();
if (!Glfw.Init())
{
    Console.Error.WriteLine("M1 FAIL: Glfw.Init returned false");
    return 1;
}

var reason = "window closed";
long loops = 0;
var keys = 0;
try
{
    Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
    var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = "Hello BRUTAL", Size = new int2(640, 360) });
    Console.WriteLine($"M1 window open after {clock.Elapsed.TotalMilliseconds:F0} ms");
    window.OnKey += (w, key, scancode, action, mods) =>
    {
        if (action == GlfwKeyAction.Release)
        {
            return;
        }

        keys++;
        Console.WriteLine($"M1 key {key} {action} scancode={scancode} mods={mods} at {clock.Elapsed.TotalSeconds:F2} s");
        if (key == GlfwKey.Escape)
        {
            reason = "Esc";
            w.ShouldClose = true;
        }
    };

    while (!window.ShouldClose)
    {
        Glfw.PollEvents();
        loops++;
        if (clock.Elapsed >= limit)
        {
            reason = "time limit";
            break;
        }

        Thread.Sleep(1);
    }

    window.Dispose();
}
finally
{
    Glfw.Terminate();
}

Console.WriteLine($"M1 closed by {reason} after {clock.Elapsed.TotalSeconds:F2} s: {loops} loop iterations, {keys} key events");
return 0;
