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
        Assert.Equal(4, content.Toys.Count); // train, bear, robot, and the boat from round 5
        Assert.Equal(11, content.Stations.Count); // 7 GDD stations + the coal scuttle, supply sled, punch clock and boat bin
        Assert.True(content.HasCoal);
        Assert.True(content.HasSupply);
        Assert.Equal(4, content.Events.Count);
        Assert.Equal(33, content.Letters.Count); // 15 plain letters, 13 asking for toy variants, 5 for boats
        Assert.Equal(300, content.Tuning.ShiftSeconds);
        Assert.Equal(StationType.Dispenser, content.Stations[0].Type);
        Assert.Equal(new Point2(180, 180), content.Stations[0].Interact);
        Assert.Equal(SantaEventType.RibbonWindow, content.Events[0].Type);
    }

    [Fact]
    public void Shipped_credits_name_the_composer_and_the_copyright_holder()
    {
        var credits = ContentLoader.LoadFromDirectory(ShippedData).Credits;
        var all = credits.Left.Concat(credits.Right).SelectMany(s => s.Lines.Append(s.Heading)).Concat(credits.Footer).ToList();
        Assert.Contains(all, l => l.Contains("blueeyedmonkey"));  // the composer's condition: credit by that name
        Assert.Contains(credits.Footer, l => l.Contains("WattsOfCode") && l.Contains("All rights reserved"));
        Assert.All(credits.Left.Concat(credits.Right).SelectMany(s => s.Lines), l => Assert.True(l.Length <= 40, $"too long for a column: {l}"));
    }

    [Fact]
    public void Every_shipped_font_goes_out_with_its_licence()
    {
        string fonts = Path.Combine(AppContext.BaseDirectory, "fonts");
        var files = Directory.GetFiles(fonts, "*.ttf").Concat(Directory.GetFiles(fonts, "*.otf")).ToList();
        Assert.NotEmpty(files); // the game ships its own font so Windows and macOS look the same
        string licence = File.ReadAllText(Path.Combine(fonts, "OFL.txt"));
        Assert.Contains("SIL Open Font License", licence);
        Assert.Contains("Barlow Project Authors", licence);
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
            "prompt_coal", "item_coal", "coal_not_a_kit", "no_ribbon_for_coal", "coal_no_naughty", "naughty_no_toy", "coal_delivered",
            "naughty_order", "working_shovel", "shovel_cancelled",
            "prompt_supply", "prompt_restock", "bin_empty", "restocked", "supply_goes_to_bin", "item_supply", "bin_stock",
            "bin_stock_empty", "picking_ribbon", "wrong_ribbon_colour", "item_ribboned_colour", "perk_title", "perk_extra",
            "prompt_punch_out", "prompt_punch_out_short", "prompt_punch_confirm", "prompt_punch_confirm_short", "punch_prompt",
            "punched_out_left",
        ];
        Assert.All(keys, k => Assert.True(d.Messages.ContainsKey(k), $"dialogue.json is missing message '{k}'"));
        Assert.All(new[] { "quota_met", "review_pass", "review_miss", "punched_out", "spirit_out" }, c => Assert.NotNull(d.Cue(c)));
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
