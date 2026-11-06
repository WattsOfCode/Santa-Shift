using SantaShift.Core.Content;
using SantaShift.Game.App;

namespace SantaShift.Game;

public static class Program
{
    public static int Main(string[] args)
    {
        string dataDir = Path.Combine(AppContext.BaseDirectory, "Content", "data");

        GameContent content;
        try
        {
            content = ContentLoader.LoadFromDirectory(dataDir);
        }
        catch (ContentException ex)
        {
            // Bad data should explain itself, not crash with a stack trace.
            string log = Path.Combine(AppContext.BaseDirectory, "santashift-error.log");
            File.WriteAllText(log, ex.Message);
            Console.Error.WriteLine($"Santa Shift could not load its data: {ex.Message}");
            return 1;
        }

        foreach (var warning in content.Warnings) Console.WriteLine($"[content] {warning}");

        new GameApp(content, ParseOptions(args)).Run();
        return 0;
    }

    private static LaunchOptions ParseOptions(string[] args)
    {
#if DEBUG
        // Debug-only automation: --screenshot <file.png> [--frame N] [--workshop] [--yaw deg] [--pitch deg]
        string? shot = null;
        int frame = 90;
        bool workshop = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) shot = Path.GetFullPath(args[++i]);
            else if (args[i] == "--frame" && i + 1 < args.Length && int.TryParse(args[++i], out int f)) frame = f;
            else if (args[i] == "--workshop") workshop = true;
            else if (args[i] == "--yaw" && i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out float yaw)) Controllers.CameraController.DebugStartYaw = yaw;
            else if (args[i] == "--pitch" && i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out float pitch)) Controllers.CameraController.DebugStartPitch = pitch;
        }
        return new LaunchOptions { ScreenshotPath = shot, ScreenshotFrame = frame, StartInWorkshop = workshop };
#else
        return new LaunchOptions();
#endif
    }
}
