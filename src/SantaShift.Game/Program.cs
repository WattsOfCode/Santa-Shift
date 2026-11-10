using SantaShift.Core.Content;
using SantaShift.Game.App;

namespace SantaShift.Game;

public static class Program
{
    public static int Main(string[] args)
    {
        string dataDir = Path.Combine(AppContext.BaseDirectory, "Content", "data");

        // Release builds have no console to print to, so a crash leaves its details in a log next to the game.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try { WriteErrorLog(e.ExceptionObject.ToString() ?? "Unknown error"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        };

        GameContent content;
        try
        {
            content = ContentLoader.LoadFromDirectory(dataDir);
        }
        catch (ContentException ex)
        {
            // Bad data should explain itself, not crash with a stack trace.
            WriteErrorLog(ex.Message);
            Console.Error.WriteLine($"Santa Shift could not load its data: {ex.Message}");
            return 1;
        }

        foreach (var warning in content.Warnings) Console.WriteLine($"[content] {warning}");

        new GameApp(content, ParseOptions(args)).Run();
        return 0;
    }

    /// <summary>
    /// The error log goes next to the saves (%AppData%/SantaShift on Windows, ~/.config/SantaShift on a Mac), not next to
    /// the game: the one-file Windows build unpacks itself to a hidden temp folder, where nobody would find it.
    /// </summary>
    public static string ErrorLogPath => Path.Combine(Core.Persistence.JsonFileStore.DefaultDirectory, "santashift-error.log");

    private static void WriteErrorLog(string text)
    {
        Directory.CreateDirectory(Core.Persistence.JsonFileStore.DefaultDirectory);
        File.WriteAllText(ErrorLogPath, text);
    }

    private static LaunchOptions ParseOptions(string[] args)
    {
#if DEBUG
        // Debug-only automation: --screenshot <file.png> [--frame N] [--workshop] [--yaw deg] [--pitch deg] [--profile]
        //   [--list] [--carry toy[:assembled|:botched][:ribbon][:v=variant] | coal | supply]
        //   [--assemble partsPlaced [toy[:variant]]] [--cart partsBuilt] [--swing-every frames]
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
            else if (args[i] == "--profile-capped") FrameProfiler.Enabled = FrameProfiler.KeepFrameRate = true;
            else if (args[i] == "--spool" && i + 1 < args.Length && int.TryParse(args[++i], out int spool)) Controllers.WorkshopController.DebugSpool = spool;
            else if (args[i] == "--guide") Controllers.WorkshopController.DebugGuide = true;
            else if (args[i] == "--unlocks") Controllers.TitleController.DebugUnlocks = true;
            else if (args[i] == "--hat" && i + 1 < args.Length) Controllers.WorkshopController.DebugHat = args[++i];
            else if (args[i] == "--pause") Controllers.WorkshopController.DebugPause = true;
            else if (args[i] == "--settings" && i + 1 < args.Length && int.TryParse(args[++i], out int tab)) Controllers.TitleController.DebugSettingsTab = tab;
            else if (args[i] == "--naughty" && i + 1 < args.Length) Controllers.WorkshopController.DebugNaughty = args[++i].Split(',');
            else if (args[i] == "--at" && i + 1 < args.Length) Controllers.WorkshopController.DebugAt = args[++i];
            else if (args[i] == "--use") Controllers.WorkshopController.DebugUse = true;
            else if (args[i] == "--cover") Controllers.WorkshopController.DebugCover = true;
            else if (args[i] == "--distance" && i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out float distance)) Controllers.CameraController.DebugDistance = distance;
            else if (args[i] == "--round" && i + 1 < args.Length && int.TryParse(args[++i], out int round)) Controllers.WorkshopController.DebugRound = round;
            else if (args[i] == "--skip" && i + 1 < args.Length && double.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture, out double skip)) Controllers.WorkshopController.DebugSkip = skip;
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
                    // toy:assembled:v=rocket carries that variant
                    Variant = parts.FirstOrDefault(p => p.StartsWith("v="))?[2..],
                };
            }
            else if (args[i] == "--assemble" && i + 1 < args.Length && int.TryParse(args[++i], out int placed))
            {
                Controllers.WorkshopController.DebugAssembleParts = placed;
                // --assemble N toy[:variant]: build that toy, following that variant's combination.
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    var which = args[++i].Split(':');
                    Controllers.WorkshopController.DebugAssembleToy = which[0];
                    Controllers.WorkshopController.DebugAssembleVariant = which.Length > 1 ? which[1] : null;
                }
            }
            else if (args[i] == "--cart" && i + 1 < args.Length && int.TryParse(args[++i], out int cartParts)) Controllers.WorkshopController.DebugCartParts = cartParts;
            else if (args[i] == "--cart-load" && i + 1 < args.Length) Controllers.WorkshopController.DebugCartLoad = args[++i].Split(',');
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
