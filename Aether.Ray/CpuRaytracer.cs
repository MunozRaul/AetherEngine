using Aether.Core;
using System.Numerics;

namespace Aether.Ray;

public class CpuRaytracer
{
    private readonly RaytracerSettings _settings;
    private readonly SunLight _sun;

    public CpuRaytracer(RaytracerSettings settings, SunLight sun)
    {
        _settings = settings;
        _sun = sun;
    }

    // Expose the direction towards the sun to the outside world since sun shall be readonly
    public Vector3 SunDirection => _sun.ToSun;

    // Renders the scene into a flat RGBA byte array (width * height * 4 bytes)
    public byte[] Render(Scene scene, Camera camera)
    {
        int w = _settings.Width, h = _settings.Height;
        byte[] pixels = new byte[w * h * 4];

        // Projection constants
        float aspectRatio = (float)w / h;
        float fovScale = MathF.Tan(MathF.PI / 180f * _settings.FovDegrees * 0.5f);

        Parallel.For(0, h, row =>
        {
            for (int col = 0; col < w; col++)
            {
                // Map pixel to [-1,1] NDC
                float u = (2f * (col + 0.5f) / w - 1f) * aspectRatio * fovScale;
                float v = (1f - 2f * (row + 0.5f) / h) * fovScale;

                // Build ray in world space from camera orientation
                Vector3 dir = Vector3.Normalize(
                    camera.Forward + u * camera.Right + v * Vector3.Cross(camera.Right, camera.Forward)
                );
                Core.Ray ray = new Core.Ray { Origin = camera.Position, Direction = dir };

                Vector3 color = TraceRay(ray, scene, 0);

                int idx = (row * w + col) * 4;
                pixels[idx + 0] = ToByte(color.X);
                pixels[idx + 1] = ToByte(color.Y);
                pixels[idx + 2] = ToByte(color.Z);
                pixels[idx + 3] = 255;
            }
        });

        return pixels;
    }

    private Vector3 TraceRay(Core.Ray ray, Scene scene, int depth)
    {
        if (depth > _settings.MaxBounces) return Vector3.Zero;

        if (!scene.HitAnything(ray, 0.001f, float.MaxValue, out HitRecord hit))
            return SkyColor(ray);   // background

        // Shadow ray toward sun
        Core.Ray shadowRay = new Core.Ray { Origin = hit.Point + hit.Normal * 0.001f, Direction = _sun.ToSun };
        bool inShadow = scene.HitAnything(shadowRay, 0.001f, float.MaxValue, out _);

        // Simple Lambertian diffuse
        float diffuse = inShadow ? 0.05f : MathF.Max(0f, Vector3.Dot(hit.Normal, _sun.ToSun));
        Vector3 albedo = new Vector3(0.8f, 0.8f, 0.8f);   // light gray for everything

        return albedo * _sun.Color * _sun.Intensity * diffuse;
    }

    private static Vector3 SkyColor(Core.Ray ray)
    {
        float t = 0.5f * (Vector3.Normalize(ray.Direction).Y + 1f);
        return Vector3.Lerp(new Vector3(1f, 1f, 1f), new Vector3(0.5f, 0.7f, 1f), t);
    }

    private static byte ToByte(float f) => (byte)(Math.Clamp(f, 0f, 1f) * 255f);
}
