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
}

/// <summary>
/// Opens the window and runs the main loop: a fixed 60 Hz simulation, with the 3D scene drawn
/// at a fixed internal resolution and scaled to the window, then sharp UI on top.
/// </summary>
public sealed class GameApp(GameContent content, LaunchOptions options)
{
    private const double StepSeconds = 1.0 / 60.0;
    private const double MaxCatchUpSeconds = 0.25; // after a long stall, don't burn the player's time

    public void Run()
    {
        string baseDir = AppContext.BaseDirectory;
        string contentDir = Path.Combine(baseDir, "Content");
        var render = RenderSettings.Load(Path.Combine(contentDir, "data"));

        var flags = ConfigFlags.ResizableWindow;
        if (render.Vsync) flags |= ConfigFlags.VSyncHint;
        if (render.Msaa) flags |= ConfigFlags.Msaa4xHint;
        Raylib.SetConfigFlags(flags);
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(render.WindowWidth, render.WindowHeight, "Santa Shift");
        Raylib.SetWindowMinSize(640, 360);
        Raylib.SetTargetFPS(render.TargetFps);
        Raylib.SetExitKey(KeyboardKey.Null); // Escape pauses; it must not close the game

        var input = new KeyboardInputService();
        var assets = new AssetLibrary(Path.Combine(contentDir, "models"));
        var lighting = new Lighting(render.Lighting, content.Lights);
        assets.Preload(content, lighting.Apply);
        var audio = RaylibAudioService.Create(Path.Combine(contentDir, "audio"));

        var save = new SaveData(new JsonFileStore(JsonFileStore.DefaultDirectory));
        var settings = save.LoadSettings();
        var best = save.LoadBest();
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
            PauseOnFocusLoss = options.ScreenshotPath == null,
        };

        var target = Raylib.LoadRenderTexture(render.InternalWidth, render.InternalHeight);
        Raylib.SetTextureFilter(target.Texture, TextureFilter.Bilinear);
        var font = Raylib.GetFontDefault();

        services.Scenes.Switch(options.StartInWorkshop
            ? WorkshopController.NewRun(services, settings.PreferredMode)
            : new TitleController(services));

        double accumulator = 0;
        int frame = 0;
        while (!Raylib.WindowShouldClose() && !services.Scenes.QuitRequested)
        {
            services.Scenes.ApplyPendingSwitch();
            var screen = services.Scenes.Current!;

            input.Poll();
            accumulator += Math.Min(Raylib.GetFrameTime(), MaxCatchUpSeconds);
            while (accumulator >= StepSeconds)
            {
                screen.FixedUpdate();
                accumulator -= StepSeconds;
            }
            screen.FrameUpdate();

            lighting.Update(Raylib.GetTime(), services.World.Camera.Position, services.World.Camera.Target, services.Settings.ReducedMotion);
            Raylib.BeginTextureMode(target);
            screen.DrawWorld();
            Raylib.EndTextureMode();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            var ui = new UiCanvas(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), font);
            var source = new Rectangle(0, 0, target.Texture.Width, -target.Texture.Height); // render textures are upside down
            if (lighting.UsePost) Raylib.BeginShaderMode(lighting.Post); // vignette and colour grade
            Raylib.DrawTexturePro(target.Texture, source, ui.ScreenArea, Vector2.Zero, 0, Color.White);
            if (lighting.UsePost) Raylib.EndShaderMode();
            screen.DrawUi(ui);
            Raylib.EndDrawing();

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
