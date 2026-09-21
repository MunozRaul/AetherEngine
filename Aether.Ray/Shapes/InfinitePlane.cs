using Aether.Core;
using System.Numerics;

namespace Aether.Ray.Shapes;

/// <summary>
/// IHittable — an infinite plane, defaulting to horizontal at Y = 0.
/// Expressed generally as dot(Normal, point) = Distance so the same two numbers
/// (Normal + Distance) can be handed to the GPU raytracer's plane test unchanged.
/// </summary>
public class InfinitePlane : IHittable
{
    public Vector3 Normal { get; init; } = Vector3.UnitY;
    public float Distance { get; init; } = 0f;

    public bool Hit(Core.Ray ray, float tMin, float tMax, out HitRecord record)
    {
        record = default;
        // Plane equation: dot(Normal, point) = Distance
        float denom = Vector3.Dot(Normal, ray.Direction);
        if (MathF.Abs(denom) < 1e-6f) return false;

        float t = (Distance - Vector3.Dot(Normal, ray.Origin)) / denom;
        if (t < tMin || t > tMax) return false;

        record = new HitRecord
        {
            T = t,
            Point = ray.At(t),
            Normal = Normal,
            FrontFace = denom < 0
        };
        return true;
    }
}
