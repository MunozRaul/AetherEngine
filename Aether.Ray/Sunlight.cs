using System.Numerics;

namespace Aether.Ray;

public class SunLight
{
    // Direction the light travels (normalised), ex. from upper-right: (-1, -1, -0.5) normalised
    public Vector3 Direction { get; init; }
    public Vector3 Color { get; init; } = Vector3.One;   // white color by default
    public float Intensity { get; init; } = 1.0f;

    // Returns the direction FROM a surface point TOWARD the sun
    public Vector3 ToSun => -Direction;
}
