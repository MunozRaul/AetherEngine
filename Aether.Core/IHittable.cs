namespace Aether.Core
{
    /// <summary>
    /// interface for anything a ray can hit
    /// </summary>
    public interface IHittable
    {
        bool Hit(Ray ray, float tMin, float tMax, out HitRecord record);
    }
}
