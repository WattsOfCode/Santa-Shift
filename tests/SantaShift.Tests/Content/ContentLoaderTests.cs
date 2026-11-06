using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Tests.Content;

public class ContentLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "santashift-tests-" + Guid.NewGuid().ToString("N"));
    private static readonly string ShippedData = Path.Combine(AppContext.BaseDirectory, "data");

    public ContentLoaderTests()
    {
        Directory.CreateDirectory(_dir);
        foreach (var file in Directory.GetFiles(ShippedData, "*.json"))
            File.Copy(file, Path.Combine(_dir, Path.GetFileName(file)));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Shipped_data_files_load_cleanly()
    {
        var content = ContentLoader.LoadFromDirectory(ShippedData);

        Assert.Empty(content.Warnings);
        Assert.Equal(3, content.Toys.Count);
        Assert.Equal(7, content.Stations.Count);
        Assert.Equal(4, content.Events.Count);
        Assert.Equal(15, content.Letters.Count);
        Assert.Equal(300, content.Tuning.ShiftSeconds);
        Assert.Equal(StationType.Dispenser, content.Stations[0].Type);
        Assert.Equal(new Point2(180, 180), content.Stations[0].Interact);
        Assert.Equal(SantaEventType.RibbonWindow, content.Events[0].Type);
    }

    [Fact]
    public void Shipped_dialogue_has_every_message_the_rules_use()
    {
        var d = ContentLoader.LoadFromDirectory(ShippedData).Dialogue;
        string[] keys =
        [
            "prompt_collect", "prompt_assemble", "prompt_ribbon", "prompt_ship", "prompt_recycle", "prompt_recycle_confirm",
            "working_assemble", "working_ribbon", "hands_full", "bench_needs_kit", "already_assembled", "ribbon_needs_toy",
            "already_ribboned", "assemble_first", "ribbon_needed", "no_matching_order", "nothing_to_ship", "nothing_to_recycle",
            "assembly_cancelled", "ribbon_cancelled", "order_expired", "delivered", "recycled", "empty_hands", "item_kit",
            "item_ribboned", "review_low", "review_survivor", "review_legend",
            "botched_wrong_part", "botched_too_slow", "botched_scrap_it", "item_botched", "prompt_scrap_botched",
        ];
        Assert.All(keys, k => Assert.True(d.Messages.ContainsKey(k), $"dialogue.json is missing message '{k}'"));
        Assert.All(new[] { "quota_met", "review_pass", "review_miss" }, c => Assert.NotNull(d.Cue(c)));
    }

    [Fact]
    public void A_letter_for_an_unknown_toy_is_skipped_with_a_warning()
    {
        File.WriteAllText(Path.Combine(_dir, "letters.json"), """
            { "letters": [
              { "id": "ok",  "child": "Ann", "age": 5, "toyId": "train", "line": "hi" },
              { "id": "bad", "child": "Bob", "age": 6, "toyId": "spaceship", "line": "hi" }
            ] }
            """);

        var content = ContentLoader.LoadFromDirectory(_dir);

        Assert.Single(content.Letters);
        Assert.Contains(content.Warnings, w => w.Contains("spaceship"));
    }

    [Fact]
    public void Broken_letters_file_does_not_stop_the_game()
    {
        File.WriteAllText(Path.Combine(_dir, "letters.json"), "{ this is not json");

        var content = ContentLoader.LoadFromDirectory(_dir);

        Assert.Empty(content.Letters);
        Assert.Contains(content.Warnings, w => w.Contains("letters.json"));
    }

    [Fact]
    public void Missing_toys_file_is_fatal_with_a_clear_message()
    {
        File.Delete(Path.Combine(_dir, "toys.json"));
        var ex = Assert.Throws<ContentException>(() => ContentLoader.LoadFromDirectory(_dir));
        Assert.Contains("toys.json", ex.Message);
    }

    [Fact]
    public void Duplicate_toy_ids_are_fatal()
    {
        File.WriteAllText(Path.Combine(_dir, "toys.json"), """{ "toys": [ { "id": "train" }, { "id": "train" }, { "id": "bear" }, { "id": "robot" } ] }""");
        Assert.Throws<ContentException>(() => ContentLoader.LoadFromDirectory(_dir));
    }

    [Fact]
    public void Dispenser_for_an_unknown_toy_is_fatal()
    {
        string stations = File.ReadAllText(Path.Combine(_dir, "stations.json")).Replace("\"toyId\": \"robot\"", "\"toyId\": \"yoyo\"");
        File.WriteAllText(Path.Combine(_dir, "stations.json"), stations);
        Assert.Throws<ContentException>(() => ContentLoader.LoadFromDirectory(_dir));
    }

    [Fact]
    public void Missing_tuning_file_falls_back_to_gdd_defaults()
    {
        File.Delete(Path.Combine(_dir, "tuning.json"));
        var content = ContentLoader.LoadFromDirectory(_dir);
        Assert.Equal(55, content.Tuning.DeadlineSeconds);
        Assert.Contains(content.Warnings, w => w.Contains("tuning.json"));
    }
}
