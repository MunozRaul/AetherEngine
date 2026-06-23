using Aether.Core;
using System.Numerics;

namespace Aether.Ray.Shapes;

/// <summary>
/// IHittable — a horizontal plane at Y = 0
/// </summary>
public class InfinitePlane : IHittable
{
    public bool Hit(Core.Ray ray, float tMin, float tMax, out HitRecord record)
    {
        record = default;
        // Plane equation: dot(normal, point) = 0 for Y=0 plane, normal = (0,1,0)
        float denom = ray.Direction.Y;
        if (MathF.Abs(denom) < 1e-6f) return false;

        float t = -ray.Origin.Y / denom;
        if (t < tMin || t > tMax) return false;

        record = new HitRecord
        {
            T = t,
            Point = ray.At(t),
            Normal = Vector3.UnitY,
            FrontFace = ray.Direction.Y < 0
        };
        return true;
    }
}
