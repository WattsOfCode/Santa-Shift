using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Persistence;
using SantaShift.Game.Controllers;
using SantaShift.Game.Services;
using SantaShift.Game.Views;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.App;

/// <summary>Options for automated checks (debug builds only).</summary>
public sealed class LaunchOptions
{
    public string? ScreenshotPath { get; init; }
    public int ScreenshotFrame { get; init; } = 90;
    public bool StartInWorkshop { get; init; }

    /// <summary>Start on a new run's naughty and nice list.</summary>
    public bool StartOnList { get; init; }

    /// <summary>Start on the perk pick after a round passed well over quota (debug).</summary>
    public bool StartOnPerks { get; init; }
}

/// <summary>
/// Opens the window and runs the main loop: a fixed 60 Hz simulation, with the 3D scene drawn
/// at a fixed internal resolution and scaled to the window, then sharp UI on top.
/// </summary>
public sealed class GameApp(GameContent content, LaunchOptions options)
{
    private const double StepSeconds = 1.0 / 60.0;
    private const double MaxCatchUpSeconds = 0.25; // after a long stall, don't burn the player's time

    /// <summary>
    /// The 3D picture's size: the game area of the window (16:9) times the player's resolution setting, within limits.
    /// With render.json > followWindow off, the fixed internalWidth x internalHeight instead (for very weak devices).
    /// </summary>
    private static (int W, int H) TargetSize(RenderSettings render, Core.Models.Settings settings)
    {
        if (!render.FollowWindow) return (render.InternalWidth, render.InternalHeight);
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        if (sw <= 0 || sh <= 0) return (render.InternalWidth, render.InternalHeight); // minimised
        float scale = MathF.Min(sw / UiCanvas.Width, sh / UiCanvas.Height) * settings.RenderScale;
        int w = Math.Clamp((int)(UiCanvas.Width * scale), 480, render.MaxRenderWidth);
        return (w, w * 9 / 16);
    }

    private static IScreen DebugPerkPick(GameServices services, Core.Models.GameMode mode)
    {
        var run = WorkshopController.NewRunProgress(services, mode);
        int quota = services.Content.Tuning.QuotaFor(mode, 1);
        run.CompleteRound(new Core.Models.RunState { Delivered = quota + 3, Score = 1500 }, quota);
        run.AddPerk(services.Content.Perks[0]);
        return new PerkPickController(services, run);
    }

    public void Run()
    {
        string baseDir = AppContext.BaseDirectory;
        string contentDir = Path.Combine(baseDir, "Content");
        var render = RenderSettings.Load(Path.Combine(contentDir, "data"));

        var flags = ConfigFlags.ResizableWindow;
        if (render.Vsync && !FrameProfiler.Enabled) flags |= ConfigFlags.VSyncHint;
        if (render.Msaa) flags |= ConfigFlags.Msaa4xHint;
        Raylib.SetConfigFlags(flags);
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(render.WindowWidth, render.WindowHeight, "Santa Shift");
        Raylib.SetWindowMinSize(640, 360);
        Raylib.SetTargetFPS(FrameProfiler.Enabled ? 0 : render.TargetFps); // profiling: run flat out to see the real cost
        Raylib.SetExitKey(KeyboardKey.Null); // Escape pauses; it must not close the game

        var assets = new AssetLibrary(Path.Combine(contentDir, "models"));
        var lighting = new Lighting(render.Lighting, content.Lights);
        assets.Preload(content, lighting.Apply);
        var audio = RaylibAudioService.Create(Path.Combine(contentDir, "audio"));

        var save = new SaveData(new JsonFileStore(JsonFileStore.DefaultDirectory));
        var settings = save.LoadSettings();
        var input = new PlayerInputService(settings.Controls);
        input.UseSettings(settings);
        var best = save.LoadBest();
        var progress = save.LoadProgress();
        if (!save.SaveSettings(settings)) save = new SaveData(new MemoryStore()); // can't write: keep playing without saves
        audio.ApplySettings(settings);

        var services = new GameServices
        {
            Content = content,
            Render = render,
            Input = input,
            Audio = audio,
            Assets = assets,
            Save = save,
            Scenes = new SceneManager(input),
            World = new WorldSpace(render, content.Tuning),
            Settings = settings,
            Best = best,
            Progress = progress,
            PauseOnFocusLoss = options.ScreenshotPath == null,
        };

        var target = Raylib.LoadRenderTexture(render.InternalWidth, render.InternalHeight);
        Raylib.SetTextureFilter(target.Texture, TextureFilter.Bilinear);
        var font = Raylib.GetFontDefault();

        services.Scenes.Switch(
            options.StartInWorkshop ? new WorkshopController(services, WorkshopController.NewRunProgress(services, settings.PreferredMode))
            : options.StartOnList ? WorkshopController.NewRun(services, settings.PreferredMode)
            : options.StartOnPerks ? DebugPerkPick(services, settings.PreferredMode)
            : new TitleController(services));

        double accumulator = 0;
        int frame = 0;
        while (!Raylib.WindowShouldClose() && !services.Scenes.QuitRequested)
        {
            services.Scenes.ApplyPendingSwitch();
            var screen = services.Scenes.Current!;

            input.Poll();
            accumulator += Math.Min(Raylib.GetFrameTime(), MaxCatchUpSeconds);
            using (FrameProfiler.Time("update"))
            {
                while (accumulator >= StepSeconds)
                {
                    screen.FixedUpdate();
                    accumulator -= StepSeconds;
                }
                screen.FrameUpdate();
            }
            audio.UpdateMusic(screen.Music, Raylib.GetFrameTime());

            WorldEffects.ReducedMotion = services.Settings.ReducedMotion;
            // The 3D picture follows the window's size (times the resolution setting), so edges stay crisp instead of
            // being stretched up from a small picture.
            var (tw, th) = TargetSize(render, services.Settings);
            if (tw != target.Texture.Width || th != target.Texture.Height)
            {
                Raylib.UnloadRenderTexture(target);
                target = Raylib.LoadRenderTexture(tw, th);
                Raylib.SetTextureFilter(target.Texture, TextureFilter.Bilinear);
                services.World.Resize(tw, th);
            }
            lighting.Update(Raylib.GetTime(), services.World.Camera.Position, services.World.Camera.Target, services.Settings.ReducedMotion);
            using (FrameProfiler.Time("world"))
            {
                Raylib.BeginTextureMode(target);
                screen.DrawWorld();
                Raylib.EndTextureMode();
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            var ui = new UiCanvas(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), font);
            var source = new Rectangle(0, 0, target.Texture.Width, -target.Texture.Height); // render textures are upside down
            lighting.SetPostFrame(target.Texture.Width, target.Texture.Height, services.Settings.SmoothEdges);
            if (lighting.UsePost) Raylib.BeginShaderMode(lighting.Post); // edge smoothing, vignette and colour grade
            Raylib.DrawTexturePro(target.Texture, source, ui.ScreenArea, Vector2.Zero, 0, Color.White);
            if (lighting.UsePost) Raylib.EndShaderMode();
            using (FrameProfiler.Time("ui")) screen.DrawUi(ui);
            using (FrameProfiler.Time("present")) Raylib.EndDrawing();
            FrameProfiler.EndFrame();

            frame++;
            if (options.ScreenshotPath != null && frame == options.ScreenshotFrame)
            {
                var image = Raylib.LoadImageFromScreen();
                Raylib.ExportImage(image, options.ScreenshotPath);
                Raylib.UnloadImage(image);
                break;
            }
        }

        services.Scenes.Current?.Exit();
        Raylib.UnloadRenderTexture(target);
        assets.Dispose();
        lighting.Dispose();
        audio.Dispose();
        Raylib.CloseWindow();
    }
}
