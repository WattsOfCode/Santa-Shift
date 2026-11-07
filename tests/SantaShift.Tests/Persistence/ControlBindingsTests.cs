using SantaShift.Core.Models;
using SantaShift.Core.Persistence;

namespace SantaShift.Tests.Persistence;

/// <summary>Rebindable keyboard keys and controller buttons, saved with the settings.</summary>
public class ControlBindingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "santashift-binds-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Defaults_bind_every_action_with_no_key_used_twice()
    {
        var d = ControlBindings.Defaults();
        Assert.All(ControlBindings.Actions, a => Assert.NotEqual("", d.KeyAt(a, 0)));
        Assert.All(ControlBindings.Actions, a => Assert.NotEqual("", d.PadFor(a)));
        var keys = d.Keys.Values.SelectMany(k => k).Where(k => k != "").ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal(d.Pad.Values.Count(), d.Pad.Values.Distinct().Count());
    }

    [Fact]
    public void Binding_a_key_takes_it_off_the_action_that_had_it()
    {
        var b = ControlBindings.Defaults();
        b.SetKey(ControlBindings.Interact, 0, "W"); // W was move up
        Assert.Equal("W", b.KeyAt(ControlBindings.Interact, 0));
        Assert.Equal("", b.KeyAt(ControlBindings.MoveUp, 0));
        Assert.Equal("Up", b.KeyAt(ControlBindings.MoveUp, 1)); // the other slot is untouched
    }

    [Fact]
    public void Binding_a_button_takes_it_off_the_action_that_had_it()
    {
        var b = ControlBindings.Defaults();
        b.SetPad(ControlBindings.Part1, "RightFaceDown"); // A was interact
        Assert.Equal("RightFaceDown", b.PadFor(ControlBindings.Part1));
        Assert.Equal("", b.PadFor(ControlBindings.Interact));
    }

    [Fact]
    public void Unknown_actions_and_slots_are_ignored()
    {
        var b = ControlBindings.Defaults();
        b.SetKey("Teleport", 0, "T");
        b.SetKey(ControlBindings.Interact, 5, "T");
        Assert.DoesNotContain(b.Keys.Values.SelectMany(k => k), k => k == "T");
    }

    [Fact]
    public void A_broken_file_is_repaired_on_load()
    {
        var broken = new ControlBindings
        {
            Keys = new() { [ControlBindings.Interact] = ["Q"], [ControlBindings.MoveUp] = ["Q", "Up", "Extra"], ["Teleport"] = ["T"] },
            Pad = new() { [ControlBindings.Interact] = "RightFaceDown", [ControlBindings.Part1] = "RightFaceDown" },
        }.Normalized();

        Assert.All(ControlBindings.Actions, a => Assert.Equal(ControlBindings.KeySlots, broken.Keys[a].Length));
        Assert.False(broken.Keys.ContainsKey("Teleport"));
        Assert.Equal("Q", broken.KeyAt(ControlBindings.MoveUp, 0));      // first in action order keeps it
        Assert.Equal("", broken.KeyAt(ControlBindings.Interact, 0));     // the duplicate is dropped
        Assert.Equal("S", broken.KeyAt(ControlBindings.MoveDown, 0));    // missing actions get their defaults
        Assert.Equal("RightFaceDown", broken.PadFor(ControlBindings.Interact));
        Assert.Equal("", broken.PadFor(ControlBindings.Part1));
    }

    [Fact]
    public void Bindings_and_sensitivity_survive_a_save_and_load()
    {
        var data = new SaveData(new JsonFileStore(_dir));
        var s = new Settings { MouseSensitivity = 1.5f, StickSensitivity = 0.5f };
        s.Controls.SetKey(ControlBindings.Interact, 0, "F");
        s.Controls.SetPad(ControlBindings.Part3, "LeftTrigger1");
        data.SaveSettings(s);

        var loaded = data.LoadSettings();
        Assert.Equal("F", loaded.Controls.KeyAt(ControlBindings.Interact, 0));
        Assert.Equal("LeftTrigger1", loaded.Controls.PadFor(ControlBindings.Part3));
        Assert.Equal(1.5f, loaded.MouseSensitivity);
        Assert.Equal(0.5f, loaded.StickSensitivity);
    }

    [Fact]
    public void Out_of_range_sensitivity_is_clamped()
    {
        var s = new Settings { MouseSensitivity = 40f, StickSensitivity = float.NaN, Controls = null! }.Clamp();
        Assert.Equal(3f, s.MouseSensitivity);
        Assert.Equal(1f, s.StickSensitivity);
        Assert.Equal("E", s.Controls.KeyAt(ControlBindings.Interact, 0));
    }

    [Theory]
    [InlineData(37, 120)]   // not one of the choices: back to the default
    [InlineData(144, 144)]
    [InlineData(Settings.MatchDisplay, Settings.MatchDisplay)]
    [InlineData(Settings.Unlimited, Settings.Unlimited)]
    public void Frame_rate_must_be_one_of_the_choices(int saved, int expected)
    {
        Assert.Equal(120, new Settings().FrameRate);
        Assert.Equal(expected, new Settings { FrameRate = saved }.Clamp().FrameRate);
    }
}
