using System.Numerics;

namespace Aether.Core;

/// <summary>
/// origin + direction
/// </summary>
public readonly struct Ray
{
    public Vector3 Origin { get; init; }
    public Vector3 Direction { get; init; }

    public Vector3 At(float t) => Origin + t * Direction;
}