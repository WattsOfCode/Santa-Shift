namespace SantaShift.Core.Models;

/// <summary>
/// A piece of furniture or decoration placed in the room (tree, fireplace, window...), drawn from its own model file.
/// Solid props also appear in <see cref="Content.GameContent.Colliders"/>; this record is only about drawing.
/// </summary>
public sealed record Prop(string Model, Point2 Position, double Yaw, double Scale);
