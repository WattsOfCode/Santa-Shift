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
        // Debug-only automation: --screenshot <file.png> [--frame N] [--workshop] [--yaw deg] [--pitch deg] [--profile]
        //   [--list] [--carry toy[:assembled|:botched][:ribbon] | coal | supply] [--assemble partsPlaced] [--swing-every frames]
        string? shot = null;
        int frame = 90;
        bool workshop = false, list = false, perks = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--screenshot" && i + 1 < args.Length) shot = Path.GetFullPath(args[++i]);
            else if (args[i] == "--frame" && i + 1 < args.Length && int.TryParse(args[++i], out int f)) frame = f;
            else if (args[i] == "--workshop") workshop = true;
            else if (args[i] == "--list") list = true;
            else if (args[i] == "--perks") perks = true;
            else if (args[i] == "--profile") FrameProfiler.Enabled = true;
            else if (args[i] == "--spool" && i + 1 < args.Length && int.TryParse(args[++i], out int spool)) Controllers.WorkshopController.DebugSpool = spool;
            else if (args[i] == "--guide") Controllers.WorkshopController.DebugGuide = true;
            else if (args[i] == "--unlocks") Controllers.TitleController.DebugUnlocks = true;
            else if (args[i] == "--hat" && i + 1 < args.Length) Controllers.WorkshopController.DebugHat = args[++i];
            else if (args[i] == "--pause") Controllers.WorkshopController.DebugPause = true;
            else if (args[i] == "--settings" && i + 1 < args.Length && int.TryParse(args[++i], out int tab)) Controllers.TitleController.DebugSettingsTab = tab;
            else if (args[i] == "--naughty" && i + 1 < args.Length) Controllers.WorkshopController.DebugNaughty = args[++i].Split(',');
            else if (args[i] == "--at" && i + 1 < args.Length) Controllers.WorkshopController.DebugAt = args[++i];
            else if (args[i] == "--use") Controllers.WorkshopController.DebugUse = true;
            else if (args[i] == "--carry" && i + 1 < args.Length)
            {
                var parts = args[++i].Split(':');
                Controllers.WorkshopController.DebugCarry = parts[0] == Core.Models.Item.CoalId ? Core.Models.Item.Coal(0)
                    : parts[0] == Core.Models.Item.SupplyId ? Core.Models.Item.Supply(0) : new Core.Models.Item
                {
                    ToyId = parts[0],
                    Stage = parts.Contains("botched") ? Core.Models.ItemStage.Botched
                        : parts.Contains("assembled") ? Core.Models.ItemStage.Assembled : Core.Models.ItemStage.Kit,
                    HasRibbon = parts.Contains("ribbon"),
                };
            }
            else if (args[i] == "--assemble" && i + 1 < args.Length && int.TryParse(args[++i], out int placed)) Controllers.WorkshopController.DebugAssembleParts = placed;
            else if (args[i] == "--swing-every" && i + 1 < args.Length && int.TryParse(args[++i], out int every)) Controllers.WorkshopController.DebugSwingEvery = every;
            else if (args[i] == "--yaw" && i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out float yaw)) Controllers.CameraController.DebugStartYaw = yaw;
            else if (args[i] == "--pitch" && i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out float pitch)) Controllers.CameraController.DebugStartPitch = pitch;
        }
        return new LaunchOptions { ScreenshotPath = shot, ScreenshotFrame = frame, StartInWorkshop = workshop, StartOnList = list, StartOnPerks = perks };
#else
        return new LaunchOptions();
#endif
    }
}
