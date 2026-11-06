using System.Text.Json;
using SantaShift.Core.Models;

namespace SantaShift.Core.Content;

/// <summary>
/// Reads the JSON files in Content/data and checks them.
/// Fatal problems (missing toys, broken layout) throw <see cref="ContentException"/>.
/// Small problems (a letter for an unknown toy) are skipped and reported in <see cref="GameContent.Warnings"/>.
/// </summary>
public static class ContentLoader
{
    private sealed class ToyFile { public List<Toy> Toys { get; init; } = []; }
    private sealed class LetterFile { public int SchemaVersion { get; init; } = 1; public List<ChildLetter> Letters { get; init; } = []; }
    private sealed class StationFile { public List<Station> Stations { get; init; } = []; }
    private sealed class EventFile { public List<SantaEvent> Events { get; init; } = []; }

    public static GameContent LoadFromDirectory(string dataDirectory)
    {
        var warnings = new List<string>();

        var tuning = ReadOptional<Tuning>(dataDirectory, "tuning.json", warnings) ?? new Tuning();
        var toys = ReadRequired<ToyFile>(dataDirectory, "toys.json").Toys;
        var stations = ReadRequired<StationFile>(dataDirectory, "stations.json").Stations;
        var events = ReadOptional<EventFile>(dataDirectory, "events.json", warnings)?.Events ?? [];
        var dialogue = ReadOptional<DialogueBook>(dataDirectory, "dialogue.json", warnings) ?? new DialogueBook();
        var letters = ReadOptional<LetterFile>(dataDirectory, "letters.json", warnings)?.Letters ?? [];

        RoomLayout? room = null;
        if (File.Exists(Path.Combine(dataDirectory, RoomLayoutFile)))
            room = ReadOptional<RoomLayout>(dataDirectory, RoomLayoutFile, warnings);

        return Build(tuning, toys, stations, events, dialogue, letters, warnings, room);
    }

    public const string RoomLayoutFile = "room_layout.json";

    /// <summary>Validates content that is already in memory. Also used by tests.</summary>
    public static GameContent Build(
        Tuning tuning, List<Toy> toys, List<Station> stations, List<SantaEvent> events,
        DialogueBook dialogue, List<ChildLetter> letters, List<string>? warnings = null, RoomLayout? room = null)
    {
        warnings ??= [];
        var floor = tuning.Floor;
        var colliders = new List<Box2>();
        if (room != null) (floor, stations, colliders) = ApplyRoom(tuning, room, stations, warnings);

        // Toys
        if (toys.Count == 0) throw new ContentException("toys.json must define at least one toy.");
        RequireUniqueIds(toys.Select(t => t.Id), "toy");
        var toyIds = toys.Select(t => t.Id).ToHashSet();
        foreach (var id in tuning.ToySequence)
            if (!toyIds.Contains(id)) throw new ContentException($"tuning.json toySequence names unknown toy '{id}'.");
        if (tuning.ToySequence.Length == 0) throw new ContentException("tuning.json toySequence is empty.");

        // Stations
        RequireUniqueIds(stations.Select(s => s.Id), "station");
        foreach (var type in new[] { StationType.Bench, StationType.Shipping })
            if (!stations.Any(s => s.Type == type)) throw new ContentException($"stations.json needs a {type} station.");
        foreach (var s in stations.Where(s => s.Type == StationType.Dispenser))
            if (s.ToyId == null || !toyIds.Contains(s.ToyId))
                throw new ContentException($"Dispenser '{s.Id}' names unknown toy '{s.ToyId}'.");
        foreach (var s in stations)
        {
            if (InsideBox(s.Interact, s.Body))
                warnings.Add($"Station '{s.Id}' has its interaction point inside its body; the player cannot reach it.");
            foreach (var c in colliders.Where(c => InsideBox(s.Interact, c)))
                warnings.Add($"Station '{s.Id}' has its interaction point inside an obstacle; the player cannot reach it.");
        }
        var start = room?.PlayerStart is { } ps ? tuning.MetresToPixels(ps.X, ps.Z) : tuning.PlayerStart;
        if (stations.Select(s => s.Body).Concat(colliders).Any(b => InsideBox(start, b)))
            warnings.Add("The player start position is inside a station or obstacle.");

        // Events
        foreach (var e in events.Where(e => e.Type == SantaEventType.RibbonWindow && (e.EndAt == null || e.EndAt <= e.StartAt)))
            throw new ContentException($"Ribbon event '{e.Id}' needs an endAt after its startAt.");

        // Letters: skip bad ones rather than failing.
        var goodLetters = new List<ChildLetter>();
        foreach (var letter in letters)
        {
            if (!toyIds.Contains(letter.ToyId)) { warnings.Add($"Letter '{letter.Id}' names unknown toy '{letter.ToyId}' and was skipped."); continue; }
            if (string.IsNullOrWhiteSpace(letter.Child)) { warnings.Add($"Letter '{letter.Id}' has no child name and was skipped."); continue; }
            goodLetters.Add(letter);
        }

        return new GameContent
        {
            Tuning = tuning, Toys = toys, Stations = stations, Events = events,
            Dialogue = dialogue, Letters = goodLetters, Warnings = warnings,
            Floor = floor, Colliders = colliders, HasRoomLayout = room != null, PlayerStart = start,
            Props = room?.Props
                .Where(p => !string.IsNullOrWhiteSpace(p.Model))
                .Select(p => new Prop(p.Model, tuning.MetresToPixels(p.X, p.Z), p.Yaw, p.Scale > 0 ? p.Scale : 1))
                .ToList() ?? [],
        };
    }

    /// <summary>Applies a Blender room: its floor, obstacles, and station positions (metres converted to pixels).</summary>
    private static (FloorBounds, List<Station>, List<Box2>) ApplyRoom(Tuning t, RoomLayout room, List<Station> stations, List<string> warnings)
    {
        double ppm = t.PixelsPerMetre;

        var floor = t.Floor;
        if (room.Floor is { } f)
        {
            var min = t.MetresToPixels(f.MinX, f.MinZ);
            var max = t.MetresToPixels(f.MaxX, f.MaxZ);
            floor = new FloorBounds { Left = min.X, Top = min.Y, Right = max.X, Bottom = max.Y };
        }

        var colliders = room.Colliders
            .Select(c => { var p = t.MetresToPixels(c.X, c.Z); return new Box2(p.X, p.Y, c.W * ppm, c.D * ppm); })
            .ToList();

        var ids = stations.Select(s => s.Id).ToHashSet();
        foreach (var id in room.Stations.Keys.Where(id => !ids.Contains(id)))
            warnings.Add($"room_layout.json places station '{id}', which stations.json does not define; it was ignored.");

        var placed = stations.Select(s =>
        {
            if (!room.Stations.TryGetValue(s.Id, out var p)) return s;
            var centre = t.MetresToPixels(p.X, p.Z);
            return new Station
            {
                Id = s.Id, Type = s.Type, ToyId = s.ToyId, Label = s.Label, Model = s.Model,
                Body = new Box2(centre.X, centre.Y, p.W * ppm, p.D * ppm),
                Interact = t.MetresToPixels(p.InteractX, p.InteractZ),
                Yaw = p.Yaw,
            };
        }).ToList();

        return (floor, placed, colliders);
    }

    private static T ReadRequired<T>(string dir, string file)
    {
        string path = Path.Combine(dir, file);
        if (!File.Exists(path)) throw new ContentException($"Required data file is missing: {path}");
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options)
                   ?? throw new ContentException($"{file} is empty.");
        }
        catch (JsonException ex) { throw new ContentException($"{file} is not valid JSON: {ex.Message}", ex); }
    }

    private static T? ReadOptional<T>(string dir, string file, List<string> warnings) where T : class
    {
        string path = Path.Combine(dir, file);
        if (!File.Exists(path)) { warnings.Add($"{file} not found; using defaults."); return null; }
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options); }
        catch (JsonException ex) { warnings.Add($"{file} is not valid JSON ({ex.Message}); using defaults."); return null; }
    }

    private static void RequireUniqueIds(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ContentException($"A {kind} has no id.");
            if (!seen.Add(id)) throw new ContentException($"Duplicate {kind} id '{id}'.");
        }
    }

    private static bool InsideBox(Point2 p, Box2 b) => p.X > b.Left && p.X < b.Right && p.Y > b.Top && p.Y < b.Bottom;
}
