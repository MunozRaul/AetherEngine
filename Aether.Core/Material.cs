using System.Numerics;

namespace Aether.Core;

/// <summary>
/// Surface appearance data, how a SceneObject responds to light.
/// Deliberately simple for now (no textures, no BRDF beyond flat Lambertian):
/// this is the minimum needed to give each object its own color on the GPU raytracer.
/// Roughness/Metallic are reserved for a future PBR shading pass and are not yet consumed.
/// </summary>
public class Material
{
    public Vector3 Albedo { get; init; } = new Vector3(0.8f, 0.8f, 0.8f);
    public Vector3 Emissive { get; init; } = Vector3.Zero;
    public float Roughness { get; init; } = 1f;
    public float Metallic { get; init; } = 0f;
}
