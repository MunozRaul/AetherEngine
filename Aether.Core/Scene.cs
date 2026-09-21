namespace Aether.Core;

/// <summary>
/// list of SceneObjects
/// </summary>
public class Scene
{
    public List<SceneObject> Objects { get; } = new();

    public bool HitAnything(Ray ray, float tMin, float tMax, out HitRecord closest)
    {
        closest = default;
        bool hitAny = false;
        float tClosest = tMax;

        foreach (SceneObject obj in Objects)
        {
            if (obj.Hittable.Hit(ray, tMin, tClosest, out HitRecord rec))
            {
                hitAny = true;
                tClosest = rec.T;
                closest = rec;
            }
        }
        return hitAny;
    }
}
