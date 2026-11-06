using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Content;

/// <summary>A room exported from Blender overrides the floor, station positions and adds obstacles.</summary>
public class RoomLayoutTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "santashift-room-" + Guid.NewGuid().ToString("N"));

    public RoomLayoutTests()
    {
        Directory.CreateDirectory(_dir);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "data"), "*.json"))
            if (Path.GetFileName(file) != ContentLoader.RoomLayoutFile)
                File.Copy(file, Path.Combine(_dir, Path.GetFileName(file)));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void WriteLayout(string json) => File.WriteAllText(Path.Combine(_dir, ContentLoader.RoomLayoutFile), json);

    [Fact]
    public void Without_a_layout_file_the_json_positions_are_used()
    {
        var content = ContentLoader.LoadFromDirectory(_dir);
        Assert.False(content.HasRoomLayout);
        Assert.Empty(content.Colliders);
        Assert.Equal(new Point2(300, 285), content.Stations.Single(s => s.Id == "bench").Interact);
    }

    [Fact]
    public void Layout_metres_are_converted_to_pixels_around_the_world_origin()
    {
        WriteLayout("""
            {
              "floor": { "minX": -10, "minZ": -4, "maxX": 10, "maxZ": 4 },
              "playerStart": { "x": 0, "z": 3 },
              "colliders": [ { "name": "tree", "x": 8, "z": -3, "w": 1, "d": 1 } ],
              "stations": { "bench": { "x": -5, "z": 0, "w": 3, "d": 1, "interactX": -5, "interactZ": 1, "yaw": 90 } }
            }
            """);

        var content = ContentLoader.LoadFromDirectory(_dir);
        var bench = content.Stations.Single(s => s.Id == "bench");

        Assert.True(content.HasRoomLayout);
        Assert.Equal(80, content.Floor.Left);    // 480 - 10 * 40
        Assert.Equal(140, content.Floor.Top);    // 300 - 4 * 40
        Assert.Equal(880, content.Floor.Right);
        Assert.Equal(460, content.Floor.Bottom);
        Assert.Equal(new Point2(480, 420), content.PlayerStart);
        Assert.Equal(new Box2(280, 300, 120, 40), bench.Body);
        Assert.Equal(new Point2(280, 340), bench.Interact);
        Assert.Equal(90, bench.Yaw);
        Assert.Equal(new Box2(800, 180, 40, 40), Assert.Single(content.Colliders));

        // Stations the layout doesn't mention keep their stations.json positions.
        Assert.Equal(new Point2(610, 285), content.Stations.Single(s => s.Id == "ribbon").Interact);
    }

    [Fact]
    public void Obstacles_from_the_room_block_the_elf()
    {
        WriteLayout("""{ "colliders": [ { "name": "crate", "x": 0, "z": 2, "w": 2, "d": 2 } ] }""");
        var sim = new Simulation(ContentLoader.LoadFromDirectory(_dir), GameMode.Standard);
        var crate = sim.Content.Colliders.Single();
        var motion = new PlayerMotion(sim.Content);

        sim.PlacePlayer(new Point2(480, 450)); // just below the crate (crate spans y 340..420)
        for (int i = 0; i < 120; i++) sim.Step(new PlayerCommand(0, -1, false));

        Assert.False(motion.ColliderAt(sim.State.PlayerPosition).Overlaps(crate));
        Assert.True(sim.State.PlayerPosition.Y > crate.Bottom);
    }

    [Fact]
    public void Props_are_loaded_with_pixel_positions_and_default_scale()
    {
        WriteLayout("""
            { "props": [
                { "model": "prop_tree", "x": 10, "z": -3, "yaw": 0 },
                { "model": "prop_window", "x": -4.5, "z": -4.5, "yaw": 180, "scale": 1.5 },
                { "model": "", "x": 0, "z": 0 }
            ] }
            """);

        var props = ContentLoader.LoadFromDirectory(_dir).Props;

        Assert.Equal(2, props.Count); // the nameless one is skipped
        Assert.Equal(new Prop("prop_tree", new Point2(880, 180), 0, 1), props[0]);
        Assert.Equal(new Prop("prop_window", new Point2(300, 120), 180, 1.5), props[1]);
    }

    [Fact]
    public void Shipped_room_layout_places_every_prop_model_that_exists()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        Assert.True(content.HasRoomLayout);
        Assert.NotEmpty(content.Props);
        Assert.Contains(content.Props, p => p.Model == "prop_tree");
        Assert.Empty(content.Warnings);
    }

    [Fact]
    public void Placing_an_unknown_station_warns_instead_of_failing()
    {
        WriteLayout("""{ "stations": { "hot_cocoa": { "x": 0, "z": 0, "w": 1, "d": 1, "interactX": 0, "interactZ": 1 } } }""");
        var content = ContentLoader.LoadFromDirectory(_dir);
        Assert.Contains(content.Warnings, w => w.Contains("hot_cocoa"));
    }

    [Fact]
    public void An_interaction_point_inside_an_obstacle_is_reported()
    {
        WriteLayout("""{ "colliders": [ { "name": "blocker", "x": -4.5, "z": -0.375, "w": 1, "d": 1 } ] }"""); // covers bench interact (300, 285)
        var content = ContentLoader.LoadFromDirectory(_dir);
        Assert.Contains(content.Warnings, w => w.Contains("bench") && w.Contains("obstacle"));
    }
}
