using System.Numerics;

namespace Aether.Core;

/// <summary>
/// result of a ray–object intersection
/// </summary>
public readonly struct HitRecord
{
    public Vector3 Point { get; init; }
    public Vector3 Normal { get; init; }   // always points outward
    public float T { get; init; }   // distance along the ray
    public bool FrontFace { get; init; }
}
