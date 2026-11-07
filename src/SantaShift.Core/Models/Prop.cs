namespace SantaShift.Core.Models;

/// <summary>
/// A piece of furniture or decoration placed in the room (tree, fireplace, window...), drawn from its own model file.
/// Solid props also appear in <see cref="Content.GameContent.Colliders"/>; this record is only about drawing.
/// </summary>
public sealed record Prop(string Model, Point2 Position, double Yaw, double Scale);

/// <summary>
/// A workshop elf working at its own bench (scenery: it never affects the rules). Placed in room.blend as an
/// "npc_&lt;n&gt;" marker; the game draws it with the player's elf model, hammering away.
/// </summary>
/// <param name="Yaw">Facing in degrees (0 = facing +Z, same as the player).</param>
public sealed record Npc(Point2 Position, double Yaw);
