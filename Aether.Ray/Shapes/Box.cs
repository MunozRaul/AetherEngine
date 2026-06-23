using Aether.Core;
using System.Numerics;

namespace Aether.Ray.Shapes;

/// <summary>
/// IHittable — axis-aligned bounding box (AABB)
/// Slab method: intersect ray against 3 pairs of parallel planes
/// </summary>
public class Box : IHittable
{
    public Vector3 Min { get; init; }
    public Vector3 Max { get; init; }

    public bool Hit(Core.Ray ray, float tMin, float tMax, out HitRecord record)
    {
        record = default;
        float tEnter = tMin, tExit = tMax;
        Vector3 normal = Vector3.Zero;

        for (int i = 0; i < 3; i++)
        {
            float origin = i == 0 ? ray.Origin.X : i == 1 ? ray.Origin.Y : ray.Origin.Z;
            float dir = i == 0 ? ray.Direction.X : i == 1 ? ray.Direction.Y : ray.Direction.Z;
            float bMin = i == 0 ? Min.X : i == 1 ? Min.Y : Min.Z;
            float bMax = i == 0 ? Max.X : i == 1 ? Max.Y : Max.Z;

            float invD = 1f / dir;
            float t0 = (bMin - origin) * invD;
            float t1 = (bMax - origin) * invD;
            if (invD < 0) (t0, t1) = (t1, t0);

            if (t0 > tEnter) { tEnter = t0; normal = GetNormal(i, dir < 0); }
            if (t1 < tExit) tExit = t1;
            if (tEnter > tExit) return false;
        }

        if (tEnter < tMin || tEnter > tMax) return false;

        record = new HitRecord { T = tEnter, Point = ray.At(tEnter), Normal = normal, FrontFace = true };
        return true;
    }

    private static Vector3 GetNormal(int axis, bool negative)
    {
        float sign = negative ? -1f : 1f;
        return axis == 0 ? new Vector3(sign, 0, 0)
             : axis == 1 ? new Vector3(0, sign, 0)
             : new Vector3(0, 0, sign);
    }
}
