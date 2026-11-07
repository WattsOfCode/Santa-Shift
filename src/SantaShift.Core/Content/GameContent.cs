using SantaShift.Core.Models;

namespace SantaShift.Core.Content;

/// <summary>All loaded and validated data for the game. Built by <see cref="ContentLoader"/>.</summary>
public sealed class GameContent
{
    public required Tuning Tuning { get; init; }
    public required IReadOnlyList<Toy> Toys { get; init; }
    public required IReadOnlyList<ChildLetter> Letters { get; init; }
    public required IReadOnlyList<Station> Stations { get; init; }
    public required IReadOnlyList<SantaEvent> Events { get; init; }
    public required DialogueBook Dialogue { get; init; }

    /// <summary>The walkable area: from room_layout.json if present, otherwise tuning.json.</summary>
    public required FloorBounds Floor { get; init; }

    /// <summary>Where the elf starts: from room_layout.json if present, otherwise tuning.json.</summary>
    public required Point2 PlayerStart { get; init; }

    /// <summary>Extra solid obstacles from the Blender room (furniture, decorations).</summary>
    public IReadOnlyList<Box2> Colliders { get; init; } = [];

    /// <summary>Furniture and decorations from the Blender room, each drawn from its own model.</summary>
    public IReadOnlyList<Prop> Props { get; init; } = [];

    /// <summary>Point lights from the Blender room (drawing only).</summary>
    public IReadOnlyList<RoomLight> Lights { get; init; } = [];

    /// <summary>Workshop elves working at their own benches (scenery, from the Blender room).</summary>
    public IReadOnlyList<Npc> Npcs { get; init; } = [];

    /// <summary>True when the layout came from a Blender room export.</summary>
    public bool HasRoomLayout { get; init; }

    /// <summary>True when there is a coal station, so naughty children can be sent coal.</summary>
    public bool HasCoal => Stations.Any(s => s.Type == StationType.Coal);

    /// <summary>With a supply sled in the room, kit bins hold limited stock (see <see cref="Tuning.ResupplyKits"/>).</summary>
    public bool HasSupply => Stations.Any(s => s.Type == StationType.Supply);

    /// <summary>The perks Santa can offer between rounds (perks.json). Empty: rounds follow each other directly.</summary>
    public IReadOnlyList<Perk> Perks { get; init; } = [];

    /// <summary>Things earned across runs (unlocks.json): extra perks, hat colours, a head start.</summary>
    public IReadOnlyList<Unlock> Unlocks { get; init; } = [];

    /// <summary>Non-fatal problems found while loading (for example a letter naming an unknown toy).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public Toy Toy(string id) => Toys.First(t => t.Id == id);
    public Toy? FindToy(string id) => Toys.FirstOrDefault(t => t.Id == id);
}

/// <summary>Content that the game cannot run without is missing or invalid.</summary>
public sealed class ContentException(string message, Exception? inner = null) : Exception(message, inner);
